using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class HazardScaleTests
    {
        private static float[] Draws(int tier, int count = 4000)
        {
            return Enumerable.Range(0, count).Select(i => HazardScale.Draw(i, tier)).ToArray();
        }

        [Fact]
        public void DrawsAreDeterministic()
        {
            Assert.Equal(HazardScale.Draw(12345, 3), HazardScale.Draw(12345, 3));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(9)]
        public void SeverityStaysWithinTheTiersRange(int tier)
        {
            foreach (float s in Draws(tier))
            {
                Assert.InRange(s, 0.05f, HazardScale.Upper(tier) + 0.0001f);
            }
        }

        [Fact]
        public void HardestRoomsNeedTheDeeperTiers()
        {
            Assert.True(HazardScale.Upper(4) < 1f);
            Assert.Equal(1f, HazardScale.Upper(5));
            Assert.True(Draws(2).Max() < 0.6f);
        }

        [Fact]
        public void DeepTiersStillProduceMildRooms()
        {
            float[] draws = Draws(7);
            double mild = draws.Count(s => s < 0.4f) / (double)draws.Length;
            Assert.InRange(mild, 0.08, 0.35);
        }

        [Fact]
        public void TypicalRoomGetsHarderWithTier()
        {
            Assert.True(Draws(1).Average() < Draws(3).Average());
            Assert.True(Draws(3).Average() < Draws(6).Average());
        }

        [Fact]
        public void TheHardestRoomIsExactlyWhatTheBestSetCovers()
        {
            Assert.Equal(HazardScale.CapRequirement, HazardScale.RequiredInsulation(1f));
            Assert.Equal(HazardScale.ComfortMax + HazardScale.CapRequirement, HazardScale.HotTemperature(1f));
            Assert.Equal(HazardScale.ComfortMin - HazardScale.CapRequirement, HazardScale.ColdTemperature(1f));
        }
    }
}
