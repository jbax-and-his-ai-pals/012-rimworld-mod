using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class DelveSizeTests
    {
        private static string[] Run(string style, int levels, int seed)
        {
            string previous = null;
            var sizes = new string[levels];
            for (int depth = 1; depth <= levels; depth++)
            {
                sizes[depth - 1] = DelveDifficulty.SizeFor(style, depth, previous, seed);
                previous = sizes[depth - 1];
            }

            return sizes;
        }

        [Fact]
        public void QuickDelvesAreAlwaysSmall()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.All(Run("Quick delves", 20, seed), size => Assert.Equal("S", size));
            }
        }

        [Fact]
        public void SteadyOpensSmallNeverUsesExtremesAndNeverRepeatsLarge()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                string[] sizes = Run("Steady", 20, seed);
                Assert.Equal("S", sizes[0]);
                Assert.Equal("S", sizes[1]);
                Assert.All(sizes, size => Assert.Contains(size, new[] { "S", "M", "L" }));
                for (int i = 1; i < sizes.Length; i++)
                {
                    Assert.False(sizes[i - 1] == "L" && sizes[i] == "L", "seed " + seed + " levels " + i);
                }
            }
        }

        [Fact]
        public void SteadyTrendsLargerWithDepth()
        {
            double early = 0, late = 0;
            int count = 400;
            for (int seed = 0; seed < count; seed++)
            {
                string[] sizes = Run("Steady", 14, seed);
                early += sizes.Skip(2).Take(3).Count(s => s != "S");
                late += sizes.Skip(10).Take(3).Count(s => s != "S");
            }

            Assert.True(late / count > early / count + 0.4, "early " + early / count + " late " + late / count);
        }

        [Fact]
        public void ExpeditionsAreMediumOrLarge()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                string[] sizes = Run("Expeditions", 15, seed);
                Assert.Equal("M", sizes[0]);
                Assert.All(sizes, size => Assert.Contains(size, new[] { "M", "L" }));
            }
        }

        [Fact]
        public void FixedSizesAndPlansAreStable()
        {
            Assert.Equal("XS", DelveDifficulty.SizeFor("Extra small", 5, null, 1));
            Assert.Equal("S", DelveDifficulty.SizeFor("Small", 5, null, 1));
            Assert.Equal("M", DelveDifficulty.SizeFor("Medium", 5, null, 1));
            Assert.Equal("L", DelveDifficulty.SizeFor("Large", 5, null, 1));
            Assert.Equal("XL", DelveDifficulty.SizeFor("XL", 5, null, 1));
            Assert.Equal(Run("Steady", 12, 7), Run("Steady", 12, 7));
            Assert.True(DelveDifficulty.IsPaceStyle("Steady"));
            Assert.False(DelveDifficulty.IsPaceStyle("Large"));
        }
    }
}
