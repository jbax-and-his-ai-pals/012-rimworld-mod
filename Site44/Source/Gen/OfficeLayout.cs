using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    // Believable office-building floor plans (corridor first, then rooms hung off the corridors).
    // Pure C#, no RimWorld/Unity/Verse references. Same coordinate system as InteriorLayout:
    // local (0,0) is the south-west interior cell, +x east, +y north.
    public static class OfficeLayout
    {
        private const int MaxAttempts = 30;

        public static InteriorLayout Generate(int width, int height, SectorKind kind, IList<LayoutCell> entries, int seed)
        {
            if (kind == SectorKind.Courtyard) return InteriorLayout.Generate(width, height, kind, entries, seed);
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
                var b = new OfficeBuilder(width, height, kind, ents, new LayoutRng(Mix(seed, attempt)), attempt);
                InteriorLayout lay = b.Build();
                if (lay != null && OfficeChecker.IsValid(lay, kind, ents)) return lay;
            }
            return Fallback(width, height, kind, entries, seed);
        }

        private static int Mix(int seed, int attempt)
        {
            unchecked
            {
                uint z = (uint)seed * 0x9E3779B1u + (uint)attempt * 0xC2B2AE35u + 0x165667B1u;
                z ^= z >> 15;
                z *= 0x2C1B3C6Du;
                z ^= z >> 12;
                z *= 0x297A2D39u;
                z ^= z >> 15;
                return (int)z;
            }
        }

        // The abstract generator is always valid; give its rooms sensible functions.
        private static InteriorLayout Fallback(int width, int height, SectorKind kind, IList<LayoutCell> entries, int seed)
        {
            InteriorLayout lay = InteriorLayout.Generate(width, height, kind, entries, seed);
            LayoutRoom main = lay.MainRoom;
            var sides = new List<LayoutRoom>();
            foreach (LayoutRoom r in lay.Rooms)
            {
                if (r.Role == RoomRole.Duct) { r.Function = RoomFunction.Duct; continue; }
                if (r.Role == RoomRole.Corridor) { r.Function = RoomFunction.Corridor; continue; }
                if (r != main) sides.Add(r);
            }
            sides.Sort(delegate (LayoutRoom a, LayoutRoom b)
            {
                int c = b.Cells.Count.CompareTo(a.Cells.Count);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            switch (kind)
            {
                case SectorKind.Valve:
                case SectorKind.Reactor:
                case SectorKind.HotZone:
                    if (main != null) main.Function = RoomFunction.MachineHall;
                    for (int i = 0; i < sides.Count; i++) sides[i].Function = i == 0 ? RoomFunction.Mechanical : RoomFunction.Closet;
                    break;
                case SectorKind.Diagnostic:
                    if (sides.Count > 0)
                    {
                        if (main != null) main.Function = RoomFunction.OpenOffice;
                        sides[0].Function = RoomFunction.ServerRoom;
                        for (int i = 1; i < sides.Count; i++) sides[i].Function = i % 2 == 1 ? RoomFunction.PrivateOffice : RoomFunction.Closet;
                    }
                    else if (main != null) main.Function = RoomFunction.ServerRoom;
                    break;
                case SectorKind.Storage:
                    if (main != null) main.Function = RoomFunction.Archive;
                    for (int i = 0; i < sides.Count; i++) sides[i].Function = i % 3 == 2 ? RoomFunction.Closet : RoomFunction.Storeroom;
                    break;
                case SectorKind.Security:
                    if (main != null) main.Function = RoomFunction.SecurityPost;
                    for (int i = 0; i < sides.Count; i++) sides[i].Function = i % 2 == 0 ? RoomFunction.Storeroom : RoomFunction.Closet;
                    break;
                default:
                    if (main != null) main.Function = RoomFunction.PrivateOffice;
                    for (int i = 0; i < sides.Count; i++) sides[i].Function = i % 2 == 0 ? RoomFunction.Closet : RoomFunction.Storeroom;
                    break;
            }
            foreach (LayoutRoom r in lay.Rooms)
                if (r.Function == RoomFunction.Unspecified) r.Function = RoomFunction.Closet;
            return lay;
        }
    }

    internal sealed class OfficeRoom
    {
        public int Group;
        public LRect R;
        public int Cells;
        public bool IsCore;
        public bool Corner;
        public RoomFunction Func;
        public int MinDim { get { return Math.Min(R.W, R.H); } }
    }

    internal struct EBand { public int Side, Start, Bw; }   // Side: 0 W, 1 E, 2 S, 3 N (canonical frame)

    internal sealed class OfficeBuilder
    {
        private const int PlanRing = 0, PlanThrough = 1, PlanPlant = 2;

        private readonly int OW, OH, W, H, plan;
        private readonly bool tp, fx, fy;
        private readonly SectorKind kind;
        private readonly LayoutRng rng;
        private readonly List<LayoutCell> origEnts;
        private readonly List<LayoutCell> ents = new List<LayoutCell>();   // canonical frame
        private readonly List<EBand> bands = new List<EBand>();
        private int cw;
        private bool[] C;
        private int[] G;
        private readonly List<OfficeRoom> rooms = new List<OfficeRoom>();
        private bool[] doorCell, winCell;
        private byte[] ori;       // 0 none, 1 = floors north/south, 2 = floors east/west
        private List<int>[] cand;

        public OfficeBuilder(int ow, int oh, SectorKind kind, List<LayoutCell> origEnts, LayoutRng rng, int attempt)
        {
            OW = ow; OH = oh; this.kind = kind; this.rng = rng; this.origEnts = origEnts;
            if (kind == SectorKind.Valve || kind == SectorKind.Reactor || kind == SectorKind.HotZone) plan = PlanPlant;
            else if (kind == SectorKind.Storage) plan = PlanThrough;
            else plan = (Math.Min(ow, oh) >= 17 && attempt < 14) ? PlanRing : PlanThrough;
            bool t;
            if (plan == PlanThrough) t = ow < oh ? true : (ow > oh ? false : rng.Next(2) == 0);
            else t = rng.Next(2) == 0;
            tp = t; fx = rng.Next(2) == 0; fy = rng.Next(2) == 0;
            W = tp ? oh : ow; H = tp ? ow : oh;
        }

        private void Fwd(int x, int y, out int cx, out int cy)
        {
            int a = x, b = y;
            if (tp) { a = y; b = x; }
            if (fx) a = W - 1 - a;
            if (fy) b = H - 1 - b;
            cx = a; cy = b;
        }

        private bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
        private bool IsC(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H && C[y * W + x]; }

        private void AddC(int x0, int y0, int x1, int y1)
        {
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > W - 1) x1 = W - 1; if (y1 > H - 1) y1 = H - 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) { C[y * W + x] = true; G[y * W + x] = 0; }
        }

        // ------------------------------------------------------------------ top level
        public InteriorLayout Build()
        {
            if (OW < 9 || OH < 9) return null;
            C = new bool[W * H];
            G = new int[W * H];
            for (int i = 0; i < G.Length; i++) G[i] = -1;
            rooms.Add(new OfficeRoom { Group = 0, Func = RoomFunction.Corridor });
            for (int i = 0; i < origEnts.Count; i++)
            {
                int cx, cy;
                Fwd(origEnts[i].X, origEnts[i].Y, out cx, out cy);
                ents.Add(new LayoutCell(cx, cy));
            }
            cw = (plan == PlanPlant) ? 2 : (Math.Min(W, H) >= 20 ? 3 : 2);
            if (!MakeBands()) return null;

            bool ok;
            if (plan == PlanRing) ok = BuildRing();
            else if (plan == PlanThrough) ok = BuildThrough();
            else ok = BuildPlant();
            if (!ok) return null;
            if (plan != PlanPlant && !AssignFunctions()) return null;
            if (!PlaceDoors()) return null;
            PlaceWindows();
            return Emit();
        }

        private int SideOf(LayoutCell e)
        {
            if (e.X == 0) return 0;
            if (e.X == W - 1) return 1;
            if (e.Y == 0) return 2;
            if (e.Y == H - 1) return 3;
            return -1;
        }

        private bool MakeBands()
        {
            for (int i = 0; i < ents.Count; i++)
            {
                LayoutCell e = ents[i];
                int side = SideOf(e);
                if (side < 0) return false;
                int along = side < 2 ? e.Y : e.X;
                int len = side < 2 ? H : W;
                int bw = (along == 0 || along == len - 1) ? Math.Max(cw, 3) : cw;
                if (bw > len) bw = len;
                int start = along - rng.Next(bw);
                if (start > len - bw) start = len - bw;
                if (start < 0) start = 0;
                bands.Add(new EBand { Side = side, Start = start, Bw = bw });
            }
            return true;
        }

        // ------------------------------------------------------------------ plan: core + ring
        private bool Split1D(int len, int wLo, int wHi, bool fLo, bool fHi, int limLo, int limHi, out int sLo, out int sHi, out int core)
        {
            sLo = sHi = core = 0;
            if ((!fLo && limLo < 4) || (!fHi && limHi < 4)) return false;
            int rem = len - wLo - wHi;
            int minCore = rem - limLo - limHi;
            int coreMax = Math.Max(10, minCore);
            var opts = new List<int>();
            int aMax = fLo ? 0 : limLo, aMin = fLo ? 0 : 4;
            int bMax = fHi ? 0 : limHi, bMin = fHi ? 0 : 4;
            for (int a = aMin; a <= aMax; a++)
                for (int b = bMin; b <= bMax; b++)
                {
                    int c = rem - a - b;
                    if (c >= 5 && c <= coreMax) opts.Add(a * 100 + b);
                }
            if (opts.Count == 0) return false;
            int pick = opts[rng.Next(opts.Count)];
            sLo = pick / 100; sHi = pick % 100; core = rem - sLo - sHi;
            return true;
        }

        private bool BuildRing()
        {
            int minBy = int.MaxValue, maxTop = -1, minBx = int.MaxValue, maxRight = -1;
            for (int i = 0; i < bands.Count; i++)
            {
                EBand b = bands[i];
                if (b.Side < 2) { minBy = Math.Min(minBy, b.Start); maxTop = Math.Max(maxTop, b.Start + b.Bw - 1); }
                else { minBx = Math.Min(minBx, b.Start); maxRight = Math.Max(maxRight, b.Start + b.Bw - 1); }
            }
            bool fS = minBy != int.MaxValue && minBy < 4;
            bool fN = maxTop >= 0 && (H - 1 - maxTop) < 4;
            bool fW = minBx != int.MaxValue && minBx < 4;
            bool fE = maxRight >= 0 && (W - 1 - maxRight) < 4;
            int wS = fS ? Math.Max(cw, 3) : cw, wN = fN ? Math.Max(cw, 3) : cw;
            int wW = fW ? Math.Max(cw, 3) : cw, wE = fE ? Math.Max(cw, 3) : cw;
            int limS = fS ? 0 : (minBy == int.MaxValue ? 9 : Math.Min(9, minBy));
            int limN = fN ? 0 : (maxTop < 0 ? 9 : Math.Min(9, H - 1 - maxTop));
            int limW = fW ? 0 : (minBx == int.MaxValue ? 9 : Math.Min(9, minBx));
            int limE = fE ? 0 : (maxRight < 0 ? 9 : Math.Min(9, W - 1 - maxRight));
            int sS, sN, coreH, sW, sE, coreW;
            if (!Split1D(H, wS, wN, fS, fN, limS, limN, out sS, out sN, out coreH)) return false;
            if (!Split1D(W, wW, wE, fW, fE, limW, limE, out sW, out sE, out coreW)) return false;

            int rx0 = sW, rx1 = W - 1 - sE, ry0 = sS, ry1 = H - 1 - sN;
            int cx0 = rx0 + wW, cx1 = rx1 - wE, cy0 = ry0 + wS, cy1 = ry1 - wN;
            for (int y = ry0; y <= ry1; y++)
                for (int x = rx0; x <= rx1; x++)
                    if (!(x >= cx0 && x <= cx1 && y >= cy0 && y <= cy1)) AddC(x, y, x, y);
            for (int i = 0; i < bands.Count; i++)
            {
                EBand b = bands[i];
                switch (b.Side)
                {
                    case 0: AddC(0, b.Start, sW, b.Start + b.Bw - 1); break;
                    case 1: AddC(W - 1 - sE, b.Start, W - 1, b.Start + b.Bw - 1); break;
                    case 2: AddC(b.Start, 0, b.Start + b.Bw - 1, sS); break;
                    default: AddC(b.Start, H - 1 - sN, b.Start + b.Bw - 1, H - 1); break;
                }
            }

            BuildCore(new LRect(cx0 + 1, cy0 + 1, cx1 - 1, cy1 - 1));

            bool[] av = Avail();
            for (int y = cy0; y <= cy1; y++)
                for (int x = cx0; x <= cx1; x++) av[y * W + x] = false;
            return MakeZoneRooms(av);
        }

        private void BuildCore(LRect inner)
        {
            var leaves = new List<LRect>();
            leaves.Add(inner);
            int target = 2 + rng.Next(3);
            for (int guard = 0; guard < 20 && leaves.Count < target; guard++)
            {
                int pick = -1, bestA = 0;
                for (int i = 0; i < leaves.Count; i++)
                    if ((leaves[i].W >= 5 || leaves[i].H >= 5) && leaves[i].Area > bestA) { bestA = leaves[i].Area; pick = i; }
                if (pick < 0) break;
                LRect r = leaves[pick];
                bool vert;   // cut line is vertical (splits x)
                if (r.W >= 5 && r.H >= 5) vert = r.W > r.H || (r.W == r.H && rng.Next(2) == 0);
                else vert = r.W >= 5;
                int L = vert ? r.W : r.H;
                int c = 2 + rng.Next(L - 4);   // [2, L-3]
                LRect a, b;
                if (vert) { a = new LRect(r.X0, r.Y0, r.X0 + c - 1, r.Y1); b = new LRect(r.X0 + c + 1, r.Y0, r.X1, r.Y1); }
                else { a = new LRect(r.X0, r.Y0, r.X1, r.Y0 + c - 1); b = new LRect(r.X0, r.Y0 + c + 1, r.X1, r.Y1); }
                leaves[pick] = a; leaves.Add(b);
            }
            for (int i = 0; i < leaves.Count; i++) NewRoom(leaves[i], true, RoomFunction.Unspecified);
        }

        // ------------------------------------------------------------------ plan: through corridor / spine
        private bool BuildThrough()
        {
            int minD = 3;
            bool two = H >= cw + 2 * minD + 2;
            int r;
            if (two)
            {
                int rlo = minD + 1, rhi = H - cw - minD - 1;
                r = rlo + (rng.Next(rhi - rlo + 1) + rng.Next(rhi - rlo + 1) + 1) / 2;
                if (r > rhi) r = rhi;
            }
            else r = rng.Next(2) == 0 ? 0 : H - cw;
            int r1 = r + cw - 1;

            int minBx = int.MaxValue, maxRight = -1;
            bool hasW = false, hasE = false;
            for (int i = 0; i < bands.Count; i++)
            {
                EBand b = bands[i];
                if (b.Side == 0) hasW = true;
                else if (b.Side == 1) hasE = true;
                else { minBx = Math.Min(minBx, b.Start); maxRight = Math.Max(maxRight, b.Start + b.Bw - 1); }
            }
            int capW = 4 + rng.Next(2), capE = 4 + rng.Next(2);
            int xa = hasW ? 0 : capW;
            int xb = hasE ? W - 1 : W - 1 - capE;
            if (minBx != int.MaxValue) { xa = Math.Min(xa, minBx); xb = Math.Max(xb, maxRight); }
            if (xa > 0 && xa < 4) xa = 0;
            if (xb < W - 1 && xb > W - 5) xb = W - 1;
            if (xb - xa + 1 < 5) { xa = 0; xb = W - 1; }
            AddC(xa, r, xb, r1);

            for (int i = 0; i < bands.Count; i++)
            {
                EBand b = bands[i];
                int b1 = b.Start + b.Bw - 1;
                switch (b.Side)
                {
                    case 0:
                        AddC(0, b.Start, 2, b1);
                        AddC(0, Math.Min(b.Start, r), b.Bw - 1, Math.Max(b1, r1));
                        break;
                    case 1:
                        AddC(W - 3, b.Start, W - 1, b1);
                        AddC(W - b.Bw, Math.Min(b.Start, r), W - 1, Math.Max(b1, r1));
                        break;
                    case 2: AddC(b.Start, 0, b1, Math.Max(2, r1)); break;
                    default: AddC(b.Start, Math.Min(r, H - 3), b1, H - 1); break;
                }
            }
            return MakeZoneRooms(Avail());
        }

        // ------------------------------------------------------------------ plan: plant (machine hall + service corridor)
        private bool BuildPlant()
        {
            bool hot = kind == SectorKind.HotZone;
            var band = new bool[4];
            band[2] = true;
            if (!hot)
            {
                for (int i = 0; i < bands.Count; i++) band[bands[i].Side] = true;
                if (band[3] && !band[0] && !band[1]) band[rng.Next(2)] = true;
            }
            else
            {
                for (int i = 0; i < ents.Count; i++)
                    if (SideOf(ents[i]) != 2 && ents[i].Y >= 1 && ents[i].Y <= 4) return false;
            }
            AddC(0, 0, W - 1, 1);
            if (band[0]) AddC(0, 0, 1, H - 1);
            if (band[1]) AddC(W - 2, 0, W - 1, H - 1);
            if (band[3]) AddC(0, H - 2, W - 1, H - 1);
            for (int i = 0; i < bands.Count; i++)
            {
                EBand b = bands[i];
                if (hot && b.Side != 2 && ents[i].Y != 0) continue;
                int b1 = b.Start + b.Bw - 1;
                switch (b.Side)
                {
                    case 0: AddC(0, b.Start, 2, b1); break;
                    case 1: AddC(W - 3, b.Start, W - 1, b1); break;
                    case 2: AddC(b.Start, 0, b1, 2); break;
                    default: AddC(b.Start, H - 3, b1, H - 1); break;
                }
            }
            bool[] av = Avail();

            int want = 1 + rng.Next(3);
            int placed = 0;
            var avSides = new List<int>();
            avSides.Add(2); if (band[0]) avSides.Add(0); if (band[1]) avSides.Add(1); if (band[3]) avSides.Add(3);
            for (int k = 0; k < want; k++)
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    int side = avSides[rng.Next(avSides.Count)];
                    int d = 3 + rng.Next(3), len = 4 + rng.Next(3);
                    LRect r;
                    if (side == 2) { int x0 = rng.Next(Math.Max(1, W - len + 1)); r = new LRect(x0, 3, x0 + len - 1, 3 + d - 1); }
                    else if (side == 3) { int x0 = rng.Next(Math.Max(1, W - len + 1)); r = new LRect(x0, H - 3 - d, x0 + len - 1, H - 4); }
                    else if (side == 0) { int y0 = rng.Next(Math.Max(1, H - len + 1)); r = new LRect(3, y0, 3 + d - 1, y0 + len - 1); }
                    else { int y0 = rng.Next(Math.Max(1, H - len + 1)); r = new LRect(W - 3 - d, y0, W - 4, y0 + len - 1); }
                    if (r.X0 < 0 || r.Y0 < 0 || r.X1 >= W || r.Y1 >= H) continue;
                    if (!AllAvail(av, r) || Contact(r) < 1) continue;
                    bool near = false;
                    for (int i = 0; i < ents.Count && !near; i++)
                    {
                        int dx = Math.Max(Math.Max(r.X0 - 1 - ents[i].X, ents[i].X - (r.X1 + 1)), 0);
                        int dy = Math.Max(Math.Max(r.Y0 - 1 - ents[i].Y, ents[i].Y - (r.Y1 + 1)), 0);
                        if (Math.Max(dx, dy) < 3) near = true;
                    }
                    if (near) continue;
                    NewRoom(r, false, placed % 2 == 0 ? RoomFunction.Mechanical : RoomFunction.Closet);
                    Consume(av, r);
                    placed++;
                    break;
                }
            }
            if (placed == 0) return false;

            // hall = largest remaining component
            int[] lab = new int[W * H];
            for (int i = 0; i < lab.Length; i++) lab[i] = -1;
            int bestLab = -1, bestCount = 0, labs = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < av.Length; i++)
            {
                if (!av[i] || lab[i] >= 0) continue;
                int cnt = 0;
                lab[i] = labs; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); cnt++;
                    int cx = c % W, cy = c / W;
                    if (cx > 0 && av[c - 1] && lab[c - 1] < 0) { lab[c - 1] = labs; stack.Push(c - 1); }
                    if (cx < W - 1 && av[c + 1] && lab[c + 1] < 0) { lab[c + 1] = labs; stack.Push(c + 1); }
                    if (cy > 0 && av[c - W] && lab[c - W] < 0) { lab[c - W] = labs; stack.Push(c - W); }
                    if (cy < H - 1 && av[c + W] && lab[c + W] < 0) { lab[c + W] = labs; stack.Push(c + W); }
                }
                if (cnt > bestCount) { bestCount = cnt; bestLab = labs; }
                labs++;
            }
            if (bestLab < 0 || bestCount < 25) return false;
            var hall = new OfficeRoom { Group = rooms.Count, Func = RoomFunction.MachineHall };
            int minx = W, miny = H, maxx = -1, maxy = -1;
            for (int i = 0; i < lab.Length; i++)
            {
                if (lab[i] != bestLab) continue;
                G[i] = hall.Group; hall.Cells++;
                int x = i % W, y = i / W;
                if (x < minx) minx = x; if (x > maxx) maxx = x;
                if (y < miny) miny = y; if (y > maxy) maxy = y;
            }
            hall.R = new LRect(minx, miny, maxx, maxy);
            rooms.Add(hall);
            return true;
        }

        // ------------------------------------------------------------------ zones -> rooms
        private bool[] Avail()
        {
            var av = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (C[i] || G[i] != -1) continue;
                    if (IsC(x - 1, y) || IsC(x + 1, y) || IsC(x, y - 1) || IsC(x, y + 1)) continue;
                    av[i] = true;
                }
            return av;
        }

        private bool AllAvail(bool[] av, LRect r)
        {
            for (int y = r.Y0; y <= r.Y1; y++)
                for (int x = r.X0; x <= r.X1; x++)
                    if (!av[y * W + x]) return false;
            return true;
        }

        private void Consume(bool[] av, LRect r)
        {
            for (int y = Math.Max(0, r.Y0 - 1); y <= Math.Min(H - 1, r.Y1 + 1); y++)
                for (int x = Math.Max(0, r.X0 - 1); x <= Math.Min(W - 1, r.X1 + 1); x++) av[y * W + x] = false;
        }

        private OfficeRoom NewRoom(LRect r, bool core, RoomFunction f)
        {
            var room = new OfficeRoom { Group = rooms.Count, R = r, Cells = r.Area, IsCore = core, Func = f };
            room.Corner = (r.X0 == 0 || r.X1 == W - 1) && (r.Y0 == 0 || r.Y1 == H - 1);
            for (int y = r.Y0; y <= r.Y1; y++)
                for (int x = r.X0; x <= r.X1; x++) G[y * W + x] = room.Group;
            rooms.Add(room);
            return room;
        }

        private bool MakeZoneRooms(bool[] av)
        {
            List<LRect> zones = GreedyZones(av);
            int minW = kind == SectorKind.Storage ? 5 : 4, maxW = kind == SectorKind.Storage ? 8 : 7;
            for (int i = 0; i < zones.Count; i++)
            {
                List<LRect> rs = SplitZone(zones[i], minW, maxW);
                for (int k = 0; k < rs.Count; k++) NewRoom(rs[k], false, RoomFunction.Unspecified);
            }
            return rooms.Count > 1;
        }

        private List<LRect> GreedyZones(bool[] av)
        {
            var zones = new List<LRect>();
            var run = new int[W * H];
            for (int guard = 0; guard < 300; guard++)
            {
                for (int y = 0; y < H; y++)
                    for (int x = W - 1; x >= 0; x--)
                    {
                        int i = y * W + x;
                        run[i] = av[i] ? (x == W - 1 ? 1 : run[i + 1] + 1) : 0;
                    }
                int bestA = 0, bx = 0, by = 0, bw = 0, bh = 0;
                for (int y0 = 0; y0 < H; y0++)
                    for (int x0 = 0; x0 < W; x0++)
                    {
                        if (run[y0 * W + x0] == 0) continue;
                        int w = int.MaxValue;
                        for (int y1 = y0; y1 < H; y1++)
                        {
                            int rr = run[y1 * W + x0];
                            if (rr == 0) break;
                            if (rr < w) w = rr;
                            int hgt = y1 - y0 + 1, a = w * hgt;
                            if (a > bestA) { bestA = a; bx = x0; by = y0; bw = w; bh = hgt; }
                        }
                    }
                if (bestA < 6) break;
                int mn = Math.Min(bw, bh);
                var rect = new LRect(bx, by, bx + bw - 1, by + bh - 1);
                if (mn < 2 || (mn == 2 && bestA > 12))
                {
                    for (int y = rect.Y0; y <= rect.Y1; y++)
                        for (int x = rect.X0; x <= rect.X1; x++) av[y * W + x] = false;
                    continue;
                }
                zones.Add(rect);
                Consume(av, rect);
            }
            return zones;
        }

        // number of wall cells along r's perimeter that could hold a door onto a corridor
        private int Contact(LRect r)
        {
            int n = 0;
            for (int x = r.X0; x <= r.X1; x++)
            {
                n += CT(x, r.Y0 - 1, x, r.Y0 - 2, 1, 0);
                n += CT(x, r.Y1 + 1, x, r.Y1 + 2, 1, 0);
            }
            for (int y = r.Y0; y <= r.Y1; y++)
            {
                n += CT(r.X0 - 1, y, r.X0 - 2, y, 0, 1);
                n += CT(r.X1 + 1, y, r.X1 + 2, y, 0, 1);
            }
            return n;
        }

        private int CT(int wx, int wy, int cx, int cy, int lx, int ly)
        {
            if (!In(wx, wy) || !In(cx, cy)) return 0;
            if (!C[cy * W + cx] || C[wy * W + wx]) return 0;
            if (IsC(wx + lx, wy + ly) || IsC(wx - lx, wy - ly)) return 0;
            return 1;
        }

        private List<int> PickWidths(int L, int minW, int maxW)
        {
            int nmin = (L + 1 + maxW) / (maxW + 1);          // ceil((L+1)/(maxW+1))
            int nmax = (L + 1) / (minW + 1);
            int n;
            if (nmax < 1) n = 1;
            else if (nmin > nmax) n = nmax;
            else n = nmin + rng.Next(nmax - nmin + 1);
            int total = L - (n - 1);
            int bse = total / n, rem = total - bse * n;
            var ws = new List<int>();
            for (int i = 0; i < n; i++) ws.Add(bse);
            var idx = new List<int>();
            for (int i = 0; i < n; i++) idx.Add(i);
            for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = idx[i]; idx[i] = idx[j]; idx[j] = t; }
            for (int k = 0; k < rem; k++) ws[idx[k]]++;
            if (n > 1)
            {
                int moves = rng.Next(n + 1);
                for (int m = 0; m < moves; m++)
                {
                    int i = rng.Next(n), j = rng.Next(n);
                    if (i != j && ws[i] > minW && ws[j] < maxW) { ws[i]--; ws[j]++; }
                }
            }
            return ws;
        }

        private List<LRect> SplitZone(LRect z, int minW, int maxW)
        {
            bool alongX = z.W > z.H || (z.W == z.H && rng.Next(2) == 0);
            int L = alongX ? z.W : z.H;
            int depth = alongX ? z.H : z.W;
            int mn = depth < 3 ? Math.Min(minW, 3) : minW;
            List<LRect> best = null;
            int bestBad = int.MaxValue;
            for (int t = 0; t < 14; t++)
            {
                List<int> ws = PickWidths(L, mn, maxW);
                var rects = new List<LRect>();
                int p = alongX ? z.X0 : z.Y0;
                for (int i = 0; i < ws.Count; i++)
                {
                    if (alongX) rects.Add(new LRect(p, z.Y0, p + ws[i] - 1, z.Y1));
                    else rects.Add(new LRect(z.X0, p, z.X1, p + ws[i] - 1));
                    p += ws[i] + 1;
                }
                int bad = 0;
                for (int i = 0; i < rects.Count; i++) if (Contact(rects[i]) == 0) bad++;
                if (bad < bestBad) { bestBad = bad; best = rects; }
                if (bad == 0) break;
            }
            while (best.Count > 1)
            {
                int bi = -1;
                for (int i = 0; i < best.Count; i++) if (Contact(best[i]) == 0) { bi = i; break; }
                if (bi < 0) break;
                int nb = bi > 0 ? bi - 1 : bi + 1;
                int lo = Math.Min(bi, nb), hi = Math.Max(bi, nb);
                LRect a = best[lo], b = best[hi];
                best[lo] = new LRect(Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1));
                best.RemoveAt(hi);
            }
            var res = new List<LRect>();
            for (int i = 0; i < best.Count; i++) if (Contact(best[i]) > 0) res.Add(best[i]);
            return res;
        }

        // ------------------------------------------------------------------ room functions
        private int NearDist(OfficeRoom r, List<LayoutCell> refs)
        {
            if (refs.Count == 0) return 0;
            int best = int.MaxValue;
            for (int i = 0; i < refs.Count; i++)
            {
                LayoutCell e = refs[i];
                int dx = e.X < r.R.X0 ? r.R.X0 - e.X : (e.X > r.R.X1 ? e.X - r.R.X1 : 0);
                int dy = e.Y < r.R.Y0 ? r.R.Y0 - e.Y : (e.Y > r.R.Y1 ? e.Y - r.R.Y1 : 0);
                best = Math.Min(best, dx + dy);
            }
            return best;
        }

        private static bool Free(OfficeRoom r) { return r.Func == RoomFunction.Unspecified; }

        private OfficeRoom PickRandom(List<OfficeRoom> list)
        {
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }

        private List<OfficeRoom> FreeWhere(List<OfficeRoom> src, Func<OfficeRoom, bool> pred)
        {
            var l = new List<OfficeRoom>();
            for (int i = 0; i < src.Count; i++) if (Free(src[i]) && pred(src[i])) l.Add(src[i]);
            return l;
        }

        private bool AssignFunctions()
        {
            var core = new List<OfficeRoom>();
            var suites = new List<OfficeRoom>();
            for (int g = 1; g < rooms.Count; g++) (rooms[g].IsCore ? core : suites).Add(rooms[g]);
            Comparison<OfficeRoom> byArea = delegate (OfficeRoom a, OfficeRoom b)
            {
                int c = b.Cells.CompareTo(a.Cells);
                return c != 0 ? c : a.Group.CompareTo(b.Group);
            };
            core.Sort(byArea);
            suites.Sort(byArea);
            if (suites.Count == 0) return false;
            bool big = Math.Min(OW, OH) >= 15;

            RoomFunction[] seq;
            switch (kind)
            {
                case SectorKind.Diagnostic: seq = new[] { RoomFunction.Mechanical, RoomFunction.Closet, RoomFunction.Restroom }; break;
                case SectorKind.Security: seq = new[] { RoomFunction.Closet, RoomFunction.Restroom, RoomFunction.Mechanical }; break;
                default: seq = new[] { RoomFunction.Restroom, RoomFunction.Closet, RoomFunction.Mechanical }; break;
            }
            for (int i = 0; i < core.Count; i++) core[i].Func = i == 0 ? RoomFunction.Stairwell : seq[(i - 1) % seq.Length];

            // entries on the side with the most entries are the "front door"
            var cnt = new int[4];
            for (int i = 0; i < ents.Count; i++) cnt[SideOf(ents[i])]++;
            int bs = 0;
            for (int s = 1; s < 4; s++) if (cnt[s] > cnt[bs]) bs = s;
            var refs = new List<LayoutCell>();
            for (int i = 0; i < ents.Count; i++) if (SideOf(ents[i]) == bs) refs.Add(ents[i]);

            int n = suites.Count;
            bool hasCore = core.Count > 0;
            switch (kind)
            {
                case SectorKind.Storage:
                    return AssignStorage(suites, refs);
                case SectorKind.Security:
                    {
                        var c1 = FreeWhere(suites, r => r.Cells >= 9);
                        c1.Sort(delegate (OfficeRoom a, OfficeRoom b)
                        {
                            int c = NearDist(a, refs).CompareTo(NearDist(b, refs));
                            return c != 0 ? c : a.Group.CompareTo(b.Group);
                        });
                        OfficeRoom post = refs.Count > 0 ? (c1.Count > 0 ? c1[0] : null) : PickRandom(c1);
                        if (post == null) { if (big) return false; post = suites[0]; }
                        post.Func = RoomFunction.SecurityPost;
                        if (n >= 7)
                        {
                            var bk = FreeWhere(suites, r => r.Cells >= 16);
                            if (bk.Count > 0) bk[0].Func = RoomFunction.BreakRoom;
                        }
                        if (n >= 5)
                        {
                            var st = FreeWhere(suites, r => true);
                            if (st.Count > 0) st[st.Count - 1].Func = RoomFunction.Storeroom;
                        }
                        var cf = FreeWhere(suites, r => r.MinDim >= 5 && r.Cells >= 25);
                        if (cf.Count > 0 && rng.Chance(500)) PickRandom(cf).Func = RoomFunction.Conference;
                        break;
                    }
                case SectorKind.Diagnostic:
                    {
                        var cs = FreeWhere(suites, r => r.MinDim >= 4 && r.Cells >= 16);
                        OfficeRoom srv = cs.Count == 0 ? null : cs[rng.Next(Math.Min(2, cs.Count))];
                        if (srv == null)
                        {
                            if (big) return false;
                            var any = FreeWhere(suites, r => true);
                            srv = any[0];
                        }
                        srv.Func = RoomFunction.ServerRoom;
                        if (n >= 7)
                        {
                            var c2 = FreeWhere(suites, r => r.MinDim >= 4 && r.Cells >= 16);
                            if (c2.Count > 0 && rng.Chance(400)) PickRandom(c2).Func = RoomFunction.ServerRoom;
                        }
                        var oo = FreeWhere(suites, r => r.Cells >= 20);
                        if (oo.Count > 0) oo[0].Func = RoomFunction.OpenOffice;
                        var cf = FreeWhere(suites, r => r.MinDim >= 5 && r.Cells >= 25);
                        if (cf.Count > 0 && rng.Chance(600)) PickRandom(cf).Func = RoomFunction.Conference;
                        if (n >= 6)
                        {
                            var bk = FreeWhere(suites, r => r.Cells >= 12 && r.Cells <= 45);
                            if (bk.Count > 0) PickRandom(bk).Func = RoomFunction.BreakRoom;
                        }
                        break;
                    }
                default:   // Hall
                    {
                        if (suites[0].Cells >= 20) suites[0].Func = RoomFunction.OpenOffice;
                        if (refs.Count > 0)
                        {
                            var rc = FreeWhere(suites, r => r.Cells >= 12);
                            rc.Sort(delegate (OfficeRoom a, OfficeRoom b)
                            {
                                int c = NearDist(a, refs).CompareTo(NearDist(b, refs));
                                return c != 0 ? c : a.Group.CompareTo(b.Group);
                            });
                            if (rc.Count > 0) rc[0].Func = rc[0].Cells >= 30 ? RoomFunction.Lobby : RoomFunction.Reception;
                        }
                        var cc = FreeWhere(suites, r => r.MinDim >= 5 && r.Cells >= 25);
                        if (cc.Count > 0)
                        {
                            var corners = new List<OfficeRoom>();
                            for (int i = 0; i < cc.Count; i++) if (cc[i].Corner) corners.Add(cc[i]);
                            OfficeRoom pick = (corners.Count > 0 && rng.Chance(750)) ? PickRandom(corners) : PickRandom(cc);
                            pick.Func = RoomFunction.Conference;
                        }
                        if (n >= 5)
                        {
                            var bk = FreeWhere(suites, r => r.Cells >= 12 && r.Cells <= 44);
                            if (bk.Count > 0) PickRandom(bk).Func = RoomFunction.BreakRoom;
                        }
                        if (n >= 6)
                        {
                            var st = FreeWhere(suites, r => true);
                            if (st.Count > 0) st[st.Count - 1].Func = RoomFunction.Storeroom;
                        }
                        break;
                    }
            }
            if (!hasCore)
            {
                var small = FreeWhere(suites, r => r.Cells >= 6);
                if (small.Count >= 4)
                {
                    small[small.Count - 1].Func = RoomFunction.Restroom;
                    small[small.Count - 2].Func = RoomFunction.Closet;
                }
            }
            for (int i = 0; i < suites.Count; i++) if (Free(suites[i])) suites[i].Func = RoomFunction.PrivateOffice;
            return true;
        }

        private bool AssignStorage(List<OfficeRoom> suites, List<LayoutCell> refs)
        {
            int n = suites.Count;
            if (n < 2) return false;
            if (n >= 5)
            {
                OfficeRoom best = null; int bd = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    int d = NearDist(suites[i], refs);
                    if (suites[i].Cells <= 40 && d < bd) { bd = d; best = suites[i]; }
                }
                if (best != null) best.Func = RoomFunction.PrivateOffice;
            }
            if (n >= 4)
            {
                var sm = FreeWhere(suites, r => true);
                if (sm.Count > 2) sm[sm.Count - 1].Func = RoomFunction.Closet;
            }
            for (int i = 0; i < n; i++)
            {
                if (!Free(suites[i])) continue;
                suites[i].Func = (suites[i].Cells >= 28 && rng.Chance(700)) ? RoomFunction.Archive : RoomFunction.Storeroom;
            }
            return true;
        }

        // ------------------------------------------------------------------ doors
        private bool DoorNear(int i)
        {
            return doorCell[i] || doorCell[i - 1] || doorCell[i + 1] || doorCell[i - W] || doorCell[i + W];
        }

        private void PairOf(int i, out int a, out int b)
        {
            if (ori[i] == 1) { a = G[i + W]; b = G[i - W]; }
            else { a = G[i + 1]; b = G[i - 1]; }
        }

        private bool PlaceDoors()
        {
            int n = W * H;
            doorCell = new bool[n];
            winCell = new bool[n];
            ori = new byte[n];
            cand = new List<int>[rooms.Count];
            for (int g = 0; g < rooms.Count; g++) cand[g] = new List<int>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (G[i] != -1) continue;
                    int gn = G[i + W], gs = G[i - W], ge = G[i + 1], gw = G[i - 1];
                    int a, b;
                    if (gn >= 0 && gs >= 0 && gn != gs && ge < 0 && gw < 0) { ori[i] = 1; a = gn; b = gs; }
                    else if (ge >= 0 && gw >= 0 && ge != gw && gn < 0 && gs < 0) { ori[i] = 2; a = ge; b = gw; }
                    else continue;
                    if (a == 0 && b > 0) cand[b].Add(i);
                    else if (b == 0 && a > 0) cand[a].Add(i);
                }

            for (int g = 1; g < rooms.Count; g++)
            {
                List<int> cl = cand[g];
                if (cl.Count == 0) continue;
                int k = cl.Count >= 20 ? 3 : (cl.Count >= 8 ? 2 : 1);
                if (rooms[g].Cells < 12 || rooms[g].IsCore) k = 1;
                var chosen = new List<int>();
                for (int j = 0; j < k; j++)
                {
                    int best = -1, bestD = -1;
                    for (int t = 0; t < 8; t++)
                    {
                        int c = cl[rng.Next(cl.Count)];
                        if (DoorNear(c)) continue;
                        int md = int.MaxValue;
                        for (int q = 0; q < chosen.Count; q++)
                        {
                            int d = Math.Abs(chosen[q] % W - c % W) + Math.Abs(chosen[q] / W - c / W);
                            if (d < md) md = d;
                        }
                        if (md > bestD) { bestD = md; best = c; }
                    }
                    if (best < 0) break;
                    if (j > 0 && bestD < 4) break;
                    chosen.Add(best);
                    doorCell[best] = true;
                }
            }

            // every room must be reachable; rooms with no corridor door get a door to a reachable neighbour
            var reach = new bool[rooms.Count];
            for (int pass = 0; pass < rooms.Count + 2; pass++)
            {
                ComputeReach(reach);
                bool allReached = true, added = false;
                for (int g = 1; g < rooms.Count; g++)
                {
                    if (reach[g]) continue;
                    allReached = false;
                    var opts = new List<int>();
                    for (int y = 1; y < H - 1; y++)
                        for (int x = 1; x < W - 1; x++)
                        {
                            int i = y * W + x;
                            if (ori[i] == 0 || DoorNear(i)) continue;
                            int a, b;
                            PairOf(i, out a, out b);
                            if ((a == g && b >= 0 && reach[b]) || (b == g && a >= 0 && reach[a])) opts.Add(i);
                        }
                    if (opts.Count == 0) continue;
                    doorCell[opts[rng.Next(opts.Count)]] = true;
                    added = true;
                    break;
                }
                if (allReached) return true;
                if (!added) return false;
            }
            return false;
        }

        private void ComputeReach(bool[] reach)
        {
            int ng = rooms.Count;
            var adj = new List<int>[ng];
            for (int g = 0; g < ng; g++) adj[g] = new List<int>();
            for (int i = 0; i < doorCell.Length; i++)
            {
                if (!doorCell[i]) continue;
                int a, b;
                PairOf(i, out a, out b);
                adj[a].Add(b); adj[b].Add(a);
            }
            for (int g = 0; g < ng; g++) reach[g] = false;
            var st = new Stack<int>();
            reach[0] = true; st.Push(0);
            while (st.Count > 0)
            {
                int g = st.Pop();
                for (int k = 0; k < adj[g].Count; k++)
                {
                    int o = adj[g][k];
                    if (!reach[o]) { reach[o] = true; st.Push(o); }
                }
            }
        }

        // ------------------------------------------------------------------ windows
        private static bool GlassRoom(RoomFunction f)
        {
            return f == RoomFunction.Conference || f == RoomFunction.OpenOffice || f == RoomFunction.Reception || f == RoomFunction.Lobby;
        }

        private void PlaceWindows()
        {
            for (int g = 1; g < rooms.Count; g++)
            {
                if (!GlassRoom(rooms[g].Func)) continue;
                var cl = new List<int>();
                for (int k = 0; k < cand[g].Count; k++)
                {
                    int c = cand[g][k];
                    int x = c % W, y = c / W;
                    if (x < 1 || y < 1 || x > W - 2 || y > H - 2) continue;
                    if (DoorNear(c)) continue;
                    cl.Add(c);
                }
                cl.Sort(delegate (int a, int b)
                {
                    if (ori[a] != ori[b]) return ori[a].CompareTo(ori[b]);
                    int la = ori[a] == 1 ? a / W : a % W, lb = ori[b] == 1 ? b / W : b % W;
                    if (la != lb) return la.CompareTo(lb);
                    int aa = ori[a] == 1 ? a % W : a / W, ab = ori[b] == 1 ? b % W : b / W;
                    return aa.CompareTo(ab);
                });
                int s = 0;
                while (s < cl.Count)
                {
                    int e = s;
                    while (e + 1 < cl.Count && Consecutive(cl[e], cl[e + 1])) e++;
                    int len = e - s + 1;
                    int rw = 2 + rng.Next(2), gap = 2;
                    int i = rng.Next(gap + 1);
                    while (i < len)
                    {
                        for (int q = 0; q < rw && i + q < len; q++) winCell[cl[s + i + q]] = true;
                        i += rw + gap;
                    }
                    s = e + 1;
                }
            }
        }

        private bool Consecutive(int a, int b)
        {
            if (ori[a] != ori[b]) return false;
            return ori[a] == 1 ? (b == a + 1) : (b == a + W);
        }

        // ------------------------------------------------------------------ emit
        private InteriorLayout Emit()
        {
            var tiles = new TileKind[OW * OH];
            for (int y = 0; y < OH; y++)
                for (int x = 0; x < OW; x++)
                {
                    int cx, cy;
                    Fwd(x, y, out cx, out cy);
                    int ci = cy * W + cx;
                    TileKind k;
                    if (doorCell[ci]) k = TileKind.Door;
                    else if (winCell[ci]) k = TileKind.Window;
                    else k = G[ci] >= 0 ? TileKind.Floor : TileKind.Wall;
                    tiles[y * OW + x] = k;
                }
            InteriorLayout lay = InteriorLayout.FromTiles(OW, OH, tiles);
            if (lay == null) return null;

            var labelsPerGroup = new int[rooms.Count];
            int mainIdx = -1, mainCells = -1;
            for (int ri = 0; ri < lay.Rooms.Count; ri++)
            {
                LayoutRoom r = lay.Rooms[ri];
                int g = -2;
                for (int k = 0; k < r.Cells.Count; k++)
                {
                    int cx, cy;
                    Fwd(r.Cells[k].X, r.Cells[k].Y, out cx, out cy);
                    int gg = G[cy * W + cx];
                    if (g == -2) g = gg;
                    else if (g != gg) return null;
                }
                if (g < 0) return null;
                labelsPerGroup[g]++;
                if (g > 0 && labelsPerGroup[g] > 1) return null;
                if (g == 0) { r.Role = RoomRole.Corridor; r.Function = RoomFunction.Corridor; }
                else
                {
                    r.Role = RoomRole.Side;
                    r.Function = rooms[g].Func == RoomFunction.Unspecified ? RoomFunction.PrivateOffice : rooms[g].Func;
                    if (r.Cells.Count > mainCells) { mainCells = r.Cells.Count; mainIdx = ri; }
                }
            }
            if (mainIdx < 0) return null;
            lay.Rooms[mainIdx].Role = RoomRole.Main;
            lay.SetMain(mainIdx);
            return lay;
        }
    }

    // Independent-ish validation used for retry decisions (the unit tests carry their own checker).
    internal static class OfficeChecker
    {
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        private static bool Open(TileKind k) { return k == TileKind.Floor || k == TileKind.Duct; }
        private static bool Solid(TileKind k) { return k == TileKind.Wall || k == TileKind.Window; }

        public static bool IsValid(InteriorLayout L, SectorKind kind, List<LayoutCell> entries)
        {
            int w = L.Width, h = L.Height, area = w * h;

            for (int i = 0; i < entries.Count; i++)
            {
                LayoutCell e = entries[i];
                if (L.At(e.X, e.Y) != TileKind.Floor || L.RoomAt(e.X, e.Y) < 0) return false;
                if (e.X == 0 && !Run(L, e.X, e.Y, 1, 0)) return false;
                if (e.X == w - 1 && !Run(L, e.X, e.Y, -1, 0)) return false;
                if (e.Y == 0 && !Run(L, e.X, e.Y, 0, 1)) return false;
                if (e.Y == h - 1 && !Run(L, e.X, e.Y, 0, -1)) return false;
            }

            int floor = 0, first = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (L.At(x, y) == TileKind.Floor) { floor++; if (first < 0) first = y * w + x; }
            if (first < 0) return false;
            var seen = new bool[area];
            var st = new Stack<int>();
            seen[first] = true; st.Push(first);
            int reached = 0;
            while (st.Count > 0)
            {
                int c = st.Pop();
                if (L.At(c % w, c / w) == TileKind.Floor) reached++;
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % w + Dx[d], ny = c / w + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (seen[ni]) continue;
                    TileKind k = L.At(nx, ny);
                    if (k == TileKind.Floor || k == TileKind.Door) { seen[ni] = true; st.Push(ni); }
                }
            }
            if (reached != floor) return false;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    TileKind k = L.At(x, y);
                    TileKind n = L.At(x, y + 1), s = L.At(x, y - 1), e = L.At(x + 1, y), wv = L.At(x - 1, y);
                    if (k == TileKind.Door)
                    {
                        bool a = Open(n) && Open(s) && Solid(e) && Solid(wv);
                        bool b = Open(e) && Open(wv) && Solid(n) && Solid(s);
                        if (!a && !b) return false;
                        if (e == TileKind.Door || n == TileKind.Door) return false;
                    }
                    else if (k == TileKind.Window)
                    {
                        if (!((n == TileKind.Floor && s == TileKind.Floor) || (e == TileKind.Floor && wv == TileKind.Floor))) return false;
                        if (n == TileKind.Door || s == TileKind.Door || e == TileKind.Door || wv == TileKind.Door) return false;
                        if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return false;
                    }
                }

            int mains = 0;
            LayoutRoom best = null;
            foreach (LayoutRoom r in L.Rooms)
            {
                if (r.Function == RoomFunction.Unspecified || r.Function == RoomFunction.Duct) return false;
                if ((r.Role == RoomRole.Corridor) != (r.Function == RoomFunction.Corridor)) return false;
                if (r.Role == RoomRole.Main) mains++;
                if (r.Role != RoomRole.Corridor && (best == null || r.Cells.Count > best.Cells.Count)) best = r;
            }
            if (mains != 1 || best == null || L.MainRoom == null || L.MainRoom.Cells.Count != best.Cells.Count) return false;

            bool both15 = w >= 15 && h >= 15;
            if (floor * 100 < (both15 ? 55 : 40) * area) return false;

            int servers = 0, stores = 0, posts = 0, halls = 0, mech = 0;
            foreach (LayoutRoom r in L.Rooms)
            {
                switch (r.Function)
                {
                    case RoomFunction.ServerRoom: servers++; break;
                    case RoomFunction.Archive:
                    case RoomFunction.Storeroom: stores++; break;
                    case RoomFunction.SecurityPost: posts++; break;
                    case RoomFunction.MachineHall: halls++; break;
                    case RoomFunction.Mechanical: mech++; break;
                }
            }
            if (both15)
            {
                if (kind == SectorKind.Diagnostic && servers < 1) return false;
                if (kind == SectorKind.Storage && stores < 2) return false;
                if (kind == SectorKind.Security && posts < 1) return false;
                if ((kind == SectorKind.Valve || kind == SectorKind.Reactor || kind == SectorKind.HotZone) && (halls < 1 || mech < 1)) return false;
            }

            // corridor access for big rooms
            var hasDoor = new bool[L.Rooms.Count];
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (L.At(x, y) != TileKind.Door) continue;
                    int a = -1, b = -1;
                    if (L.At(x, y + 1) == TileKind.Floor && L.At(x, y - 1) == TileKind.Floor) { a = L.RoomAt(x, y + 1); b = L.RoomAt(x, y - 1); }
                    else if (L.At(x + 1, y) == TileKind.Floor && L.At(x - 1, y) == TileKind.Floor) { a = L.RoomAt(x + 1, y); b = L.RoomAt(x - 1, y); }
                    if (a < 0 || b < 0) continue;
                    if (L.Rooms[a].Role == RoomRole.Corridor) hasDoor[b] = true;
                    if (L.Rooms[b].Role == RoomRole.Corridor) hasDoor[a] = true;
                }
            int bigRooms = 0, bigOk = 0;
            foreach (LayoutRoom r in L.Rooms)
            {
                if (r.Role == RoomRole.Corridor || r.Cells.Count < 12) continue;
                bigRooms++;
                if (hasDoor[r.Id]) bigOk++;
            }
            if (bigRooms > 0 && bigOk * 100 < bigRooms * 80) return false;
            return true;
        }

        private static bool Run(InteriorLayout L, int x, int y, int dx, int dy)
        {
            for (int k = 1; k <= 2; k++)
            {
                int nx = x + dx * k, ny = y + dy * k;
                if (nx < 0 || ny < 0 || nx >= L.Width || ny >= L.Height) continue;
                if (L.At(nx, ny) != TileKind.Floor) return false;
            }
            return true;
        }
    }
}
