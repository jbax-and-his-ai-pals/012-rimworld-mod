using System;

namespace Site44.Gen
{
    // How much of a tended wound a bed closes in one pulse. Exponential closing (the deeper the wound, the more it loses) plus a small
    // floor so the tail ends instead of trailing off.
    public static class BedHealing
    {
        public const float FloorPerHour = 1.2f;

        // The base game's full sleep takes this long at bed quality 1.
        public const float VanillaSleepHours = 10.5f;

        // Extra rest to add in one pulse so that a full sleep takes "hours" instead (zero when that is not shorter than the base game's).
        public static float SleepTopUp(float hours, float quality, int interval)
        {
            if (hours <= 0f || hours >= VanillaSleepHours)
            {
                return 0f;
            }

            return (1f / (hours * 2500f) - 1f / (VanillaSleepHours * 2500f)) * interval * quality;
        }

        public static float Step(float severity, float windowTicks, int interval)
        {
            float tau = windowTicks / 4f;
            return Math.Max(severity * interval / tau, interval / 2500f * FloorPerHour);
        }
    }
}
