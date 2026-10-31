using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class BedHealingTests
    {
        private static int TicksToClose(float severity, float windowTicks)
        {
            int ticks = 0;
            while (severity > 0f && ticks < 1000000)
            {
                severity -= BedHealing.Step(severity, windowTicks, 60);
                ticks += 60;
            }

            return ticks;
        }

        [Theory]
        [InlineData(2f)]
        [InlineData(10f)]
        [InlineData(25f)]
        [InlineData(40f)]
        public void WoundsCloseWithinAboutTheWindow(float severity)
        {
            float window = 3f * 2500f;
            Assert.InRange(TicksToClose(severity, window), 1, (int)(window * 1.25f));
        }

        [Fact]
        public void DeepWoundsTakeNoMuchLongerThanLightOnes()
        {
            float window = 3f * 2500f;
            Assert.True(TicksToClose(40f, window) < TicksToClose(5f, window) * 4);
        }

        [Fact]
        public void ShorterWindowHealsFaster()
        {
            Assert.True(TicksToClose(20f, 1.5f * 2500f) < TicksToClose(20f, 6f * 2500f));
        }

        // The base game gains 0.005714286 rest per 150 ticks at quality 1 (a full sleep in 10.5 hours); add the top-up and a full sleep
        // should take the chosen time.
        [Theory]
        [InlineData(4f)]
        [InlineData(2f)]
        [InlineData(1f)]
        public void FullSleepTakesTheChosenTime(float hours)
        {
            float level = 0f;
            int ticks = 0;
            while (level < 1f && ticks < 100000)
            {
                level += 0.005714286f * 60f / 150f + BedHealing.SleepTopUp(hours, 1f, 60);
                ticks += 60;
            }

            Assert.InRange(ticks / 2500f, hours * 0.9f, hours * 1.1f);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(10.5f)]
        [InlineData(12f)]
        public void NoTopUpWhenNotShorterThanVanilla(float hours)
        {
            Assert.Equal(0f, BedHealing.SleepTopUp(hours, 1f, 60));
        }
    }
}
