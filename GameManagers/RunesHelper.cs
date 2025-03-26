namespace GreatRune.GameManagers
{
    public static class RunesHelper
    {
        public record GreatRunesRecord(
            bool Godrick,
            bool Rykard,
            bool Radahn,
            bool Morgott,
            bool Mohg,
            bool Malenia,
            bool Rennala
        );

        internal enum GreatRunesID
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

        internal static GreatRunesRecord GreatRunes(uint[] inventoryItems)
        {
            bool Godrick = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.GODRICK_S_GREAT_RUNE_UNPOWERED
            );
            bool Rykard = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.RYKARD_S_GREAT_RUNE_UNPOWERED
            );
            bool Radahn = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.RADAHN_S_GREAT_RUNE_UNPOWERED
            );
            bool Morgott = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.MORGOTT_S_GREAT_RUNE_UNPOWERED
            );
            bool Mohg = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.MOHG_S_GREAT_RUNE_UNPOWERED
            );
            bool Malenia = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.MALENIA_S_GREAT_RUNE_UNPOWERED
            );
            bool Rennala = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.MALENIA_S_GREAT_RUNE_UNPOWERED
            );

            return new GreatRunesRecord(Godrick, Rykard, Radahn, Morgott, Mohg, Malenia, Rennala);
        }

        internal static bool ActivateGreateRune(GreatRunesID runeId)
        {
            // rykard 400000C2
            int data = 40000000 | (int)runeId;

            return false;
        }

        internal static GreatRunesRecord ActivatedRunes(uint[] inventoryItems)
        {
            bool Godrick = inventoryItems.Contains<uint>((uint)GreatRunesID.GODRICK_S_GREAT_RUNE);
            bool Rykard = inventoryItems.Contains<uint>((uint)GreatRunesID.RYKARD_S_GREAT_RUNE);
            bool Radahn = inventoryItems.Contains<uint>((uint)GreatRunesID.RADAHN_S_GREAT_RUNE);
            bool Morgott = inventoryItems.Contains<uint>((uint)GreatRunesID.MORGOTT_S_GREAT_RUNE);
            bool Mohg = inventoryItems.Contains<uint>((uint)GreatRunesID.MOHG_S_GREAT_RUNE);
            bool Malenia = inventoryItems.Contains<uint>((uint)GreatRunesID.MALENIA_S_GREAT_RUNE);
            bool Rennala = inventoryItems.Contains<uint>(
                (uint)GreatRunesID.GREAT_RUNE_OF_THE_UNBORN
            );

            return new GreatRunesRecord(Godrick, Rykard, Radahn, Morgott, Mohg, Malenia, Rennala);
        }
    }
}
