using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class DelveDifficultyTests
    {
        [Fact]
        public void FirstLevelIsSmallAndGentle()
        {
            DelveLevel level = DelveDifficulty.For(1);
            Assert.Equal("S", level.SizeName);
            Assert.Equal(0f, level.HostileCasketBonus);
            Assert.Equal(1f, level.LootMultiplier);
        }

        [Fact]
        public void DepthBelowOneIsTreatedAsFirstLevel()
        {
            Assert.Equal(DelveDifficulty.For(1).SizeName, DelveDifficulty.For(-5).SizeName);
            Assert.Equal(1, DelveDifficulty.For(0).Depth);
        }

        [Fact]
        public void DifficultyNeverDecreasesWithDepth()
        {
            string[] order = { "XS", "S", "M", "L", "XL" };
            DelveLevel previous = DelveDifficulty.For(1);
            for (int d = 2; d <= 40; d++)
            {
                DelveLevel level = DelveDifficulty.For(d);
                Assert.True(System.Array.IndexOf(order, level.SizeName) >= System.Array.IndexOf(order, previous.SizeName));
                Assert.True(level.HostileCasketBonus >= previous.HostileCasketBonus);
                Assert.True(level.LootMultiplier >= previous.LootMultiplier);
                Assert.True(level.SecureRoomFactor >= previous.SecureRoomFactor);
                Assert.True(level.DroidGuardChance >= previous.DroidGuardChance);
                previous = level;
            }
        }

        [Fact]
        public void ValuesAreCapped()
        {
            DelveLevel deep = DelveDifficulty.For(500);
            Assert.Equal("XL", deep.SizeName);
            Assert.True(deep.HostileCasketBonus <= 0.35f);
            Assert.True(deep.LootMultiplier <= 1.8f);
            Assert.True(deep.DroidGuardChance <= 0.9f);
        }
    }
}
