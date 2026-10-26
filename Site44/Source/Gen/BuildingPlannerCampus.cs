using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    internal sealed class PBldg
    {
        public string Arch;
        public int Bid, W, H;
        public List<PBar> Bars = new List<PBar>();
        public PBar Front, Hub;
        public bool Isolated;
        public List<int[]> Courts = new List<int[]>();
        public int Filled, Open;
        public List<int[]> Joins = new List<int[]>();

        public void Shift(int dx, int dy)
        {
            foreach (PBar b in Bars) { b.X0 += dx; b.Y0 += dy; }
            foreach (int[] c in Courts) { c[0] += dx; c[2] += dx; c[1] += dy; c[3] += dy; c[6] += dy; c[7] += dy; }
            foreach (int[] j in Joins) { j[0] += dx; j[2] += dx; j[1] += dy; j[3] += dy; }
        }

        public void Transpose()
        {
            foreach (PBar b in Bars) { int t = b.X0; b.X0 = b.Y0; b.Y0 = t; b.Horiz = !b.Horiz; }
            int w = W; W = H; H = w;
        }

        public void Measure()
        {
            int mx = 0, my = 0;
            foreach (PBar b in Bars) { mx = Math.Max(mx, b.XMax + 1); my = Math.Max(my, b.Y1 + 1); }
            W = mx; H = my;
        }
    }

    internal sealed class PPort
    {
        public PBar Bar;
        public bool High, Used, Side;
        public int Bld, X, Y, Dir;   // Dir: 0 E, 1 W, 2 N, 3 S
        public int Lc { get { return Math.Min(3, Bar.Cw); } }
    }

    internal sealed partial class PlanBuild
    {
        public string Why;
        bool Fail(string w) { Why = w; return false; }

        readonly List<PBldg> blds = new List<PBldg>();
        readonly List<int[]> linkRects = new List<int[]>();
        PBar entryBar;
        bool entSet;
        int px0, px1, py0, py1;
        readonly List<int[]> gateRects = new List<int[]>();
        readonly List<int[]> booths = new List<int[]>();   // fx0, fy0, fx1, fy1, doorX, doorY, bid
        int minLinkDx = 9, minLinkTotal = 8;
        int paradeX0, paradeX1, paradeY1;
        int gate2Side;
        int scatterBound = 3;
        int[] occ;
        int ax0, ay0, ax1, ay1;

        // ------------------------------------------------------------------ campus planning
        bool Plan(bool simple, int attempt)
        {
            if (tiny) return PlanTiny(simple);
            int mS = 10 + (simple ? 0 : rng.Next(2));
            int mN = 3 + (simple ? 0 : rng.Next(2));
            int mW = 3 + (simple ? 0 : rng.Next(2));
            int mE = 3 + (simple ? 0 : rng.Next(2));
            ax0 = mW; ax1 = W - 1 - mE; ay0 = mS; ay1 = H - 1 - mN;
            bool compound = style == "compound", scattered = style == "scattered";
            if (compound)
            {
                int ie = 4 + rng.Next(5), iw = 4 + rng.Next(5), inn = 4 + rng.Next(5), isouth = 10 + rng.Next(2);
                px0 = iw; px1 = W - 1 - ie; py0 = isouth; py1 = H - 1 - inn;
                gate2Side = rng.Next(3);
                ax0 = px0 + 4 + (gate2Side == 0 ? 12 : 0); ax1 = px1 - 4 - (gate2Side == 1 ? 12 : 0);
                ay0 = py0 + 15; ay1 = py1 - 4 - (gate2Side == 2 ? 12 : 0);
                minLinkDx = 7; minLinkTotal = 6;
            }

            int aw = ax1 - ax0 + 1, ah = ay1 - ay0 + 1;
            if (style == "campus" ? (aw < 80 || ah < 70) : (aw < 52 || ah < 52)) return Fail("area");

            bool big = w0 >= 180 && h0 >= 180;
            double target = style == "campus" ? (big ? 0.60 : 0.48) : (big ? 0.42 : 0.36);
            int mnSite = Math.Min(w0, h0);
            int nbMin, nbMax;
            if (mnSite <= 130) { nbMin = 1; nbMax = 2; }
            else if (mnSite <= 170) { nbMin = 2; nbMax = 4; }
            else { nbMin = 3; nbMax = (W * H < 60000) ? 7 : 9; }
            if (profile != null)
            {
                if (profile.MinBuildings > 0) nbMin = Math.Min(profile.MinBuildings, nbMax);
                if (profile.MaxBuildings > 0) nbMax = profile.MaxBuildings;
                if (nbMax < nbMin) nbMax = nbMin;
                if (mnSite <= 130) nbMin = 1;
            }

            if (scattered)
            {
                if (!PlaceScattered(simple, attempt)) return false;
                return Finish(simple);
            }

            int gapAssumed = compound ? 7 : 10;
            List<int[]> opts = new List<int[]>();
            int[] best = null;
            for (int cols = 1; cols <= 4; cols++)
            {
                for (int rows = 1; rows <= 4; rows++)
                {
                    int nb = cols * rows;
                    if (nb < nbMin || nb > nbMax) continue;
                    int cwid = (aw - (cols - 1) * gapAssumed) / cols, chei = (ah - (rows - 1) * gapAssumed) / rows;
                    int capDim = nb == 1 ? 105 : 95;
                    if (cwid < 52 || chei < 52) continue;
                    double est = nb * (double)Math.Min(capDim, cwid) * Math.Min(capDim, chei) * 0.70 / (W * H);
                    int[] o = { cols, rows, (int)(est * 1000) };
                    if (best == null || o[2] > best[2]) best = o;
                    if (est >= target) opts.Add(o);
                }
            }

            if (best == null) return Fail("grid");
            int[] pickO = opts.Count > 0 ? opts[rng.Next(opts.Count)] : best;
            int gc = pickO[0], gr = pickO[1];

            int[] gx = new int[gc], gy = new int[gr];
            int sumGx = 0, sumGy = 0;
            for (int i = 0; i < gc - 1; i++) { gx[i] = compound ? (simple ? 8 : 6 + rng.Next(4)) : (simple ? 10 : 8 + rng.Next(4)); sumGx += gx[i]; }
            for (int i = 0; i < gr - 1; i++) { gy[i] = compound ? (simple ? 8 : 6 + rng.Next(4)) : (simple ? 10 : 8 + rng.Next(4)); sumGy += gy[i]; }
            int[] cwv = new int[gc], chv = new int[gr];
            for (int i = 0; i < gc; i++) cwv[i] = (aw - sumGx) / gc + (i < (aw - sumGx) % gc ? 1 : 0);
            for (int i = 0; i < gr; i++) chv[i] = (ah - sumGy) / gr + (i < (ah - sumGy) % gr ? 1 : 0);

            // cell order: the office takes a bottom-row cell; the rest follow in row-major order
            int officeCol = rng.Next(gc);
            int bid = 0;
            bool securityUsed = false;
            int cy = ay0;
            for (int r = 0; r < gr; r++)
            {
                int cx = ax0;
                for (int c = 0; c < gc; c++)
                {
                    bool isOffice = r == 0 && c == officeCol;
                    int capD = gc * gr == 1 ? 105 : 95;
                    int bw = Math.Min(capD, cwv[c]), bh = Math.Min(capD, chv[r]);
                    if (!simple)
                    {
                        if (!isOffice && rng.Next(100) < 25) { bw = Math.Max(52, bw - rng.Next(6)); bh = Math.Max(52, bh - rng.Next(6)); }
                    }

                    string arch = principal;
                    bool weighted = profile != null && profile.Weights != null && profile.Weights.Count > 0;
                    if (!isOffice && weighted && !simple) arch = PickWeightedArch();
                    else if (!isOffice)
                    {
                        int k = rng.Next(18);
                        if (k < 4) arch = "Office";
                        else if (k < 8) arch = "Lab";
                        else if (k < 12) arch = "Warehouse";
                        else if (k < 15) arch = "Plant";
                        else arch = "Commons";
                        if (!securityUsed && !simple && Math.Min(bw, bh) < 70 && rng.Next(100) < 30) { arch = "Security"; securityUsed = true; }
                        if (simple) arch = "Lab";
                    }

                    PBldg b = new PBldg();
                    b.Arch = arch;
                    bool dense = simple || attempt >= 2 || rng.Next(100) < 88;
                    if (arch == "Security") { dense = false; bw = Math.Min(bw, 45); bh = Math.Min(bh, 60); }
                    MakeBuilding(b, bw, bh, dense, isOffice, simple);
                    if (b.Bars.Count == 0) return Fail("form");
                    b.Measure();
                    if (b.W > cwv[c] || b.H > chv[r]) return Fail("oversize");
                    b.Bid = bid;
                    foreach (PBar bar in b.Bars) bar.Bid = bid;
                    int offx = simple ? 0 : rng.Next(Math.Max(1, cwv[c] - b.W + 1));
                    int offy = (isOffice || simple) ? 0 : rng.Next(Math.Max(1, chv[r] - b.H + 1));
                    if (isOffice) offx = Math.Min(offx, 4);
                    b.Shift(cx + offx, cy + offy);
                    foreach (int[] j in b.Joins) linkRects.Add(j);
                    if (isOffice) { blds.Insert(0, b); }
                    else blds.Add(b);
                    cx += cwv[c] + (c < gc - 1 ? gx[c] : 0);
                    bid++;
                }

                cy += chv[r] + (r < gr - 1 ? gy[r] : 0);
            }

            return Finish(simple);
        }

        bool Finish(bool simple)
        {
            bool compound = style == "compound", scattered = style == "scattered";
            // re-number: the office is building 0
            for (int i = 0; i < blds.Count; i++)
            {
                blds[i].Bid = i;
                foreach (PBar bar in blds[i].Bars) bar.Bid = i;
                archs.Add(blds[i].Arch);
            }

            if (blds[0].Arch != principal) return Fail("principal missing");
            entryBar = blds[0].Hub != null ? blds[0].Hub : blds[0].Front;

            occ = new int[W * H];
            foreach (PBldg b in blds)
                foreach (PBar bar in b.Bars)
                    for (int u = 0; u < bar.L; u++)
                        for (int v = 0; v < bar.Dp; v++)
                            occ[bar.Y(u, v) * W + bar.X(u, v)] = b.Bid + 1;

            if (!entSet) SetEntrance(simple);

            if (!scattered && !tiny && blds.Count >= 4)
            {
                int isoWant = rng.Next(100) < 40 ? 0 : (rng.Next(100) < 60 ? 1 : 2);
                if (simple) isoWant = 0;
                // only the smallest non-principal buildings may stand alone
                List<int> order = new List<int>();
                for (int k = 1; k < blds.Count; k++) order.Add(k);
                order.Sort(delegate (int a, int c) { return (blds[a].W * blds[a].H).CompareTo(blds[c].W * blds[c].H); });
                int pool = Math.Min(order.Count, 2);
                for (int tries = 0; tries < 8 && isoWant > 0; tries++)
                {
                    int k = order[rng.Next(pool)];
                    if (blds[k].Isolated) continue;
                    long totalArea = 0, isoArea = blds[k].W * blds[k].H;
                    foreach (PBldg o in blds) { totalArea += o.W * o.H; if (o.Isolated) isoArea += o.W * o.H; }
                    if (isoArea * 100 > totalArea * 18) continue;
                    blds[k].Isolated = true; isoWant--;
                }
            }

            if (scattered || tiny) { if (!LinkScatter(simple)) return false; }
            else if (!LinkAll()) return false;

            // exterior doors: every building gets at least one, larger ones more
            foreach (PBldg bl in blds)
            {
                if (bl.Bars.Count == 0) continue;
                List<PBar> freeBar = new List<PBar>();
                List<bool> freeHigh = new List<bool>();
                foreach (PBar bar in bl.Bars)
                {
                    if (bar.IsHub) continue;
                    if (!bar.LowAtt && !bar.LinkLow) { freeBar.Add(bar); freeHigh.Add(false); }
                    if (!bar.HighAtt && !bar.LinkHigh) { freeBar.Add(bar); freeHigh.Add(true); }
                }

                int maxDim = Math.Max(bl.W, bl.H);
                int want = 1 + (maxDim >= 50 ? 1 : 0) + ((bl.Arch == "Warehouse" || bl.Arch == "Plant") ? 1 : 0) + (maxDim >= 75 && rng.Next(2) == 0 ? 1 : 0);
                if (bl.Isolated && maxDim > 40) want = Math.Max(want, 2);
                want = Math.Min(Math.Min(want, 4), freeBar.Count);
                if (want < 1 || (bl.Isolated && maxDim > 40 && want < 2)) return Fail("noext");
                int[] idxs = new int[freeBar.Count];
                for (int i = 0; i < idxs.Length; i++) idxs[i] = i;
                for (int i = idxs.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int tmp = idxs[i]; idxs[i] = idxs[j]; idxs[j] = tmp;
                }

                if (scattered)
                {
                    int pcx = (paradeX0 + paradeX1) / 2, pcy = (ay0 + paradeY1) / 2;
                    int[] dist = new int[idxs.Length];
                    for (int i = 0; i < idxs.Length; i++)
                    {
                        PBar fb = freeBar[idxs[i]];
                        int uu = freeHigh[idxs[i]] ? fb.L - 1 : 0;
                        dist[i] = Math.Abs(fb.X(uu, fb.Mid) - pcx) + Math.Abs(fb.Y(uu, fb.Mid) - pcy);
                    }

                    for (int i = 1; i < idxs.Length; i++)
                        for (int j = i; j > 0 && dist[j] < dist[j - 1]; j--)
                        {
                            int t1 = dist[j]; dist[j] = dist[j - 1]; dist[j - 1] = t1;
                            int t2 = idxs[j]; idxs[j] = idxs[j - 1]; idxs[j - 1] = t2;
                        }
                }

                for (int i = 0; i < want; i++)
                {
                    PBar bar = freeBar[idxs[i]];
                    if (freeHigh[idxs[i]]) bar.ExtHigh = true; else bar.ExtLow = true;
                }
            }

            foreach (PBldg b in blds) foreach (PBar bar in b.Bars) SetRange(bar);
            foreach (PBldg b in blds) AssignChamfers(b, simple);
            foreach (PBldg b in blds) { courtFilled += b.Filled; courtOpen += b.Open; }

            if (compound && !BuildPerimeter(simple)) return false;
            if (entY < 9) return Fail("front");
            foreach (PBldg b in blds) foreach (PBar bar in b.Bars) bars.Add(bar);
            return true;
        }

        void SetEntrance(bool simple)
        {
            entSet = true;
            entryBar = blds[0].Hub != null ? blds[0].Hub : blds[0].Front;
            PBar f0 = blds[0].Front;
            if (blds[0].Hub != null)
            {
                PBar hub = blds[0].Hub;
                entX = hub.X0 + hub.L / 2;
                entY = hub.Y0;
            }
            else
            {
                int lw = tiny ? 8 + rng.Next(3) : 12 + rng.Next(5);
                int span = f0.L / 6;
                int cu = f0.L / 2 + (simple ? 0 : rng.Next(2 * span + 1) - span);
                int u0 = Clamp(cu - lw / 2, 3, f0.L - 3 - lw);
                f0.LobbyU0 = u0; f0.LobbyU1 = u0 + lw - 1;
                f0.Atrium = !simple && !tiny && ((w0 >= 200 || h0 >= 200) ? rng.Next(100) < 65 : rng.Next(100) < 30);
                entX = f0.X((f0.LobbyU0 + f0.LobbyU1) / 2, 0);
                entY = f0.Y(0, 0);
            }

        }

        static void SetRange(PBar b)
        {
            if (b.IsHub) { b.Ua = 1; b.Ub = b.L - 2; return; }
            int ua0 = 6, ub0 = b.L - 7;
            if (b.HighBlocks.Count > 0 || b.LowBlocks.Count > 0)
            {
                ua0 = int.MaxValue; ub0 = -1;
                for (int i = 0; i < b.HighBlocks.Count; i += 2) { ua0 = Math.Min(ua0, b.HighBlocks[i]); ub0 = Math.Max(ub0, b.HighBlocks[i + 1]); }
                for (int i = 0; i < b.LowBlocks.Count; i += 2) { ua0 = Math.Min(ua0, b.LowBlocks[i]); ub0 = Math.Max(ub0, b.LowBlocks[i + 1]); }
            }

            if (b.LowAtt) b.Ua = 0;
            else if (b.LinkLow || b.ExtLow) b.Ua = 1;
            else b.Ua = Math.Min(6, ua0);
            if (b.HighAtt) b.Ub = b.L - 1;
            else if (b.LinkHigh || b.ExtHigh) b.Ub = b.L - 2;
            else b.Ub = Math.Max(b.L - 7, ub0);
            if (!b.LowAtt && b.Ua >= 2 && b.Ua <= 4) { b.Ua = 1; b.ExtLow = true; b.LinkLow = false; }
            if (!b.HighAtt && b.Ub <= b.L - 3 && b.Ub >= b.L - 5) { b.Ub = b.L - 2; b.ExtHigh = true; b.LinkHigh = false; }
        }

        // ------------------------------------------------------------------ Compound: perimeter wall, gates and guard booths
        bool BuildPerimeter(bool simple)
        {
            // main gate on the south side, aligned with the principal entrance
            int g0 = entX - 2;
            if (g0 < px0 + 6 || g0 + 3 > px1 - 6) return Fail("main gate");
            gateRects.Add(new[] { g0, py0, g0 + 3, py0 });
            AddBoothPair(0, g0);
            // second gate: west (0), east (1) or north (2)
            int g2 = 0;
            if (gate2Side == 2)
            {
                int lo = px0 + 16, hi = px1 - 20;
                if (hi < lo) return Fail("gate2");
                g2 = lo + rng.Next(hi - lo + 1);
                gateRects.Add(new[] { g2, py1, g2 + 3, py1 });
                AddBoothPair(3, g2);
            }
            else
            {
                int lo = py0 + 16, hi = py1 - 20;
                if (hi < lo) return Fail("gate2");
                g2 = lo + rng.Next(hi - lo + 1);
                int gx = gate2Side == 0 ? px0 : px1;
                gateRects.Add(new[] { gx, g2, gx, g2 + 3 });
                AddBoothPair(gate2Side == 0 ? 1 : 2, g2);
            }

            startX = entX; startY = py0 - 4;
            return true;
        }

        // side: 0 south wall, 3 north wall (gap along x), 1 west wall, 2 east wall (gap along y)
        void AddBoothPair(int side, int g0)
        {
            for (int k = 0; k < 2; k++)
            {
                int across = 5 + rng.Next(2), along = 6 + rng.Next(3);
                int fx0, fy0, fx1, fy1, dx, dy;
                if (side == 0 || side == 3)
                {
                    if (side == 0) { fy0 = py0 + 2; fy1 = fy0 + along + 1; }
                    else { fy1 = py1 - 2; fy0 = fy1 - along - 1; }
                    if (k == 0) { fx1 = g0 - 1; fx0 = fx1 - across - 1; dx = fx1; }
                    else { fx0 = g0 + 4; fx1 = fx0 + across + 1; dx = fx0; }
                    dy = fy0 + 1 + along / 2;
                }
                else
                {
                    if (side == 1) { fx0 = px0 + 2; fx1 = fx0 + along + 1; }
                    else { fx1 = px1 - 2; fx0 = fx1 - along - 1; }
                    if (k == 0) { fy1 = g0 - 1; fy0 = fy1 - across - 1; dy = fy1; }
                    else { fy0 = g0 + 4; fy1 = fy0 + across + 1; dy = fy0; }
                    dx = fx0 + 1 + along / 2;
                }

                int bidv = blds.Count;
                PBldg dummy = new PBldg();
                dummy.Arch = "Security"; dummy.Bid = bidv; dummy.Isolated = true;
                dummy.W = fx1 - fx0 + 1; dummy.H = fy1 - fy0 + 1;
                blds.Add(dummy);
                archs.Add("Security");
                booths.Add(new[] { fx0, fy0, fx1, fy1, dx, dy, bidv, (int)RoomFunction.SecurityPost });
                extDoors.Add(dy * W + dx);
                extBid.Add(bidv);
            }
        }

        // ------------------------------------------------------------------ tiny sites (under 100 cells)
        bool PlanTiny(bool simple)
        {
            ax0 = 3; ax1 = W - 1 - 3; ay0 = 10; ay1 = H - 1 - 3;
            int aw = ax1 - ax0 + 1, ah = ay1 - ay0 + 1;
            bool xs = Math.Min(w0, h0) <= 70;
            scatterBound = 2;
            PBldg pb = new PBldg();
            pb.Arch = principal;
            if (xs)
            {
                int tw = Math.Min(aw, 37 + rng.Next(4));
                int lb = 9 + rng.Next(2), hb = 9 + rng.Next(2);
                PBar bar = new PBar(true, 0, 0, tw, lb, hb, 3, 0, principal);
                pb.Bars.Add(bar); pb.Front = bar;
            }
            else
            {
                int tw = Math.Min(aw, 46 + rng.Next(5));
                PBar f = new PBar(true, 0, 0, tw, 8, 8, 4, 0, principal);
                pb.Bars.Add(f); pb.Front = f;
                int sl = 3 + rng.Next(5), sr = 3 + rng.Next(5);
                int style2 = rng.Next(100);
                if (style2 < 25) sl = 0; else if (style2 < 50) sr = 0;
                int len = tw - sl - sr;
                PBar t = new PBar(true, sl, f.Y1, len, 7, 7, 3, 0, principal);
                pb.Bars.Add(t);
                int px = t.X0 + 9 + rng.Next(len - 9 - 10 - 3 + 1);
                f.HighBlocks.Add(px - f.X0); f.HighBlocks.Add(px - f.X0 + 2);
                t.LowBlocks.Add(px - t.X0); t.LowBlocks.Add(px - t.X0 + 2);
                pb.Joins.Add(new[] { px, t.Y0, px + 2, t.Y0 });
            }

            pb.Measure();
            if (pb.W > aw || pb.H > ah) return Fail("tiny oversize");
            pb.Bid = 0;
            foreach (PBar bar in pb.Bars) bar.Bid = 0;
            int ox = ax0 + rng.Next(Math.Max(1, aw - pb.W + 1));
            pb.Shift(ox, ay0);
            foreach (int[] j in pb.Joins) linkRects.Add(j);
            blds.Add(pb);
            entryBar = pb.Front;
            SetEntrance(simple);
            int minx = int.MaxValue, maxx = -1, maxy = -1;
            foreach (PBar bar in pb.Bars) { minx = Math.Min(minx, bar.X0); maxx = Math.Max(maxx, bar.XMax); maxy = Math.Max(maxy, bar.Y1); }
            int[] mainRect = { minx, ay0, maxx, maxy };
            int[] startBlock = { entX - 6, 0, entX + 5, entY };

            if (xs)
            {
                // an optional hut with its own door
                if (!simple && rng.Next(100) < 65)
                {
                    int fw = 8 + rng.Next(5), fh = 6 + rng.Next(3);
                    for (int t = 0; t < 120; t++)
                    {
                        int fx0 = ax0 + rng.Next(Math.Max(1, aw - fw - 1));
                        int fy0 = ay0 + rng.Next(Math.Max(1, ah - fh - 1));
                        int[] cand = { fx0, fy0, fx0 + fw + 1, fy0 + fh + 1 };
                        if (RectsClash(cand, mainRect, 5) || RectsClash(cand, startBlock, 1)) continue;
                        int bidv = blds.Count;
                        PBldg hut = new PBldg();
                        hut.Arch = "Outbuilding"; hut.Bid = bidv; hut.Isolated = true;
                        hut.W = fw + 2; hut.H = fh + 2;
                        blds.Add(hut);
                        int dx = fx0 + 1 + fw / 2;
                        booths.Add(new[] { cand[0], cand[1], cand[2], cand[3], dx, cand[1], bidv, (int)RoomFunction.Storeroom });
                        extDoors.Add(cand[1] * W + dx);
                        extBid.Add(bidv);
                        break;
                    }
                }
            }
            else if (!simple && rng.Next(100) < 85)
            {
                int w = 18 + rng.Next(11), h = 12 + rng.Next(7);
                if (rng.Next(2) == 0) { int t0 = w; w = h; h = t0; }
                PBldg b = new PBldg();
                b.Arch = PickArch();
                BuildHut(b, w, h);
                b.Measure();
                for (int t = 0; t < 200; t++)
                {
                    if (aw < b.W || ah < b.H) break;
                    int px = ax0 + rng.Next(aw - b.W + 1), py = ay0 + rng.Next(ah - b.H + 1);
                    int[] cand = { px, py, px + b.W - 1, py + b.H - 1 };
                    if (RectsClash(cand, mainRect, 6) || RectsClash(cand, startBlock, 1)) continue;
                    b.Bid = blds.Count;
                    foreach (PBar bar in b.Bars) bar.Bid = b.Bid;
                    b.Shift(px, py);
                    foreach (int[] j in b.Joins) linkRects.Add(j);
                    blds.Add(b);
                    break;
                }
            }

            return Finish(simple);
        }

        // ------------------------------------------------------------------ Scattered
        void BuildHut(PBldg b, int w, int h)
        {
            int len = Math.Max(14, Math.Max(w, h)), dep = Clamp(Math.Min(w, h), 10, 20);
            int lb, hb;
            if (dep <= 12) { lb = dep - 5; hb = 0; }
            else { lb = (dep - 6) / 2; hb = dep - 6 - lb; }
            PBar bar = new PBar(true, 0, 0, len, lb, hb, 2, 0, b.Arch);
            b.Bars.Add(bar); b.Front = bar;
            b.Measure();
            if (h > w) b.Transpose();
        }

        string PickArch()
        {
            if (profile != null && profile.Weights != null && profile.Weights.Count > 0) return PickWeightedArch();
            int k = rng.Next(18);
            if (k < 4) return "Office";
            if (k < 8) return "Lab";
            if (k < 12) return "Warehouse";
            if (k < 15) return "Plant";
            return "Commons";
        }

        static bool RectsClash(int[] a, int[] b, int gap)
        {
            return a[0] - gap <= b[2] && a[2] + gap >= b[0] && a[1] - gap <= b[3] && a[3] + gap >= b[1];
        }

        bool PlaceScattered(bool simple, int attempt)
        {
            int aw = ax1 - ax0 + 1, ah = ay1 - ay0 + 1;
            int mnSite = Math.Min(w0, h0);
            int nbMin, nbMax;
            if (mnSite <= 130) { nbMin = 1; nbMax = 2; }
            else if (mnSite <= 170) { nbMin = 2; nbMax = 4; }
            else { nbMin = 5; nbMax = (W * H < 60000) ? 8 : 9; }
            if (profile != null)
            {
                if (profile.MinBuildings > 0) nbMin = Math.Min(profile.MinBuildings, nbMax);
                if (profile.MaxBuildings > 0) nbMax = profile.MaxBuildings;
                if (nbMax < nbMin) nbMax = nbMin;
                if (mnSite <= 130) nbMin = 1;
            }

            int nb = simple ? Math.Min(nbMin, 2) : nbMin + rng.Next(nbMax - nbMin + 1);

            // principal building, set back so that an open parade yard lies in front of it
            int yP = ay0 + 20 + (simple ? 0 : rng.Next(9));
            int bhMax = ay1 - yP + 1;
            if (bhMax < 46 || aw < 48) return Fail("scatter space");
            int bwP = Math.Min(aw, 50 + rng.Next(36));
            int bhP = Math.Min(bhMax, 46 + rng.Next(18));
            if (mnSite > 170)
            {
                bwP = Math.Min(95, Clamp(70 + rng.Next(26), 50, aw));
                bhP = Math.Min(bhMax, 55 + rng.Next(21));
            }

            if (mnSite <= 170)
            {
                bwP = Math.Min(95, Clamp(aw * (70 + rng.Next(31)) / 100, 50, aw));
                bhP = Math.Min(95, Clamp(bhMax * (75 + rng.Next(26)) / 100, 46, bhMax));
            }
            PBldg pb = new PBldg();
            pb.Arch = principal;
            MakeBuilding(pb, bwP, bhP, true, true, simple);
            if (pb.Bars.Count == 0) return Fail("form");
            pb.Measure();
            if (pb.W > aw || pb.H > bhMax) return Fail("oversize");
            pb.Bid = 0;
            foreach (PBar bar in pb.Bars) bar.Bid = 0;
            pb.Shift(ax0 + rng.Next(Math.Max(1, aw - pb.W + 1)), yP);
            foreach (int[] j in pb.Joins) linkRects.Add(j);
            blds.Add(pb);
            entryBar = pb.Hub != null ? pb.Hub : pb.Front;
            SetEntrance(simple);
            int shift = 0;
            if (entX - 14 < ax0) shift = ax0 - (entX - 14);
            else if (entX + 13 > ax1) shift = ax1 - (entX + 13);
            if (shift != 0)
            {
                pb.Shift(shift, 0);
                entX += shift;
            }

            paradeX0 = entX - 14; paradeX1 = entX + 13; paradeY1 = yP - 1;
            List<int[]> placed = new List<int[]>();
            int minx = int.MaxValue, maxx = -1, maxy = -1;
            foreach (PBar bar in pb.Bars) { minx = Math.Min(minx, bar.X0); maxx = Math.Max(maxx, bar.XMax); maxy = Math.Max(maxy, bar.Y1); }
            placed.Add(new[] { minx, yP, maxx, maxy });
            int[] parade = { paradeX0, ay0, paradeX1, paradeY1 };

            int remaining = nb - 1;
            int largeN = nb >= 6 ? 2 : (nb >= 4 ? 1 : 0);
            int smallN = (remaining - largeN + 1) / 3;
            int mediumN = remaining - largeN - smallN;
            if (mediumN < 0) { smallN += mediumN; mediumN = 0; }
            int[] cats = new int[remaining];
            int ci = 0;
            for (int i = 0; i < largeN && ci < remaining; i++) cats[ci++] = 2;
            for (int i = 0; i < mediumN && ci < remaining; i++) cats[ci++] = 1;
            while (ci < remaining) cats[ci++] = 0;
            foreach (int cat in cats)
            {
                int w, h;
                if (mnSite > 170)
                {
                    if (cat == 2) { w = 80 + rng.Next(16); h = 52 + rng.Next(19); }
                    else if (cat == 1) { w = 45 + rng.Next(26); h = 30 + rng.Next(21); }
                    else { w = 18 + rng.Next(13); h = 12 + rng.Next(9); }
                }
                else if (cat == 2) { w = 70 + rng.Next(26); h = 40 + rng.Next(21); }
                else if (cat == 1) { w = 35 + rng.Next(26); h = 25 + rng.Next(16); }
                else { w = 14 + rng.Next(17); h = 10 + rng.Next(11); }
                if (cat > 0 && rng.Next(100) < 40) { int t = w; w = h; h = t; }
                if (cat == 0 && rng.Next(100) < 40) { int t = w; w = h; h = t; }
                w = Math.Min(w, aw); h = Math.Min(h, ah);
                PBldg b = new PBldg();
                b.Arch = simple ? "Lab" : PickArch();
                if (b.Arch == "Security" && cat > 0) { w = Math.Min(w, 45); h = Math.Min(h, 60); }
                if (cat == 0) BuildHut(b, w, h);
                else MakeBuilding(b, w, h, rng.Next(100) < 60, false, simple);
                if (b.Bars.Count == 0) continue;
                b.Measure();
                if (b.W > aw || b.H > ah) continue;
                bool ok = false;
                int px = 0, py = 0;
                for (int t = 0; t < 260 && !ok; t++)
                {
                    px = ax0 + rng.Next(aw - b.W + 1);
                    py = ay0 + rng.Next(ah - b.H + 1);
                    int gap = 5 + (t < 120 ? rng.Next(10) : 0);
                    int[] cand = { px, py, px + b.W - 1, py + b.H - 1 };
                    bool clash = RectsClash(cand, parade, 2);
                    for (int k = 0; k < placed.Count && !clash; k++) if (RectsClash(cand, placed[k], gap)) clash = true;
                    if (!clash) ok = true;
                }

                if (!ok) continue;
                b.Bid = blds.Count;
                foreach (PBar bar in b.Bars) bar.Bid = b.Bid;
                b.Shift(px, py);
                foreach (int[] j in b.Joins) linkRects.Add(j);
                placed.Add(new[] { px, py, px + b.W - 1, py + b.H - 1 });
                blds.Add(b);
            }

            if (blds.Count < nbMin) return Fail("scatter count");
            return true;
        }

        // At most two corridors, each joining a building to the principal building's interior; everything else stands alone.
        bool LinkScatter(bool simple)
        {
            int n = blds.Count;
            List<List<PPort>> ports = new List<List<PPort>>();
            for (int i = 0; i < n; i++) ports.Add(PortsOf(blds[i]));
            bool[] inMain = new bool[n];
            inMain[0] = true;
            int want = simple ? 0 : rng.Next(scatterBound);
            if (n < 2) want = 0;
            for (int k = 0; k < want; k++)
            {
                int bestCost = int.MaxValue; PPort bp = null, bq = null; List<int[]> bw = null; int bj = -1;
                for (int i = 0; i < n; i++)
                {
                    if (!inMain[i]) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (inMain[j]) continue;
                        foreach (PPort p in ports[i])
                        {
                            if (p.Used) continue;
                            foreach (PPort q in ports[j])
                            {
                                if (q.Used) continue;
                                List<int[]> w = Route(p, q);
                                if (w == null) continue;
                                int cost = 0;
                                foreach (int[] r in w) cost += (r[2] - r[0] + 1) * (r[3] - r[1] + 1);
                                cost += rng.Next(8);
                                if (cost < bestCost) { bestCost = cost; bp = p; bq = q; bw = w; bj = j; }
                            }
                        }
                    }
                }

                if (bp == null) break;
                Commit(bp, bq, bw);
                inMain[bj] = true;
            }

            for (int i = 0; i < n; i++) blds[i].Isolated = !inMain[i];
            return true;
        }

        static readonly string[] KnownArchs = { "Office", "Lab", "Warehouse", "Plant", "Commons", "Security", "Medical", "Barracks", "Residential", "Research" };

        string PickWeightedArch()
        {
            List<string> keys = new List<string>();
            foreach (string k in profile.Weights.Keys) if (Array.IndexOf(KnownArchs, k) >= 0 && profile.Weights[k] > 0) keys.Add(k);
            keys.Sort(StringComparer.Ordinal);
            if (keys.Count == 0) return "Lab";
            int total = 0;
            foreach (string k in keys) total += Math.Max(1, (int)(profile.Weights[k] * 10));
            int r = rng.Next(total);
            foreach (string k in keys)
            {
                r -= Math.Max(1, (int)(profile.Weights[k] * 10));
                if (r < 0) return k;
            }

            return keys[0];
        }

        void AssignChamfers(PBldg b, bool simple)
        {
            if (simple) return;
            int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
            foreach (PBar bar in b.Bars)
            {
                minx = Math.Min(minx, bar.X0); miny = Math.Min(miny, bar.Y0);
                maxx = Math.Max(maxx, bar.XMax); maxy = Math.Max(maxy, bar.Y1);
            }

            foreach (PBar bar in b.Bars)
            {
                if (bar.IsHub) continue;
                for (int e = 0; e < 2; e++)
                {
                    bool high = e == 1;
                    if (high ? (bar.HighAtt || bar.LinkHigh) : (bar.LowAtt || bar.LinkLow)) continue;
                    bool endOut;
                    if (bar.Horiz) endOut = high ? bar.XMax == maxx : bar.X0 == minx;
                    else endOut = high ? bar.Y1 == maxy : bar.Y0 == miny;
                    if (!endOut) continue;
                    for (int sd = 0; sd < 2; sd++)
                    {
                        bool sHigh = sd == 1;
                        bool sideOut;
                        if (bar.Horiz) sideOut = sHigh ? bar.Y1 == maxy : bar.Y0 == miny;
                        else sideOut = sHigh ? bar.XMax == maxx : bar.X0 == minx;
                        if (!sideOut || (sHigh ? bar.Hb : bar.Lb) < 5) continue;
                        if (rng.Next(100) < 38) bar.Cham[e * 2 + sd] = 3 + rng.Next(5);
                    }
                }
            }
        }

        void BuildZig(PBldg b, int tw, int th, bool simple)
        {
            int levels = 2 + rng.Next(3);
            int x = 0, len = tw, y = 0;
            PBar prev = null;
            for (int k = 0; k < levels; k++)
            {
                int c, lb, hb;
                ParamsFor(b.Arch, k == 0, rng, out c, out lb, out hb);
                PBar bar = new PBar(true, x, y, len, lb, hb, c, 0, b.Arch);
                if (k > 0 && y + bar.Dp > th) break;
                b.Bars.Add(bar);
                if (k == 0) b.Front = bar;
                if (prev != null)
                {
                    int lc = Math.Min(3, Math.Min(prev.Cw, bar.Cw));
                    int passages = len >= 50 && rng.Next(2) == 0 ? 2 : 1;
                    int cx0 = x + 9, cx1 = x + len - 10 - lc;
                    List<int> placed = new List<int>();
                    for (int pn = 0; pn < passages && cx1 > cx0; pn++)
                    {
                        int px = cx0 + rng.Next(cx1 - cx0 + 1);
                        bool ok = true;
                        foreach (int q in placed) if (Math.Abs(px - q) < 14) ok = false;
                        if (!ok) continue;
                        placed.Add(px);
                        prev.HighBlocks.Add(px - prev.X0); prev.HighBlocks.Add(px - prev.X0 + lc - 1);
                        bar.LowBlocks.Add(px - bar.X0); bar.LowBlocks.Add(px - bar.X0 + lc - 1);
                        b.Joins.Add(new[] { px, bar.Y0, px + lc - 1, bar.Y0 });
                    }
                }

                prev = bar;
                y = bar.Y1;
                int sl = 5 + rng.Next(8), sr = 5 + rng.Next(8);
                int style = rng.Next(100);
                if (style < 25) sl = 0; else if (style < 50) sr = 0;
                if (len - sl - sr < 34) break;
                x += sl; len -= sl + sr;
            }

            b.Measure();
        }

        // ------------------------------------------------------------------ building forms (local coordinates)
        void AddWing(PBar spine, bool spineHigh, PBar wing, bool wingLowTouches)
        {
            int u0, u1;
            if (spine.Horiz) { u0 = wing.X0 + wing.Cv0 - spine.X0; u1 = wing.X0 + wing.Cv1 - spine.X0; }
            else { u0 = wing.Y0 + wing.Cv0 - spine.Y0; u1 = wing.Y0 + wing.Cv1 - spine.Y0; }
            List<int> list = spineHigh ? spine.HighBlocks : spine.LowBlocks;
            list.Add(u0); list.Add(u1);
            if (wingLowTouches) wing.LowAtt = true; else wing.HighAtt = true;
        }

        void MakeBuilding(PBldg b, int bw, int bh, bool dense, bool office, bool simple)
        {
            int r = rng.Next(100);
            if (b.Arch == "Security")
            {
                if (r < 40 && bw >= 30 && !simple) BuildZig(b, Math.Max(30, bw), Math.Max(bh, 40), simple);
                else BuildLadder(b, Math.Max(25, bw), Math.Max(bh, 30), 4, false, simple);
                return;
            }

            if (simple) { BuildLadder(b, bw, bh, 0, !office, simple); return; }
            if (office)
            {
                if (!dense && bw >= 64 && bh >= 64 && r < 30) { BuildHub(b, bw, bh, true); return; }
                if (dense)
                {
                    if (r < 25) BuildZig(b, bw, bh, simple);
                    else if (r < 58 && bw >= 52) BuildLadder(b, bw, bh, 1, true, simple);
                    else BuildLadder(b, bw, bh, 0, r < 80, simple);
                }
                else
                {
                    if (r < 25) { BuildZig(b, bw, bh, simple); return; }
                    int[] modes = { 1, 2, 3, 4, 5 };
                    BuildLadder(b, bw, bh, modes[rng.Next(modes.Length)], false, simple);
                }

                return;
            }

            if (dense)
            {
                if (r < 25) BuildZig(b, bw, bh, simple);
                else if (r < 55 && bw >= 52 && bh >= 52) BuildLadder(b, bw, bh, 1, true, simple);
                else BuildLadder(b, bw, bh, 0, r < 80, simple);
            }
            else
            {
                if (r < 28 && bw >= 60 && bh >= 60) BuildHub(b, bw, bh, false);
                else if (r < 55 && bw >= 56 && bh >= 40) BuildH(b, bw, bh);
                else if (r < 70) BuildZig(b, bw, bh, simple);
                else BuildLadder(b, bw, bh, 1 + rng.Next(5), false, simple);
            }
        }

        // modes: 0 fill, 1 ends (U / ring with a top bar), 2 left, 3 right, 4 centre, 5 three ribs, 6 none
        void BuildLadder(PBldg b, int tw, int th, int mode, bool hasT, bool simple)
        {
            int fc, fl, fh;
            ParamsFor(b.Arch, true, rng, out fc, out fl, out fh);
            PBar f = new PBar(true, 0, 0, tw, fl, fh, fc, 0, b.Arch);
            b.Bars.Add(f); b.Front = f;
            const int gmin = 6;
            int ribBase = f.Y1;
            PBar t = null;
            if (hasT)
            {
                int tc, tl, tt;
                ParamsFor(b.Arch, false, rng, out tc, out tl, out tt);
                int tdp = 2 + tc + tl + 1 + tt + 1;
                if (th - tdp - ribBase + 1 >= 24)
                {
                    t = new PBar(true, 0, th - tdp, tw, tl, tt, tc, 0, b.Arch);
                    b.Bars.Add(t);
                }
            }

            int len = t != null ? t.Y0 - ribBase + 1 : th - ribBase;
            if (len < 24 || mode == 6) { b.Measure(); return; }

            int nom = NomDp(b.Arch);
            int n;
            switch (mode)
            {
                case 0: n = Math.Max(1, (tw + gmin + nom / 2) / (nom + gmin)); break;
                case 1: n = 2; break;
                case 5: n = 3; break;
                default: n = 1; break;
            }

            List<int[]> dims = new List<int[]>();
            for (int i = 0; i < n; i++)
            {
                int c, lb, hb;
                ParamsFor(b.Arch, false, rng, out c, out lb, out hb);
                dims.Add(new[] { c, lb, hb, 2 + c + lb + 1 + hb + 1 });
            }

            int[] xs = RibPositions(dims, tw, mode, gmin, simple);
            if (xs == null) { dims.RemoveRange(1, dims.Count - 1); xs = RibPositions(dims, tw, 4, gmin, simple); }
            if (xs == null) { b.Measure(); return; }
            List<PBar> ribList = new List<PBar>();
            for (int i = 0; i < dims.Count; i++)
            {
                int[] d = dims[i];
                int rl = len;
                if (t == null && !simple && rng.Next(100) < 12) rl = Math.Max(24, len - (4 + rng.Next(12)));
                PBar rib = new PBar(false, xs[i], ribBase, rl, d[1], d[2], d[0], 0, b.Arch);
                b.Bars.Add(rib);
                AddWing(f, true, rib, true);
                if (t != null) AddWing(t, false, rib, false);
                ribList.Add(rib);
            }

            if (t != null)
            {
                for (int i = 0; i + 1 < ribList.Count; i++)
                {
                    int cx0 = xs[i] + dims[i][3], cx1 = xs[i + 1] - 1;
                    int cy0 = f.Y1 + 1, cy1 = t.Y0 - 1;
                    if (cx1 - cx0 + 1 < 6 || cy1 - cy0 + 1 < 8) continue;
                    if (simple || rng.Next(100) >= 55) { b.Open++; continue; }
                    int kr = rng.Next(100);
                    RoomFunction kind;
                    if (kr < 30) kind = RoomFunction.Auditorium;
                    else if (kr < 45) kind = RoomFunction.Cafeteria;
                    else if (kr < 60) kind = RoomFunction.ServerRoom;
                    else if (kr < 70) kind = RoomFunction.Vault;
                    else if (kr < 85) kind = RoomFunction.MachineHall;
                    else kind = RoomFunction.Atrium;
                    if (kind == RoomFunction.Vault && (cx1 - cx0 + 1) * (cy1 - cy0 + 1) > 400) kind = RoomFunction.Auditorium;
                    bool useHigh = rng.Next(2) == 0;
                    PBar rib = useHigh ? ribList[i] : ribList[i + 1];
                    int lc = Math.Min(3, rib.Cw);
                    int uu = rib.L / 2 + rng.Next(9) - 4;
                    int u0 = uu - (lc - 1) / 2;
                    List<int> blocks = useHigh ? rib.HighBlocks : rib.LowBlocks;
                    blocks.Add(u0); blocks.Add(u0 + lc - 1);
                    int p0 = rib.Y0 + u0, p1 = rib.Y0 + u0 + lc - 1;
                    int shapeK = rng.Next(100) < 60 ? 2 + rng.Next(2) : 0;
                    if (p0 < cy0 + shapeK + 1 || p1 > cy1 - shapeK - 1) shapeK = 0;
                    b.Courts.Add(new[] { cx0, cy0, cx1, cy1, (int)kind, shapeK, p0, p1 });
                    b.Filled++;
                }
            }

            b.Measure();
        }

        int[] RibPositions(List<int[]> dims, int tw, int mode, int gmin, bool simple)
        {
            int n = dims.Count;
            int total = 0;
            foreach (int[] d in dims) total += d[3];
            int[] xs = new int[n];
            if (mode == 0)
            {
                while (n > 0 && tw - total < (n - 1) * gmin) { total -= dims[n - 1][3]; dims.RemoveAt(n - 1); n--; xs = new int[n]; }
                if (n == 0) return null;
                int free = tw - total;
                int leftover = free - (n - 1) * gmin;
                int m0, m1;
                if (n == 1) { m0 = simple ? leftover / 2 : rng.Next(free + 1); m1 = free - m0; }
                else
                {
                    m0 = (simple || rng.Next(100) < 75) ? 0 : rng.Next(Math.Min(9, leftover + 1));
                    m1 = (simple || rng.Next(100) < 75) ? 0 : rng.Next(Math.Min(9, leftover - m0 + 1));
                    if (m0 + m1 > leftover) { m0 = 0; m1 = 0; }
                }

                int[] gaps = n > 1 ? Split(free - m0 - m1, n - 1, gmin) : new int[0];
                int x = m0;
                for (int i = 0; i < n; i++) { xs[i] = x; x += dims[i][3] + (i < n - 1 ? gaps[i] : 0); }
                return xs;
            }

            switch (mode)
            {
                case 1:
                    xs[0] = 0; xs[1] = tw - dims[1][3];
                    break;
                case 2: xs[0] = 0; break;
                case 3: xs[0] = tw - dims[0][3]; break;
                case 4: xs[0] = (tw - dims[0][3]) / 2; break;
                case 5:
                    xs[0] = 0; xs[2] = tw - dims[2][3]; xs[1] = (tw - dims[1][3]) / 2;
                    break;
            }

            for (int i = 0; i + 1 < n; i++)
                if (xs[i + 1] - (xs[i] + dims[i][3]) < gmin) return null;
            if (xs[n - 1] + dims[n - 1][3] > tw) return null;
            return xs;
        }

        void BuildHub(PBldg b, int tw, int th, bool office)
        {
            // hub: a wide open atrium; spokes are narrow pods wings
            PBar hub = new PBar(true, 0, 0, 16, 0, 0, 14, 0, b.Arch);
            hub.IsHub = true;
            bool hasN = true, hasS = !office && rng.Next(100) < 55, hasE = rng.Next(100) < 75, hasW = rng.Next(100) < 75;
            if (!hasE && !hasW) hasE = true;
            int lv = Clamp((th - 16) / ((hasN && hasS) ? 2 : 1), 18, 40);
            int ll = Clamp((tw - 16) / ((hasE && hasW) ? 2 : 1), 18, 40);
            int ox = hasW ? ll : 0, oy = hasS ? lv : 0;
            hub.X0 = ox; hub.Y0 = oy;
            b.Bars.Add(hub); b.Hub = hub; b.Front = hub;
            for (int d = 0; d < 4; d++)
            {
                if ((d == 0 && !hasN) || (d == 1 && !hasS) || (d == 2 && !hasE) || (d == 3 && !hasW)) continue;
                int cw = 2 + rng.Next(2);
                int lb = cw == 2 ? 4 : 3, hb = lb;
                PBar sp;
                if (d < 2)
                {
                    int dp = 2 + cw + lb + 1 + hb + 1;
                    int sx = ox + (16 - dp) / 2;
                    if (d == 0)
                    {
                        sp = new PBar(false, sx, hub.Y1, lv + 1, lb, hb, cw, 0, b.Arch);
                        AddWing(hub, true, sp, true);
                    }
                    else
                    {
                        sp = new PBar(false, sx, oy - lv, lv + 1, lb, hb, cw, 0, b.Arch);
                        AddWing(hub, false, sp, false);
                    }
                }
                else
                {
                    PBar probe = new PBar(true, 0, 0, 10, lb, hb, cw, 0, b.Arch);
                    int sy = oy + hub.Mid - probe.Mid;
                    if (d == 2)
                    {
                        sp = new PBar(true, hub.XMax, sy, ll + 1, lb, hb, cw, 0, b.Arch);
                        sp.LowAtt = true; hub.LinkHigh = true;
                    }
                    else
                    {
                        sp = new PBar(true, ox - ll, sy, ll + 1, lb, hb, cw, 0, b.Arch);
                        sp.HighAtt = true; hub.LinkLow = true;
                    }
                }

                b.Bars.Add(sp);
            }

            b.Measure();
        }

        void BuildH(PBldg b, int tw, int th)
        {
            int c1, l1, h1, c2, l2, h2, cm = 2 + rng.Next(2), lm = 3 + rng.Next(2), hm = 3 + rng.Next(2);
            ParamsFor(b.Arch, false, rng, out c1, out l1, out h1);
            ParamsFor(b.Arch, false, rng, out c2, out l2, out h2);
            PBar v1 = new PBar(false, 0, 0, th, l1, h1, c1, 0, b.Arch);
            PBar v2 = new PBar(false, tw - (2 + c2 + l2 + 1 + h2 + 1), 0, th, l2, h2, c2, 0, b.Arch);
            PBar m = new PBar(true, v1.XMax, 0, v2.X0 - v1.XMax + 1, lm, hm, cm, 0, b.Arch);
            if (m.L < 14) { BuildLadder(b, tw, th, 1, false, false); return; }
            m.Y0 = (th - m.Dp) / 2;
            b.Bars.Add(v1); b.Bars.Add(v2); b.Bars.Add(m);
            b.Front = v1;
            AddWing(v1, true, m, true);
            AddWing(v2, false, m, false);
            b.Measure();
        }

        // ------------------------------------------------------------------ links between buildings
        List<PPort> PortsOf(PBldg b)
        {
            List<PPort> ports = new List<PPort>();
            int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
            foreach (PBar bar in b.Bars)
            {
                minx = Math.Min(minx, bar.X0); miny = Math.Min(miny, bar.Y0);
                maxx = Math.Max(maxx, bar.XMax); maxy = Math.Max(maxy, bar.Y1);
            }

            foreach (PBar bar in b.Bars)
            {
                if (bar.IsHub) continue;
                for (int e = 0; e < 2; e++)
                {
                    bool high = e == 1;
                    if (high ? bar.HighAtt : bar.LowAtt) continue;
                    PPort p = new PPort();
                    p.Bar = bar; p.High = high; p.Bld = b.Bid;
                    if (bar.Horiz)
                    {
                        p.Y = bar.Y0 + bar.Mid;
                        p.X = high ? bar.XMax : bar.X0;
                        p.Dir = high ? 0 : 1;
                        if (p.X != (high ? maxx : minx)) continue;
                    }
                    else
                    {
                        p.X = bar.X0 + bar.Mid;
                        p.Y = high ? bar.Y1 : bar.Y0;
                        p.Dir = high ? 2 : 3;
                        if (p.Y != (high ? maxy : miny)) continue;
                    }

                    ports.Add(p);
                }
            }

            foreach (PBar bar in b.Bars)
            {
                if (bar.IsHub) continue;
                for (int sd = 0; sd < 2; sd++)
                {
                    bool high = sd == 1;
                    bool outer;
                    if (bar.Horiz) outer = high ? bar.Y1 == maxy : bar.Y0 == miny;
                    else outer = high ? bar.XMax == maxx : bar.X0 == minx;
                    if (!outer || (high ? bar.Hb : bar.Lb) < 3) continue;
                    int lo = 8, hi = bar.L - 9;
                    for (int t = 0; t < 3 && hi > lo; t++)
                    {
                        int u = lo + (hi - lo) * (t + 1) / 4 + rng.Next(5) - 2;
                        PPort p = new PPort();
                        p.Bar = bar; p.High = high; p.Side = true; p.Bld = b.Bid;
                        if (bar.Horiz) { p.X = bar.X0 + u; p.Y = high ? bar.Y1 : bar.Y0; p.Dir = high ? 2 : 3; }
                        else { p.Y = bar.Y0 + u; p.X = high ? bar.XMax : bar.X0; p.Dir = high ? 0 : 1; }
                        ports.Add(p);
                    }
                }
            }

            return ports;
        }

        bool SideClear(PPort p, int lc)
        {
            PBar bar = p.Bar;
            int mid = bar.Horiz ? p.X - bar.X0 : p.Y - bar.Y0;
            int u0 = mid - (lc - 1) / 2, u1 = u0 + lc - 1;
            if (u0 < 7 || u1 > bar.L - 8) return false;
            List<int> blocks = p.High ? bar.HighBlocks : bar.LowBlocks;
            for (int i = 0; i < blocks.Count; i += 2)
                if (u0 <= blocks[i + 1] + 4 && u1 >= blocks[i] - 4) return false;
            if (!p.High && bar.LobbyU0 >= 0 && u0 <= bar.LobbyU1 + 4 && u1 >= bar.LobbyU0 - 4) return false;
            return true;
        }

        static void MapPt(int dir, int x, int y, out int ox, out int oy)
        {
            switch (dir)
            {
                case 0: ox = x; oy = y; break;
                case 1: ox = -x; oy = y; break;
                case 2: ox = y; oy = x; break;
                default: ox = -y; oy = x; break;
            }
        }

        static void UnmapPt(int dir, int x, int y, out int ox, out int oy)
        {
            switch (dir)
            {
                case 0: ox = x; oy = y; break;
                case 1: ox = -x; oy = y; break;
                case 2: ox = y; oy = x; break;
                default: ox = y; oy = -x; break;
            }
        }

        static void MapVec(int dir, int dirB, out int vx, out int vy)
        {
            int x = dirB == 0 ? 1 : (dirB == 1 ? -1 : 0), y = dirB == 2 ? 1 : (dirB == 3 ? -1 : 0);
            MapPt(dir, x, y, out vx, out vy);
        }

        void Cross(PPort p, int lc, out int x0, out int y0, out int x1, out int y1)
        {
            if (p.Dir <= 1)
            {
                x0 = x1 = p.X;
                y0 = p.Y - (lc - 1) / 2; y1 = y0 + lc - 1;
            }
            else
            {
                y0 = y1 = p.Y;
                x0 = p.X - (lc - 1) / 2; x1 = x0 + lc - 1;
            }
        }

        // Returns rectangles (world coordinates) or null.
        List<int[]> Route(PPort a, PPort b)
        {
            int lc = Math.Min(a.Lc, b.Lc);
            if (lc < 2) return null;
            if (a.Side && !SideClear(a, lc)) return null;
            if (b.Side && !SideClear(b, lc)) return null;
            int ax_0, ay_0, ax_1, ay_1, bx_0, by_0, bx_1, by_1;
            Cross(a, lc, out ax_0, out ay_0, out ax_1, out ay_1);
            Cross(b, lc, out bx_0, out by_0, out bx_1, out by_1);
            int m0x, m0y, m1x, m1y;
            MapPt(a.Dir, ax_0, ay_0, out m0x, out m0y); MapPt(a.Dir, ax_1, ay_1, out m1x, out m1y);
            int xa = m0x, yaLo = Math.Min(m0y, m1y), yaHi = Math.Max(m0y, m1y);
            MapPt(a.Dir, bx_0, by_0, out m0x, out m0y); MapPt(a.Dir, bx_1, by_1, out m1x, out m1y);
            int bxLo = Math.Min(m0x, m1x), bxHi = Math.Max(m0x, m1x), byLo = Math.Min(m0y, m1y), byHi = Math.Max(m0y, m1y);
            int bvx, bvy;
            MapVec(a.Dir, b.Dir, out bvx, out bvy);
            List<int[]> rects = new List<int[]>();
            int total;
            if (bvx == -1)
            {
                int xb = bxLo;
                if (xb - xa < minLinkDx) return null;
                if (yaLo == byLo)
                {
                    rects.Add(new[] { xa, yaLo, xb, yaHi });
                    total = xb - xa;
                }
                else
                {
                    int lo = xa + 3, hi = xb - 3 - (lc - 1);
                    if (hi < lo) return null;
                    int xm = Clamp((xa + xb) / 2 - lc / 2 + rng.Next(5) - 2, lo, hi);
                    rects.Add(new[] { xa, yaLo, xm + lc - 1, yaHi });
                    rects.Add(new[] { xm, Math.Min(yaLo, byLo), xm + lc - 1, Math.Max(yaHi, byHi) });
                    rects.Add(new[] { xm, byLo, xb, byHi });
                    total = (xb - xa) + Math.Abs(byLo - yaLo);
                }
            }
            else if (bvy == -1)
            {
                // B faces south: it lies above the link and the link arrives from below
                if (bxLo < xa + 4 || byLo < yaHi + 4) return null;
                rects.Add(new[] { xa, yaLo, bxHi, yaHi });
                rects.Add(new[] { bxLo, yaLo, bxHi, byLo });
                total = (bxHi - xa) + (byLo - yaLo);
            }
            else if (bvy == 1)
            {
                if (bxLo < xa + 4 || byHi > yaLo - 4) return null;
                rects.Add(new[] { xa, yaLo, bxHi, yaHi });
                rects.Add(new[] { bxLo, byHi, bxHi, yaHi });
                total = (bxHi - xa) + (yaHi - byHi);
            }
            else if (bvx == 1)
            {
                // both ports face the same way: a C-shaped link
                int xb = bxLo;
                int xc = Math.Max(xa, xb) + 4 + rng.Next(3);
                rects.Add(new[] { xa, yaLo, xc + lc - 1, yaHi });
                rects.Add(new[] { xc, Math.Min(yaLo, byLo), xc + lc - 1, Math.Max(yaHi, byHi) });
                rects.Add(new[] { xb, byLo, xc + lc - 1, byHi });
                total = (xc - xa) + (xc - xb) + Math.Abs(byLo - yaLo);
            }
            else return null;
            if (total < minLinkTotal || total > 44) return null;

            List<int[]> world = new List<int[]>();
            foreach (int[] r in rects)
            {
                int wx0, wy0, wx1, wy1;
                UnmapPt(a.Dir, r[0], r[1], out wx0, out wy0);
                UnmapPt(a.Dir, r[2], r[3], out wx1, out wy1);
                world.Add(new[] { Math.Min(wx0, wx1), Math.Min(wy0, wy1), Math.Max(wx0, wx1), Math.Max(wy0, wy1) });
            }

            // collision / bounds check, rings of 1 around every rectangle
            foreach (int[] r in world)
            {
                if (r[0] - 2 < 1 || r[1] - 2 < 1 || r[2] + 2 > W - 2 || r[3] + 2 > H - 2) return null;
                for (int y = r[1] - 1; y <= r[3] + 1; y++)
                    for (int x = r[0] - 1; x <= r[2] + 1; x++)
                    {
                        int o = occ[y * W + x];
                        if (o == 0) continue;
                        if (o == a.Bld + 1 && NearLine(a, x, y)) continue;
                        if (o == b.Bld + 1 && NearLine(b, x, y)) continue;
                        return null;
                    }
            }

            return world;
        }

        static bool NearLine(PPort p, int x, int y)
        {
            return p.Dir <= 1 ? Math.Abs(x - p.X) <= 1 : Math.Abs(y - p.Y) <= 1;
        }

        void Commit(PPort a, PPort b, List<int[]> world)
        {
            a.Used = true; b.Used = true;
            int lc = Math.Min(a.Lc, b.Lc);
            foreach (PPort p in new[] { a, b })
            {
                if (p.Side)
                {
                    int mid = p.Bar.Horiz ? p.X - p.Bar.X0 : p.Y - p.Bar.Y0;
                    int u0 = mid - (lc - 1) / 2;
                    List<int> blocks = p.High ? p.Bar.HighBlocks : p.Bar.LowBlocks;
                    blocks.Add(u0); blocks.Add(u0 + lc - 1);
                }
                else if (p.High) p.Bar.LinkHigh = true; else p.Bar.LinkLow = true;
            }
            foreach (int[] r in world)
            {
                linkRects.Add(r);
                for (int y = r[1] - 1; y <= r[3] + 1; y++)
                    for (int x = r[0] - 1; x <= r[2] + 1; x++)
                        if (occ[y * W + x] == 0) occ[y * W + x] = 100;
            }
        }

        bool LinkAll()
        {
            int n = blds.Count;
            List<List<PPort>> ports = new List<List<PPort>>();
            for (int i = 0; i < n; i++) { List<PPort> pl = PortsOf(blds[i]); ports.Add(blds[i].Isolated ? new List<PPort>() : pl); }
            int[] comp = new int[n];
            for (int i = 0; i < n; i++) comp[i] = i;
            int comps = n;
            for (int i = 0; i < n; i++) if (blds[i].Isolated) { comp[i] = comp[0]; comps--; }
            while (comps > 1)
            {
                int bestCost = int.MaxValue; PPort bp = null, bq = null; List<int[]> bw = null; int bi = -1, bj = -1;
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        if (comp[i] == comp[j]) continue;
                        foreach (PPort p in ports[i])
                        {
                            if (p.Used) continue;
                            foreach (PPort q in ports[j])
                            {
                                if (q.Used) continue;
                                List<int[]> w = Route(p, q);
                                if (w == null) continue;
                                int cost = 0;
                                foreach (int[] r in w) cost += (r[2] - r[0] + 1) * (r[3] - r[1] + 1);
                                cost += rng.Next(8);
                                if (cost < bestCost) { bestCost = cost; bp = p; bq = q; bw = w; bi = i; bj = j; }
                            }
                        }
                    }

                if (bp == null)
                {
                    string d = "";
                    for (int k = 0; k < n; k++)
                    {
                        PBldg bb = blds[k];
                        d += " [" + bb.Arch + " " + bb.Bars[0].X0 + "," + bb.Bars[0].Y0 + " " + bb.W + "x" + bb.H + " comp" + comp[k] + " ports:";
                        foreach (PPort pp in ports[k]) d += pp.Dir + "@" + pp.X + "," + pp.Y + (pp.Used ? "u" : "") + ";";
                        d += "]";
                    }
                    Why = "links" + d;
                    return false;
                }
                Commit(bp, bq, bw);
                int from = comp[bj], to = comp[bi];
                for (int k = 0; k < n; k++) if (comp[k] == from) comp[k] = to;
                comps--;
            }

            int loops = n >= 3 ? 1 + rng.Next(2) : 0;
            for (int l = 0; l < loops; l++)
            {
                int bestCost = int.MaxValue; PPort bp = null, bq = null; List<int[]> bw = null;
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        foreach (PPort p in ports[i])
                        {
                            if (p.Used) continue;
                            foreach (PPort q in ports[j])
                            {
                                if (q.Used) continue;
                                List<int[]> w = Route(p, q);
                                if (w == null) continue;
                                int cost = 0;
                                foreach (int[] r in w) cost += (r[2] - r[0] + 1) * (r[3] - r[1] + 1);
                                cost += rng.Next(30);
                                if (cost < bestCost) { bestCost = cost; bp = p; bq = q; bw = w; }
                            }
                        }

                if (bp != null) Commit(bp, bq, bw);
            }

            return true;
        }
    }
}
