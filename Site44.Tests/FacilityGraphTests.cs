using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    // Synthetic plan construction helper (does not depend on BuildingPlanner).
    internal sealed class PlanSketch
    {
        public readonly BuildingPlan Plan;

        public PlanSketch(int w, int h) { Plan = new BuildingPlan(w, h); }

        public int Room(int x0, int y0, int w, int h, RoomFunction f, bool corridor, int building)
        {
            int id = Plan.Rooms.Count;
            var r = new PlanRoom { Id = id, Function = f, IsCorridor = corridor, BuildingId = building };
            for (int y = y0 - 1; y <= y0 + h; y++)
            {
                for (int x = x0 - 1; x <= x0 + w; x++)
                {
                    int idx = y * Plan.Width + x;
                    bool inner = x >= x0 && x < x0 + w && y >= y0 && y < y0 + h;
                    if (inner)
                    {
                        Plan.Tiles[idx] = PlanTile.Floor;
                        Plan.RoomIds[idx] = id;
                        r.Cells.Add(new LayoutCell(x, y));
                    }
                    else if (Plan.Tiles[idx] == PlanTile.Outside)
                    {
                        Plan.Tiles[idx] = PlanTile.Wall;
                    }
                }
            }
            r.Anchor = r.Cells[0];
            Plan.Rooms.Add(r);
            return id;
        }

        public int Door(int a, int b, int x, int y, bool fire)
        {
            Plan.Tiles[y * Plan.Width + x] = PlanTile.Door;
            Plan.Links.Add(new PlanLink { RoomA = a, RoomB = b, Cell = new LayoutCell(x, y), Fire = fire });
            return Plan.Links.Count - 1;
        }
    }

    internal static class FacilityTestPlans
    {
        private static readonly RoomFunction[] Fns =
        {
            RoomFunction.OpenOffice, RoomFunction.PrivateOffice, RoomFunction.ServerRoom, RoomFunction.Storeroom,
            RoomFunction.Conference, RoomFunction.SecurityPost, RoomFunction.Mechanical, RoomFunction.Archive,
            RoomFunction.BreakRoom, RoomFunction.MachineHall, RoomFunction.Restroom, RoomFunction.Closet
        };

        // Double-loaded bar: corridor segments (3 offices each side) separated by fire doors. Returns last corridor interior x.
        private static int Bar(PlanSketch s, int x0, int cy, int segs, int building, bool lobby, int h, ref int fnCounter, List<int> corridors)
        {
            int prevCorr = -1;
            for (int j = 0; j < segs; j++)
            {
                int cx = x0 + j * 18;
                int corr = s.Room(cx, cy, 17, 1, RoomFunction.Corridor, true, building);
                corridors.Add(corr);
                if (prevCorr >= 0) s.Door(prevCorr, corr, cx - 1, cy, true);
                prevCorr = corr;
                var north = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    int ox = cx + i * 6;
                    RoomFunction fn = (lobby && j == 0 && i == 0) ? RoomFunction.Lobby : Fns[fnCounter++ % Fns.Length];
                    north[i] = s.Room(ox, cy + 2, 5, h, fn, false, building);
                    int south = s.Room(ox, cy - 1 - h, 5, h, Fns[fnCounter++ % Fns.Length], false, building);
                    s.Door(corr, north[i], ox + 2, cy + 1, false);
                    s.Door(corr, south, ox + 2, cy - 1, false);
                }
                for (int i = 0; i < 2; i++)
                    if ((i + j) % 2 == 0) s.Door(north[i], north[i + 1], cx + i * 6 + 5, cy + 2 + h / 2, false);
            }
            if (lobby)
            {
                // exterior door on the lobby's north wall
                int ox = x0;
                s.Door(corridors[0] + 1, -1, ox + 2, cy + 2 + h, false);
                s.Plan.MainEntrance = new LayoutCell(ox + 2, cy + 2 + h);
                s.Plan.Start = new LayoutCell(ox + 2, cy + 3 + h);
            }
            return x0 + (segs - 1) * 18 + 16;
        }

        public static BuildingPlan MakeBar(int segs)
        {
            int h = 6;
            int cy = h + 3;
            var s = new PlanSketch(2 + segs * 18 + 4, cy + h + 6);
            s.Plan.Buildings.Add(new PlanBuilding { Id = 0, Archetype = "Office", MinX = 1, MinY = 0, MaxX = 2 + segs * 18, MaxY = cy + h + 3 });
            int fn = 0;
            Bar(s, 2, cy, segs, 0, true, h, ref fn, new List<int>());
            return s.Plan;
        }

        // Main bar (with lobby + a service door on a south office) and an ISOLATED bar (no link corridor) entered only from the yard
        // through 1..2 exterior doors. With extra = true a second small isolated 2-room building is added.
        public static BuildingPlan MakeIsolated(int segsA, int segsB, bool extra)
        {
            int h = 6;
            int cy = h + 3;
            int x0A = 2;
            int xe = x0A + (segsA - 1) * 18 + 16;
            int x0B = xe + 4;
            int xEnd = x0B + segsB * 18 + 2;
            var s = new PlanSketch(xEnd + (extra ? 20 : 4), cy + h + 6);
            s.Plan.Buildings.Add(new PlanBuilding { Id = 0, Archetype = "Office" });
            s.Plan.Buildings.Add(new PlanBuilding { Id = 1, Archetype = "Warehouse" });
            int fn = 0;
            var ca = new List<int>();
            var cb = new List<int>();
            Bar(s, x0A, cy, segsA, 0, true, h, ref fn, ca);
            Bar(s, x0B, cy, segsB, 1, false, h, ref fn, cb);
            // service door on a south office of the main building
            int cxA = x0A + 18;
            s.Door(ca[1] + 2, -1, cxA + 2, cy - 2 - h, false);
            // exterior doors of the isolated bar: corridor west end, and a north office of the last segment
            s.Door(cb[0], -1, x0B - 1, cy, false);
            s.Door(cb[cb.Count - 1] + 1, -1, x0B + (segsB - 1) * 18 + 2, cy + 2 + h, false);
            if (extra)
            {
                int ex = xEnd + 3;
                int a = s.Room(ex, 2, 6, 6, RoomFunction.Storeroom, false, 1);
                int b = s.Room(ex + 8, 2, 6, 6, RoomFunction.Mechanical, false, 1);
                s.Door(a, b, ex + 6, 4, false);
                s.Door(a, -1, ex + 2, 8, false);
            }
            return s.Plan;
        }

        public static BuildingPlan MakeLShape(int segsA, int segsB)
        {
            int h = 6;
            int cyA = h + 3;
            int cyB = cyA + 10;
            int x0A = 2;
            int xe = x0A + (segsA - 1) * 18 + 16;
            int x0B = xe + 4;
            var s = new PlanSketch(x0B + segsB * 18 + 4, cyB + h + 6);
            s.Plan.Buildings.Add(new PlanBuilding { Id = 0, Archetype = "Office" });
            s.Plan.Buildings.Add(new PlanBuilding { Id = 1, Archetype = "Lab" });
            int fn = 0;
            var ca = new List<int>();
            var cb = new List<int>();
            Bar(s, x0A, cyA, segsA, 0, true, h, ref fn, ca);
            Bar(s, x0B, cyB, segsB, 1, false, h, ref fn, cb);
            int link = s.Room(xe + 2, cyA, 1, cyB - cyA + 1, RoomFunction.Corridor, true, 0);
            s.Door(ca[ca.Count - 1], link, xe + 1, cyA, false);
            s.Door(link, cb[0], x0B - 1, cyB, false);
            return s.Plan;
        }

        public static BuildingPlan MakeRing(int n)
        {
            int h = 7, ih = 6;
            int by = h + 3;
            int ty = by + ih + 3;
            int x0 = 4;
            int x1 = x0 + n * 6 - 2;
            int rx = x1 + 2;
            var s = new PlanSketch(rx + 4, ty + h + 6);
            s.Plan.Buildings.Add(new PlanBuilding { Id = 0, Archetype = "Plant" });
            int half = n / 2;
            int hw = half * 6 - 1;
            int topA = s.Room(x0, ty, hw, 1, RoomFunction.Corridor, true, 0);
            int topB = s.Room(x0 + half * 6, ty, (n - half) * 6 - 1, 1, RoomFunction.Corridor, true, 0);
            int botA = s.Room(x0, by, hw, 1, RoomFunction.Corridor, true, 0);
            int botB = s.Room(x0 + half * 6, by, (n - half) * 6 - 1, 1, RoomFunction.Corridor, true, 0);
            int left = s.Room(x0 - 2, by, 1, ty - by + 1, RoomFunction.Corridor, true, 0);
            int right = s.Room(rx, by, 1, ty - by + 1, RoomFunction.Corridor, true, 0);
            s.Door(topA, topB, x0 + half * 6 - 1, ty, true);
            s.Door(botA, botB, x0 + half * 6 - 1, by, true);
            s.Door(topB, right, x1 + 1, ty, true);
            s.Door(botB, right, x1 + 1, by, false);
            s.Door(topA, left, x0 - 1, ty, false);
            s.Door(botA, left, x0 - 1, by, true);
            int fn = 0;
            for (int i = 0; i < n; i++)
            {
                int ox = x0 + i * 6;
                int top = i < half ? topA : topB, bot = i < half ? botA : botB;
                RoomFunction f0 = i == 0 ? RoomFunction.Lobby : Fns[fn++ % Fns.Length];
                int north = s.Room(ox, ty + 2, 5, h, f0, false, 0);
                int south = s.Room(ox, by - 1 - h, 5, h, Fns[fn++ % Fns.Length], false, 0);
                int inner = s.Room(ox, by + 2, 5, ih, Fns[fn++ % Fns.Length], false, 0);
                s.Door(top, north, ox + 2, ty + 1, false);
                s.Door(bot, south, ox + 2, by - 1, false);
                s.Door(top, inner, ox + 2, ty - 1, false);
                s.Door(bot, inner, ox + 2, by + 1, false);
                if (i == 0)
                {
                    s.Door(north, -1, ox + 2, ty + 2 + h, false);
                    s.Plan.MainEntrance = new LayoutCell(ox + 2, ty + 2 + h);
                    s.Plan.Start = new LayoutCell(ox + 2, ty + 3 + h);
                }
            }
            return s.Plan;
        }
    }

    public class FacilityGraphTests
    {
        public static IEnumerable<object[]> Shapes()
        {
            yield return new object[] { "bar6", 4 };
            yield return new object[] { "bar6", 6 };
            yield return new object[] { "bar6", 8 };
            yield return new object[] { "bar12", 12 };
            yield return new object[] { "bar12", 16 };
            yield return new object[] { "bar4", 4 };
            yield return new object[] { "iso", 8 };
            yield return new object[] { "iso", 12 };
            yield return new object[] { "iso2", 8 };
            yield return new object[] { "L", 5 };
            yield return new object[] { "L", 8 };
            yield return new object[] { "ring", 4 };
            yield return new object[] { "ring", 6 };
        }

        static BuildingPlan Plan(string name)
        {
            switch (name)
            {
                case "bar6": return FacilityTestPlans.MakeBar(6);
                case "bar12": return FacilityTestPlans.MakeBar(12);
                case "bar4": return FacilityTestPlans.MakeBar(4);
                case "iso": return FacilityTestPlans.MakeIsolated(4, 3, false);
                case "iso2": return FacilityTestPlans.MakeIsolated(3, 3, true);
                case "L": return FacilityTestPlans.MakeLShape(3, 3);
                default: return FacilityTestPlans.MakeRing(6);
            }
        }

        static string Dump(FacilityGraph g)
        {
            var sb = new StringBuilder();
            foreach (var z in g.Zones)
                sb.Append(z.Id).Append(':').Append(z.ParentId).Append(':').Append(z.Depth).Append(':').Append(z.Kind).Append(':').Append(z.Gate)
                  .Append(':').Append(z.RequiredTier).Append(':').Append(z.GateLink).Append(':').Append(z.EvaCache).Append(':').Append(string.Join(",", z.RoomIds)).Append('|');
            sb.Append(string.Join(",", g.SealedLinks));
            return sb.ToString();
        }

        static bool IsMain(BuildingPlan plan, PlanLink l)
        {
            return (l.RoomA < 0 || l.RoomB < 0) && l.Cell.X == plan.MainEntrance.X && l.Cell.Y == plan.MainEntrance.Y;
        }

        static bool PathHas(FacilityGraph g, int id, Func<Zone, bool> p)
        {
            while (id >= 0) { var z = g.Zones[id]; if (p(z)) return true; id = z.ParentId; }
            return false;
        }

        [Theory, MemberData(nameof(Shapes))]
        public void Invariants(string shape, int target)
        {
            var plan = Plan(shape);
            int lo = (int)Math.Ceiling(0.65 * target - 1e-9), hi = (int)Math.Floor(1.35 * target + 1e-9);
            int totalCells = plan.Rooms.Sum(r => r.Cells.Count);
            for (int seed = 0; seed < 120; seed++)
            {
                string ctx = shape + " T" + target + " seed " + seed;
                var g = FacilityGraph.Build(plan, target, seed);
                int n = g.Zones.Count;
                Assert.Equal(seed, g.Seed);
                Assert.Same(plan, g.Plan);

                // count and balance
                Assert.True(n >= lo && n <= hi, ctx + " zone count " + n);
                double avg = totalCells / (double)n;
                foreach (var z in g.Zones)
                {
                    Assert.True(z.CellCount >= 60, ctx + " zone " + z.Id + " has " + z.CellCount + " cells");
                    Assert.True(z.CellCount <= 3 * avg || z.RoomIds.Count == 1, ctx + " zone " + z.Id + " oversize " + z.CellCount + " avg " + avg);
                    Assert.Equal(z.RoomIds.Sum(r => plan.Rooms[r].Cells.Count), z.CellCount);
                }

                // every room in exactly one zone; ZoneOfRoom / ZoneAt consistent
                var seen = new int[plan.Rooms.Count];
                foreach (var z in g.Zones)
                    foreach (int r in z.RoomIds) { seen[r]++; Assert.Equal(z.Id, g.ZoneOfRoom(r)); }
                Assert.All(seen, c => Assert.Equal(1, c));
                Assert.Equal(-1, g.ZoneOfRoom(-1));
                Assert.Equal(-1, g.ZoneOfRoom(plan.Rooms.Count));
                for (int y = 0; y < plan.Height; y++)
                {
                    for (int x = 0; x < plan.Width; x++)
                    {
                        var zat = g.ZoneAt(x, y);
                        switch (plan.At(x, y))
                        {
                            case PlanTile.Floor:
                                Assert.Equal(g.ZoneOfRoom(plan.RoomAt(x, y)), zat.Id);
                                break;
                            case PlanTile.Door:
                                var link = plan.Links.First(l => l.Cell.X == x && l.Cell.Y == y);
                                Assert.Equal(g.ZoneOfRoom(link.RoomA >= 0 ? link.RoomA : link.RoomB), zat.Id);
                                break;
                            default:
                                Assert.Null(zat);
                                break;
                        }
                    }
                }
                Assert.Null(g.ZoneAt(-1, 0));
                Assert.Null(g.ZoneAt(plan.Width, 0));

                // each zone connected via links inside it
                foreach (var z in g.Zones)
                {
                    var set = new HashSet<int>(z.RoomIds);
                    var reach = new HashSet<int> { z.RoomIds[0] };
                    var q = new Queue<int>(); q.Enqueue(z.RoomIds[0]);
                    while (q.Count > 0)
                    {
                        int r = q.Dequeue();
                        foreach (var l in plan.Links)
                        {
                            if (l.RoomB < 0) continue;
                            int o = l.RoomA == r ? l.RoomB : l.RoomB == r ? l.RoomA : -1;
                            if (o >= 0 && set.Contains(o) && reach.Add(o)) q.Enqueue(o);
                        }
                    }
                    Assert.True(reach.Count == set.Count, ctx + " zone " + z.Id + " disconnected");
                }

                // root
                var root = g.Root;
                Assert.Same(g.Zones[0], root);
                Assert.Equal(0, root.Depth); Assert.Equal(-1, root.ParentId); Assert.Equal(-1, root.GateLink);
                Assert.Equal(SectorKind.Courtyard, root.Kind);
                Assert.Equal(1, g.Zones.Count(z => z.Kind == SectorKind.Courtyard));
                Assert.Equal(root.Id, g.ZoneAt(plan.MainEntrance.X, plan.MainEntrance.Y).Id);

                // spanning tree
                int edges = 0;
                var gateLinks = new HashSet<int>();
                foreach (var z in g.Zones)
                {
                    foreach (int c in z.Children) Assert.Equal(z.Id, g.Zones[c].ParentId);
                    if (z == root) continue;
                    edges++;
                    Assert.True(z.ParentId >= 0, ctx);
                    var p = g.Zones[z.ParentId];
                    Assert.Equal(p.Depth + 1, z.Depth);
                    Assert.Contains(z.Id, p.Children);
                    Assert.True(z.GateLink >= 0 && z.GateLink < plan.Links.Count, ctx);
                    var gl = plan.Links[z.GateLink];
                    Assert.True(gateLinks.Add(z.GateLink), ctx + " duplicate gate link");
                    Assert.False(IsMain(plan, gl), ctx + " main entrance used as gate");
                    if (gl.RoomA >= 0 && gl.RoomB >= 0)
                    {
                        int za = g.ZoneOfRoom(gl.RoomA), zb = g.ZoneOfRoom(gl.RoomB);
                        Assert.True((za == z.Id && zb == p.Id) || (zb == z.Id && za == p.Id), ctx + " gate link not between parent and child");
                    }
                    else
                    {
                        Assert.Equal(root.Id, z.ParentId);
                        Assert.Equal(z.Id, g.ZoneOfRoom(gl.RoomA >= 0 ? gl.RoomA : gl.RoomB));
                    }
                    // tiers / gates
                    if (z.Gate == GateKind.Open) Assert.Equal(0, z.RequiredTier);
                    // only a restricted wing (a dead-end hall or storage zone, never the EVA cache) asks for clearance
                    Assert.Equal(z.Restricted, z.RequiredTier > 0);
                    if (z.Restricted)
                    {
                        Assert.Equal(GateKind.Terminal, z.Gate);
                        Assert.Empty(z.Children);
                        Assert.False(z.EvaCache);
                    }
                    Assert.InRange(z.RequiredTier, 0, Progression.MaxTier);
                }
                Assert.Equal(n - 1, edges);
                Assert.Equal(n - 1, g.Zones.Sum(z => z.Children.Count));
                // reachability from the root along parents
                foreach (var z in g.Zones)
                {
                    int id = z.Id, steps = 0;
                    while (id != 0 && steps++ <= n) id = g.Zones[id].ParentId;
                    Assert.Equal(0, id);
                }

                // sealed links: exactly the non-tree cross-zone door links
                var expected = new List<int>();
                for (int li = 0; li < plan.Links.Count; li++)
                {
                    var l = plan.Links[li];
                    if (gateLinks.Contains(li)) continue;
                    if (l.RoomA < 0 || l.RoomB < 0)
                    {
                        if (!IsMain(plan, l)) expected.Add(li);
                        continue;
                    }
                    if (g.ZoneOfRoom(l.RoomA) == g.ZoneOfRoom(l.RoomB)) continue;
                    expected.Add(li);
                }
                Assert.Equal(expected, g.SealedLinks.ToList());
                for (int li = 0; li < plan.Links.Count; li++)
                    if (IsMain(plan, plan.Links[li]))
                    {
                        Assert.DoesNotContain(li, gateLinks);
                        Assert.DoesNotContain(li, g.SealedLinks.ToList());
                    }

                // each interior component not containing Root is entered through exactly one exterior tree edge
                var zc = Enumerable.Repeat(-1, n).ToArray();
                int ncomp = 0;
                for (int st = 0; st < n; st++)
                {
                    if (zc[st] >= 0) continue;
                    var qq = new Queue<int>(); qq.Enqueue(st); zc[st] = ncomp;
                    while (qq.Count > 0)
                    {
                        int cz = qq.Dequeue();
                        foreach (var l in plan.Links)
                        {
                            if (l.RoomA < 0 || l.RoomB < 0) continue;
                            int a = g.ZoneOfRoom(l.RoomA), b = g.ZoneOfRoom(l.RoomB);
                            int o = a == cz && b != cz ? b : b == cz && a != cz ? a : -1;
                            if (o >= 0 && zc[o] < 0) { zc[o] = ncomp; qq.Enqueue(o); }
                        }
                    }
                    ncomp++;
                }
                for (int cidx = 1; cidx < ncomp; cidx++)
                    if (zc[0] != cidx)
                        Assert.Equal(1, g.Zones.Count(z => zc[z.Id] == cidx && z.ParentId == 0));

                // role rules
                int hot = g.Zones.Count(z => z.Kind == SectorKind.HotZone);
                int valve = g.Zones.Count(z => z.Kind == SectorKind.Valve);
                Assert.Equal(1, g.Zones.Count(z => z.Kind == SectorKind.Reactor));
                Assert.Equal(hot > 0 ? 1 : 0, valve);
                if (n < 12) Assert.Equal(0, hot);
                Assert.True(hot <= Math.Max(1, n / 10), ctx);
                Assert.Equal((n + 7) / 8, g.Zones.Count(z => z.Kind == SectorKind.Diagnostic));
                var valveZone = g.Zones.FirstOrDefault(z => z.Kind == SectorKind.Valve);
                if (valveZone != null)
                    Assert.False(PathHas(g, valveZone.Id, z => z.Kind == SectorKind.HotZone), ctx + " hot zone on valve path");
                foreach (var z in g.Zones.Where(z => z.Kind == SectorKind.HotZone))
                {
                    Assert.True(z.Depth >= 3 && z.Children.Count > 0, ctx);
                }
                var evas = g.Zones.Where(z => z.EvaCache).ToList();
                Assert.True(evas.Count <= 1, ctx);
                if (g.Zones.Any(z => z.Kind == SectorKind.Storage) || hot > 0) Assert.Single(evas);
                foreach (var e in evas)
                {
                    Assert.Equal(SectorKind.Storage, e.Kind);
                    Assert.False(PathHas(g, e.Id, z => z.Kind == SectorKind.HotZone), ctx + " eva behind hot zone");
                }

                Assert.True(g.Simulate().AllReachable, ctx + "\n" + g.Simulate().Report);
            }
        }

        [Theory, MemberData(nameof(Shapes))]
        public void Deterministic(string shape, int target)
        {
            var plan = Plan(shape);
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.Equal(Dump(FacilityGraph.Build(plan, target, seed)), Dump(FacilityGraph.Build(Plan(shape), target, seed)));
            }
        }

        [Fact]
        public void DifferentSeedsVary()
        {
            var plan = FacilityTestPlans.MakeBar(6);
            var dumps = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++) dumps.Add(Dump(FacilityGraph.Build(plan, 10, seed)));
            Assert.True(dumps.Count > 10);
        }

        [Fact]
        public void SomeSeedsHaveARestrictedWingButNotAll()
        {
            var plan = FacilityTestPlans.MakeBar(6);
            int with = 0;
            for (int seed = 0; seed < 60; seed++)
                if (FacilityGraph.Build(plan, 16, seed).Zones.Any(z => z.Restricted)) with++;
Assert.InRange(with, 15, 58);
        }

        [Fact]
        public void BarZonesCutAtFireDoorsMostly()
        {
            // corridor-adjacent gates should dominate; fire doors should be used as gates fairly often
            var plan = FacilityTestPlans.MakeBar(6);
            int fire = 0, total = 0, corr = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var g = FacilityGraph.Build(plan, 8, seed);
                foreach (var z in g.Zones.Where(z => z.GateLink >= 0))
                {
                    var l = plan.Links[z.GateLink];
                    total++;
                    if (l.Fire) fire++;
                    if (plan.Rooms[l.RoomA].IsCorridor || plan.Rooms[l.RoomB].IsCorridor) corr++;
                }
            }
            Assert.True(corr * 100 >= total * 70, "corridor gates " + corr + "/" + total);
            Assert.True(fire * 100 >= total * 15, "fire gates " + fire + "/" + total);
        }

        [Fact]
        public void LShapeSeparatesBuildingsAtLinkCorridor()
        {
            var plan = FacilityTestPlans.MakeLShape(3, 3);
            int spanning = 0, runs = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var g = FacilityGraph.Build(plan, 8, seed);
                runs++;
                foreach (var z in g.Zones)
                {
                    var b = z.RoomIds.Select(r => plan.Rooms[r].BuildingId).Distinct().Count();
                    if (b > 1) spanning++;
                }
            }
            // buildings 0 and 1 only meet via the link corridor (building 0); a zone may hold it plus building-1 rooms rarely
            Assert.True(spanning * 100 <= runs * 8 * 20, "zones spanning buildings: " + spanning);
        }

        [Fact]
        public void TinyPlansDegradeGracefully()
        {
            var s = new PlanSketch(20, 10);
            int a = s.Room(1, 1, 4, 4, RoomFunction.Lobby, false, 0);
            int b = s.Room(7, 1, 4, 4, RoomFunction.OpenOffice, false, 0);
            s.Door(a, b, 5, 2, false);
            s.Door(a, -1, 2, 5, false);
            s.Plan.MainEntrance = new LayoutCell(2, 5);
            s.Plan.Buildings.Add(new PlanBuilding { Id = 0 });
            for (int seed = 0; seed < 50; seed++)
            {
                var g = FacilityGraph.Build(s.Plan, 12, seed);
                Assert.Equal(2, g.Zones.Count);
                Assert.True(g.Simulate().AllReachable);
                Assert.Equal(SectorKind.Courtyard, g.Root.Kind);
                Assert.Equal(SectorKind.Reactor, g.Zones[1].Kind);
            }

            var one = new PlanSketch(10, 10);
            one.Room(1, 1, 4, 4, RoomFunction.Lobby, false, 0);
            var g1 = FacilityGraph.Build(one.Plan, 5, 1);
            Assert.Single(g1.Zones);
            Assert.True(g1.Simulate().AllReachable);
        }
    }
}
