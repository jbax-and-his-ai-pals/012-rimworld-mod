using System;

namespace Site44.Gen
{
    // What a level of delve mode asks of the party: how big the complex is and how much it fights back or pays out.
    public sealed class DelveLevel
    {
        public int Depth;
        public string SizeName;            // XS, S, M, L or XL
        public float HostileCasketBonus;   // added to the hostile casket share
        public float LootMultiplier;       // on every loot stack
        public float SecureRoomFactor;     // on the secure room density
        public float DroidGuardChance;     // chance a secure room is guarded by a droid
    }

    public static class DelveDifficulty
    {
        public const string QuickDelves = "Quick delves";
        public const string Steady = "Steady";
        public const string Expeditions = "Expeditions";

        public static bool IsPaceStyle(string style)
        {
            return style == QuickDelves || style == Steady || style == Expeditions || style == "Grows with depth";
        }

        // The size of one level, from a pace style or a fixed size. Deterministic from the run seed and the depth; "previous" is
        // the size of the level before it, so a long level is never followed by another long one on the steady pace.
        public static string SizeFor(string style, int depth, string previous, int seed)
        {
            depth = Math.Max(1, depth);
            switch (style)
            {
                case "Extra small": case "XS": return "XS";
                case "Small": case "S": return "S";
                case "Medium": case "M": return "M";
                case "Large": case "L": return "L";
                case "XL": return "XL";
                case QuickDelves: return "S";
            }

            var rng = new FacilityRng(unchecked(seed * 31 + depth * 7919));
            if (style == Expeditions)
            {
                return depth <= 1 ? "M" : Draw(rng, 0, 50, 50);
            }

            // Steady: two small levels to begin with, then larger sizes become more likely the deeper the run goes.
            if (depth <= 2)
            {
                return "S";
            }

            int small, medium, large;
            if (depth <= 5) { small = 40; medium = 50; large = 10; }
            else if (depth <= 9) { small = 20; medium = 40; large = 40; }
            else { small = 15; medium = 30; large = 55; }
            if (previous == "L")
            {
                large = 0;
            }

            return Draw(rng, small, medium, large);
        }

        private static string Draw(FacilityRng rng, int small, int medium, int large)
        {
            int roll = rng.Next(small + medium + large);
            return roll < small ? "S" : roll < small + medium ? "M" : "L";
        }

        private static readonly string[] Sizes = { "S", "S", "M", "M", "L", "L", "XL" };

        // Depth 1 is the first complex. Complexes grow, hostile cryosleepers become likelier, secure rooms multiply and
        // loot gets richer, up to a cap so a long run stays playable.
        public static DelveLevel For(int depth)
        {
            depth = Math.Max(1, depth);
            int steps = depth - 1;
            return new DelveLevel
            {
                Depth = depth,
                SizeName = Sizes[Math.Min(Sizes.Length - 1, steps)],
                HostileCasketBonus = Math.Min(0.35f, 0.04f * steps),
                LootMultiplier = Math.Min(1.8f, 1f + 0.08f * steps),
                SecureRoomFactor = Math.Min(1.8f, 1f + 0.1f * steps),
                DroidGuardChance = Math.Min(0.9f, 0.35f + 0.08f * steps)
            };
        }
    }
}
