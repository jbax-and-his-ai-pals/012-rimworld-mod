using System.Collections.Generic;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class StoryPickerTests
    {
        private static List<StoryCandidate> Pool()
        {
            return new List<StoryCandidate>
            {
                new StoryCandidate { Name = "any1" },
                new StoryCandidate { Name = "any2" },
                new StoryCandidate { Name = "med1", SiteTypes = { "Medical" } },
                new StoryCandidate { Name = "off1", SiteTypes = { "Office" } }
            };
        }

        [Fact]
        public void NeverPicksAThreadForAnotherKindOfComplex()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                bool reset;
                string pick = StoryPicker.Pick(Pool(), new HashSet<string>(), "Medical", seed, out reset);
                Assert.NotEqual("off1", pick);
            }
        }

        [Fact]
        public void SkipsThreadsAlreadyToldInTheRun()
        {
            var used = new HashSet<string> { "any1", "any2" };
            for (int seed = 0; seed < 100; seed++)
            {
                bool reset;
                Assert.Equal("med1", StoryPicker.Pick(Pool(), used, "Medical", seed, out reset));
                Assert.False(reset);
            }
        }

        [Fact]
        public void StartsOverWhenEverythingSuitableWasTold()
        {
            var used = new HashSet<string> { "any1", "any2", "med1" };
            bool reset;
            Assert.NotNull(StoryPicker.Pick(Pool(), used, "Medical", 1, out reset));
            Assert.True(reset);
        }

        [Fact]
        public void FavoursThreadsWrittenForTheComplex()
        {
            int specific = 0;
            for (int seed = 0; seed < 1000; seed++)
            {
                bool reset;
                if (StoryPicker.Pick(Pool(), new HashSet<string>(), "Medical", seed, out reset) == "med1")
                {
                    specific++;
                }
            }

            Assert.InRange(specific, 450, 650);
        }
    }
}
