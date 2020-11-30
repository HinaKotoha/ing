﻿namespace Sakuno.ING.Game.Models.Knowledge
{
    public static class KnownLeveling
    {
        public static readonly int MaxShipLevel = 175;
        public static readonly int MaxAdmiralLevel = 120;

        private static readonly int[] shipExpTable =
        {
            0, 0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500,
            5500, 6600, 7800, 9100, 10500, 12000, 13600, 15300, 17100, 19000,
            21000, 23100, 25300, 27600, 30000, 32500, 35100, 37800, 40600, 43500,
            46500, 49600, 52800, 56100, 59500, 63000, 66600, 70300, 74100, 78000,
            82000, 86100, 90300, 94600, 99000, 103500, 108100, 112800, 117600, 122500,
            127500, 132700, 138100, 143700, 149500, 155500, 161700, 168100, 174700, 181500,
            188500, 195800, 203400, 211300, 219500, 228000, 236800, 245900, 255300, 265000,
            275000, 285400, 296200, 307400, 319000, 331000, 343400, 356200, 369400, 383000,
            397000, 411500, 426500, 442000, 458000, 474500, 491500, 509000, 527000, 545500,
            564500, 584500, 606500, 631500, 661500, 701500, 761500, 851500, 1000000, 1000000,
            1010000, 1011000, 1013000, 1016000, 1020000, 1025000, 1031000, 1038000, 1046000, 1055000,
            1065000, 1077000, 1091000, 1107000, 1125000, 1145000, 1168000, 1194000, 1223000, 1255000,
            1290000, 1329000, 1372000, 1419000, 1470000, 1525000, 1584000, 1647000, 1714000, 1785000,
            1860000, 1940000, 2025000, 2115000, 2210000, 2310000, 2415000, 2525000, 2640000, 2760000,
            2887000, 3021000, 3162000, 3310000, 3465000, 3628000, 3799000, 3978000, 4165000, 4360000,
            4564000, 4777000, 4999000, 5230000, 5470000, 5720000, 5780000, 5860000, 5970000, 6120000,
            6320000, 6580000, 6910000, 7320000, 7820000, 7920000, 8033000, 8172000, 8350000, 8580000,
            8875000, 9248000, 9705000, 10266000, 10995000,
        };

        private static readonly int[] admiralExpTable =
        {
            0, 0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500,
            5500, 6600, 7800, 9100, 10500, 12000, 13600, 15300, 17100, 19000,
            21000, 23100, 25300, 27600, 30000, 32500, 35100, 37800, 40600, 43500,
            46500, 49600, 52800, 56100, 59500, 63000, 66600, 70300, 74100, 78000,
            82000, 86100, 90300, 94600, 99000, 103500, 108100, 112800, 117600, 122500,
            127500, 132700, 138100, 143700, 149500, 155500, 161700, 168100, 174700, 181500,
            188500, 195800, 203400, 211300, 219500, 228000, 236800, 245900, 255300, 265000,
            275000, 285400, 296200, 307400, 319000, 331000, 343400, 356200, 369400, 383000,
            397000, 411500, 426500, 442000, 458000, 474500, 491500, 509000, 527000, 545500,
            564500, 584500, 606500, 631500, 661500, 701500, 761500, 851500, 1000000, 1300000,
            1_600_000, 1_900_000, 2_200_000, 2_600_000, 3_000_000,
            3_500_000, 4_000_000, 4_600_000, 5_200_000, 5_900_000,
            6_600_000, 7_400_000, 8_200_000, 9_100_000, 10_000_000,
            11_000_000, 12_000_000, 13_000_000, 14_000_000, 15_000_000,
            300_000_000,
        };

        public static int GetShipExp(int level)
        {
            if (level < 0)
                level = 0;
            else if (level > MaxShipLevel)
                level = MaxShipLevel;

            return shipExpTable[level];
        }

        public static int GetAdmiralExp(int level)
        {
            if (level < 0)
                level = 0;
            else if (level > MaxAdmiralLevel + 1)
                level = MaxAdmiralLevel + 1;

            return admiralExpTable[level];
        }
    }
}
