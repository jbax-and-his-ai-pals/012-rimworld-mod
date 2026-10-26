using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class TownPlanTests
    {
        static readonly int[] Counts = { 12, 30, 45 };

        public static IEnumerable<object[]> SizeMask()
        {
            foreach (int size in new[] { 80, 120, 160 })
                for (int mask = 0; mask < 4; mask++)
                    yield return new object[] { size, mask };
        }

        static bool[] MakeMask(int size, int mask)
        {
            var b = new bool[size * size];
            int c = size / 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool bl = false;
                    switch (mask)
                    {
                        case 1: bl = Math.Abs((x - y) - size / 4) <= 2; break;                       // river diagonal
                        case 2:                                                                         // rock blob
                        {
                            int dx = x - (c + size / 4), dy = y - (c + size / 8), r = size / 10;
                            bl = dx * dx + dy * dy <= r * r;
                            break;
                        }
                        case 3:                                                                         // ring wall
                        {
                            int d = Math.Max(Math.Abs(x - c), Math.Abs(y - c));
                            int r = (int)(size * 0.36);
                            bl = d == r || d == r + 1;
                            break;
                        }
                    }
                    b[y * size + x] = bl;
                }
            return b;
        }

        static bool Inside(TownProject p, int x, int y)
        {
            return x >= p.X && y >= p.Y && x < p.X + p.Width && y < p.Y + p.Height;
        }

        static TownPlan Gen(int size, int mask, int seed, int count)
        {
            return TownPlan.Generate(size, size, size / 2, size / 2, MakeMask(size, mask), seed, count);
        }

        static void CheckProject(TownPlan plan, bool[] blocked, HashSet<int> streets, int[] owner, TownProject p, string ctx)
        {
            int W = plan.Width, H = plan.Height;
            Assert.True(p.Width > 0 && p.Height > 0 && p.Tiles.Length == p.Width * p.Height, ctx + " tiles length");
            Assert.True(p.X >= 0 && p.Y >= 0 && p.X + p.Width <= W && p.Y + p.Height <= H, ctx + " bounds");
            for (int ly = 0; ly < p.Height; ly++)
                for (int lx = 0; lx < p.Width; lx++)
                {
                    int x = p.X + lx, y = p.Y + ly, i = y * W + x;
                    Assert.False(blocked[i], ctx + " on blocked cell");
                    Assert.False(streets.Contains(i), ctx + " on street/plaza cell");
                }
            // 1-cell gap to every other project
            for (int y = p.Y - 1; y <= p.Y + p.Height; y++)
                for (int x = p.X - 1; x <= p.X + p.Width; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int o = owner[y * W + x];
                    if (o == 0 || o == p.Id + 1) continue;
                    Assert.True(false, ctx + " touches project " + (o - 1));
                }

            // door
            Assert.True(Inside(p, p.DoorX, p.DoorY), ctx + " door inside");
            int dlx = p.DoorX - p.X, dly = p.DoorY - p.Y;
            var door = p.Tiles[dly * p.Width + dlx];
            int[] ddx = { 1, -1, 0, 0 }, ddy = { 0, 0, 1, -1 };
            int found = 0, fdx = 0, fdy = 0;
            for (int d = 0; d < 4; d++)
            {
                int nx = p.DoorX + ddx[d], ny = p.DoorY + ddy[d];
                if (Inside(p, nx, ny)) continue;
                if (nx >= 0 && ny >= 0 && nx < W && ny < H && streets.Contains(ny * W + nx)) { found++; fdx = ddx[d]; fdy = ddy[d]; }
            }
            Assert.True(found >= 1, ctx + " door outside is not a street cell");
            bool building = p.Kind == TownProjectKind.House || p.Kind == TownProjectKind.Workshop || p.Kind == TownProjectKind.Canteen
                || p.Kind == TownProjectKind.Clinic || p.Kind == TownProjectKind.Watchtower;
            if (building || p.Kind == TownProjectKind.Garden)
            {
                Assert.Equal(PlanTile.Door, door);
                Assert.Equal(1, found);
                int bx = dlx - fdx, by = dly - fdy;
                Assert.True(bx >= 0 && by >= 0 && bx < p.Width && by < p.Height, ctx + " nothing behind door");
                var behind = p.Tiles[by * p.Width + bx];
                Assert.Equal(building ? PlanTile.Floor : PlanTile.Outside, behind);
            }
            else if (p.Kind == TownProjectKind.Stall)
                Assert.True(door == PlanTile.Floor || door == PlanTile.Outside, ctx + " stall entry");
            else if (p.Kind == TownProjectKind.WallSegment)
            {
                Assert.True(p.Width == 1 || p.Height == 1, ctx + " wall is thin");
                int n = Math.Max(p.Width, p.Height);
                Assert.InRange(n, 6, 12);
                Assert.All(p.Tiles, t => Assert.Equal(PlanTile.Wall, t));
            }

            // windows: floor behind, open outside
            for (int ly = 0; ly < p.Height; ly++)
                for (int lx = 0; lx < p.Width; lx++)
                {
                    if (p.Tiles[ly * p.Width + lx] != PlanTile.Window) continue;
                    int ix = lx, iy = ly, wx = p.X + lx, wy = p.Y + ly;
                    if (ly == 0) { iy++; wy--; } else if (ly == p.Height - 1) { iy--; wy++; }
                    else if (lx == 0) { ix++; wx--; } else if (lx == p.Width - 1) { ix--; wx++; }
                    else Assert.True(false, ctx + " window not on the edge");
                    Assert.Equal(PlanTile.Floor, p.Tiles[iy * p.Width + ix]);
                    Assert.True(wx >= 0 && wy >= 0 && wx < W && wy < H, ctx + " window outside bounds");
                    int wi = wy * W + wx;
                    Assert.False(blocked[wi], ctx + " window faces blocked cell");
                    Assert.True(owner[wi] == 0, ctx + " window faces a project");
                }
        }

        static void CheckPlan(TownPlan plan, bool[] blocked, int requested, string ctx)
        {
            int W = plan.Width, H = plan.Height;
            var streets = new HashSet<int>(plan.StreetCells);
            Assert.Equal(plan.StreetCells.Count, streets.Count);
            foreach (int s in streets) Assert.False(blocked[s], ctx + " street on blocked");

            // plaza and connectivity
            int pc = plan.PlazaY * W + plan.PlazaX;
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                    Assert.Contains((plan.PlazaY + dy) * W + plan.PlazaX + dx, streets);
            var seen = new HashSet<int> { pc };
            var q = new Queue<int>(); q.Enqueue(pc);
            while (q.Count > 0)
            {
                int c = q.Dequeue(); int x = c % W, y = c / W;
                int[] nb = { x > 0 ? c - 1 : -1, x < W - 1 ? c + 1 : -1, y > 0 ? c - W : -1, y < H - 1 ? c + W : -1 };
                foreach (int n in nb) if (n >= 0 && streets.Contains(n) && seen.Add(n)) q.Enqueue(n);
            }
            Assert.True(seen.Count == streets.Count, ctx + " street cells not connected: " + seen.Count + "/" + streets.Count);

            var owner = new int[W * H];
            foreach (var p in plan.Projects)
                for (int y = p.Y; y < p.Y + p.Height; y++)
                    for (int x = p.X; x < p.X + p.Width; x++)
                    {
                        Assert.True(x >= 0 && y >= 0 && x < W && y < H, ctx + " out of bounds");
                        Assert.True(owner[y * W + x] == 0, ctx + " overlap");
                        owner[y * W + x] = p.Id + 1;
                    }
            var projects = plan.Projects;
            Assert.True(projects.Count <= requested);
            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                Assert.Equal(i, p.Id);
                string pctx = ctx + " project " + i + " " + p.Kind;
                CheckProject(plan, blocked, streets, owner, p, pctx);
                Assert.InRange(p.StandingRequired, 0, 420);
                Assert.True(p.RevealStanding <= p.StandingRequired && p.RevealStanding >= 0, pctx + " reveal");
                Assert.True(p.WorkUnits > 0);
                foreach (int r in p.Requires) Assert.True(r >= 0 && r < p.Id, pctx + " requires");
                Assert.Equal(p.Requires.Length, p.Requires.Distinct().Count());
                if (p.Kind == TownProjectKind.WallSegment)
                    Assert.True(p.Requires.Count(r => projects[r].Kind == TownProjectKind.House) >= Math.Min(2, projects.Take(i).Count(x => x.Kind == TownProjectKind.House)), pctx + " wall needs houses");
                for (int j = i + 1; j < projects.Count; j++)
                    Assert.True(projects[j].StandingRequired >= p.StandingRequired - 25, pctx + " standing order vs " + j);
            }

            if (requested >= 30 && projects.Count > 0)
                Assert.True(projects.Count(p => p.Kind == TownProjectKind.House) * 100 >= 55 * projects.Count, ctx + " house share");
            if (requested >= 12)
            {
                int ca = projects.ToList().FindIndex(p => p.Kind == TownProjectKind.Canteen);
                int cl = projects.ToList().FindIndex(p => p.Kind == TownProjectKind.Clinic);
                Assert.InRange(ca, 0, 7);
                Assert.InRange(cl, 0, 7);
            }
        }

        [Theory, MemberData(nameof(SizeMask))]
        public void PlansAreValid(int size, int mask)
        {
            var blocked = MakeMask(size, mask);
            int total = 0, runs = 0;
            for (int seed = 1; seed <= 25; seed++)
                foreach (int count in Counts)
                {
                    var plan = Gen(size, mask, seed, count);
                    CheckPlan(plan, blocked, count, size + "x" + size + " mask " + mask + " seed " + seed + " n " + count);
                    total += plan.Projects.Count; runs++;
                }
            // the plans should come reasonably close to the requested counts on open ground
            if (mask == 0 && size >= 120) Assert.True(total >= runs * 0.9 * (12 + 30 + 45) / 3, "average placed " + (double)total / runs);
        }

        [Theory, MemberData(nameof(SizeMask))]
        public void Deterministic(int size, int mask)
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var a = Gen(size, mask, seed, 40);
                var b = Gen(size, mask, seed, 40);
                Assert.Equal(a.Render(1), b.Render(1));
                Assert.Equal(a.StreetCells.ToArray(), b.StreetCells.ToArray());
                Assert.Equal(a.Projects.Count, b.Projects.Count);
                for (int i = 0; i < a.Projects.Count; i++)
                {
                    var p = a.Projects[i]; var q = b.Projects[i];
                    Assert.Equal(p.Kind, q.Kind); Assert.Equal(p.X, q.X); Assert.Equal(p.Y, q.Y);
                    Assert.Equal(p.Tiles, q.Tiles); Assert.Equal(p.StandingRequired, q.StandingRequired);
                    Assert.Equal(p.Requires, q.Requires); Assert.Equal(p.WorkUnits, q.WorkUnits);
                }
            }
            Assert.NotEqual(Gen(size, mask, 1, 40).Render(1), Gen(size, mask, 2, 40).Render(1));
        }

        [Fact]
        public void BlockedPlazaThrows()
        {
            var b = new bool[80 * 80];
            b[42 * 80 + 38] = true;
            Assert.Throws<ArgumentException>(() => TownPlan.Generate(80, 80, 40, 40, b, 1, 30));
            Assert.Throws<ArgumentException>(() => TownPlan.Generate(80, 80, 2, 40, null, 1, 30));
            // a blocked cell just outside the plaza is fine
            b = new bool[80 * 80];
            b[45 * 80 + 40] = true;
            Assert.NotNull(TownPlan.Generate(80, 80, 40, 40, b, 1, 30));
            Assert.NotNull(TownPlan.Generate(80, 80, 40, 40, null, 1, 0));
        }

        static void CheckMoved(TownPlan plan, bool[] blocked, bool[] occupied, TownProject orig, TownProject moved, string ctx)
        {
            Assert.NotSame(orig, moved);
            Assert.Equal(orig.Id, moved.Id); Assert.Equal(orig.Kind, moved.Kind);
            Assert.Equal(orig.StandingRequired, moved.StandingRequired);
            Assert.Equal(orig.RevealStanding, moved.RevealStanding);
            Assert.Equal(orig.WorkUnits, moved.WorkUnits);
            Assert.Equal(orig.Requires, moved.Requires);
            Assert.Equal(orig.Width * orig.Height, moved.Width * moved.Height);
            Assert.True((moved.Width == orig.Width && moved.Height == orig.Height) || (moved.Width == orig.Height && moved.Height == orig.Width), ctx + " dims");
            foreach (PlanTile t in Enum.GetValues(typeof(PlanTile)))
                Assert.Equal(orig.Tiles.Count(x => x == t), moved.Tiles.Count(x => x == t));
            for (int y = moved.Y; y < moved.Y + moved.Height; y++)
                for (int x = moved.X; x < moved.X + moved.Width; x++)
                    Assert.False(occupied[y * plan.Width + x], ctx + " on occupied cell");
            // validate against the plan with the project swapped in
            plan.Apply(moved);
            CheckPlan(plan, blocked, int.MaxValue, ctx + " after Apply");
            Assert.Same(moved, plan.Projects[moved.Id]);
        }

        [Fact]
        public void TryReplaceFindsValidLots()
        {
            var rnd = new Random(1234);
            int ok = 0, total = 0;
            for (int c = 0; c < 200; c++)
            {
                int size = c % 2 == 0 ? 80 : 120;
                int mask = c % 4 == 3 ? 1 : (c % 4 == 1 ? 2 : 0);
                var blocked = MakeMask(size, mask);
                var plan = Gen(size, mask, 100 + c, 30 + rnd.Next(15));
                if (plan.Projects.Count == 0) continue;
                var p = plan.Projects[rnd.Next(plan.Projects.Count)];
                var occ = new bool[size * size];
                for (int y = p.Y; y < p.Y + p.Height; y++)
                    for (int x = p.X; x < p.X + p.Width; x++) occ[y * size + x] = true;
                int px = p.X + rnd.Next(-6, p.Width + 6), py = p.Y + rnd.Next(-6, p.Height + 6);
                for (int y = py; y < py + 6; y++)
                    for (int x = px; x < px + 6; x++)
                        if (x >= 0 && y >= 0 && x < size && y < size) occ[y * size + x] = true;
                int ox = p.X, oy = p.Y, ow = p.Width, oh = p.Height;
                var snapshot = (PlanTile[])p.Tiles.Clone();

                TownProject moved;
                bool res = plan.TryReplace(p, occ, out moved);
                total++;
                Assert.Equal(ox, p.X); Assert.Equal(oy, p.Y); Assert.Equal(ow, p.Width); Assert.Equal(oh, p.Height);
                Assert.Equal(snapshot, p.Tiles);
                Assert.Same(p, plan.Projects[p.Id]);
                if (!res) { Assert.Null(moved); continue; }
                ok++;
                CheckMoved(plan, blocked, occ, p, moved, "case " + c);
            }
            Assert.True(ok >= total * 0.85, "replace success " + ok + "/" + total);
        }

        [Fact]
        public void TryReplaceFailsWhenEverythingIsOccupied()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var plan = Gen(80, 0, seed, 30);
                var occ = new bool[80 * 80];
                for (int i = 0; i < occ.Length; i++) occ[i] = true;
                foreach (var p in plan.Projects)
                {
                    TownProject moved;
                    Assert.False(plan.TryReplace(p, occ, out moved));
                    Assert.Null(moved);
                }
                // everything near occupied (a 50x50 block around the project), open ground far away is still allowed to be used
                var q = plan.Projects[plan.Projects.Count - 1];
                var near = new bool[80 * 80];
                for (int y = 0; y < 80; y++)
                    for (int x = 0; x < 80; x++)
                        near[y * 80 + x] = true;
                TownProject m2;
                Assert.False(plan.TryReplace(q, near, out m2));
            }
        }

        [Fact]
        public void GeneratePerformance()
        {
            Gen(80, 0, 1, 30);   // warm up the JIT
            var sw = Stopwatch.StartNew();
            var plan = Gen(160, 0, 7, 45);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 300, "Generate took " + sw.ElapsedMilliseconds + " ms");
            Assert.True(plan.Projects.Count >= 40);
        }

        [Fact]
        public void PrintSample()
        {
            var plan = Gen(80, 0, 3, 45);
            var text = plan.Render(1);
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "townplan_sample.txt"),
                plan.Projects.Count + " projects\n" + text);
            Assert.True(plan.Projects.Count > 0);
        }
    }
}
