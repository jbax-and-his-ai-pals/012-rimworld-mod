using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class GearCatalogTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BestSetCoversTheHardestRoom(bool hot)
        {
            Assert.True(GearCatalog.BestSet(hot) >= HazardScale.CapRequirement);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NoSingleSlotCoversTheHardestRoom(bool hot)
        {
            Assert.All(GearCatalog.For(hot), p => Assert.True(p.Value < HazardScale.CapRequirement));
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 1)]
        [InlineData(true, 4)]
        [InlineData(false, 9)]
        public void EntryGearReachesTheShareWhenTheDepthAllowsIt(bool hot, int depth)
        {
            for (float needed = 4f; needed <= 100f; needed += 4f)
            {
                var pieces = GearCatalog.Entry(hot, depth, needed, 0.6f);
                Assert.NotEmpty(pieces);
                float best = GearCatalog.BestSet(hot, depth);
                float target = needed * 0.6f;
                // Either the target is met, or this depth simply has nothing better.
                Assert.True(pieces.Sum(p => p.Value) >= target - 0.001f || pieces.Sum(p => p.Value) >= best - 0.001f);
            }
        }

        [Fact]
        public void EntryGearNeverStacksTwoPiecesInOneSlot()
        {
            var pieces = GearCatalog.Entry(true, 9, 100f, 0.9f);
            Assert.Equal(pieces.Count, pieces.Select(p => p.Slot).Distinct().Count());
        }

        [Fact]
        public void MildRoomsGetASmallPiece()
        {
            var pieces = GearCatalog.Entry(false, 9, 8f, 0.6f);
            Assert.Single(pieces);
            Assert.True(pieces[0].Value <= 16f);
        }
    }
}
