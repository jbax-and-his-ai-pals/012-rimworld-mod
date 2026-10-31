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
    }
}
