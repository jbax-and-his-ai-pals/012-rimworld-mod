using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    public class BuildingPlannerTests
    {
        private readonly ITestOutputHelper output;
        public BuildingPlannerTests(ITestOutputHelper output) { this.output = output; }

        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

        static readonly int[][] Sizes =
        {
            new[] { 100, 100 }, new[] { 150, 150 }, new[] { 200, 200 }, new[] { 226, 226 }, new[] { 240, 180 }, new[] { 260, 260 },
        };

        const int SeedsPerSize = 20;

        static IEnumerable<KeyValuePair<int[], int>> AllCases()
        {
            foreach (int[] s in Sizes)
                for (int seed = 1; seed <= SeedsPerSize; seed++)
                    yield return new KeyValuePair<int[], int>(s, seed);
        }

        static bool IsOpen(PlanTile t) { return t == PlanTile.Floor || t == PlanTile.Outside; }
        static bool IsSolid(PlanTile t) { return t == PlanTile.Wall || t == PlanTile.Window; }

        // ------------------------------------------------------------------ independent invariant checks
        internal static void CheckPlan(BuildingPlan p, int w, int h, string tag)
        {
            Assert.True(p.Width == w && p.Height == h, tag);
            int n = w * h;

            // 1. rooms and ids
            var cellsById = new int[p.Rooms.Count];
            for (int i = 0; i < n; i++)
            {
                if (p.Tiles[i] == PlanTile.Floor)
                {
                    Assert.True(p.RoomIds[i] >= 0 && p.RoomIds[i] < p.Rooms.Count, tag + " floor w/o room");
                    cellsById[p.RoomIds[i]]++;
                }
                else Assert.True(p.RoomIds[i] == -1, tag + " nonfloor w/ room");
            }

            for (int r = 0; r < p.Rooms.Count; r++)
            {
                PlanRoom room = p.Rooms[r];
                Assert.Equal(r, room.Id);
                Assert.Equal(cellsById[r], room.Cells.Count);
                Assert.NotEqual(RoomFunction.Unspecified, room.Function);
                Assert.True(room.BuildingId >= 0 && room.BuildingId < p.Buildings.Count, tag);
                var set = new HashSet<int>(room.Cells.Select(c => c.Y * w + c.X));
                Assert.Equal(room.Cells.Count, set.Count);
                Assert.True(set.Contains(room.Anchor.Y * w + room.Anchor.X), tag + " anchor");
                int minx = room.Cells.Min(c => c.X), maxx = room.Cells.Max(c => c.X), miny = room.Cells.Min(c => c.Y), maxy = room.Cells.Max(c => c.Y);
                Assert.Equal((maxx - minx + 1) * (maxy - miny + 1) > room.Cells.Count, room.Concave);
                var seen = new HashSet<int> { room.Anchor.Y * w + room.Anchor.X };
                var st = new Stack<int>(seen);
                while (st.Count > 0)
                {
                    int c = st.Pop();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c % w + Dx[d], ny = c / w + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int ni = ny * w + nx;
                        if (set.Contains(ni) && seen.Add(ni)) st.Push(ni);
                    }
                }

                Assert.True(seen.Count == set.Count, tag + " room " + r + " not 4-connected");
            }

            // 2. interior connectivity: the principal building's component holds every non-isolated building;
            //    each isolated building is internally connected and separate
            var visited = new bool[n];
            var me0 = p.MainEntrance;
            int first = -1;
            for (int d = 0; d < 4; d++) if (p.At(me0.X + Dx[d], me0.Y + Dy[d]) == PlanTile.Floor) first = (me0.Y + Dy[d]) * w + me0.X + Dx[d];
            Assert.True(first >= 0, tag);
            var stack = new Stack<int>();
            stack.Push(first); visited[first] = true;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % w + Dx[d], ny = c / w + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (!visited[ni] && (p.Tiles[ni] == PlanTile.Floor || p.Tiles[ni] == PlanTile.Door)) { visited[ni] = true; stack.Push(ni); }
                }
            }

            int mainFloor = 0, allFloor = 0;
            var isoSeen = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                if (p.Tiles[i] != PlanTile.Floor) continue;
                allFloor++;
                var pb = p.Buildings[p.Rooms[p.RoomIds[i]].BuildingId];
                if (visited[i]) { mainFloor++; Assert.False(pb.Isolated, tag + " isolated building is linked"); }
                else Assert.True(pb.Isolated, tag + " disconnected floor of a linked building at " + (i % w) + "," + (i / w));
            }

            foreach (var pb in p.Buildings.Where(b => b.Isolated))
            {
                // internally connected: flood from one of its cells and compare counts
                var cells = new List<int>();
                for (int i = 0; i < n; i++) if (p.Tiles[i] == PlanTile.Floor && p.Rooms[p.RoomIds[i]].BuildingId == pb.Id) cells.Add(i);
                Assert.True(cells.Count > 0, tag);
                var seenI = new HashSet<int> { cells[0] };
                var stI = new Stack<int>(); stI.Push(cells[0]);
                while (stI.Count > 0)
                {
                    int c = stI.Pop();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c % w + Dx[d], ny = c / w + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int ni = ny * w + nx;
                        if ((p.Tiles[ni] == PlanTile.Floor || p.Tiles[ni] == PlanTile.Door) && seenI.Add(ni)) stI.Push(ni);
                    }
                }

                Assert.True(cells.All(seenI.Contains), tag + " isolated building not internally connected");
            }

            if ((w >= 200 || h >= 200) && p.Style == "Campus") Assert.True(mainFloor * 100 >= allFloor * 75, tag + " principal component only " + mainFloor + "/" + allFloor);

            // entrance + start
            var me = p.MainEntrance;
            Assert.Equal(PlanTile.Door, p.At(me.X, me.Y));
            Assert.Equal(PlanTile.Outside, p.At(p.Start.X, p.Start.Y));
            int floorSide = 0, outSide = 0;
            for (int d = 0; d < 4; d++)
            {
                var t = p.At(me.X + Dx[d], me.Y + Dy[d]);
                if (t == PlanTile.Outside) outSide++;
                if (t == PlanTile.Floor)
                {
                    floorSide++;
                    var rm = p.Rooms[p.RoomAt(me.X + Dx[d], me.Y + Dy[d])];
                    Assert.Equal(0, rm.BuildingId);
                    Assert.True(rm.Function == RoomFunction.Lobby || rm.Function == RoomFunction.Atrium, tag + " entrance room " + rm.Function);
                }
            }

            Assert.True(floorSide == 1 && outSide == 1, tag + " entrance flanks");
            var os = new bool[n];
            var st2 = new Stack<int>();
            st2.Push(p.Start.Y * w + p.Start.X); os[p.Start.Y * w + p.Start.X] = true;
            bool border = false;
            while (st2.Count > 0)
            {
                int c = st2.Pop(); int cx = c % w, cy = c / w;
                if (cx == 0 || cy == 0 || cx == w - 1 || cy == h - 1) border = true;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (!os[ni] && p.Tiles[ni] == PlanTile.Outside) { os[ni] = true; st2.Push(ni); }
                }
            }

            Assert.True(border, tag + " start yard closed");
            bool front = false;
            for (int d = 0; d < 4; d++) if (p.At(me.X + Dx[d], me.Y + Dy[d]) == PlanTile.Outside && os[(me.Y + Dy[d]) * w + me.X + Dx[d]]) front = true;
            Assert.True(front, tag + " entrance not in start yard");
            Assert.True(os.Count(b => b) >= 64, tag);
            // every building has an exterior door; isolated ones are reachable outdoors from Start
            foreach (var pb in p.Buildings)
            {
                var ext = p.Links.Where(l => l.RoomB == -1 && p.Rooms[l.RoomA].BuildingId == pb.Id).ToList();
                Assert.True(ext.Count >= 1, tag + " building " + pb.Id + " has no exterior door");
                if (!pb.Isolated) continue;
                int good = ext.Count(l => Enumerable.Range(0, 4).Any(d => { int nx = l.Cell.X + Dx[d], ny = l.Cell.Y + Dy[d]; return nx >= 0 && ny >= 0 && nx < w && ny < h && os[ny * w + nx]; }));
                Assert.True(good >= (Math.Max(pb.MaxX - pb.MinX, pb.MaxY - pb.MinY) + 1 > 40 ? 2 : 1), tag + " isolated building not reachable outdoors");
            }
            if (p.Style != "Compound") Assert.True(Math.Abs(p.Start.X - me.X) + Math.Abs(p.Start.Y - me.Y) == 6, tag + " start distance");

            // 3/4. doors and windows
            var linkCells = new Dictionary<int, int>();
            foreach (var l in p.Links)
            {
                Assert.Equal(PlanTile.Door, p.At(l.Cell.X, l.Cell.Y));
                Assert.True(linkCells.ContainsKey(l.Cell.Y * w + l.Cell.X) == false, tag + " duplicate link");
                linkCells[l.Cell.Y * w + l.Cell.X] = 1;
                Assert.True(l.RoomA >= 0 && l.RoomA < p.Rooms.Count && (l.RoomB == -1 || (l.RoomB > l.RoomA && l.RoomB < p.Rooms.Count)), tag + " link rooms");
                var a1 = p.RoomAt(l.Cell.X - 1, l.Cell.Y); var a2 = p.RoomAt(l.Cell.X + 1, l.Cell.Y);
                var b1 = p.RoomAt(l.Cell.X, l.Cell.Y - 1); var b2 = p.RoomAt(l.Cell.X, l.Cell.Y + 1);
                bool ok = (a1 == l.RoomA && a2 == l.RoomB) || (a2 == l.RoomA && a1 == l.RoomB) || (b1 == l.RoomA && b2 == l.RoomB) || (b2 == l.RoomA && b1 == l.RoomB);
                Assert.True(ok, tag + " link flank");
                if (l.Fire) Assert.True(p.Rooms[l.RoomA].IsCorridor && l.RoomB >= 0 && p.Rooms[l.RoomB].IsCorridor, tag + " fire link");
            }

            int doors = 0, windows = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var t = p.At(x, y);
                    if (t != PlanTile.Door && t != PlanTile.Window) continue;
                    var e = p.At(x + 1, y); var wv = p.At(x - 1, y); var no = p.At(x, y + 1); var so = p.At(x, y - 1);
                    bool a = IsOpen(e) && IsOpen(wv) && IsSolid(no) && IsSolid(so);
                    bool b = IsOpen(no) && IsOpen(so) && IsSolid(e) && IsSolid(wv);
                    Assert.True(a || b, tag + " bad " + t + " at " + x + "," + y);
                    if (t == PlanTile.Door)
                    {
                        doors++;
                        Assert.True(linkCells.ContainsKey(y * w + x), tag + " door without link");
                        Assert.False(e == PlanTile.Door || no == PlanTile.Door, tag + " adjacent doors");
                    }
                    else
                    {
                        windows++;
                        Assert.False(e == PlanTile.Door || wv == PlanTile.Door || no == PlanTile.Door || so == PlanTile.Door, tag + " window by door");
                    }
                }

            Assert.Equal(doors, p.Links.Count);
            Assert.True(windows > (Math.Min(w, h) < 100 ? -1 : (p.Buildings[0].Archetype == "Plant" || p.Style != "Campus" ? 0 : 10)), tag + " windows " + windows);

            // 5. corridor links for rooms
            var corrLinked = new HashSet<int>();
            foreach (var l in p.Links)
            {
                if (l.RoomB < 0) continue;
                if (p.Rooms[l.RoomB].IsCorridor) corrLinked.Add(l.RoomA);
                if (p.Rooms[l.RoomA].IsCorridor) corrLinked.Add(l.RoomB);
            }

            // one hop is allowed: suite rooms and interlocked closets are doored through their parent room
            var viaParent = new HashSet<int>(corrLinked);
            foreach (var l in p.Links)
            {
                if (l.RoomB < 0) continue;
                if (corrLinked.Contains(l.RoomA) && !p.Rooms[l.RoomA].IsCorridor) viaParent.Add(l.RoomB);
                if (corrLinked.Contains(l.RoomB) && !p.Rooms[l.RoomB].IsCorridor) viaParent.Add(l.RoomA);
            }

            var big = p.Rooms.Where(r => !r.IsCorridor && r.Cells.Count >= 10).ToList();
            Assert.True(big.Count(r => viaParent.Contains(r.Id) || (p.Buildings[r.BuildingId].Isolated && p.Rooms.Count(q => q.BuildingId == r.BuildingId) == 1)) >= big.Count * 0.95, tag + " rooms without corridor");

            // no 1-cell-wide spurs inside rooms of 12 cells or more
            foreach (var r in p.Rooms.Where(r => !r.IsCorridor && r.Cells.Count >= 12))
            {
                foreach (var c in r.Cells)
                {
                    int same = 0;
                    for (int d = 0; d < 4; d++) if (p.RoomAt(c.X + Dx[d], c.Y + Dy[d]) == r.Id) same++;
                    Assert.True(same >= 2, tag + " spur cell in room " + r.Id + " (" + r.Function + ") at " + c.X + "," + c.Y);
                }
            }

            // 6. corridors
            foreach (var r in p.Rooms.Where(r => r.IsCorridor))
            {
                int len = Math.Max(r.Cells.Max(c => c.X) - r.Cells.Min(c => c.X), r.Cells.Max(c => c.Y) - r.Cells.Min(c => c.Y)) + 1;
                Assert.True(len <= 30, tag + " corridor length " + len);
                if (r.Cells.Count > 12)
                {
                    var set = new HashSet<int>(r.Cells.Select(c => c.Y * w + c.X));
                    foreach (var c in r.Cells)
                    {
                        bool two = false;
                        for (int sx = -1; sx <= 0 && !two; sx++)
                            for (int sy = -1; sy <= 0 && !two; sy++)
                                two = set.Contains((c.Y + sy) * w + c.X + sx) && set.Contains((c.Y + sy) * w + c.X + sx + 1) &&
                                      set.Contains((c.Y + sy + 1) * w + c.X + sx) && set.Contains((c.Y + sy + 1) * w + c.X + sx + 1);
                        Assert.True(two, tag + " corridor narrower than 2");
                    }
                }
            }

            // no straight corridor run longer than 36 (fire doors, junctions and walls break runs)
            for (int y = 0; y < h; y++)
            {
                int run = 0;
                for (int x = 0; x < w; x++)
                {
                    int id = p.RoomAt(x, y);
                    run = id >= 0 && p.Rooms[id].IsCorridor ? run + 1 : 0;
                    Assert.True(run <= 36, tag + " straight corridor run " + run + " at row " + y);
                }
            }

            for (int x = 0; x < w; x++)
            {
                int run = 0;
                for (int y = 0; y < h; y++)
                {
                    int id = p.RoomAt(x, y);
                    run = id >= 0 && p.Rooms[id].IsCorridor ? run + 1 : 0;
                    Assert.True(run <= 36, tag + " straight corridor run " + run + " at col " + x);
                }
            }

            // campus scale: no building bounding box above 110 in either dimension
            foreach (var b in p.Buildings)
            {
                Assert.True(b.MaxX - b.MinX + 1 <= 110 && b.MaxY - b.MinY + 1 <= 110, tag + " building too large " + b.Archetype);
            }

            // dead ends: every corridor room of any size has an exit besides... must have >= 2 links unless tiny
            int[] lc = new int[p.Rooms.Count];
            foreach (var l in p.Links) { lc[l.RoomA]++; if (l.RoomB >= 0) lc[l.RoomB]++; }
            foreach (var r in p.Rooms.Where(r => r.IsCorridor && r.Cells.Count > 15)) Assert.True(lc[r.Id] >= 2, tag + " dead-end corridor");

            // 7. coverage
            int nonOutside = p.Tiles.Count(t => t != PlanTile.Outside);
            double need = Math.Min(w, h) < 100 ? 0.25 : p.Style != "Campus" ? ((w >= 180 && h >= 180) ? 0.30 : 0.25) : ((w >= 180 && h >= 180) ? 0.50 : 0.40);
            Assert.True(nonOutside >= need * n, tag + " coverage " + (100.0 * nonOutside / n));

            // 8. principal building functions
            var f0 = new HashSet<RoomFunction>(p.Rooms.Where(r => r.BuildingId == 0).Select(r => r.Function));
            if (Math.Min(w, h) < 100)
            {
                Assert.True(f0.Contains(RoomFunction.Lobby), tag + " missing Lobby");
            }
            else if (p.Buildings[0].Archetype == "Office")
            {
                foreach (var f in new[] { RoomFunction.Lobby, RoomFunction.OpenOffice, RoomFunction.PrivateOffice, RoomFunction.Conference, RoomFunction.Restroom, RoomFunction.Stairwell, RoomFunction.ServerRoom, RoomFunction.Storeroom })
                    Assert.True(f0.Contains(f), tag + " missing " + f);
                Assert.True(f0.Contains(RoomFunction.Mechanical) || f0.Contains(RoomFunction.Boiler), tag + " missing Mechanical");
            }
            else
            {
                foreach (var f in new[] { RoomFunction.Lobby, RoomFunction.Restroom, RoomFunction.Stairwell })
                    Assert.True(f0.Contains(f), tag + " missing " + f);
            }

            Assert.True(f0.Contains(RoomFunction.Reception) || f0.Contains(RoomFunction.Lobby), tag);
        }

        static bool NonRect(BuildingPlan p)
        {
            foreach (var b in p.Buildings)
            {
                int cells = 0;
                for (int y = b.MinY; y <= b.MaxY; y++)
                    for (int x = b.MinX; x <= b.MaxX; x++)
                        if (p.At(x, y) != PlanTile.Outside) cells++;
                if ((b.MaxX - b.MinX + 1) * (b.MaxY - b.MinY + 1) > cells * 1.15) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ tests
        [Fact]
        public void AllPlansSatisfyInvariants()
        {
            foreach (var c in AllCases())
            {
                var p = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value);
                CheckPlan(p, c.Key[0], c.Key[1], c.Key[0] + "x" + c.Key[1] + " seed " + c.Value);
            }
        }

        [Fact]
        public void FootprintsAreOftenNonRectangularAndVaried()
        {
            int nonRect = 0, total = 0, atriumBig = 0, big = 0, campus3 = 0;
            var combos = new HashSet<string>();
            foreach (var c in AllCases())
            {
                var p = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value);
                total++;
                if (NonRect(p)) nonRect++;
                combos.Add(p.Buildings.Count + ":" + string.Join(",", p.Buildings.Select(b => b.Archetype)));
                if (c.Key[0] >= 200)
                {
                    big++;
                    if (p.Buildings.Count >= 3) campus3++;
                    if (p.Rooms.Any(r => r.BuildingId == 0 && (r.Function == RoomFunction.Atrium || r.Function == RoomFunction.Lobby) && r.Cells.Count >= 80)) atriumBig++;
                }
            }

            output.WriteLine("campus>=3 " + campus3 + "/" + big + " nonRect " + nonRect + "/" + total + " combos " + combos.Count + " bigLobby " + atriumBig + "/" + big);
            foreach (var s in combos) output.WriteLine("  " + s);
            Assert.True(nonRect >= total * 0.7);
            Assert.True(campus3 * 4 >= big * 3);
            Assert.True(combos.Count >= 4);
            Assert.True(atriumBig * 2 > big);
        }

        [Fact]
        public void FootprintVarietyAndFilledCourtyards()
        {
            int rects = 0, buildings = 0, filled = 0, open = 0, isolatedBuildings = 0, plansWithIsolated = 0;
            foreach (var c in AllCases())
            {
                var p = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value);
                foreach (var b in p.Buildings)
                {
                    buildings++;
                    if (BuildingPlanner.IsPlainRectangle(p, b)) rects++;
                    if (b.Isolated) isolatedBuildings++;
                }

                if (p.Buildings.Any(b => b.Isolated)) plansWithIsolated++;
                Assert.False(p.Buildings[0].Isolated);
                Assert.True(p.Buildings.Count >= 4 || !p.Buildings.Any(b => b.Isolated));
                Assert.True(p.Buildings.Count(b => b.Isolated) <= 2);
                filled += p.CourtyardsFilled; open += p.CourtyardsOpen;
            }

            output.WriteLine("plain rectangles " + rects + "/" + buildings + ", courtyards filled " + filled + " open " + open + ", isolated buildings " + isolatedBuildings + " in " + plansWithIsolated + " plans");
            Assert.True(rects * 100 <= buildings * 30, "too many plain rectangles " + rects + "/" + buildings);
            Assert.True(filled + open > 0 && filled * 100 >= (filled + open) * 40, "filled courtyard share " + filled + "/" + (filled + open));
            Assert.True(plansWithIsolated > 0);
        }

        [Fact]
        public void RoomShapesAndSuites()
        {
            int rooms = 0, nonRect = 0, largeSuites = 0, large = 0, interlocks = 0;
            double classSum = 0; int classPlans = 0;
            var classes = new HashSet<string>();
            foreach (var c in AllCases())
            {
                var p = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value);
                foreach (var r in p.Rooms.Where(r => !r.IsCorridor && r.Cells.Count >= 20))
                {
                    rooms++;
                    string sc = BuildingPlanner.ShapeClass(p, r);
                    classes.Add(sc);
                    if (sc != "Rect") nonRect++;
                    Assert.Equal(sc != "Rect", r.Concave);
                    Assert.True(p.RoomIds[r.Anchor.Y * p.Width + r.Anchor.X] == r.Id);
                }

                if (c.Key[0] == 226 && c.Key[1] == 226) { classSum += BuildingPlanner.ShapeClasses(p).Count; classPlans++; }
                if (c.Key[0] >= 200) { large++; if (BuildingPlanner.CountSuites(p, 2) >= 1) largeSuites++; }
                interlocks += BuildingPlanner.CountSuites(p, 1);
            }

            output.WriteLine("non-rect rooms " + nonRect + "/" + rooms + ", mean classes at 226 " + (classSum / classPlans).ToString("0.0") + ", plans with suites " + largeSuites + "/" + large + ", interlock parents " + interlocks + ", classes: " + string.Join(",", classes));
            Assert.True(nonRect * 100 >= rooms * 35, "non-rectangular share " + nonRect + "/" + rooms);
            Assert.True(classSum / classPlans >= 3.0, "shape classes per plan");
            Assert.True(largeSuites * 100 >= large * 60, "suites in large plans " + largeSuites + "/" + large);
            Assert.True(interlocks > 0);
        }

        [Fact]
        public void LargePlansHavePlantAndStorage()
        {
            int hall = 0, store = 0, n = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var p = BuildingPlanner.Generate(226, 226, seed);
                n++;
                if (p.Rooms.Any(r => r.Function == RoomFunction.MachineHall) && p.Rooms.Any(r => r.Function == RoomFunction.ServerRoom)) hall++;
                if (p.Rooms.Where(r => r.Function == RoomFunction.Archive || r.Function == RoomFunction.Storeroom).Sum(r => r.Cells.Count) >= 40) store++;
            }

            Assert.Equal(n, hall);
            Assert.Equal(n, store);
        }

        [Fact]
        public void IsDeterministic()
        {
            foreach (var c in AllCases().Where(k => k.Value <= 4))
            {
                var a = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value).ToAscii();
                var b = BuildingPlanner.Generate(c.Key[0], c.Key[1], c.Value).ToAscii();
                Assert.Equal(a, b);
            }
        }

        [Fact]
        public void FullSizePerformance()
        {
            BuildingPlanner.Generate(260, 260, 999);   // warm up the JIT
            var sw = new Stopwatch();
            long worst = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                sw.Restart();
                BuildingPlanner.Generate(260, 260, seed);
                worst = Math.Max(worst, sw.ElapsedMilliseconds);
            }

            output.WriteLine("worst 260x260 generate: " + worst + " ms");
            Assert.True(worst < 500, "worst " + worst + " ms");
        }

        [Fact]
        public void FirstAttemptStatistics()
        {
            var fails = new Dictionary<string, int>();
            var covSum = new Dictionary<string, double>(); var nbSum = new Dictionary<string, double>();
            int ok = 0, total = 0;
            foreach (var c in AllCases())
            {
                BuildingPlan p;
                string err = BuildingPlanner.TryOnce(c.Key[0], c.Key[1], c.Value, 0, out p);
                total++;
                if (p != null) { double cv = 100.0 * p.Tiles.Count(x => x != PlanTile.Outside) / p.Tiles.Length; double nb = p.Buildings.Count; string k2 = c.Key[0] + "x" + c.Key[1]; double a0; covSum.TryGetValue(k2, out a0); covSum[k2] = a0 + cv; double n0; nbSum.TryGetValue(k2, out n0); nbSum[k2] = n0 + nb; }
                if (err == null) ok++;
                else { if (err.StartsWith("build") || err.StartsWith("cov") || err.StartsWith("corr")) output.WriteLine("FAIL " + c.Key[0] + "x" + c.Key[1] + " s" + c.Value + " " + err); string key = err.Length > 400 ? err.Substring(0, 400) : err; int v; fails.TryGetValue(key, out v); fails[key] = v + 1; }
            }

            output.WriteLine("first attempt valid " + ok + "/" + total);
            foreach (var kv in covSum) output.WriteLine("  mean coverage " + kv.Key + " = " + (kv.Value / SeedsPerSize).ToString("0.0") + " buildings " + (nbSum[kv.Key] / SeedsPerSize).ToString("0.0"));
            foreach (var kv in fails.OrderByDescending(k => k.Value)) output.WriteLine("  " + kv.Value + " x " + kv.Key);
            Assert.True(ok * 100 >= total * 40, "first-attempt success too low: " + ok + "/" + total);
        }

        [Fact]
        public void SummaryMentionsArchetypesAndCoverage()
        {
            var p = BuildingPlanner.Generate(150, 150, 3);
            string s = BuildingPlanner.Summary(p);
            Assert.Contains("Office", s);
            Assert.Contains("Coverage", s);
        }

        [Fact]
        public void PrintSamples()
        {
            for (int seed = 1; seed <= 2; seed++)
            {
                var p = BuildingPlanner.Generate(226, 226, seed);
                output.WriteLine("=== 226x226 seed " + seed + " ===");
                output.WriteLine(BuildingPlanner.Summary(p));
                output.WriteLine(p.ToAscii());
            }

            foreach (var prof in GameProfiles.All())
            {
                var pp = BuildingPlanner.Generate(226, 226, 1, prof);
                output.WriteLine("--- profile " + prof.Name + " ---");
                output.WriteLine(BuildingPlanner.Summary(pp));
            }
        }
    }
}
