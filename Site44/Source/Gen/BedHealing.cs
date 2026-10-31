using System;

namespace Site44.Gen
{
    // How much of a tended wound a bed closes in one pulse. Exponential closing (the deeper the wound, the more it loses) plus a small
    // floor so the tail ends instead of trailing off.
    public static class BedHealing
    {
        public const float FloorPerHour = 1.2f;

        public static float Step(float severity, float windowTicks, int interval)
        {
            float tau = windowTicks / 4f;
            return Math.Max(severity * interval / tau, interval / 2500f * FloorPerHour);
        }
    }
}
