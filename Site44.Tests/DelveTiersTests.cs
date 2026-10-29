using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class DelveTiersTests
    {
        [Theory]
        [InlineData(1, 0)]
        [InlineData(10, 0)]
        [InlineData(11, 1)]
        [InlineData(15, 1)]
        [InlineData(16, 2)]
        [InlineData(30, 4)]
        public void TierFollowsDepth(int depth, int tier)
        {
            Assert.Equal(tier, DelveTiers.TierOf(depth));
        }

        [Fact]
        public void GuardianEndsEachTier()
        {
            Assert.False(DelveTiers.IsGuardianDepth(9));
            Assert.True(DelveTiers.IsGuardianDepth(10));
            Assert.False(DelveTiers.IsGuardianDepth(11));
            Assert.True(DelveTiers.IsGuardianDepth(15));
            Assert.True(DelveTiers.IsGuardianDepth(20));
        }

        [Fact]
        public void BaseTierIsUntouchedAtAnyStrength()
        {
            for (int depth = 1; depth <= 10; depth++)
            {
                Assert.Equal(1f, DelveTiers.Pressure(depth, 3f), 3);
            }
        }

        [Fact]
        public void DefaultStrengthGrowsThirtyPercentPerTier()
        {
            Assert.Equal(1.3f, DelveTiers.Pressure(11, 1f), 3);
            Assert.Equal(1.69f, DelveTiers.Pressure(16, 1f), 3);
        }

        [Fact]
        public void ZeroStrengthIsFlat()
        {
            Assert.Equal(1f, DelveTiers.Pressure(40, 0f), 3);
        }

        [Fact]
        public void RewardsGrowAlmostAsFastAsDanger()
        {
            for (int depth = 11; depth <= 40; depth++)
            {
                float danger = DelveTiers.Pressure(depth, 1f);
                float reward = DelveTiers.RewardFactor(depth, 1f);
                Assert.InRange(reward, 1f, danger);
            }
        }

        [Fact]
        public void HarsherPresetsGrowFaster()
        {
            Assert.True(DelveTiers.Pressure(25, 2f) > DelveTiers.Pressure(25, 1f));
            Assert.True(DelveTiers.Pressure(25, 1f) > DelveTiers.Pressure(25, 0.33f));
        }
    }
}
