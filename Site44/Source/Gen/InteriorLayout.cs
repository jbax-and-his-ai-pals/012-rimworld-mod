using System;
using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    // Pure C# interior generator for one sector block. No RimWorld/Unity/Verse references.
    // Coordinates are local: (0,0) is the interior's south-west cell, +x east, +y north.

    public enum TileKind : byte { Wall, Floor, Door, Duct, Window }
    public enum RoomRole { Main, Side, Corridor, Duct }

    public enum RoomFunction { Unspecified, Corridor, Lobby, Reception, OpenOffice, PrivateOffice, Conference, ServerRoom, Restroom, BreakRoom, Storeroom, Archive, Mechanical, MachineHall, Stairwell, SecurityPost, Closet, Duct, Atrium, Cafeteria, Lab, Mailroom, Boiler, Auditorium, Vault, RecRoom, Kitchen, Dormitory, Infirmary, LockerRoom, Library, Workshop, Armory, CommsRoom, Classroom, Ward, OperatingRoom, Pharmacy, Morgue, Bunkroom, Apartment, Cell, Containment }

    public struct LayoutCell
    {
        public int X;
        public int Y;
        public LayoutCell(int x, int y) { X = x; Y = y; }
    }

    public sealed class LayoutRoom
    {
        public int Id;                       // index in layout.Rooms
        public RoomRole Role;
        public RoomFunction Function;        // Unspecified from InteriorLayout.Generate (ducts get Duct)
        public bool Concave;                 // L-shaped or C-shaped (bounding box area > cell count)
        public int MinX, MinY, MaxX, MaxY;   // bounding box (inclusive)
        public List<LayoutCell> Cells = new List<LayoutCell>();   // every Floor (or Duct) cell of the room
        public LayoutCell Anchor;            // a Floor/Duct cell of the room closest to its centroid
    }

    public sealed class InteriorLayout
    {
        public int Width, Height;
        public int WindowCount, DuctCount;

        private TileKind[] tiles;
        private int[] roomIds;
        private readonly List<LayoutRoom> rooms = new List<LayoutRoom>();
        private int mainIndex = -1;

        public IReadOnlyList<LayoutRoom> Rooms { get { return rooms; } }
        public LayoutRoom MainRoom { get { return mainIndex >= 0 ? rooms[mainIndex] : null; } }

        public TileKind At(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return TileKind.Wall;
            return tiles[y * Width + x];
        }

        public int RoomAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            return roomIds[y * Width + x];
        }

        public string ToAscii()
        {
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    char c;
                    switch (tiles[y * Width + x])
                    {
                        case TileKind.Floor: c = '.'; break;
                        case TileKind.Door: c = 'D'; break;
                        case TileKind.Duct: c = ':'; break;
                        case TileKind.Window: c = 'W'; break;
                        default: c = '#'; break;
                    }
                    sb.Append(c);
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ generation entry point
        private const int MaxAttempts = 48;

        public static InteriorLayout Generate(int width, int height, SectorKind kind, IList<LayoutCell> entries, int seed)
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;
            var ents = new List<LayoutCell>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    LayoutCell e = entries[i];
                    if (e.X >= 0 && e.Y >= 0 && e.X < width && e.Y < height) ents.Add(e);
                }
            }

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new LayoutRng(Mix(seed, attempt));
                bool allowDuct = attempt < 28;
                bool allowShapes = attempt < 38;
                bool strict = attempt < 24;
                var builder = new InteriorBuilder(width, height, kind, ents, rng, allowDuct, allowShapes);
                TileKind[] t = builder.Build();
                if (t == null) continue;
                InteriorLayout lay = Finish(width, height, t);
                if (lay != null && lay.Validate(kind, ents, strict)) return lay;
            }

            // Trivially valid fallback: one open Main room with a few pillars.
            var frng = new LayoutRng(Mix(seed, 9999));
            var fb = new InteriorBuilder(width, height, SectorKind.Courtyard, ents, frng, false, false);
            return Finish(width, height, fb.Build());
        }

        // Used by OfficeLayout: build a layout (rooms = Floor components) from a prepared tile grid.
        internal static InteriorLayout FromTiles(int w, int h, TileKind[] t) { return Finish(w, h, t); }
        internal void SetMain(int index) { mainIndex = index; }

        private static int Mix(int seed, int attempt)
        {
            unchecked
            {
                uint z = (uint)seed * 0x9E3779B1u + (uint)attempt * 0x85EBCA6Bu + 0x7F4A7C15u;
                z ^= z >> 15;
                z *= 0x2C1B3C6Du;
                z ^= z >> 12;
                return (int)z;
            }
        }

        // ------------------------------------------------------------------ room extraction
        internal static int[] LabelFloor(TileKind[] t, int w, int h, out int count)
        {
            int[] lab = new int[w * h];
            for (int i = 0; i < lab.Length; i++) lab[i] = -1;
            count = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] != TileKind.Floor || lab[i] >= 0) continue;
                lab[i] = count;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    int cx = c % w, cy = c / w;
                    if (cx > 0) Visit(t, lab, stack, c - 1, TileKind.Floor, count);
                    if (cx < w - 1) Visit(t, lab, stack, c + 1, TileKind.Floor, count);
                    if (cy > 0) Visit(t, lab, stack, c - w, TileKind.Floor, count);
                    if (cy < h - 1) Visit(t, lab, stack, c + w, TileKind.Floor, count);
                }
                count++;
            }
            return lab;
        }

        private static void Visit(TileKind[] t, int[] lab, Stack<int> stack, int i, TileKind kind, int id)
        {
            if (t[i] == kind && lab[i] < 0) { lab[i] = id; stack.Push(i); }
        }

        private static InteriorLayout Finish(int w, int h, TileKind[] t)
        {
            var lay = new InteriorLayout();
            lay.Width = w; lay.Height = h; lay.tiles = t;
            lay.roomIds = new int[w * h];
            for (int i = 0; i < lay.roomIds.Length; i++) lay.roomIds[i] = -1;

            int floorRooms;
            int[] lab = LabelFloor(t, w, h, out floorRooms);
            for (int r = 0; r < floorRooms; r++) lay.rooms.Add(new LayoutRoom { Id = r });
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int l = lab[y * w + x];
                    if (l >= 0) { lay.rooms[l].Cells.Add(new LayoutCell(x, y)); lay.roomIds[y * w + x] = l; }
                }

            // duct rooms
            int[] dl = new int[w * h];
            for (int i = 0; i < dl.Length; i++) dl[i] = -1;
            var stack = new Stack<int>();
            int ductCount = 0;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] != TileKind.Duct || dl[i] >= 0) continue;
                var room = new LayoutRoom { Id = lay.rooms.Count, Role = RoomRole.Duct, Function = RoomFunction.Duct };
                dl[i] = 1; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    int cx = c % w, cy = c / w;
                    room.Cells.Add(new LayoutCell(cx, cy));
                    lay.roomIds[c] = room.Id;
                    if (cx > 0) VisitDuct(t, dl, stack, c - 1);
                    if (cx < w - 1) VisitDuct(t, dl, stack, c + 1);
                    if (cy > 0) VisitDuct(t, dl, stack, c - w);
                    if (cy < h - 1) VisitDuct(t, dl, stack, c + w);
                }
                lay.rooms.Add(room);
                ductCount++;
            }
            lay.DuctCount = ductCount;

            int windows = 0;
            for (int i = 0; i < t.Length; i++) if (t[i] == TileKind.Window) windows++;
            lay.WindowCount = windows;

            int best = -1, bestCount = -1;
            for (int r = 0; r < lay.rooms.Count; r++)
            {
                LayoutRoom room = lay.rooms[r];
                int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
                long sx = 0, sy = 0;
                for (int i = 0; i < room.Cells.Count; i++)
                {
                    LayoutCell c = room.Cells[i];
                    if (c.X < minx) minx = c.X; if (c.X > maxx) maxx = c.X;
                    if (c.Y < miny) miny = c.Y; if (c.Y > maxy) maxy = c.Y;
                    sx += c.X; sy += c.Y;
                }
                int n = room.Cells.Count;
                if (n == 0) return null;
                room.MinX = minx; room.MinY = miny; room.MaxX = maxx; room.MaxY = maxy;
                room.Concave = (maxx - minx + 1) * (maxy - miny + 1) > n;
                // anchor: cell nearest the centroid (compare scaled by n to stay in integers)
                long bd = long.MaxValue; LayoutCell ba = room.Cells[0];
                for (int i = 0; i < n; i++)
                {
                    LayoutCell c = room.Cells[i];
                    long dx = c.X * (long)n - sx, dy = c.Y * (long)n - sy;
                    long d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; ba = c; }
                }
                room.Anchor = ba;
                if (room.Role != RoomRole.Duct && n > bestCount) { bestCount = n; best = r; }
            }
            if (best < 0) return null;
            lay.mainIndex = best;
            for (int r = 0; r < lay.rooms.Count; r++)
            {
                LayoutRoom room = lay.rooms[r];
                if (room.Role == RoomRole.Duct) continue;
                if (r == best) room.Role = RoomRole.Main;
                else
                {
                    int bw = room.MaxX - room.MinX + 1, bh = room.MaxY - room.MinY + 1;
                    room.Role = Math.Min(bw, bh) <= 2 ? RoomRole.Corridor : RoomRole.Side;
                }
            }
            return lay;
        }

        private static void VisitDuct(TileKind[] t, int[] dl, Stack<int> stack, int i)
        {
            if (t[i] == TileKind.Duct && dl[i] < 0) { dl[i] = 1; stack.Push(i); }
        }

        // ------------------------------------------------------------------ validation (mirrors the hard invariants)
        private bool IsOpen(TileKind k) { return k == TileKind.Floor || k == TileKind.Duct; }
        private bool IsSolid(TileKind k) { return k == TileKind.Wall || k == TileKind.Window; }

        private bool Validate(SectorKind kind, List<LayoutCell> entries, bool strict)
        {
            int w = Width, h = Height;
            int area = w * h;
            int floorCells = 0, ductCells = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == TileKind.Floor) floorCells++;
                else if (tiles[i] == TileKind.Duct) ductCells++;
            }
            if (floorCells == 0) return false;

            // 1. entries
            for (int i = 0; i < entries.Count; i++)
            {
                LayoutCell e = entries[i];
                if (At(e.X, e.Y) != TileKind.Floor || RoomAt(e.X, e.Y) < 0) return false;
                if (e.X == 0 && !RunClear(e.X, e.Y, 1, 0)) return false;
                if (e.X == w - 1 && !RunClear(e.X, e.Y, -1, 0)) return false;
                if (e.Y == 0 && !RunClear(e.X, e.Y, 0, 1)) return false;
                if (e.Y == h - 1 && !RunClear(e.X, e.Y, 0, -1)) return false;
            }

            // 2. connectivity through Floor + Door
            bool[] seen = new bool[area];
            int start = -1;
            for (int i = 0; i < tiles.Length; i++) if (tiles[i] == TileKind.Floor) { start = i; break; }
            var st = new Stack<int>();
            seen[start] = true; st.Push(start);
            int reached = 0;
            while (st.Count > 0)
            {
                int c = st.Pop();
                if (tiles[c] == TileKind.Floor) reached++;
                int cx = c % w, cy = c / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (seen[ni]) continue;
                    TileKind k = tiles[ni];
                    if (k == TileKind.Floor || k == TileKind.Door) { seen[ni] = true; st.Push(ni); }
                }
            }
            if (reached != floorCells) return false;

            // 3. doors, 4. windows
            var doorPairs = new HashSet<long>();
            bool anyDoor = false, anyWindow = false;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    TileKind k = tiles[y * w + x];
                    if (k == TileKind.Door)
                    {
                        anyDoor = true;
                        TileKind n = At(x, y + 1), s = At(x, y - 1), e = At(x + 1, y), wv = At(x - 1, y);
                        bool ns = IsOpen(n) && IsOpen(s) && IsSolid(e) && IsSolid(wv);
                        bool ew = IsOpen(e) && IsOpen(wv) && IsSolid(n) && IsSolid(s);
                        if (!ns && !ew) return false;
                        if (At(x + 1, y) == TileKind.Door || At(x, y + 1) == TileKind.Door) return false;
                        if (ns && n == TileKind.Floor && s == TileKind.Floor) doorPairs.Add(PairKey(RoomAt(x, y + 1), RoomAt(x, y - 1)));
                        if (ew && e == TileKind.Floor && wv == TileKind.Floor) doorPairs.Add(PairKey(RoomAt(x + 1, y), RoomAt(x - 1, y)));
                    }
                    else if (k == TileKind.Window)
                    {
                        anyWindow = true;
                        TileKind n = At(x, y + 1), s = At(x, y - 1), e = At(x + 1, y), wv = At(x - 1, y);
                        if (!((n == TileKind.Floor && s == TileKind.Floor) || (e == TileKind.Floor && wv == TileKind.Floor))) return false;
                        if (n == TileKind.Door || s == TileKind.Door || e == TileKind.Door || wv == TileKind.Door) return false;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (At(x + dx, y + dy) == TileKind.Duct) return false;
                    }
                }

            // 5. ducts
            bool ductsAllowed = w >= 15 && h >= 15 && kind != SectorKind.Courtyard && kind != SectorKind.HotZone;
            if (DuctCount > 0 && (!ductsAllowed || DuctCount > 2)) return false;
            for (int r = 0; r < rooms.Count; r++)
            {
                LayoutRoom dr = rooms[r];
                if (dr.Role != RoomRole.Duct) continue;
                var doorRooms = new List<int>();
                for (int i = 0; i < dr.Cells.Count; i++)
                {
                    LayoutCell c = dr.Cells[i];
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c.X + Dx[d], ny = c.Y + Dy[d];
                        TileKind k = At(nx, ny);
                        if (k == TileKind.Floor || k == TileKind.Window) return false;
                        if (k == TileKind.Door)
                        {
                            int fx = nx + Dx[d], fy = ny + Dy[d];
                            if (At(fx, fy) != TileKind.Floor) return false;
                            doorRooms.Add(RoomAt(fx, fy));
                        }
                    }
                }
                if (doorRooms.Count < 2) return false;
                for (int a = 0; a < doorRooms.Count; a++)
                    for (int b = a + 1; b < doorRooms.Count; b++)
                    {
                        if (doorRooms[a] == doorRooms[b]) return false;
                        if (doorPairs.Contains(PairKey(doorRooms[a], doorRooms[b]))) return false;
                    }
            }

            // 7. area rules
            if ((floorCells + ductCells) * 100 < 40 * area) return false;
            LayoutRoom main = MainRoom;
            if (main == null || main.Cells.Count * 100 < 25 * area) return false;

            if (kind == SectorKind.Courtyard)
            {
                if (anyDoor || anyWindow || DuctCount > 0) return false;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (tiles[y * w + x] != TileKind.Wall) continue;
                        if (x < 3 || y < 3 || x > w - 4 || y > h - 4) return false;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && At(x + dx, y + dy) != TileKind.Floor) return false;
                        for (int i = 0; i < entries.Count; i++)
                            if (Math.Max(Math.Abs(entries[i].X - x), Math.Abs(entries[i].Y - y)) < 3) return false;
                    }
                int fr = 0;
                for (int i = 0; i < rooms.Count; i++) if (rooms[i].Role != RoomRole.Duct) fr++;
                if (fr != 1) return false;
            }

            if (strict)
            {
                if (kind == SectorKind.Security && main.Cells.Count * 100 < 45 * area) return false;
                if (kind == SectorKind.HotZone)
                {
                    if (DuctCount > 0) return false;
                    int concave = 0;
                    for (int i = 0; i < rooms.Count; i++) if (rooms[i].Concave) concave++;
                    if (concave > 1) return false;
                }
            }
            return true;
        }

        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        private static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        private bool RunClear(int x, int y, int dx, int dy)
        {
            for (int k = 1; k <= 2; k++)
            {
                int nx = x + dx * k, ny = y + dy * k;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                if (At(nx, ny) != TileKind.Floor) return false;
            }
            return true;
        }
    }

    // ---------------------------------------------------------------------- deterministic PRNG
    internal sealed class LayoutRng
    {
        private uint s;
        public LayoutRng(int seed)
        {
            uint z = unchecked((uint)seed + 0x9E3779B9u);
            z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu);
            z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
            z ^= z >> 16;
            s = z == 0 ? 0x1234567u : z;
            for (int i = 0; i < 4; i++) NextU();
        }
        public uint NextU()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }
        public int Next(int n) { return n <= 1 ? 0 : (int)(NextU() % (uint)n); }
        public bool Chance(int permille) { return Next(1000) < permille; }
    }

    // ---------------------------------------------------------------------- builder
    internal struct LRect
    {
        public int X0, Y0, X1, Y1;
        public LRect(int x0, int y0, int x1, int y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public int W { get { return X1 - X0 + 1; } }
        public int H { get { return Y1 - Y0 + 1; } }
        public int Area { get { return W * H; } }
    }

    internal sealed class InteriorBuilder
    {
        private sealed class Split { public bool Vert; public int Pos; public LRect R; }
        private sealed class Leaf { public LRect R; public bool NoShape; public bool Arena; }
        private sealed class Lane { public List<LayoutCell> Duct = new List<LayoutCell>(); public int Ix, Iy; }

        private readonly int W, H;
        private readonly SectorKind kind;
        private readonly LayoutRng rng;
        private readonly List<LayoutCell> entries;
        private readonly bool allowDuct, allowShapes;
        private TileKind[] t;
        private bool[] keep;
        private readonly List<Split> splits = new List<Split>();
        private readonly List<Leaf> leaves = new List<Leaf>();
        private readonly List<Lane> lanes = new List<Lane>();

        public InteriorBuilder(int w, int h, SectorKind kind, List<LayoutCell> entries, LayoutRng rng, bool allowDuct, bool allowShapes)
        {
            W = w; H = h; this.kind = kind; this.entries = entries; this.rng = rng;
            this.allowDuct = allowDuct; this.allowShapes = allowShapes;
        }

        private TileKind Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return TileKind.Wall;
            return t[y * W + x];
        }
        private void Set(int x, int y, TileKind k) { t[y * W + x] = k; }

        public TileKind[] Build()
        {
            t = new TileKind[W * H];
            for (int i = 0; i < t.Length; i++) t[i] = TileKind.Floor;

            // Courtyard: one open Main room with sparse single-cell pillars.
            if (kind == SectorKind.Courtyard) { Pillars(); return t; }

            LRect rect = new LRect(0, 0, W - 1, H - 1);
            if (allowDuct && W >= 15 && H >= 15 && kind != SectorKind.HotZone) rect = ReserveLanes(rect);

            if (kind == SectorKind.HotZone) BuildGallery(rect);
            else BuildMainRest(rect);

            CarveWalls();
            if (!PlaceDoors()) return null;
            ComputeKeep();
            if (allowShapes) Shapes();
            if (kind == SectorKind.Security) ArenaPillars();
            if (!Ducts()) return null;
            if (kind != SectorKind.HotZone) Windows();
            return t;
        }

        // ---------------------------------------------------------------- courtyard / fallback pillars
        private void Pillars()
        {
            int count = (W * H) / 60;
            if (count < 1 && W * H >= 49) count = 1;
            var placed = new List<LayoutCell>();
            for (int tries = 0; tries < count * 40 && placed.Count < count; tries++)
            {
                if (W - 6 <= 0 || H - 6 <= 0) break;
                int x = 3 + rng.Next(W - 6), y = 3 + rng.Next(H - 6);
                bool ok = true;
                for (int i = 0; i < entries.Count && ok; i++)
                    if (Math.Max(Math.Abs(entries[i].X - x), Math.Abs(entries[i].Y - y)) < 3) ok = false;
                for (int i = 0; i < placed.Count && ok; i++)
                    if (Math.Max(Math.Abs(placed[i].X - x), Math.Abs(placed[i].Y - y)) < 2) ok = false;
                if (!ok) continue;
                placed.Add(new LayoutCell(x, y));
                Set(x, y, TileKind.Wall);
            }
        }

        // ---------------------------------------------------------------- duct lanes (reserved before partitioning)
        private LRect ReserveLanes(LRect rect)
        {
            int r = rng.Next(100);
            int want = r < 35 ? 0 : (r < 75 ? 1 : 2);
            if (want == 0) return rect;
            int[] sides = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); int tmp = sides[i]; sides[i] = sides[j]; sides[j] = tmp; }
            for (int si = 0; si < 4 && lanes.Count < want; si++)
            {
                int side = sides[si];
                bool ok = true;
                for (int i = 0; i < entries.Count; i++)
                {
                    LayoutCell e = entries[i];
                    if (side == 0 && e.Y >= H - 2) ok = false;
                    if (side == 1 && e.Y <= 1) ok = false;
                    if (side == 2 && e.X >= W - 2) ok = false;
                    if (side == 3 && e.X <= 1) ok = false;
                }
                if (!ok) continue;
                var ln = new Lane();
                switch (side)
                {
                    case 0:
                        for (int x = 0; x < W; x++) { Set(x, H - 1, TileKind.Wall); Set(x, H - 2, TileKind.Wall); }
                        for (int x = 2; x <= W - 3; x++) ln.Duct.Add(new LayoutCell(x, H - 1));
                        ln.Ix = 0; ln.Iy = -1; rect.Y1 -= 2; break;
                    case 1:
                        for (int x = 0; x < W; x++) { Set(x, 0, TileKind.Wall); Set(x, 1, TileKind.Wall); }
                        for (int x = 2; x <= W - 3; x++) ln.Duct.Add(new LayoutCell(x, 0));
                        ln.Ix = 0; ln.Iy = 1; rect.Y0 += 2; break;
                    case 2:
                        for (int y = 0; y < H; y++) { Set(W - 1, y, TileKind.Wall); Set(W - 2, y, TileKind.Wall); }
                        for (int y = 2; y <= H - 3; y++) ln.Duct.Add(new LayoutCell(W - 1, y));
                        ln.Ix = -1; ln.Iy = 0; rect.X1 -= 2; break;
                    default:
                        for (int y = 0; y < H; y++) { Set(0, y, TileKind.Wall); Set(1, y, TileKind.Wall); }
                        for (int y = 2; y <= H - 3; y++) ln.Duct.Add(new LayoutCell(0, y));
                        ln.Ix = 1; ln.Iy = 0; rect.X0 += 2; break;
                }
                lanes.Add(ln);
            }
            return rect;
        }

        // ---------------------------------------------------------------- partitioning
        private bool Blocked(LRect r, bool vert, int pos)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                LayoutCell e = entries[i];
                if (vert) { if (e.X == pos && e.Y >= r.Y0 && e.Y <= r.Y1) return true; }
                else { if (e.Y == pos && e.X >= r.X0 && e.X <= r.X1) return true; }
            }
            return false;
        }

        private bool PickPos(LRect r, bool vert, int target, int minD, out int pos)
        {
            int lo = (vert ? r.X0 : r.Y0) + minD;
            int hi = (vert ? r.X1 : r.Y1) - minD;
            pos = 0;
            if (lo > hi) return false;
            if (target < lo) target = lo;
            if (target > hi) target = hi;
            for (int d = 0; d <= hi - lo; d++)
            {
                int a = target + d, b = target - d;
                if (a <= hi && !Blocked(r, vert, a)) { pos = a; return true; }
                if (d > 0 && b >= lo && !Blocked(r, vert, b)) { pos = b; return true; }
            }
            return false;
        }

        private void MakeSplit(LRect r, bool vert, int pos, out LRect a, out LRect b)
        {
            splits.Add(new Split { Vert = vert, Pos = pos, R = r });
            if (vert) { a = new LRect(r.X0, r.Y0, pos - 1, r.Y1); b = new LRect(pos + 1, r.Y0, r.X1, r.Y1); }
            else { a = new LRect(r.X0, r.Y0, r.X1, pos - 1); b = new LRect(r.X0, pos + 1, r.X1, r.Y1); }
        }

        // Hall / Storage / Security / Diagnostic / Valve / Reactor:
        //   one big Main region (Security: pillared arena >=45%; Storage: modest main) plus a strip of side rooms.
        //   Hall: 2-6 side rooms, L/C shapes common. Storage: 4-7 small side rooms.
        //   Security: 2-4 small side rooms. Diagnostic/Valve/Reactor: 1-3 side rooms plus a 3x3..4x4 closet.
        private void BuildMainRest(LRect rect)
        {
            int fLo, fHi, nLo, nHi; bool closet = false;
            switch (kind)
            {
                case SectorKind.Security: fLo = 620; fHi = 720; nLo = 2; nHi = 4; break;
                case SectorKind.Storage: fLo = 330; fHi = 420; nLo = 4; nHi = 7; break;
                case SectorKind.Diagnostic:
                case SectorKind.Valve:
                case SectorKind.Reactor: fLo = 550; fHi = 680; nLo = 1; nHi = 3; closet = true; break;
                default: fLo = 420; fHi = 550; nLo = 2; nHi = 6; break;
            }
            int f = fLo + rng.Next(fHi - fLo + 1);
            int n = nLo + rng.Next(nHi - nLo + 1);
            bool vertFirst = rect.W > rect.H + 2 ? true : (rect.H > rect.W + 2 ? false : rng.Next(2) == 0);
            bool mainLow = rng.Next(2) == 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool vert = pass == 0 ? vertFirst : !vertFirst;
                int len = vert ? rect.W : rect.H;
                int a0 = vert ? rect.X0 : rect.Y0, a1 = vert ? rect.X1 : rect.Y1;
                int target = mainLow ? a0 + len * f / 1000 : a1 - len * f / 1000;
                int pos;
                if (!PickPos(rect, vert, target, 4, out pos)) continue;
                LRect lo, hi;
                MakeSplit(rect, vert, pos, out lo, out hi);
                LRect mainR = mainLow ? lo : hi, restR = mainLow ? hi : lo;
                leaves.Add(new Leaf
                {
                    R = mainR,
                    Arena = kind == SectorKind.Security,
                    NoShape = kind == SectorKind.Security || kind == SectorKind.Storage
                });
                SplitRest(restR, n, closet);
                return;
            }
            leaves.Add(new Leaf { R = rect, Arena = kind == SectorKind.Security, NoShape = kind == SectorKind.Security });
        }

        private void SplitRest(LRect rest, int target, bool closet)
        {
            var parts = new List<LRect>();
            parts.Add(rest);
            if (closet && rng.Chance(850)) TryCloset(parts);
            while (parts.Count < target) { if (!SplitLargest(parts, 4)) break; }
            for (int i = 0; i < parts.Count; i++) leaves.Add(new Leaf { R = parts[i] });
        }

        private bool SplitLargest(List<LRect> parts, int minD)
        {
            var idx = new List<int>();
            for (int i = 0; i < parts.Count; i++) idx.Add(i);
            idx.Sort(delegate (int a, int b)
            {
                int c = parts[b].Area.CompareTo(parts[a].Area);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k];
                LRect r = parts[i];
                bool preferVert = r.W > r.H ? true : (r.W < r.H ? false : rng.Next(2) == 0);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool vert = pass == 0 ? preferVert : !preferVert;
                    int len = vert ? r.W : r.H;
                    int a0 = vert ? r.X0 : r.Y0;
                    int frac = 350 + rng.Next(301);
                    int pos;
                    if (!PickPos(r, vert, a0 + len * frac / 1000, minD, out pos)) continue;
                    LRect a, b;
                    MakeSplit(r, vert, pos, out a, out b);
                    parts[i] = a; parts.Add(b);
                    return true;
                }
            }
            return false;
        }

        // Carve a small 3x3..4x4 closet out of one end of the side strip.
        private bool TryCloset(List<LRect> parts)
        {
            LRect r = parts[0];
            bool vert1 = r.W > r.H;
            int len = vert1 ? r.W : r.H;
            int c = 3 + rng.Next(2);
            if (len < c + 1 + 4) return false;
            bool atLow = rng.Next(2) == 0;
            int pos1 = vert1 ? (atLow ? r.X0 + c : r.X1 - c) : (atLow ? r.Y0 + c : r.Y1 - c);
            if (Blocked(r, vert1, pos1)) return false;
            LRect piece;
            if (vert1) piece = atLow ? new LRect(r.X0, r.Y0, pos1 - 1, r.Y1) : new LRect(pos1 + 1, r.Y0, r.X1, r.Y1);
            else piece = atLow ? new LRect(r.X0, r.Y0, r.X1, pos1 - 1) : new LRect(r.X0, pos1 + 1, r.X1, r.Y1);
            bool vert2 = !vert1;
            int tlen = vert2 ? piece.W : piece.H;
            int c2 = 3 + rng.Next(2);
            if (tlen < c2 + 1 + 3) return false;
            bool atLow2 = rng.Next(2) == 0;
            int pos2 = vert2 ? (atLow2 ? piece.X0 + c2 : piece.X1 - c2) : (atLow2 ? piece.Y0 + c2 : piece.Y1 - c2);
            if (Blocked(piece, vert2, pos2)) return false;
            LRect a, b;
            MakeSplit(r, vert1, pos1, out a, out b);
            LRect pieceR = atLow ? a : b, restR = atLow ? b : a;
            LRect p1, p2;
            MakeSplit(pieceR, vert2, pos2, out p1, out p2);
            parts[0] = restR; parts.Add(p1); parts.Add(p2);
            return true;
        }

        // HotZone: a long gallery of 2-4 slices in a chain along the long axis.
        private void BuildGallery(LRect rect)
        {
            bool vert = rect.W > rect.H ? true : (rect.W < rect.H ? false : rng.Next(2) == 0);
            int len = vert ? rect.W : rect.H;
            int n = 2 + rng.Next(3);
            int cap = (len + 1) / 5;
            if (n > cap) n = cap;
            if (n < 2) { leaves.Add(new Leaf { R = rect }); return; }
            int[] wts = new int[n];
            for (int i = 0; i < n; i++) wts[i] = 100 + rng.Next(120);
            wts[rng.Next(n)] += 150 + rng.Next(150);
            LRect cur = rect;
            for (int i = 0; i < n - 1; i++)
            {
                int rem = 0;
                for (int j = i; j < n; j++) rem += wts[j];
                int length = vert ? cur.W : cur.H;
                int target = (vert ? cur.X0 : cur.Y0) + length * wts[i] / rem;
                int pos;
                if (!PickPos(cur, vert, target, 4, out pos)) break;
                LRect a, b;
                MakeSplit(cur, vert, pos, out a, out b);
                leaves.Add(new Leaf { R = a });
                cur = b;
            }
            leaves.Add(new Leaf { R = cur });
        }

        // ---------------------------------------------------------------- walls and doors
        private void CarveWalls()
        {
            for (int i = 0; i < splits.Count; i++)
            {
                Split s = splits[i];
                if (s.Vert) { for (int y = s.R.Y0; y <= s.R.Y1; y++) Set(s.Pos, y, TileKind.Wall); }
                else { for (int x = s.R.X0; x <= s.R.X1; x++) Set(x, s.Pos, TileKind.Wall); }
            }
        }

        private bool DoorCandidate(int x, int y, bool vert)
        {
            if (x < 1 || y < 1 || x > W - 2 || y > H - 2) return false;
            if (Get(x, y) != TileKind.Wall) return false;
            if (vert)
                return Get(x - 1, y) == TileKind.Floor && Get(x + 1, y) == TileKind.Floor
                    && Get(x, y - 1) == TileKind.Wall && Get(x, y + 1) == TileKind.Wall;
            return Get(x, y - 1) == TileKind.Floor && Get(x, y + 1) == TileKind.Floor
                && Get(x - 1, y) == TileKind.Wall && Get(x + 1, y) == TileKind.Wall;
        }

        private bool PlaceDoors()
        {
            for (int i = 0; i < splits.Count; i++)
            {
                Split s = splits[i];
                int lo = s.Vert ? s.R.Y0 : s.R.X0, hi = s.Vert ? s.R.Y1 : s.R.X1;
                int first = -1;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (pass == 1 && (kind == SectorKind.HotZone || !rng.Chance(250))) break;
                    var cands = new List<int>();
                    for (int p = lo; p <= hi; p++)
                    {
                        if (pass == 1 && Math.Abs(p - first) < 3) continue;
                        int x = s.Vert ? s.Pos : p, y = s.Vert ? p : s.Pos;
                        if (DoorCandidate(x, y, s.Vert)) cands.Add(p);
                    }
                    if (cands.Count == 0) { if (pass == 0) return false; break; }
                    int pick = cands[rng.Next(cands.Count)];
                    if (pass == 0) first = pick;
                    Set(s.Vert ? s.Pos : pick, s.Vert ? pick : s.Pos, TileKind.Door);
                }
            }
            return true;
        }

        // Cells that obstructions (L/C carving) must not remove: door approaches and entry runs.
        private void ComputeKeep()
        {
            keep = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Get(x, y) != TileKind.Door) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        for (int k = 1; k <= 2; k++)
                        {
                            int nx = x + DDx[d] * k, ny = y + DDy[d] * k;
                            if (nx >= 0 && ny >= 0 && nx < W && ny < H) keep[ny * W + nx] = true;
                        }
                    }
                }
            for (int i = 0; i < entries.Count; i++)
            {
                LayoutCell e = entries[i];
                keep[e.Y * W + e.X] = true;
                for (int d = 0; d < 4; d++)
                    for (int k = 1; k <= 3; k++)
                    {
                        int nx = e.X + DDx[d] * k, ny = e.Y + DDy[d] * k;
                        if (nx >= 0 && ny >= 0 && nx < W && ny < H) keep[ny * W + nx] = true;
                    }
            }
        }

        private static readonly int[] DDx = { 1, -1, 0, 0 };
        private static readonly int[] DDy = { 0, 0, 1, -1 };

        // ---------------------------------------------------------------- L / C shaped rooms
        private bool Free(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (keep[y * W + x]) return false;
            return true;
        }
        private void Fill(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Set(x, y, TileKind.Wall);
        }

        private void Shapes()
        {
            int budget = kind == SectorKind.HotZone ? 1 : 99;
            int p;
            switch (kind)
            {
                case SectorKind.Hall: p = 480; break;
                case SectorKind.Security: p = 250; break;
                case SectorKind.Storage: p = 180; break;
                case SectorKind.HotZone: p = 450; break;
                default: p = 380; break;
            }
            for (int i = 0; i < leaves.Count && budget > 0; i++)
            {
                Leaf lf = leaves[i];
                if (lf.NoShape || !rng.Chance(p)) continue;
                bool ok = rng.Next(2) == 0 ? (TryC(lf.R) || TryL(lf.R)) : (TryL(lf.R) || TryC(lf.R));
                if (ok) budget--;
            }
        }

        private bool TryL(LRect r)
        {
            if (r.W < 5 || r.H < 5) return false;
            int a = 2 + rng.Next(r.W - 4);
            int b = 2 + rng.Next(r.H - 4);
            while (a * b * 100 > r.Area * 40 && (a > 2 || b > 2))
            {
                if (a >= b && a > 2) a--; else if (b > 2) b--; else break;
            }
            int cx = rng.Next(2) == 0 ? r.X0 : r.X1 - a + 1;
            int cy = rng.Next(2) == 0 ? r.Y0 : r.Y1 - b + 1;
            if (!Free(cx, cy, cx + a - 1, cy + b - 1)) return false;
            Fill(cx, cy, cx + a - 1, cy + b - 1);
            return true;
        }

        private bool TryC(LRect r)
        {
            bool horiz = r.H >= 7 && r.W >= 6;   // slab runs along x, arms above and below
            bool vertical = r.W >= 7 && r.H >= 6; // slab runs along y, arms left and right
            if (!horiz && !vertical) return false;
            bool useH = horiz && (!vertical || rng.Next(2) == 0);
            if (useH)
            {
                int th = (r.H >= 9 && rng.Chance(250)) ? 2 : 1;
                int rowLo = r.Y0 + 3, rowHi = r.Y1 - 3 - (th - 1);
                if (rowLo > rowHi) return false;
                int row = rowLo + rng.Next(rowHi - rowLo + 1);
                int gap = 2 + rng.Next(2);
                if (gap > r.W - 3) gap = 2;
                int len = r.W - gap;
                bool west = rng.Next(2) == 0;
                int x0 = west ? r.X0 : r.X1 - len + 1, x1 = west ? r.X0 + len - 1 : r.X1;
                if (!Free(x0, row, x1, row + th - 1)) return false;
                Fill(x0, row, x1, row + th - 1);
                return true;
            }
            else
            {
                int th = (r.W >= 9 && rng.Chance(250)) ? 2 : 1;
                int colLo = r.X0 + 3, colHi = r.X1 - 3 - (th - 1);
                if (colLo > colHi) return false;
                int col = colLo + rng.Next(colHi - colLo + 1);
                int gap = 2 + rng.Next(2);
                if (gap > r.H - 3) gap = 2;
                int len = r.H - gap;
                bool south = rng.Next(2) == 0;
                int y0 = south ? r.Y0 : r.Y1 - len + 1, y1 = south ? r.Y0 + len - 1 : r.Y1;
                if (!Free(col, y0, col + th - 1, y1)) return false;
                Fill(col, y0, col + th - 1, y1);
                return true;
            }
        }

        // ---------------------------------------------------------------- Security arena cover
        private void ArenaPillars()
        {
            for (int i = 0; i < leaves.Count; i++)
            {
                Leaf lf = leaves[i];
                if (!lf.Arena) continue;
                LRect r = lf.R;
                if (r.W < 5 || r.H < 5) continue;
                int count = r.Area / 28;
                if (count < 3) count = 3;
                if (count > 10) count = 10;
                var placed = new List<LayoutCell>();
                for (int tries = 0; tries < count * 30 && placed.Count < count; tries++)
                {
                    int x = r.X0 + 1 + rng.Next(r.W - 2), y = r.Y0 + 1 + rng.Next(r.H - 2);
                    if (!PillarOk(x, y, placed)) continue;
                    placed.Add(new LayoutCell(x, y));
                    Set(x, y, TileKind.Wall);
                }
            }
        }

        private bool PillarOk(int x, int y, List<LayoutCell> placed)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Get(x + dx, y + dy) != TileKind.Floor) return false;
            for (int i = 0; i < placed.Count; i++)
                if (Math.Max(Math.Abs(placed[i].X - x), Math.Abs(placed[i].Y - y)) < 3) return false;
            for (int i = 0; i < entries.Count; i++)
                if (Math.Max(Math.Abs(entries[i].X - x), Math.Abs(entries[i].Y - y)) < 3) return false;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (Get(x + dx, y + dy) == TileKind.Door) return false;
            return true;
        }

        // ---------------------------------------------------------------- ducts
        private bool Ducts()
        {
            if (lanes.Count == 0) return true;
            int n;
            int[] lab = InteriorLayout.LabelFloor(t, W, H, out n);
            var pairs = new HashSet<long>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (t[y * W + x] != TileKind.Door) continue;
                    int a = -1, b = -1;
                    if (Get(x - 1, y) == TileKind.Floor && Get(x + 1, y) == TileKind.Floor) { a = lab[y * W + x - 1]; b = lab[y * W + x + 1]; }
                    else if (Get(x, y - 1) == TileKind.Floor && Get(x, y + 1) == TileKind.Floor) { a = lab[(y - 1) * W + x]; b = lab[(y + 1) * W + x]; }
                    if (a >= 0) pairs.Add(Key(a, b));
                }

            for (int li = 0; li < lanes.Count; li++)
            {
                Lane ln = lanes[li];
                var idx = new List<int>();
                var labs = new List<int>();
                for (int s = 0; s < ln.Duct.Count; s++)
                {
                    LayoutCell d = ln.Duct[s];
                    int fx = d.X + ln.Ix, fy = d.Y + ln.Iy;     // flank wall
                    int ax = d.X + 2 * ln.Ix, ay = d.Y + 2 * ln.Iy; // approach
                    if (Get(fx, fy) != TileKind.Wall || Get(ax, ay) != TileKind.Floor) continue;
                    idx.Add(s); labs.Add(lab[ay * W + ax]);
                }
                if (idx.Count < 2) return false;
                // random order of pair search
                int m = idx.Count;
                int[] order = new int[m];
                for (int i = 0; i < m; i++) order[i] = i;
                for (int i = m - 1; i > 0; i--) { int j = rng.Next(i + 1); int tmp = order[i]; order[i] = order[j]; order[j] = tmp; }
                int ia = -1, ib = -1;
                for (int p = 0; p < m && ia < 0; p++)
                {
                    int a = order[p];
                    for (int q = p + 1; q < m; q++)
                    {
                        int b = order[q];
                        if (labs[a] == labs[b]) continue;
                        if (Math.Abs(idx[a] - idx[b]) < 2) continue;
                        if (pairs.Contains(Key(labs[a], labs[b]))) continue;
                        ia = a; ib = b; break;
                    }
                }
                if (ia < 0) return false;
                int sa = idx[ia], sb = idx[ib];
                int lo = Math.Max(0, Math.Min(sa, sb) - rng.Next(3));
                int hi = Math.Min(ln.Duct.Count - 1, Math.Max(sa, sb) + rng.Next(3));
                for (int s = lo; s <= hi; s++) Set(ln.Duct[s].X, ln.Duct[s].Y, TileKind.Duct);
                LayoutCell da = ln.Duct[sa], db = ln.Duct[sb];
                Set(da.X + ln.Ix, da.Y + ln.Iy, TileKind.Door);
                Set(db.X + ln.Ix, db.Y + ln.Iy, TileKind.Door);
            }
            return true;
        }

        private static long Key(int a, int b)
        {
            if (a > b) { int x = a; a = b; b = x; }
            return ((long)a << 32) | (uint)b;
        }

        // ---------------------------------------------------------------- windows
        private void Windows()
        {
            int n;
            int[] lab = InteriorLayout.LabelFloor(t, W, H, out n);
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    bool vw;
                    if (!WinEligible(x, y, lab, out vw) || !rng.Chance(80)) continue;
                    int r = rng.Next(100);
                    int runLen = r < 55 ? 1 : (r < 80 ? 2 : 3);
                    int sx = vw ? 0 : 1, sy = vw ? 1 : 0;
                    int cx = x, cy = y;
                    for (int k = 0; k < runLen; k++)
                    {
                        bool v2;
                        if (!WinEligible(cx, cy, lab, out v2) || v2 != vw) break;
                        Set(cx, cy, TileKind.Window);
                        if (RunLength(cx, cy, sx, sy) > 3) { Set(cx, cy, TileKind.Wall); break; }
                        cx += sx; cy += sy;
                    }
                }
        }

        private int RunLength(int x, int y, int sx, int sy)
        {
            int n = 1;
            int cx = x - sx, cy = y - sy;
            while (Get(cx, cy) == TileKind.Window) { n++; cx -= sx; cy -= sy; }
            cx = x + sx; cy = y + sy;
            while (Get(cx, cy) == TileKind.Window) { n++; cx += sx; cy += sy; }
            return n;
        }

        private bool WinEligible(int x, int y, int[] lab, out bool vertWall)
        {
            vertWall = false;
            if (x < 1 || y < 1 || x > W - 2 || y > H - 2) return false;
            if (t[y * W + x] != TileKind.Wall) return false;
            TileKind n = t[(y + 1) * W + x], s = t[(y - 1) * W + x], e = t[y * W + x + 1], w = t[y * W + x - 1];
            if (n == TileKind.Door || s == TileKind.Door || e == TileKind.Door || w == TileKind.Door) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (t[(y + dy) * W + x + dx] == TileKind.Duct) return false;
            if (e == TileKind.Floor && w == TileKind.Floor && lab[y * W + x + 1] != lab[y * W + x - 1]
                && (n == TileKind.Wall || n == TileKind.Window) && (s == TileKind.Wall || s == TileKind.Window))
            { vertWall = true; return true; }
            if (n == TileKind.Floor && s == TileKind.Floor && lab[(y + 1) * W + x] != lab[(y - 1) * W + x]
                && (e == TileKind.Wall || e == TileKind.Window) && (w == TileKind.Wall || w == TileKind.Window))
            { vertWall = false; return true; }
            return false;
        }
    }
}
