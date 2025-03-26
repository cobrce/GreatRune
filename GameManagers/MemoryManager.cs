using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace GreatRune.GameManagers
{
    internal partial class MemoryManager
    {
        public Process? Process { get; private set; }

        private IntPtr hProcess = IntPtr.Zero;
        private nuint GameMan;

        public const uint PROCEESS_ALL_ACCESS = 0x1F0FFF;
        public const uint MEM_COMMIT = 0x1000;
        public const int MEM_RESERVE = 0x2000;
        public const uint MEM_RELEASE = 32768U;
        public const uint PAGE_READONLY = 0x02;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public bool IsOpen { get; private set; }
        public uint[] InventoryItems { get; private set; } = [];
        public nint GameDataMan { get; private set; }

        public nint MapItemMan { get; private set; }
        public nint ItemGive { get; private set; }

        internal void Close()
        {
            IsOpen = false;
            GameMan = 0;
            GameDataMan = 0;
            MapItemMan = 0;
            ItemGive = 0;
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

            hProcess = process.Handle;
            // hProcess = OpenProcess(
            //     PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION
            //         | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ
            //         | PROCESS_ACCESS_RIGHTS.PROCESS_VM_WRITE,
            //     false,
            //     process.Id
            // );
            // if (hProcess == IntPtr.Zero)
            //     return false;

            if (process == null || process.MainModule == null)
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

        // Incomplete, works with great runes, not tested with other items
        internal void ItemGib(int fullItemID, int quantity)
        {
            const int ItemStructOffset = 0xC;
            const int MapItemMapOffset = 0x4B;
            const int ItemGiveOffset = 0x66;

            byte[] code = new byte[ItemGiveShellCode.Length];
            Array.Copy(ItemGiveShellCode, code, ItemGiveShellCode.Length);
            byte[] buffer = BitConverter.GetBytes(1);
            Array.Copy(buffer, 0, code, ItemStructOffset + 32, buffer.Length);
            buffer = BitConverter.GetBytes(fullItemID);
            Array.Copy(buffer, 0, code, ItemStructOffset + 36, buffer.Length);
            buffer = BitConverter.GetBytes(quantity);
            Array.Copy(buffer, 0, code, ItemStructOffset + 40, buffer.Length);
            buffer = BitConverter.GetBytes(-1);
            Array.Copy(buffer, 0, code, ItemStructOffset + 48, buffer.Length);

            if (!ResolveItemGive())
                return;
            if (!ResolveMapItemMan())
                return;

            buffer = BitConverter.GetBytes(MapItemMan);
            Array.Copy(buffer, 0, code, MapItemMapOffset, buffer.Length);

            buffer = BitConverter.GetBytes(ItemGive);
            Array.Copy(buffer, 0, code, ItemGiveOffset, buffer.Length);

            var memory = VirtualAllocEx(
                hProcess,
                0,
                code.Length,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_EXECUTE_READWRITE
            );
            if (memory == 0)
                return;

            if (WriteProcessMemory(hProcess, memory, code, code.Length, out int _))
            {
                var hThread = CreateRemoteThread(hProcess, 0, 0, memory, 0, 0, out uint threadID);
                if (hThread != 0)
                    CloseHandle(hThread);
            }

            VirtualFreeEx(hProcess, memory, (uint)buffer.Length, MEM_RELEASE);
        }

        private bool ResolveItemGive()
        {
            // base.RegisterAbsoluteAOB("8B 02 83 F8 0A", Array.Empty<int>());
            if (ItemGive != 0)
                return true;

            if (Process == null || Process.MainModule == null)
                return false;

            const string AOB = "8B 02 83 F8 0A";
            if (!FindAOB(Process.MainModule.BaseAddress, AOB, out nuint result))
                return false;
            ItemGive = (nint)result - 82;
            return true;
        }

        private bool ResolveMapItemMan()
        {
            if (MapItemMan != 0)
                return true;

            if (Process == null || Process.MainModule == null)
                return false;

            // base.RegisterRelativeAOB("48 8B 0D ? ? ? ? C7 44 24 50 FF FF FF FF C7 45 A0 FF FF FF FF 48 85 C9 75 2E", 3, 7, Array.Empty<int>());
            const string AOB =
                "48 8B 0D ? ? ? ? C7 44 24 50 FF FF FF FF C7 45 A0 FF FF FF FF 48 85 C9 75 2E";

            if (
                !FindAobAndExtractPointer(Process.MainModule.BaseAddress, AOB, 3, out nuint address)
            )
                return false;

            MapItemMan = (nint)address;
            return true;
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

            if (!ResolveGameDataMan())
                return false;

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

        private bool ResolveGameDataMan()
        {
            const string gameDataManAOB = "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 05 48 8B 40 58 C3 C3";
            const int pointerOffset = 3;
            if (GameDataMan != 0)
                return true;

            if (Process == null || Process.MainModule == null)
                return false;
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
            return true;
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

        private readonly byte[] ItemGiveShellCode =
        [
            0x48,
            0x83,
            0xEC,
            0x48,
            0x45,
            0x31,
            0xC9,
            0xE8,
            0x34,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x41,
            0x58,
            0x49,
            0x8D,
            0x50,
            0x20,
            0xE8,
            0x08,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x41,
            0x5A,
            0x4D,
            0x8B,
            0x12,
            0x49,
            0x8B,
            0x0A,
            0xC7,
            0x02,
            0x01,
            0x00,
            0x00,
            0x00,
            0xE8,
            0x08,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x58,
            0xFF,
            0x10,
            0x48,
            0x83,
            0xC4,
            0x48,
            0xC3,
        ];

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

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool VirtualFreeEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint dwFreeType
        );

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            [Optional] IntPtr lpThreadAttributes,
            nuint dwStackSize,
            IntPtr lpStartAddress,
            [Optional] IntPtr lpParameter,
            int dwCreationFlags,
            [Optional] out uint lpThreadId
        );

        [LibraryImport("kernel32.dll")]
        public static partial IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            IntPtr dwSize,
            uint flAllocationType,
            uint flProtect
        );

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
