using System;
using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    public class OfficeLayoutTests
    {
        private readonly ITestOutputHelper output;
        public OfficeLayoutTests(ITestOutputHelper output) { this.output = output; }

        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

        static readonly int[][] Sizes =
        {
            new[] { 11, 11 }, new[] { 13, 17 }, new[] { 15, 15 }, new[] { 17, 17 }, new[] { 21, 21 },
            new[] { 24, 24 }, new[] { 25, 25 }, new[] { 29, 29 }, new[] { 20, 30 },
        };

        static List<LayoutCell> RandomEntries(Random r, int w, int h)
        {
            var list = new List<LayoutCell>();
            int n = r.Next(5);
            for (int i = 0; i < n; i++)
            {
                switch (r.Next(4))
                {
                    case 0: list.Add(new LayoutCell(0, r.Next(h))); break;
                    case 1: list.Add(new LayoutCell(w - 1, r.Next(h))); break;
                    case 2: list.Add(new LayoutCell(r.Next(w), 0)); break;
                    default: list.Add(new LayoutCell(r.Next(w), h - 1)); break;
                }
            }
            return list;
        }

        static bool Open(TileKind k) { return k == TileKind.Floor || k == TileKind.Duct; }
        static bool Solid(TileKind k) { return k == TileKind.Wall || k == TileKind.Window; }

        static void Check(InteriorLayout L, int w, int h, SectorKind kind, List<LayoutCell> entries, string tag)
        {
            string ctx = tag + "\n" + L.ToAscii();
            Assert.True(L.Width == w && L.Height == h, ctx);
            bool courtyard = kind == SectorKind.Courtyard;
            int area = w * h, floor = 0;

            // entries: Floor with a straight clear run of 2 into the interior
            foreach (var e in entries)
            {
                Assert.True(L.At(e.X, e.Y) == TileKind.Floor, "entry not floor " + ctx);
                Assert.True(L.RoomAt(e.X, e.Y) >= 0, ctx);
                var dirs = new List<int[]>();
                if (e.X == 0) dirs.Add(new[] { 1, 0 });
                if (e.X == w - 1) dirs.Add(new[] { -1, 0 });
                if (e.Y == 0) dirs.Add(new[] { 0, 1 });
                if (e.Y == h - 1) dirs.Add(new[] { 0, -1 });
                foreach (var d in dirs)
                    for (int k = 1; k <= 2; k++)
                    {
                        int nx = e.X + d[0] * k, ny = e.Y + d[1] * k;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        Assert.True(L.At(nx, ny) == TileKind.Floor, "pinch at entry " + e.X + "," + e.Y + "\n" + ctx);
                    }
            }

            // connectivity through Floor + Door
            var seen = new HashSet<int>();
            var st = new Stack<int>();
            int first = -1;
            for (int y = 0; y < h && first < 0; y++)
                for (int x = 0; x < w; x++)
                    if (L.At(x, y) == TileKind.Floor) { first = y * w + x; break; }
            Assert.True(first >= 0, ctx);
            seen.Add(first); st.Push(first);
            while (st.Count > 0)
            {
                int c = st.Pop();
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % w + Dx[d], ny = c / w + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var k = L.At(nx, ny);
                    if ((k == TileKind.Floor || k == TileKind.Door) && seen.Add(ny * w + nx)) st.Push(ny * w + nx);
                }
            }

            int windows = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var k = L.At(x, y);
                    var n = L.At(x, y + 1); var s = L.At(x, y - 1); var e = L.At(x + 1, y); var wv = L.At(x - 1, y);
                    if (k == TileKind.Floor)
                    {
                        floor++;
                        Assert.True(seen.Contains(y * w + x), "disconnected floor " + x + "," + y + "\n" + ctx);
                        Assert.True(L.RoomAt(x, y) >= 0, ctx);
                    }
                    else Assert.True(L.RoomAt(x, y) == -1, "non-floor has room\n" + ctx);
                    Assert.True(k != TileKind.Duct, "unexpected duct\n" + ctx);
                    if (k == TileKind.Door)
                    {
                        bool a = Open(n) && Open(s) && Solid(e) && Solid(wv);
                        bool b = Open(e) && Open(wv) && Solid(n) && Solid(s);
                        Assert.True(a || b, "bad door " + x + "," + y + "\n" + ctx);
                        Assert.False(e == TileKind.Door || n == TileKind.Door, "adjacent doors\n" + ctx);
                    }
                    if (k == TileKind.Window)
                    {
                        windows++;
                        Assert.True((n == TileKind.Floor && s == TileKind.Floor) || (e == TileKind.Floor && wv == TileKind.Floor), "bad window\n" + ctx);
                        Assert.False(n == TileKind.Door || s == TileKind.Door || e == TileKind.Door || wv == TileKind.Door, "window by door\n" + ctx);
                        if (!courtyard) Assert.True(x > 0 && y > 0 && x < w - 1 && y < h - 1, "window on outer row/col\n" + ctx);
                    }
                }
            Assert.Equal(windows, L.WindowCount);

            // rooms partition the Floor cells exactly, anchors and concavity
            int total = 0;
            for (int i = 0; i < L.Rooms.Count; i++)
            {
                var r = L.Rooms[i];
                Assert.Equal(i, r.Id);
                Assert.NotEmpty(r.Cells);
                total += r.Cells.Count;
                foreach (var c in r.Cells)
                {
                    Assert.True(L.At(c.X, c.Y) == TileKind.Floor, ctx);
                    Assert.Equal(i, L.RoomAt(c.X, c.Y));
                }
                Assert.Contains(r.Cells, c => c.X == r.Anchor.X && c.Y == r.Anchor.Y);
                int bw = r.MaxX - r.MinX + 1, bh = r.MaxY - r.MinY + 1;
                Assert.Equal(bw * bh > r.Cells.Count, r.Concave);
            }
            Assert.Equal(floor, total);

            // exactly one Main = largest non-corridor room
            var nonCorr = L.Rooms.Where(r => r.Role != RoomRole.Corridor).ToList();
            Assert.Equal(1, L.Rooms.Count(r => r.Role == RoomRole.Main));
            Assert.NotNull(L.MainRoom);
            Assert.Equal(RoomRole.Main, L.MainRoom.Role);
            if (nonCorr.Count > 0) Assert.Equal(nonCorr.Max(r => r.Cells.Count), L.MainRoom.Cells.Count);
            else Assert.Equal(L.Rooms.Max(r => r.Cells.Count), L.MainRoom.Cells.Count);

            if (courtyard) return;

            // functions
            foreach (var r in L.Rooms)
            {
                Assert.NotEqual(RoomFunction.Unspecified, r.Function);
                Assert.NotEqual(RoomRole.Duct, r.Role);
                Assert.Equal(r.Role == RoomRole.Corridor, r.Function == RoomFunction.Corridor);
            }
            bool both15 = w >= 15 && h >= 15;
            if (both15)
            {
                Assert.True(floor * 100 >= 55 * area, "floor fraction " + floor + "/" + area + "\n" + ctx);
                int servers = L.Rooms.Count(r => r.Function == RoomFunction.ServerRoom);
                int stores = L.Rooms.Count(r => r.Function == RoomFunction.Archive || r.Function == RoomFunction.Storeroom);
                int posts = L.Rooms.Count(r => r.Function == RoomFunction.SecurityPost);
                int halls = L.Rooms.Count(r => r.Function == RoomFunction.MachineHall);
                int mech = L.Rooms.Count(r => r.Function == RoomFunction.Mechanical);
                switch (kind)
                {
                    case SectorKind.Diagnostic: Assert.True(servers >= 1, "no server room\n" + ctx); break;
                    case SectorKind.Storage: Assert.True(stores >= 2, "storage needs 2 archive/store\n" + ctx); break;
                    case SectorKind.Security: Assert.True(posts >= 1, "no security post\n" + ctx); break;
                    case SectorKind.Valve:
                    case SectorKind.Reactor:
                    case SectorKind.HotZone: Assert.True(halls >= 1 && mech >= 1, "no machine hall / mechanical\n" + ctx); break;
                }
            }
        }

        static string Key(InteriorLayout L)
        {
            return L.ToAscii() + "|" + string.Join(",", L.Rooms.Select(r => (int)r.Function + ":" + (int)r.Role));
        }

        [Fact]
        public void AllSizesKindsSeedsAreValidAndDeterministic()
        {
            int fallbacks = 0, total = 0;
            var fbBy = new SortedDictionary<string, int>();
            foreach (var sz in Sizes)
            {
                int w = sz[0], h = sz[1];
                foreach (SectorKind kind in Enum.GetValues(typeof(SectorKind)))
                    for (int seed = 0; seed <= 60; seed++)
                    {
                        var rnd = new Random(seed * 7919 + w * 31 + h * 17 + (int)kind);
                        var sets = new List<List<LayoutCell>>();
                        sets.Add(RandomEntries(rnd, w, h));
                        sets.Add(new List<LayoutCell> { new LayoutCell(0, h / 2) });   // courtyard-style west entry
                        foreach (var ents in sets)
                        {
                            string tag = string.Format("{0}x{1} {2} seed {3} entries [{4}]", w, h, kind, seed,
                                string.Join(";", ents.Select(e => e.X + "," + e.Y)));
                            var lay = OfficeLayout.Generate(w, h, kind, ents, seed);
                            Check(lay, w, h, kind, ents, tag);
                            var again = OfficeLayout.Generate(w, h, kind, ents, seed);
                            Assert.Equal(Key(lay), Key(again));
                            if (kind != SectorKind.Courtyard)
                            {
                                total++;
                                if (lay.ToAscii() == InteriorLayout.Generate(w, h, kind, ents, seed).ToAscii())
                                {
                                    fallbacks++;
                                    string fk = w + "x" + h + " " + kind;
                                    int cur; fbBy.TryGetValue(fk, out cur); fbBy[fk] = cur + 1;
                                }
                            }
                        }
                    }
            }
            foreach (var kv in fbBy) output.WriteLine("  fallback " + kv.Key + ": " + kv.Value);
            output.WriteLine("fallbacks to abstract generator: " + fallbacks + " / " + total);
            Assert.True(fallbacks * 100 <= total * 3, "too many fallbacks: " + fallbacks + "/" + total);
        }

        [Fact]
        public void CourtyardDelegatesUnchanged()
        {
            var ents = new List<LayoutCell> { new LayoutCell(0, 12) };
            Assert.Equal(InteriorLayout.Generate(25, 25, SectorKind.Courtyard, ents, 5).ToAscii(),
                         OfficeLayout.Generate(25, 25, SectorKind.Courtyard, ents, 5).ToAscii());
        }

        [Fact]
        public void HallPlansVaryAndHaveGlassAndConferenceRooms()
        {
            var counts = new HashSet<int>();
            int withWindows = 0, withConf = 0, withOpen = 0;
            var ents = new List<LayoutCell> { new LayoutCell(0, 12), new LayoutCell(23, 11) };
            var plans = new HashSet<string>();
            for (int seed = 0; seed < 200; seed++)
            {
                var lay = OfficeLayout.Generate(24, 24, SectorKind.Hall, ents, seed);
                Check(lay, 24, 24, SectorKind.Hall, ents, "variety seed " + seed);
                counts.Add(lay.Rooms.Count);
                plans.Add(lay.ToAscii());
                if (lay.WindowCount > 0) withWindows++;
                if (lay.Rooms.Any(r => r.Function == RoomFunction.Conference)) withConf++;
                if (lay.Rooms.Any(r => r.Function == RoomFunction.OpenOffice)) withOpen++;
            }
            output.WriteLine("distinct room counts " + counts.Count + ", distinct plans " + plans.Count + ", windows " + withWindows + ", conference " + withConf + ", open office " + withOpen);
            Assert.True(counts.Count >= 3, "room count variety " + counts.Count);
            Assert.True(plans.Count >= 150);
            Assert.True(withWindows >= 140, "windows " + withWindows);
            Assert.True(withConf >= 50, "conference " + withConf);
        }

        [Fact]
        public void PrintSamples()
        {
            var west = new List<LayoutCell> { new LayoutCell(0, 12) };
            output.WriteLine("24x24 Hall (W entry + E entry)\n" + OfficeLayout.Generate(24, 24, SectorKind.Hall, new List<LayoutCell> { new LayoutCell(0, 12), new LayoutCell(23, 10), new LayoutCell(8, 0) }, 3).ToAscii());
            output.WriteLine("24x24 Storage\n" + OfficeLayout.Generate(24, 24, SectorKind.Storage, west, 3).ToAscii());
            output.WriteLine("21x21 Diagnostic\n" + OfficeLayout.Generate(21, 21, SectorKind.Diagnostic, west, 3).ToAscii());
            output.WriteLine("15x15 Reactor\n" + OfficeLayout.Generate(15, 15, SectorKind.Reactor, new List<LayoutCell> { new LayoutCell(0, 7), new LayoutCell(7, 14) }, 3).ToAscii());
            output.WriteLine("24x24 Hall variants");
            for (int s = 10; s < 13; s++)
                output.WriteLine(OfficeLayout.Generate(24, 24, SectorKind.Hall, west, s).ToAscii() + "\n");
        }
    }
}
