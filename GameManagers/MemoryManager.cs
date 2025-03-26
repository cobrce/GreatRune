using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace GreatRune.GameManagers
{
    internal partial class MemoryManager
    {
        public Process? Process { get; private set; }

        private IntPtr hProcess = IntPtr.Zero;
        private nuint GameMan;

        private const uint PROCEESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;

        public bool IsOpen { get; private set; }
        public uint[] InventoryItems { get; private set; } = [];
        public nint GameDataMan { get; private set; }

        internal void Close()
        {
            IsOpen = false;
            GameMan = 0;
            GameDataMan = 0;
            Process = null;
            if (hProcess != IntPtr.Zero)
            {
                CloseHandle(hProcess);
                hProcess = IntPtr.Zero;
            }
        }

        internal bool Open(Process process, string pattern, int pointerOffset)
        {
            if (IsOpen)
                return false;
            Process = process;

            hProcess = OpenProcess(
                PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION
                    | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ
                    | PROCESS_ACCESS_RIGHTS.PROCESS_VM_WRITE,
                false,
                process.Id
            );
            if (hProcess == IntPtr.Zero)
                return false;

            if (
                !FindAobAndExtractPointer(
                    process.MainModule.BaseAddress,
                    pattern,
                    pointerOffset,
                    out nuint gameManPtr
                )
            )
            {
                Close();
                return false;
            }

            // byte[] buffer = new byte[8];

            // if (!ReadProcessMemory(hProcess, (nint)gameManPtr, buffer, 8, out int read))
            // {
            //     Close();
            //     return false;
            // }
            // GameMan = (nuint)BitConverter.ToUInt64(buffer);
            if (!ReadPointer((nint)gameManPtr, out nint GameMan) || GameMan == 0)
            {
                Close();
                return false;
            }
            IsOpen = true;
            return true;
        }

        internal bool Open(Process process)
        {
            const string gameManAOB = "48 8B 05 ?? ?? ?? ?? 80 B8 ?? ?? ?? ?? 0D 0F 94 C0 C3";
            const int pointerOffset = 3;
            return Open(process, gameManAOB, pointerOffset);
        }

        internal bool QuitOutGame()
        {
            if (!IsOpen || GameMan == 0)
                return false;

            byte[] data = { 1 };
            if (!WriteProcessMemory(hProcess, (nint)GameMan + 0x10, data, 1, out int _))
                return false;

            Thread.Sleep(100);

            data[0] = 0;
            if (!WriteProcessMemory(hProcess, (nint)GameMan + 0x10, data, 1, out _))
                return false;

            return true;
        }

        internal bool ReadPointer(nint address, out nint result)
        {
            result = 0;
            if (hProcess == nint.Zero)
                return false;

            byte[] buffer = new byte[8];
            if (!ReadProcessMemory(hProcess, address, buffer, buffer.Length, out int _))
                return false;

            result = (nint)BitConverter.ToInt64(buffer);
            return true;
        }

        internal bool ReadInt(nint address, out int result)
        {
            result = 0;
            if (hProcess == nint.Zero)
                return false;

            byte[] buffer = new byte[4];
            if (!ReadProcessMemory(hProcess, address, buffer, buffer.Length, out int _))
                return false;

            result = BitConverter.ToInt32(buffer);
            return true;
        }

        internal bool UpdateInventory()
        {
            if (!IsOpen)
                return false;

            const string gameDataManAOB = "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 05 48 8B 40 58 C3 C3";
            const int pointerOffset = 3;
            if (GameDataMan == 0)
            {
                if (
                    !FindAobAndExtractPointer(
                        Process.MainModule.BaseAddress,
                        gameDataManAOB,
                        pointerOffset,
                        out nuint gameDataManPtr
                    )
                )
                    return false;

                if (!ReadPointer((nint)gameDataManPtr, out nint gameDataMan))
                    return false;
                GameDataMan = gameDataMan;
            }

            var inventory = new List<uint>();

            var KeyItemInvData = new int[] { 0x5D0, 1, 384 }; // offset,isKey,length
            if (
                !ReadPointer((nint)GameDataMan + 0x8, out nint playerGameData)
                || playerGameData == 0
            )
                return false;
            if (
                !ReadPointer(playerGameData + KeyItemInvData[0], out nint equipInventoryData)
                || equipInventoryData == 0
            )
                return false;

            if (
                !ReadPointer(
                    equipInventoryData + 0x10 + 0x10 * KeyItemInvData[1],
                    out nint inventoryList
                )
                || inventoryList == 0
            )
                return false;
            if (!ReadInt(equipInventoryData + 0x18, out int inventoryNum) || inventoryNum == 0)
                return false;

            int count = 0;
            for (int i = 0; i < KeyItemInvData[2]; i++)
            {
                var itemStruct = inventoryList + (i * 0X18);
                if (!ReadInt(itemStruct, out var GaItemHandle))
                    return false;
                if (!ReadInt(itemStruct + 4, out int itemIDint))
                    return false;
                uint itemID = (uint)itemIDint;
                var itemType = itemID & 0xF0000000;
                itemID &= ~itemType;

                if (!ReadInt(itemStruct + 8, out int quantity))
                    return false;

                if (
                    itemID <= 0x5FFFFFFF
                    && itemID != 0
                    && quantity != 0
                    && itemID != 0xFFFFFFFF
                    && GaItemHandle != 0
                )
                {
                    inventory.Add(itemID);
                    count++;
                }

                if (count > inventoryNum)
                {
                    break;
                }
            }

            InventoryItems = inventory.ToArray();
            return true;
        }

        public record GreateRunes(
            bool Godrick,
            bool Rykard,
            bool Radahn,
            bool Morgott,
            bool Mohg,
            bool Malenia,
            bool Rennala
        );

        internal enum GreateRunesID
        {
            GODRICK_S_GREAT_RUNE = 191,
            GODRICK_S_GREAT_RUNE_UNPOWERED = 8148,

            RADAHN_S_GREAT_RUNE = 192,
            RADAHN_S_GREAT_RUNE_UNPOWERED = 8149,

            MORGOTT_S_GREAT_RUNE = 193,
            MORGOTT_S_GREAT_RUNE_UNPOWERED = 8150,

            RYKARD_S_GREAT_RUNE = 194,
            RYKARD_S_GREAT_RUNE_UNPOWERED = 8151,

            MOHG_S_GREAT_RUNE = 195,
            MOHG_S_GREAT_RUNE_UNPOWERED = 8152,

            MALENIA_S_GREAT_RUNE = 196,
            MALENIA_S_GREAT_RUNE_UNPOWERED = 8153,

            GREAT_RUNE_OF_THE_UNBORN = 10080,
        }

        internal GreateRunes GreatRunes()
        {
            bool Godrick = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.GODRICK_S_GREAT_RUNE_UNPOWERED
            );
            bool Rykard = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.RYKARD_S_GREAT_RUNE_UNPOWERED
            );
            bool Radahn = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.RADAHN_S_GREAT_RUNE_UNPOWERED
            );
            bool Morgott = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.MORGOTT_S_GREAT_RUNE_UNPOWERED
            );
            bool Mohg = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.MOHG_S_GREAT_RUNE_UNPOWERED
            );
            bool Malenia = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.MALENIA_S_GREAT_RUNE_UNPOWERED
            );
            bool Rennala = InventoryItems.Contains<uint>(
                (uint)GreateRunesID.MALENIA_S_GREAT_RUNE_UNPOWERED
            );

            return new GreateRunes(Godrick, Rykard, Radahn, Morgott, Mohg, Malenia, Rennala);
        }

        internal bool ActivateGreateRune(GreateRunesID runeId)
        {

            // rykard 400000C2
            int data = 40000000 | (int)runeId;

            return false;
        }

        internal GreateRunes ActivatedRunes()
        {
            bool Godrick = InventoryItems.Contains<uint>((uint)GreateRunesID.GODRICK_S_GREAT_RUNE);
            bool Rykard = InventoryItems.Contains<uint>((uint)GreateRunesID.RYKARD_S_GREAT_RUNE);
            bool Radahn = InventoryItems.Contains<uint>((uint)GreateRunesID.RADAHN_S_GREAT_RUNE);
            bool Morgott = InventoryItems.Contains<uint>((uint)GreateRunesID.MORGOTT_S_GREAT_RUNE);
            bool Mohg = InventoryItems.Contains<uint>((uint)GreateRunesID.MOHG_S_GREAT_RUNE);
            bool Malenia = InventoryItems.Contains<uint>((uint)GreateRunesID.MALENIA_S_GREAT_RUNE);
            bool Rennala = InventoryItems.Contains<uint>((uint)GreateRunesID.MALENIA_S_GREAT_RUNE);

            return new GreateRunes(Godrick, Rykard, Radahn, Morgott, Mohg, Malenia, Rennala);
        }

        private bool FindAobAndExtractPointer(
            IntPtr baseAddress,
            string pattern,
            int pointerOffset,
            out nuint pointer
        )
        {
            pointer = default;
            if (!FindAOB(baseAddress, pattern, out nuint foundPointer))
                return false;

            if (!ReadInt((nint)foundPointer + pointerOffset, out int offset))
                return false;

            pointer = (nuint)((nint)foundPointer + pointerOffset + offset + 4);
            return true;
        }

        struct AOB
        {
            internal List<byte> data;
            internal List<byte> mask;

            public AOB()
            {
                data = [];
                mask = [];
            }
        };

        private bool FindAOB(nint baseAddress, string pattern, out nuint result)
        {
            result = 0;

            AOB aob = AOBFromPattern(pattern);
            if (hProcess == nint.Zero)
                return false;

            nint currentAddress = baseAddress;
            while (
                VirtualQueryEx(
                    hProcess,
                    currentAddress,
                    out MEMORY_BASIC_INFORMATION info,
                    Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))
                )
            )
            {
                currentAddress = (nint)(info.BaseAddress + info.RegionSize);
                if (info.State != MEM_COMMIT)
                    continue;

                byte[] buffer = new byte[(int)info.RegionSize];
                if (
                    !ReadProcessMemory(
                        hProcess,
                        (IntPtr)info.BaseAddress,
                        buffer,
                        (IntPtr)buffer.Length,
                        out int _
                    )
                )
                    continue;

                for (int i = 0; i < buffer.Length - aob.data.Count; i++)
                {
                    bool found = true;
                    for (int j = 0; j < aob.data.Count; j++)
                    {
                        if (aob.mask[j] == 0)
                            continue;
                        if (buffer[i + j] != aob.data[j])
                        {
                            found = false;
                            break;
                        }
                    }
                    if (found)
                    {
                        result = (nuint)(info.BaseAddress + (ulong)i);
                        return true;
                    }
                }
            }
            return false;
        }

        private static AOB AOBFromPattern(string pattern)
        {
            AOB aob = new();
            var split = pattern.Split(" ");

            foreach (var slice in split)
            {
                if (slice.Contains('?'))
                {
                    aob.mask.Add(0);
                    aob.data.Add(0);
                }
                else
                {
                    aob.mask.Add(1);
                    if (byte.TryParse(slice, NumberStyles.HexNumber, null, out byte result))
                        aob.data.Add(result);
                    else
                        aob.data.Add(0);
                }
            }
            return aob;
        }

        [LibraryImport("KERNEL32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [In] byte[] lpBuffer,
            nint nSize,
            [Optional] out int lpNumberOfBytesWritten
        );

        [LibraryImport("KERNEL32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            nint nSize,
            [Optional] out int lpNumberOfBytesRead
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public ulong BaseAddress;
            public ulong AllocationBase;
            public int AllocationProtect;
            public ulong RegionSize;
            public int State;
            public ulong Protect;
            public ulong Type;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool VirtualQueryEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer,
            int dwLength
        );

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial IntPtr OpenProcess(
            PROCESS_ACCESS_RIGHTS dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwProcessId
        );

        [LibraryImport("KERNEL32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(nint hObject);

        [Flags]
        public enum PROCESS_ACCESS_RIGHTS : uint
        {
            PROCESS_TERMINATE = 0x00000001,
            PROCESS_CREATE_THREAD = 0x00000002,
            PROCESS_SET_SESSIONID = 0x00000004,
            PROCESS_VM_OPERATION = 0x00000008,
            PROCESS_VM_READ = 0x00000010,
            PROCESS_VM_WRITE = 0x00000020,
            PROCESS_DUP_HANDLE = 0x00000040,
            PROCESS_CREATE_PROCESS = 0x00000080,
            PROCESS_SET_QUOTA = 0x00000100,
            PROCESS_SET_INFORMATION = 0x00000200,
            PROCESS_QUERY_INFORMATION = 0x00000400,
            PROCESS_SUSPEND_RESUME = 0x00000800,
            PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
            PROCESS_SET_LIMITED_INFORMATION = 0x00002000,
            PROCESS_ALL_ACCESS = 0x001FFFFF,
            PROCESS_DELETE = 0x00010000,
            PROCESS_READ_CONTROL = 0x00020000,
            PROCESS_WRITE_DAC = 0x00040000,
            PROCESS_WRITE_OWNER = 0x00080000,
            PROCESS_SYNCHRONIZE = 0x00100000,
            PROCESS_STANDARD_RIGHTS_REQUIRED = 0x000F0000,
        }
    }
}
