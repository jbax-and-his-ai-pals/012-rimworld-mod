using System;

namespace Site44.Gen
{
    // How severe a temperature sector is. Severity is a fraction of the hardest room that can exist: 1.0 is a room that needs the best
    // dedicated set of gear to stay comfortable in. Each tier has a spread of severities, bell-shaped and clipped, so deep levels still
    // produce mild rooms now and then ("breather" rooms) and the hardest rooms only start to appear in the deeper tiers.
    public static class HazardScale
    {
        // Insulation points that keep a colonist comfortable in the hardest possible room: the best dedicated set in the catalogue.
        // Today that is the polar shell plus thermal leggings (67) for cold and the heat shell plus vest and leggings (82) for heat,
        // so the cap is the lower of the two. Raise it only when better gear exists.
        public const float CapRequirement = 65f;
        public const float MinRequirement = 4f;      // barely outside normal tolerance
        public const float BreatherChance = 0.12f;   // a room drawn from the first tier's spread, whatever the depth

        // A colonist is comfortable up to about this temperature plus their heat insulation, and down to this minus their cold insulation.
        public const float ComfortMax = 26f;
        public const float ComfortMin = 16f;

        public static float Mean(int tier)
        {
            return Math.Min(0.75f, 0.15f + 0.12f * Math.Max(0, tier));
        }

        public static float Spread(int tier)
        {
            return Math.Min(0.25f, 0.12f + 0.02f * Math.Max(0, tier));
        }

        // The hardest severity a tier can produce.
        public static float Upper(int tier)
        {
            return Math.Min(1f, 0.3f + 0.14f * Math.Max(0, tier));
        }

        public static float Draw(int seed, int tier)
        {
            var rng = new FacilityRng(unchecked(seed * 2654435 + 97));
            if (tier > 0 && rng.Next(1000) < BreatherChance * 1000f)
            {
                tier = 0;
            }

            double u1 = (rng.Next(999999) + 1) / 1000000.0;
            double u2 = rng.Next(1000000) / 1000000.0;
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            float s = Mean(tier) + Spread(tier) * (float)normal;
            return Math.Max(0.05f, Math.Min(Upper(tier), s));
        }

        public static float RequiredInsulation(float severity)
        {
            return MinRequirement + Math.Max(0f, Math.Min(1f, severity)) * (CapRequirement - MinRequirement);
        }

        public static float HotTemperature(float severity)
        {
            return ComfortMax + RequiredInsulation(severity);
        }

        public static float ColdTemperature(float severity)
        {
            return ComfortMin - RequiredInsulation(severity);
        }
    }
}
