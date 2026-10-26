using System;
using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    // Master plan of the friendly enclave town: an ordered list of buildable projects laid out around a plaza,
    // plus a way to re-place a project when the player has built on its lot.
    // Coordinates: local to the town area, (0,0) south-west, +x east, +y north.
    // Pure data; no RimWorld references.

    public enum TownProjectKind { House, Stall, Workshop, Garden, Watchtower, WallSegment, Well, Canteen, Clinic }

    public sealed class TownProject
    {
        public int Id;                    // index in plan.Projects
        public TownProjectKind Kind;
        public int X, Y;                  // south-west corner of the footprint in town coordinates
        public int Width, Height;
        public PlanTile[] Tiles;          // Width*Height, index = ly*Width + lx, local to the footprint
        public int DoorX, DoorY;          // town coordinates of the door tile (or entry cell); the cell just outside it is a street cell
        public int StandingRequired;      // standing points needed before the town starts this project
        public int RevealStanding;        // standing at which the lot becomes visible to the player (<= StandingRequired)
        public int WorkUnits;             // build effort (hours)
        public int[] Requires = new int[0]; // ids of projects that must be finished first (ids < this.Id)
    }

    public sealed class TownPlan
    {
        public int Width, Height;
        public int PlazaX, PlazaY;

        private readonly List<TownProject> projects = new List<TownProject>();
        private readonly List<int> streets = new List<int>();
        private readonly bool[] blocked;
        private readonly bool[] street;      // plaza + main streets + side lanes
        private readonly int[] owner;        // project id + 1 per cell, 0 = none
        private int ringR;                   // Chebyshev radius (from the plaza centre) of the outer wall ring, 0 = no ring
        private int lim;                     // Chebyshev limit for ordinary lots

        public IReadOnlyList<TownProject> Projects { get { return projects; } }
        // Street network the plan keeps clear. Includes the 9x9 plaza cells.
        public IReadOnlyList<int> StreetCells { get { return streets; } }

        private TownPlan(int w, int h, int px, int py, bool[] blk)
        {
            Width = w; Height = h; PlazaX = px; PlazaY = py;
            blocked = new bool[w * h];
            if (blk != null) Array.Copy(blk, blocked, w * h);
            street = new bool[w * h];
            owner = new int[w * h];
        }

        // ------------------------------------------------------------------ PRNG

        private sealed class Rng
        {
            private ulong s;
            public Rng(int seed) { s = ((ulong)(uint)seed) * 0x9E3779B97F4A7C15UL ^ 0xD1B54A32D192ED03UL; Next64(); }
            private ulong Next64()
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
            public int Next(int n) { return n <= 1 ? 0 : (int)((Next64() >> 33) % (ulong)n); }
        }

        private sealed class Shape
        {
            public int W, H, Dx, Dy;
            public PlanTile[] T;
        }

        private sealed class Pend
        {
            public TownProjectKind Kind;
            public Shape S;
            public int X, Y;
        }

        // ------------------------------------------------------------------ Generate

        public static TownPlan Generate(int width, int height, int plazaX, int plazaY, bool[] blocked, int seed, int projectCount)
        {
            if (width < 16 || height < 16) throw new ArgumentException("town area too small");
            if (blocked != null && blocked.Length != width * height) throw new ArgumentException("blocked has the wrong length");
            if (projectCount < 0) throw new ArgumentException("projectCount must not be negative");
            if (plazaX - 4 < 0 || plazaY - 4 < 0 || plazaX + 4 >= width || plazaY + 4 >= height)
                throw new ArgumentException("plaza does not fit inside the town area");
            if (blocked != null)
                for (int y = plazaY - 4; y <= plazaY + 4; y++)
                    for (int x = plazaX - 4; x <= plazaX + 4; x++)
                        if (blocked[y * width + x]) throw new ArgumentException("plaza is blocked at " + x + "," + y);

            var plan = new TownPlan(width, height, plazaX, plazaY, blocked);
            var rng = new Rng(seed);
            plan.BuildStreets(rng);
            plan.PlaceProjects(rng, projectCount);
            return plan;
        }

        // ------------------------------------------------------------------ streets

        private bool Free(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && !blocked[y * Width + x];
        }

        private void AddStreet(int x, int y)
        {
            int i = y * Width + x;
            if (street[i]) return;
            street[i] = true;
            streets.Add(i);
        }

        private int Cheb(int x, int y) { return Math.Max(Math.Abs(x - PlazaX), Math.Abs(y - PlazaY)); }

        private void BuildStreets(Rng rng)
        {
            int px = PlazaX, py = PlazaY;
            int ext = Math.Min(Math.Min(px, py), Math.Min(Width - 1 - px, Height - 1 - py));
            ringR = ext >= 16 ? ext - 2 : 0;
            lim = ringR > 0 ? ringR - 2 : ext - 1;
            int laneLim = ringR > 0 ? ringR : ext - 1;

            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                    AddStreet(px + dx, py + dy);

            int[] ddx = { 1, -1, 0, 0 };
            int[] ddy = { 0, 0, 1, -1 };
            for (int d = 0; d < 4; d++)
            {
                int ax = ddx[d], ay = ddy[d];
                int pdx = Math.Abs(ay), pdy = Math.Abs(ax);   // perpendicular axis
                int last = 4;
                for (int k = 5; k <= ext - 1; k++)
                {
                    bool ok = true;
                    for (int o = -2; o <= 1 && ok; o++)
                        if (!Free(px + ax * k + pdx * o, py + ay * k + pdy * o)) ok = false;
                    if (!ok) break;
                    for (int o = -2; o <= 1; o++) AddStreet(px + ax * k + pdx * o, py + ay * k + pdy * o);
                    last = k;
                }

                int lk = 8 + rng.Next(3);
                while (lk + 2 <= last)
                {
                    for (int side = 1; side >= -1; side -= 2)
                    {
                        int so = side > 0 ? 2 : -3;
                        int len = 12 + rng.Next(17);
                        for (int j = 0; j < len; j++)
                        {
                            int off = so + side * j;
                            bool ok = true;
                            for (int i = 0; i < 3 && ok; i++)
                            {
                                int x = px + ax * (lk + i) + pdx * off;
                                int y = py + ay * (lk + i) + pdy * off;
                                if (!Free(x, y) || Cheb(x, y) > laneLim) ok = false;
                            }
                            if (!ok) break;
                            for (int i = 0; i < 3; i++)
                                AddStreet(px + ax * (lk + i) + pdx * off, py + ay * (lk + i) + pdy * off);
                        }
                    }
                    lk += 14 + rng.Next(7);
                }
            }
        }

        // ------------------------------------------------------------------ shapes

        private static Shape Rotate(Shape s)
        {
            var r = new Shape { W = s.H, H = s.W, T = new PlanTile[s.W * s.H] };
            for (int y = 0; y < s.H; y++)
                for (int x = 0; x < s.W; x++)
                    r.T[x * r.W + (s.H - 1 - y)] = s.T[y * s.W + x];
            r.Dx = s.H - 1 - s.Dy; r.Dy = s.Dx;
            return r;
        }

        private static Shape Mirror(Shape s)
        {
            var r = new Shape { W = s.W, H = s.H, T = new PlanTile[s.W * s.H] };
            for (int y = 0; y < s.H; y++)
                for (int x = 0; x < s.W; x++)
                    r.T[y * s.W + (s.W - 1 - x)] = s.T[y * s.W + x];
            r.Dx = s.W - 1 - s.Dx; r.Dy = s.Dy;
            return r;
        }

        private static Shape Variant(Shape s, int v)
        {
            if (v >= 4) s = Mirror(s);
            for (int i = 0; i < (v & 3); i++) s = Rotate(s);
            return s;
        }

        // Direction pointing from the door tile to the street cell in front of it.
        private static void OutDir(Shape s, TownProjectKind kind, out int ox, out int oy)
        {
            ox = 0; oy = 0;
            if (kind == TownProjectKind.WallSegment)
            {
                if (s.H == 1) ox = s.Dx == 0 ? -1 : 1;
                else oy = s.Dy == 0 ? -1 : 1;
                return;
            }
            if (s.Dy == 0) oy = -1;
            else if (s.Dy == s.H - 1) oy = 1;
            else if (s.Dx == 0) ox = -1;
            else ox = 1;
        }

        // Template facing south: door on the bottom wall at doorLx.
        private static Shape Box(int w, int h, PlanTile inner, int doorLx, int partition, int partDoor, int windows, Rng rng)
        {
            var s = new Shape { W = w, H = h, T = new PlanTile[w * h], Dx = doorLx, Dy = 0 };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    s.T[y * w + x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? PlanTile.Wall : inner;
            if (partition > 0)
            {
                for (int y = 1; y <= h - 2; y++) s.T[y * w + partition] = PlanTile.Wall;
                s.T[partDoor * w + partition] = PlanTile.Door;
            }
            s.T[doorLx] = PlanTile.Door;
            if (windows > 0)
            {
                var cand = new List<int>();
                for (int x = 1; x <= w - 2; x++)
                {
                    if (Math.Abs(x - doorLx) > 1 && s.T[1 * w + x] == PlanTile.Floor) cand.Add(x);
                    if (s.T[(h - 2) * w + x] == PlanTile.Floor) cand.Add((h - 1) * w + x);
                }
                for (int y = 1; y <= h - 2; y++)
                {
                    if (s.T[y * w + 1] == PlanTile.Floor) cand.Add(y * w);
                    if (s.T[y * w + w - 2] == PlanTile.Floor) cand.Add(y * w + w - 1);
                }
                for (int k = 0; k < windows && cand.Count > 0; k++)
                {
                    int pick = rng.Next(cand.Count);
                    s.T[cand[pick]] = PlanTile.Window;
                    cand.RemoveAt(pick);
                }
            }
            return s;
        }

        private static int DoorCol(int w, int avoid, Rng rng)
        {
            int c;
            do { c = 1 + rng.Next(w - 2); } while (c == avoid);
            return c;
        }

        private static Shape MakeShape(TownProjectKind kind, Rng rng, bool small)
        {
            switch (kind)
            {
                case TownProjectKind.House:
                {
                    int w = small ? 7 : 7 + rng.Next(3), h = small ? 6 : 6 + rng.Next(2);
                    int pc = (!small && rng.Next(2) == 0) ? 3 + rng.Next(w - 6) : 0;
                    int pd = 1 + rng.Next(h - 2);
                    return Box(w, h, PlanTile.Floor, DoorCol(w, pc, rng), pc, pd, 1 + rng.Next(2), rng);
                }
                case TownProjectKind.Workshop:
                {
                    int w = small ? 7 : 8, h = small ? 6 : 7;
                    return Box(w, h, PlanTile.Floor, DoorCol(w, 0, rng), 0, 0, 2, rng);
                }
                case TownProjectKind.Canteen:
                case TownProjectKind.Clinic:
                {
                    int w = 9, h = 7;
                    int pc = 3 + rng.Next(w - 6);
                    return Box(w, h, PlanTile.Floor, DoorCol(w, pc, rng), pc, 1 + rng.Next(h - 2), 2, rng);
                }
                case TownProjectKind.Garden:
                {
                    int w = small ? 5 : 5 + rng.Next(3), h = small ? 5 : 5 + rng.Next(2);
                    return Box(w, h, PlanTile.Outside, DoorCol(w, 0, rng), 0, 0, 0, rng);
                }
                case TownProjectKind.Watchtower:
                    return Box(4, 4, PlanTile.Floor, 1 + rng.Next(2), 0, 0, 1, rng);
                case TownProjectKind.Stall:
                {
                    var s = new Shape { W = 4, H = 3, T = new PlanTile[12], Dx = 1 + rng.Next(2), Dy = 0 };
                    PlanTile[] row0 = { PlanTile.Outside, PlanTile.Floor, PlanTile.Floor, PlanTile.Outside };
                    for (int x = 0; x < 4; x++)
                    {
                        s.T[x] = row0[x];
                        s.T[4 + x] = PlanTile.Floor;
                        s.T[8 + x] = PlanTile.Wall;
                    }
                    return s;
                }
                case TownProjectKind.WallSegment:
                {
                    int n = small ? 6 : 6 + rng.Next(7);
                    var s = new Shape { W = n, H = 1, T = new PlanTile[n], Dx = 0, Dy = 0 };
                    for (int i = 0; i < n; i++) s.T[i] = PlanTile.Wall;
                    return s;
                }
                default:
                {
                    var s = new Shape { W = 1, H = 1, T = new PlanTile[1], Dx = 0, Dy = 0 };
                    s.T[0] = PlanTile.Floor;
                    return s;
                }
            }
        }

        // ------------------------------------------------------------------ lot search

        private bool Fits(Shape sh, TownProjectKind kind, int X, int Y, bool[] occ, int selfId, bool replace)
        {
            if (X < 1 || Y < 1 || X + sh.W > Width - 1 || Y + sh.H > Height - 1) return false;
            bool wall = kind == TownProjectKind.WallSegment;
            if (wall && ringR == 0) return false;
            int self = selfId + 1;
            for (int ly = 0; ly < sh.H; ly++)
                for (int lx = 0; lx < sh.W; lx++)
                {
                    int x = X + lx, y = Y + ly, idx = y * Width + x;
                    if (blocked[idx] || street[idx]) return false;
                    if (occ != null && occ[idx]) return false;
                    int o = owner[idx];
                    if (o != 0 && o != self) return false;
                    if (wall) { if (Cheb(x, y) != ringR) return false; }
                    else if (!replace && Cheb(x, y) > lim) return false;
                }
            for (int y = Y - 1; y <= Y + sh.H; y++)
                for (int x = X - 1; x <= X + sh.W; x++)
                {
                    if (x >= X && x < X + sh.W && y >= Y && y < Y + sh.H) continue;
                    int o = owner[y * Width + x];
                    if (o != 0 && o != self) return false;
                }
            for (int ly = 0; ly < sh.H; ly++)
                for (int lx = 0; lx < sh.W; lx++)
                {
                    if (sh.T[ly * sh.W + lx] != PlanTile.Window) continue;
                    int wx = X + lx, wy = Y + ly;
                    if (ly == 0) wy--; else if (ly == sh.H - 1) wy++; else if (lx == 0) wx--; else wx++;
                    int wi = wy * Width + wx;
                    if (blocked[wi] || (occ != null && occ[wi])) return false;
                }
            return true;
        }

        private double GenScore(TownProjectKind kind, double cx, double cy, Rng rng)
        {
            double dx = cx - PlazaX, dy = cy - PlazaY;
            double d = Math.Sqrt(dx * dx + dy * dy);
            switch (kind)
            {
                case TownProjectKind.House: return d + rng.Next(40) / 10.0;
                case TownProjectKind.Workshop: return Math.Abs(d - 0.5 * lim) + rng.Next(30) / 10.0;
                case TownProjectKind.Garden: return Math.Abs(d - 0.4 * lim) + rng.Next(30) / 10.0;
                case TownProjectKind.Watchtower: return -d + rng.Next(30) / 10.0;
                case TownProjectKind.WallSegment: return rng.Next(1000);
                default: return d + rng.Next(10) / 10.0;
            }
        }

        private bool Search(Shape baseShape, TownProjectKind kind, bool replace, bool[] occ, int selfId, Rng rng,
            double ocx, double ocy, int origX, int origY, int origW, int origH,
            out Shape best, out int bestX, out int bestY)
        {
            best = null; bestX = 0; bestY = 0;
            double bestScore = double.MaxValue;
            int variants = replace ? 8 : 4;
            for (int v = 0; v < variants; v++)
            {
                Shape sh = Variant(baseShape, v);
                int ox, oy;
                OutDir(sh, kind, out ox, out oy);
                for (int i = 0; i < streets.Count; i++)
                {
                    int idx = streets[i];
                    int X = idx % Width - ox - sh.Dx;
                    int Y = idx / Width - oy - sh.Dy;
                    if (replace && X == origX && Y == origY && sh.W == origW && sh.H == origH) continue;
                    if (!Fits(sh, kind, X, Y, occ, selfId, replace)) continue;
                    double cx = X + (sh.W - 1) / 2.0, cy = Y + (sh.H - 1) / 2.0;
                    double score;
                    if (replace) score = (cx - ocx) * (cx - ocx) + (cy - ocy) * (cy - ocy);
                    else score = GenScore(kind, cx, cy, rng);
                    if (score < bestScore) { bestScore = score; best = sh; bestX = X; bestY = Y; }
                }
            }
            return best != null;
        }

        private void Mark(Shape sh, int X, int Y, int value)
        {
            for (int ly = 0; ly < sh.H; ly++)
                for (int lx = 0; lx < sh.W; lx++)
                    owner[(Y + ly) * Width + X + lx] = value;
        }

        private bool TryPlaceNew(TownProjectKind kind, Rng rng, List<Pend> pend)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Shape sh = MakeShape(kind, rng, attempt == 1);
                Shape best; int bx, by;
                if (Search(sh, kind, false, null, -1, rng, 0, 0, 0, 0, 0, 0, out best, out bx, out by))
                {
                    pend.Add(new Pend { Kind = kind, S = best, X = bx, Y = by });
                    Mark(best, bx, by, pend.Count);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ project placement

        private List<TownProjectKind> Sequence(int n)
        {
            var seq = new List<TownProjectKind>();
            if (n <= 0) return seq;
            bool ring = ringR > 0 && n >= 10;
            int canteen = 1, clinic = n >= 2 ? 1 : 0;
            int stalls = n >= 8 ? Math.Max(1, n * 7 / 100) : 0;
            int shops = n >= 8 ? Math.Max(1, n * 5 / 100) : 0;
            int gardens = n >= 8 ? Math.Max(1, n * 7 / 100) : 0;
            int towers = ring ? Math.Max(1, n * 7 / 100) : 0;
            int walls = ring ? Math.Max(1, n * 13 / 100) : 0;
            int houses = Math.Max(0, n - canteen - clinic - stalls - shops - gardens - towers - walls);

            seq.Add(TownProjectKind.Canteen);
            if (clinic > 0) seq.Add(TownProjectKind.Clinic);
            int first = Math.Min(3, houses);
            for (int i = 0; i < first; i++) seq.Add(TownProjectKind.House);
            int rest = houses - first;

            var mid = new List<TownProjectKind>();
            for (int i = 0; i < stalls; i++) mid.Add(TownProjectKind.Stall);
            for (int i = 0; i < shops; i++) mid.Add(TownProjectKind.Workshop);
            for (int i = 0; i < gardens; i++) mid.Add(TownProjectKind.Garden);
            int midHouses = (rest + 1) / 2;
            int mi = 0, hDone = 0;
            for (int i = 0; i < mid.Count; i++)
            {
                int upTo = (int)((long)(i + 1) * midHouses / (mid.Count + 1));
                while (hDone < upTo) { seq.Add(TownProjectKind.House); hDone++; }
                seq.Add(mid[mi++]);
            }
            while (hDone < midHouses) { seq.Add(TownProjectKind.House); hDone++; }
            for (int i = midHouses; i < rest; i++) seq.Add(TownProjectKind.House);
            int t = 0, w = 0;
            while (t < towers || w < walls)
            {
                if (w < walls) { seq.Add(TownProjectKind.WallSegment); w++; }
                if (t < towers) { seq.Add(TownProjectKind.Watchtower); t++; }
            }
            while (seq.Count > n) seq.RemoveAt(seq.Count - 1);
            return seq;
        }

        private void PlaceProjects(Rng rng, int requested)
        {
            var seq = Sequence(requested);
            var pend = new List<Pend>();
            foreach (var kind in seq)
            {
                if (TryPlaceNew(kind, rng, pend)) continue;
                if (kind != TownProjectKind.House) TryPlaceNew(TownProjectKind.House, rng, pend);
            }

            if (requested >= 30)
            {
                while (true)
                {
                    int houses = 0;
                    foreach (var p in pend) if (p.Kind == TownProjectKind.House) houses++;
                    if (houses * 100 >= 55 * pend.Count) break;
                    int drop = -1;
                    for (int i = pend.Count - 1; i >= 0; i--)
                    {
                        var k = pend[i].Kind;
                        if (k != TownProjectKind.House && k != TownProjectKind.Canteen && k != TownProjectKind.Clinic) { drop = i; break; }
                    }
                    if (drop < 0) break;
                    pend.RemoveAt(drop);
                }
            }

            Array.Clear(owner, 0, owner.Length);
            int n = pend.Count;
            for (int i = 0; i < n; i++)
            {
                var p = pend[i];
                int baseS = n <= 1 ? 0 : i * 380 / (n - 1);
                int s = Math.Max(0, Math.Min(400, baseS + rng.Next(17) - 8));
                var tp = new TownProject
                {
                    Id = i, Kind = p.Kind, X = p.X, Y = p.Y, Width = p.S.W, Height = p.S.H,
                    Tiles = p.S.T, DoorX = p.X + p.S.Dx, DoorY = p.Y + p.S.Dy,
                    StandingRequired = s,
                    RevealStanding = Math.Max(0, s - RevealLead(p.Kind)),
                    WorkUnits = Math.Max(2, p.S.W * p.S.H / 2),
                };
                projects.Add(tp);
                Mark(p.S, p.X, p.Y, i + 1);
            }
            for (int i = 0; i < n; i++) projects[i].Requires = ComputeRequires(i);
        }

        private static int RevealLead(TownProjectKind k)
        {
            switch (k)
            {
                case TownProjectKind.House: return 25;
                case TownProjectKind.Stall: return 30;
                case TownProjectKind.Garden: return 30;
                case TownProjectKind.Workshop: return 40;
                case TownProjectKind.Canteen: return 40;
                case TownProjectKind.Clinic: return 40;
                case TownProjectKind.Watchtower: return 50;
                default: return 60;
            }
        }

        private double Dist(TownProject a, TownProject b)
        {
            return Math.Abs(a.X + a.Width / 2.0 - b.X - b.Width / 2.0) + Math.Abs(a.Y + a.Height / 2.0 - b.Y - b.Height / 2.0);
        }

        // ids of the `count` earlier projects of `kind` nearest to project i (restricted to ids < maxId)
        private void Nearest(int i, TownProjectKind kind, int count, int maxId, List<int> into)
        {
            var used = new List<int>();
            for (int c = 0; c < count; c++)
            {
                int best = -1; double bd = double.MaxValue;
                for (int j = 0; j < i && j < maxId; j++)
                {
                    if (projects[j].Kind != kind || used.Contains(j)) continue;
                    double d = Dist(projects[i], projects[j]);
                    if (d < bd) { bd = d; best = j; }
                }
                if (best < 0) break;
                used.Add(best);
                if (!into.Contains(best)) into.Add(best);
            }
        }

        private int[] ComputeRequires(int i)
        {
            var req = new List<int>();
            var p = projects[i];
            switch (p.Kind)
            {
                case TownProjectKind.House:
                {
                    int h = 0;
                    for (int j = 0; j < i; j++) if (projects[j].Kind == TownProjectKind.House) h++;
                    if (h >= 1 && h < 3) Nearest(i, TownProjectKind.House, 1, int.MaxValue, req);
                    else if (h >= 3)
                    {
                        Nearest(i, TownProjectKind.House, 1, int.MaxValue, req);
                        if (h >= 6)
                        {
                            // second ring: also needs a first-ring house (one of the first six houses)
                            int cnt = 0, firstRingLimit = 0;
                            for (int j = 0; j < i; j++)
                                if (projects[j].Kind == TownProjectKind.House && ++cnt == 6) { firstRingLimit = j + 1; break; }
                            Nearest(i, TownProjectKind.House, 1, firstRingLimit, req);
                        }
                    }
                    break;
                }
                case TownProjectKind.Stall:
                    Nearest(i, TownProjectKind.Canteen, 1, int.MaxValue, req);
                    break;
                case TownProjectKind.Workshop:
                case TownProjectKind.Watchtower:
                case TownProjectKind.WallSegment:
                    Nearest(i, TownProjectKind.House, 2, int.MaxValue, req);
                    break;
                case TownProjectKind.Garden:
                    Nearest(i, TownProjectKind.House, 1, int.MaxValue, req);
                    break;
            }
            req.Sort();
            return req.ToArray();
        }

        // ------------------------------------------------------------------ re-placing

        public bool TryReplace(TownProject project, bool[] occupied, out TownProject moved)
        {
            moved = null;
            if (project == null || project.Tiles == null || project.Tiles.Length != project.Width * project.Height) return false;
            if (occupied != null && occupied.Length != Width * Height) return false;
            var baseShape = new Shape
            {
                W = project.Width, H = project.Height, T = (PlanTile[])project.Tiles.Clone(),
                Dx = project.DoorX - project.X, Dy = project.DoorY - project.Y
            };
            if (baseShape.Dx < 0 || baseShape.Dy < 0 || baseShape.Dx >= baseShape.W || baseShape.Dy >= baseShape.H) return false;
            int selfId = project.Id >= 0 && project.Id < projects.Count ? project.Id : -1;
            double ocx = project.X + (project.Width - 1) / 2.0, ocy = project.Y + (project.Height - 1) / 2.0;
            Shape best; int bx, by;
            if (!Search(baseShape, project.Kind, true, occupied, selfId, null, ocx, ocy,
                    project.X, project.Y, project.Width, project.Height, out best, out bx, out by))
                return false;
            moved = new TownProject
            {
                Id = project.Id, Kind = project.Kind, X = bx, Y = by, Width = best.W, Height = best.H,
                Tiles = best.T, DoorX = bx + best.Dx, DoorY = by + best.Dy,
                StandingRequired = project.StandingRequired, RevealStanding = project.RevealStanding,
                WorkUnits = project.WorkUnits,
                Requires = project.Requires == null ? new int[0] : (int[])project.Requires.Clone()
            };
            return true;
        }

        // Replaces the project with the same Id by `moved` (as returned by TryReplace).
        public void Apply(TownProject moved)
        {
            if (moved == null) throw new ArgumentNullException("moved");
            if (moved.Id < 0 || moved.Id >= projects.Count) throw new ArgumentException("unknown project id " + moved.Id);
            var old = projects[moved.Id];
            for (int y = old.Y; y < old.Y + old.Height; y++)
                for (int x = old.X; x < old.X + old.Width; x++)
                    if (owner[y * Width + x] == moved.Id + 1) owner[y * Width + x] = 0;
            projects[moved.Id] = moved;
            for (int y = moved.Y; y < moved.Y + moved.Height; y++)
                for (int x = moved.X; x < moved.X + moved.Width; x++)
                    owner[y * Width + x] = moved.Id + 1;
        }

        // ------------------------------------------------------------------ debug rendering

        // ASCII map, north at the top. step > 1 samples every step-th cell.
        // '~' blocked, '.' street/plaza, '#' wall, 'o' window, 'D' door, ',' open yard, letters = floor of a kind.
        public string Render(int step)
        {
            if (step < 1) step = 1;
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y -= step)
            {
                for (int x = 0; x < Width; x += step)
                {
                    int i = y * Width + x;
                    char c = ' ';
                    if (blocked[i]) c = '~';
                    else if (street[i]) c = '.';
                    int o = owner[i];
                    if (o != 0)
                    {
                        var p = projects[o - 1];
                        var t = p.Tiles[(y - p.Y) * p.Width + (x - p.X)];
                        switch (t)
                        {
                            case PlanTile.Wall: c = '#'; break;
                            case PlanTile.Window: c = 'o'; break;
                            case PlanTile.Door: c = 'D'; break;
                            case PlanTile.Outside: c = ','; break;
                            default: c = "HSWGTbXCL"[(int)p.Kind]; break;
                        }
                    }
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
