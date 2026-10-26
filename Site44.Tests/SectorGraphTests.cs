using System;
using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class SectorGraphTests
    {
        public static IEnumerable<object[]> Sizes()
        {
            foreach (var s in new[] { new[] { 2, 2 }, new[] { 3, 3 }, new[] { 4, 7 }, new[] { 5, 5 }, new[] { 7, 7 }, new[] { 9, 9 }, new[] { 10, 6 } })
                yield return new object[] { s[0], s[1] };
        }

        static bool PathHas(SectorGraph g, int id, Func<SectorBlock, bool> p)
        {
            while (id >= 0) { var b = g.Blocks[id]; if (p(b)) return true; id = b.ParentId; }
            return false;
        }

        [Theory, MemberData(nameof(Sizes))]
        public void Invariants(int nx, int ny)
        {
            int n = nx * ny;
            for (int seed = 0; seed <= 300; seed++)
            {
                var g = Generate(nx, ny, seed);
                string ctx = nx + "x" + ny + " seed " + seed;
                Assert.Equal(n, g.Blocks.Count);
                Assert.Equal(seed, g.Seed);
                var root = g.Root;
                Assert.Equal(0, root.Gx); Assert.Equal(ny / 2, root.Gy);
                Assert.Equal(SectorKind.Courtyard, root.Kind);
                Assert.Equal(0, root.Depth); Assert.Equal(-1, root.ParentId);
                Assert.Equal(1, g.Blocks.Count(b => b.Kind == SectorKind.Courtyard));

                int edges = 0;
                foreach (var b in g.Blocks)
                {
                    Assert.Equal(b.Gy * nx + b.Gx, b.Id);
                    Assert.Same(b, g.At(b.Gx, b.Gy));
                    if (b == root) continue;
                    Assert.True(b.ParentId >= 0, ctx);
                    var p = g.Blocks[b.ParentId];
                    Assert.Equal(1, Math.Abs(p.Gx - b.Gx) + Math.Abs(p.Gy - b.Gy));
                    Assert.Equal(p.Depth + 1, b.Depth);
                    Assert.Contains(b.Id, p.Children);
                    edges++;
                    if (b.Gate == GateKind.Terminal)
                        Assert.InRange(b.RequiredTier, 0, Progression.MaxTier);
                }
                Assert.Equal(n - 1, edges);
                Assert.Equal(n - 1, g.Blocks.Sum(b => b.Children.Count));
                foreach (var b in g.Blocks) // every block reaches root
                {
                    int id = b.Id, steps = 0;
                    while (g.Blocks[id].ParentId >= 0 && steps++ <= n) id = g.Blocks[id].ParentId;
                    Assert.Equal(root.Id, id);
                }

                var leaves = g.Blocks.Where(b => b != root && b.Children.Count == 0).ToList();
                var reactors = g.Blocks.Where(b => b.Kind == SectorKind.Reactor).ToList();
                Assert.Single(reactors);
                Assert.Empty(reactors[0].Children);
                Assert.Equal(leaves.Max(l => l.Depth), reactors[0].Depth);

                Assert.Equal((n + 7) / 8, g.Blocks.Count(b => b.Kind == SectorKind.Diagnostic));

                var hots = g.Blocks.Where(b => b.Kind == SectorKind.HotZone).ToList();
                var valves = g.Blocks.Where(b => b.Kind == SectorKind.Valve).ToList();
                Assert.Equal(hots.Count > 0, valves.Count == 1);
                Assert.True(valves.Count <= 1);
                if (n < 12) { Assert.Empty(hots); Assert.Empty(valves); }
                else Assert.True(hots.Count <= Math.Max(1, n / 10));
                foreach (var h in hots)
                {
                    Assert.True(h.Depth >= 3); Assert.NotEmpty(h.Children);
                }
                if (valves.Count == 1)
                {
                    Assert.Empty(valves[0].Children);
                    Assert.False(PathHas(g, valves[0].Id, x => x.Kind == SectorKind.HotZone), ctx);
                }

                var storages = g.Blocks.Where(b => b.Kind == SectorKind.Storage).ToList();
                var evas = g.Blocks.Where(b => b.EvaCache).ToList();
                if (storages.Count > 0 || hots.Count > 0)
                {
                    Assert.Single(evas);
                    Assert.Equal(SectorKind.Storage, evas[0].Kind);
                    Assert.False(PathHas(g, evas[0].Id, x => x.Kind == SectorKind.HotZone), ctx);
                }
                else Assert.Empty(evas);

                var sim = g.Simulate();
                Assert.True(sim.AllReachable, ctx + " " + sim.Report);
                Assert.Empty(sim.UnreachableIds);
                Assert.False(string.IsNullOrEmpty(sim.Report));
            }
        }

        static SectorGraph Generate(int nx, int ny, int seed) { return SectorGraph.Generate(nx, ny, seed); }

        static string Sig(SectorGraph g)
        {
            return string.Join("|", g.Blocks.Select(b => string.Join(",", b.Id, b.ParentId, b.Depth, (int)b.Kind, (int)b.Gate, b.RequiredTier, b.EvaCache)));
        }

        [Theory, MemberData(nameof(Sizes))]
        public void Deterministic(int nx, int ny)
        {
            for (int seed = 0; seed <= 300; seed++)
                Assert.Equal(Sig(Generate(nx, ny, seed)), Sig(Generate(nx, ny, seed)));
        }

        [Fact]
        public void DifferentSeedsDiffer()
        {
            var set = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++) set.Add(Sig(Generate(7, 7, seed)));
            Assert.True(set.Count > 25);
        }

        [Fact]
        public void TierThresholds()
        {
            Assert.Equal(0, Progression.TierForPoints(1));
            Assert.Equal(1, Progression.TierForPoints(2));
            Assert.Equal(1, Progression.TierForPoints(4));
            Assert.Equal(2, Progression.TierForPoints(5));
            Assert.Equal(2, Progression.TierForPoints(8));
            Assert.Equal(3, Progression.TierForPoints(9));
        }

        [Theory]
        [InlineData(1, 5)]
        [InlineData(5, 1)]
        [InlineData(0, 0)]
        public void SmallSizesThrow(int nx, int ny)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SectorGraph.Generate(nx, ny, 1));
        }
    }
}
