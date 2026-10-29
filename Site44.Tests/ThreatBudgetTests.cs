using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class ThreatBudgetTests
    {
        // A strip of rooms 0..n-1, each next to its neighbours, four rooms to a zone; the last few are security zones.
        private static List<ThreatSite> Sites(int rooms, int securityZones)
        {
            var list = new List<ThreatSite>();
            for (int i = 0; i < rooms; i++)
            {
                var site = new ThreatSite { Id = i, ZoneId = i / 4, Kind = ThreatKind.Nest };
                if (i > 0) site.Neighbours.Add(i - 1);
                if (i < rooms - 1) site.Neighbours.Add(i + 1);
                list.Add(site);
            }

            for (int z = 0; z < securityZones; z++)
            {
                list.Add(new ThreatSite { Id = 100 + z, ZoneId = 100 + z, Kind = ThreatKind.Security });
            }

            return list;
        }

        private static ThreatSettings Settings(int depth, float budget)
        {
            return new ThreatSettings { Depth = depth, Budget = budget };
        }

        [Fact]
        public void SameSeedSamePlan()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var a = ThreatBudget.Plan(seed, Sites(40, 3), Settings(3, 20f));
                var b = ThreatBudget.Plan(seed, Sites(40, 3), Settings(3, 20f));
                Assert.Equal(string.Join(",", a.Select(p => p.Kind + ":" + p.SiteId + ":" + p.Tier)), string.Join(",", b.Select(p => p.Kind + ":" + p.SiteId + ":" + p.Tier)));
            }
        }

        [Fact]
        public void NeverSpendsMoreThanTheBudget()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (float budget in new[] { 0f, 2f, 5f, 12f, 30f, 80f })
                {
                    var plan = ThreatBudget.Plan(seed, Sites(60, 4), Settings(1 + seed % 8, budget));
                    Assert.True(plan.Sum(p => p.Cost) <= budget + 0.001f, "seed " + seed + " budget " + budget);
                }
            }
        }

        [Fact]
        public void NothingWithoutBudgetOrWeights()
        {
            Assert.Empty(ThreatBudget.Plan(1, Sites(40, 3), Settings(2, 0f)));
            Assert.Empty(ThreatBudget.Plan(1, Sites(40, 3), new ThreatSettings { Depth = 2, Budget = 30f, NestWeight = 0f, SecurityWeight = 0f }));
        }

        [Fact]
        public void TiersFollowDepth()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                foreach (int depth in new[] { 1, 2, 4, 8 })
                {
                    var plan = ThreatBudget.Plan(seed, Sites(60, 4), Settings(depth, 200f));
                    Assert.All(plan.Where(p => p.Kind == ThreatKind.Nest), p => Assert.InRange(p.Tier, 1, ThreatBudget.NestTierCap(depth)));
                    Assert.All(plan.Where(p => p.Kind == ThreatKind.Security), p => Assert.InRange(p.Tier, 1, ThreatBudget.SecurityTierCap(depth)));
                }
            }
        }

        [Fact]
        public void NestsAreNeverNextToEachOtherOrCrowdedInAZone()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var sites = Sites(60, 0);
                var nests = ThreatBudget.Plan(seed, sites, Settings(5, 300f)).Where(p => p.Kind == ThreatKind.Nest).ToList();
                var ids = new HashSet<int>(nests.Select(n => n.SiteId));
                Assert.All(nests, n => Assert.DoesNotContain(sites[n.SiteId].Neighbours, ids.Contains));
                Assert.All(nests.GroupBy(n => sites[n.SiteId].ZoneId), g => Assert.InRange(g.Count(), 1, 2));
            }
        }

        [Fact]
        public void WeightsMoveTheMixAndZeroSwitchesAKindOff()
        {
            var noNests = ThreatBudget.Plan(3, Sites(60, 4), new ThreatSettings { Depth = 4, Budget = 60f, NestWeight = 0f });
            Assert.DoesNotContain(noNests, p => p.Kind == ThreatKind.Nest);
            Assert.Contains(noNests, p => p.Kind == ThreatKind.Security);

            var noSecurity = ThreatBudget.Plan(3, Sites(60, 4), new ThreatSettings { Depth = 4, Budget = 60f, SecurityWeight = 0f });
            Assert.DoesNotContain(noSecurity, p => p.Kind == ThreatKind.Security);
            Assert.Contains(noSecurity, p => p.Kind == ThreatKind.Nest);
        }

        [Fact]
        public void UnspentSecurityMoneyGoesToNests()
        {
            // No security sectors at all: the whole budget is available to the nests.
            var plan = ThreatBudget.Plan(5, Sites(60, 0), Settings(3, 25f));
            Assert.True(plan.Count(p => p.Kind == ThreatKind.Nest) >= 4);
        }

        [Fact]
        public void SmallComplexesStillGetHarderWithDepth()
        {
            Assert.True(ThreatBudget.BudgetFor(10, 25, 1f) - ThreatBudget.BudgetFor(1, 25, 1f) >= 8f);
        }

        [Fact]
        public void AnyLevelThatCanAffordAThreatGetsOne()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                // Only security sites and a budget whose security share is too small: it is funded anyway.
                var onlySecurity = ThreatBudget.Plan(seed, Sites(0, 2), Settings(1, 4f));
                Assert.Single(onlySecurity);
                Assert.Equal(ThreatKind.Security, onlySecurity[0].Kind);

                // A budget just above the cheapest nest.
                var nest = ThreatBudget.Plan(seed, Sites(12, 0), Settings(1, 2.6f));
                Assert.Single(nest);
                Assert.Equal(ThreatKind.Nest, nest[0].Kind);

                // Too poor for anything.
                Assert.Empty(ThreatBudget.Plan(seed, Sites(12, 2), Settings(1, 2.0f)));
            }
        }

        [Fact]
        public void BudgetGrowsWithDepthDangerAndSize()
        {
            Assert.True(ThreatBudget.BudgetFor(4, 40, 1f) > ThreatBudget.BudgetFor(1, 40, 1f));
            Assert.True(ThreatBudget.BudgetFor(1, 40, 1.5f) > ThreatBudget.BudgetFor(1, 40, 1f));
            Assert.True(ThreatBudget.BudgetFor(1, 80, 1f) > ThreatBudget.BudgetFor(1, 40, 1f));
            Assert.Equal(0f, ThreatBudget.BudgetFor(3, 40, 0f));
            Assert.Equal(ThreatBudget.PointsPerRoom(20), ThreatBudget.PointsPerRoom(40));
        }
    }
}
