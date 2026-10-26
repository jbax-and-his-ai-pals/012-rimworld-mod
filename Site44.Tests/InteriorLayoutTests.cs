using System;
using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    public class InteriorLayoutTests
    {
        private readonly ITestOutputHelper output;
        public InteriorLayoutTests(ITestOutputHelper output) { this.output = output; }

        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

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
            int area = w * h, floor = 0, duct = 0;

            // 1 entries
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

            // 2 connectivity
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

            int windows = 0, doorCount = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var k = L.At(x, y);
                    if (k == TileKind.Floor) { floor++; Assert.True(seen.Contains(y * w + x), "disconnected floor\n" + ctx); Assert.True(L.RoomAt(x, y) >= 0, ctx); }
                    else
                    {
                        if (k == TileKind.Duct) { duct++; Assert.True(L.RoomAt(x, y) >= 0, ctx); }
                        else Assert.True(L.RoomAt(x, y) == -1, ctx);
                    }
                    var n = L.At(x, y + 1); var s = L.At(x, y - 1); var e = L.At(x + 1, y); var wv = L.At(x - 1, y);
                    if (k == TileKind.Door)
                    {
                        doorCount++;
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
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                Assert.True(L.At(x + dx, y + dy) != TileKind.Duct, "window touches duct\n" + ctx);
                    }
                }
            Assert.Equal(windows, L.WindowCount);

            // 5 ducts
            bool ductsAllowed = w >= 15 && h >= 15 && kind != SectorKind.Courtyard;
            if (!ductsAllowed) Assert.True(L.DuctCount == 0 && duct == 0, ctx);
            Assert.InRange(L.DuctCount, 0, 2);
            var doorPairs = new HashSet<string>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (L.At(x, y) != TileKind.Door) continue;
                    if (L.At(x, y + 1) == TileKind.Floor && L.At(x, y - 1) == TileKind.Floor) doorPairs.Add(PairKey(L.RoomAt(x, y + 1), L.RoomAt(x, y - 1)));
                    if (L.At(x + 1, y) == TileKind.Floor && L.At(x - 1, y) == TileKind.Floor) doorPairs.Add(PairKey(L.RoomAt(x + 1, y), L.RoomAt(x - 1, y)));
                }
            int ductRooms = 0;
            foreach (var r in L.Rooms.Where(r => r.Role == RoomRole.Duct))
            {
                ductRooms++;
                var rooms = new List<int>();
                foreach (var c in r.Cells)
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c.X + Dx[d], ny = c.Y + Dy[d];
                        var k = L.At(nx, ny);
                        Assert.False(k == TileKind.Floor || k == TileKind.Window, "duct touches floor/window\n" + ctx);
                        if (k == TileKind.Door)
                        {
                            Assert.True(L.At(nx + Dx[d], ny + Dy[d]) == TileKind.Floor, ctx);
                            rooms.Add(L.RoomAt(nx + Dx[d], ny + Dy[d]));
                        }
                    }
                Assert.True(rooms.Count >= 2, "duct needs 2 doors\n" + ctx);
                for (int a = 0; a < rooms.Count; a++)
                    for (int b = a + 1; b < rooms.Count; b++)
                    {
                        Assert.NotEqual(rooms[a], rooms[b]);
                        Assert.DoesNotContain(PairKey(rooms[a], rooms[b]), doorPairs);
                    }
            }
            Assert.Equal(L.DuctCount, ductRooms);

            // 6 rooms
            int mains = 0;
            int bestCount = -1, bestId = -1;
            for (int i = 0; i < L.Rooms.Count; i++)
            {
                var r = L.Rooms[i];
                Assert.Equal(i, r.Id);
                if (r.Role != RoomRole.Duct && r.Cells.Count > bestCount) { bestCount = r.Cells.Count; bestId = i; }
            }
            int cellSum = 0;
            foreach (var r in L.Rooms)
            {
                cellSum += r.Cells.Count;
                Assert.Contains(r.Anchor, r.Cells.Select(c => c).ToList(), new CellCmp());
                foreach (var c in r.Cells)
                {
                    Assert.Equal(r.Id, L.RoomAt(c.X, c.Y));
                    Assert.True(L.At(c.X, c.Y) == (r.Role == RoomRole.Duct ? TileKind.Duct : TileKind.Floor));
                }
                int bw = r.MaxX - r.MinX + 1, bh = r.MaxY - r.MinY + 1;
                Assert.Equal(bw * bh > r.Cells.Count, r.Concave);
                if (r.Role == RoomRole.Main) mains++;
                else if (r.Role == RoomRole.Corridor) Assert.True(Math.Min(bw, bh) <= 2);
                else if (r.Role == RoomRole.Side) Assert.True(Math.Min(bw, bh) > 2);
            }
            Assert.Equal(floor + duct, cellSum);
            Assert.Equal(1, mains);
            Assert.Same(L.Rooms[bestId], L.MainRoom);
            Assert.Equal(RoomRole.Main, L.MainRoom.Role);

            // 7 area
            Assert.True((floor + duct) * 100 >= 40 * area, "floor ratio\n" + ctx);
            Assert.True(L.MainRoom.Cells.Count * 100 >= 25 * area, "main ratio\n" + ctx);
            if (kind == SectorKind.Courtyard)
            {
                Assert.Equal(0, windows + doorCount + duct);
                Assert.Equal(1, L.Rooms.Count);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (L.At(x, y) == TileKind.Wall)
                        {
                            Assert.True(x >= 3 && y >= 3 && x <= w - 4 && y <= h - 4, ctx);
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    if (dx != 0 || dy != 0) Assert.True(L.At(x + dx, y + dy) == TileKind.Floor, ctx);
                            foreach (var e in entries) Assert.True(Math.Max(Math.Abs(e.X - x), Math.Abs(e.Y - y)) >= 3, ctx);
                        }
            }
            if (kind == SectorKind.HotZone) Assert.Equal(0, L.DuctCount);
        }

        sealed class CellCmp : IEqualityComparer<LayoutCell>
        {
            public bool Equals(LayoutCell a, LayoutCell b) { return a.X == b.X && a.Y == b.Y; }
            public int GetHashCode(LayoutCell c) { return c.X * 1000 + c.Y; }
        }

        static string PairKey(int a, int b) { return a < b ? a + "," + b : b + "," + a; }

        [Fact]
        public void InvariantsHoldAcrossSizesKindsSeeds()
        {
            var sizes = new[] {
                new[] { 9, 9 }, new[] { 9, 14 }, new[] { 10, 10 }, new[] { 11, 11 }, new[] { 13, 21 }, new[] { 15, 15 },
                new[] { 15, 29 }, new[] { 17, 12 }, new[] { 20, 20 }, new[] { 24, 24 }, new[] { 29, 15 }, new[] { 30, 30 }, new[] { 30, 9 } };
            var kinds = (SectorKind[])Enum.GetValues(typeof(SectorKind));
            var rnd = new Random(12345);
            int total = 0;
            foreach (var sz in sizes)
                foreach (var kind in kinds)
                    for (int seed = 0; seed < 40; seed++)
                    {
                        var entries = RandomEntries(rnd, sz[0], sz[1]);
                        var L = InteriorLayout.Generate(sz[0], sz[1], kind, entries, seed);
                        Check(L, sz[0], sz[1], kind, entries, sz[0] + "x" + sz[1] + " " + kind + " seed " + seed);
                        total++;
                    }
            output.WriteLine("checked layouts: " + total);
        }

        [Fact]
        public void CourtyardWithWestEntryIsOpen()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var entries = new List<LayoutCell> { new LayoutCell(0, 12) };
                var L = InteriorLayout.Generate(25, 25, SectorKind.Courtyard, entries, seed);
                Check(L, 25, 25, SectorKind.Courtyard, entries, "courtyard " + seed);
                if (seed == 0) output.WriteLine(L.ToAscii());
            }
        }

        [Fact]
        public void Deterministic()
        {
            var entries = new List<LayoutCell> { new LayoutCell(0, 9), new LayoutCell(23, 14) };
            foreach (SectorKind kind in Enum.GetValues(typeof(SectorKind)))
                for (int seed = 0; seed < 20; seed++)
                {
                    var a = InteriorLayout.Generate(24, 21, kind, entries, seed).ToAscii();
                    var b = InteriorLayout.Generate(24, 21, kind, entries, seed).ToAscii();
                    Assert.Equal(a, b);
                }
        }

        [Fact]
        public void HallVarietyOver300Seeds()
        {
            int concave = 0, lShapes = 0, cShapes = 0, withWindows = 0, withDucts = 0;
            var roomCounts = new HashSet<int>();
            var entries = new List<LayoutCell> { new LayoutCell(0, 12) };
            int printed = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var L = InteriorLayout.Generate(24, 24, SectorKind.Hall, entries, seed);
                Check(L, 24, 24, SectorKind.Hall, entries, "hall " + seed);
                roomCounts.Add(L.Rooms.Count(r => r.Role != RoomRole.Duct));
                if (L.WindowCount > 0) withWindows++;
                if (L.DuctCount > 0) withDucts++;
                foreach (var r in L.Rooms.Where(r => r.Concave))
                {
                    concave++;
                    var kindShape = Classify(r);
                    if (kindShape == 'L') lShapes++;
                    else if (kindShape == 'C') cShapes++;
                }
                if (printed < 3 && L.DuctCount > 0 && L.WindowCount > 0 && L.Rooms.Any(r => r.Concave)) { output.WriteLine("seed " + seed + "\n" + L.ToAscii() + "\n"); printed++; }
            }
            output.WriteLine($"concave rooms {concave}, L {lShapes}, C {cShapes}, layouts with windows {withWindows}, with ducts {withDucts}, room counts [{string.Join(",", roomCounts.OrderBy(x => x))}]");
            Assert.True(concave > 0);
            Assert.True(lShapes > 0);
            Assert.True(cShapes > 0);
            Assert.True(withWindows > 0);
            Assert.True(withDucts > 0);
            Assert.True(roomCounts.Count >= 3);
        }

        // 'L' = bbox minus cells is a rectangle containing a bbox corner; 'C' = a rectangle touching the bbox edge but no corner.
        static char Classify(LayoutRoom r)
        {
            var have = new HashSet<long>(r.Cells.Select(c => ((long)c.X << 32) | (uint)c.Y));
            int mx0 = int.MaxValue, my0 = int.MaxValue, mx1 = int.MinValue, my1 = int.MinValue, cnt = 0;
            for (int y = r.MinY; y <= r.MaxY; y++)
                for (int x = r.MinX; x <= r.MaxX; x++)
                    if (!have.Contains(((long)x << 32) | (uint)y))
                    {
                        cnt++;
                        mx0 = Math.Min(mx0, x); mx1 = Math.Max(mx1, x); my0 = Math.Min(my0, y); my1 = Math.Max(my1, y);
                    }
            if (cnt != (mx1 - mx0 + 1) * (my1 - my0 + 1)) return '?';
            bool left = mx0 == r.MinX, right = mx1 == r.MaxX, bottom = my0 == r.MinY, top = my1 == r.MaxY;
            if ((left || right) && (bottom || top)) return 'L';
            if (left || right || bottom || top) return 'C';
            return '?';
        }

        [Fact]
        public void PrintSamples()
        {
            var west = new List<LayoutCell> { new LayoutCell(0, 12) };
            output.WriteLine(InteriorLayout.Generate(24, 24, SectorKind.Hall, west, 7).ToAscii() + "\n");
            output.WriteLine(InteriorLayout.Generate(15, 15, SectorKind.Security, new List<LayoutCell> { new LayoutCell(0, 7), new LayoutCell(14, 3) }, 3).ToAscii() + "\n");
            output.WriteLine(InteriorLayout.Generate(20, 20, SectorKind.Reactor, west, 11).ToAscii() + "\n");
            output.WriteLine(InteriorLayout.Generate(26, 14, SectorKind.HotZone, new List<LayoutCell> { new LayoutCell(0, 7), new LayoutCell(25, 7) }, 5).ToAscii() + "\n");
            output.WriteLine(InteriorLayout.Generate(24, 24, SectorKind.Storage, west, 2).ToAscii());
        }
    }
}
