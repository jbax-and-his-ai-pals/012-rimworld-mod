using System;

namespace Site44.Gen
{
    // Depths 1 to BaseLevels are the gentle base tier. Past it, a new tier begins every TierLength levels and each one multiplies the
    // danger of the last. The last level of a tier (depth 10, 15, 20 ...) holds its guardian, which previews the next tier.
    // Tiers follow depth alone; the tier strength setting decides how steeply each one grows.
    public static class DelveTiers
    {
        public const int BaseLevels = 10;
        public const int TierLength = 5;
        public const float GrowthAtDefault = 0.3f;   // each tier is 1.3x the one before at 100% tier strength
        public const float RewardShare = 0.85f;      // rewards grow almost as fast as danger (as a power of the danger multiplier)

        public static int TierOf(int depth)
        {
            return depth <= BaseLevels ? 0 : (depth - BaseLevels - 1) / TierLength + 1;
        }

        public static bool IsGuardianDepth(int depth)
        {
            return depth >= BaseLevels && (depth - BaseLevels) % TierLength == 0;
        }

        public static float GrowthPerTier(float strength)
        {
            return 1f + GrowthAtDefault * Math.Max(0f, strength);
        }

        public static float Pressure(int depth, float strength)
        {
            return (float)Math.Pow(GrowthPerTier(strength), TierOf(depth));
        }

        public static float RewardFactor(int depth, float strength)
        {
            return (float)Math.Pow(Pressure(depth, strength), RewardShare);
        }

        public static string TierName(int tier)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return tier <= 0 ? "Base tier" : "Deep " + (tier <= numerals.Length ? numerals[tier - 1] : tier.ToString());
        }
    }
}
