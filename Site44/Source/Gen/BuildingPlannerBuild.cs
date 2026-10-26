using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    internal struct PIntent
    {
        public RoomFunction F;
        public int Wt, Min, Max;
        public PIntent(RoomFunction f, int wt, int min, int max) { F = f; Wt = wt; Min = min; Max = max; }
    }

    internal sealed class PRoom
    {
        public RoomFunction F;
        public int Bid;
        public int X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1;
        public List<int> Cells = new List<int>();
        public int Len;
        public bool Locked, Dead, IsCap, IsLobby, High, Standalone;
        public int Parent = -1;   // reached only through this room (suite / interlock)
    }

    // One bar in its own (u along the corridor, v across) frame. Horizontal: u=x, v=y. Vertical: u=y, v=x.
    internal sealed class PBar
    {
        public bool Horiz;
        public int X0, Y0, L, Lb, Hb, Cw, Dp, Cv0, Cv1, Ua, Ub, Bid;
        public string Arch;
        public bool ExtLow, ExtHigh, LinkLow, LinkHigh, LowAtt, HighAtt, IsHub;
        public int[] Cham = new int[4];   // corner chamfer sizes: [end*2 + side]
        public List<int> LowBlocks = new List<int>(), HighBlocks = new List<int>();
        public int LobbyU0 = -1, LobbyU1 = -1;
        public bool Atrium;

        public PBar(bool horiz, int x0, int y0, int len, int lb, int hb, int cw, int bid, string arch)
        {
            Horiz = horiz; X0 = x0; Y0 = y0; L = len; Lb = lb; Hb = hb; Cw = cw; Bid = bid; Arch = arch;
            Dp = 2 + cw + (lb > 0 ? lb + 1 : 0) + (hb > 0 ? hb + 1 : 0);
            Cv0 = 1 + (lb > 0 ? lb + 1 : 0);
            Cv1 = Cv0 + cw - 1;
        }

        public int X(int u, int v) { return Horiz ? X0 + u : X0 + v; }
        public int Y(int u, int v) { return Horiz ? Y0 + v : Y0 + u; }
        public int Mid { get { return Cv0 + (Cw - 1) / 2; } }
        public int Y1 { get { return Horiz ? Y0 + Dp - 1 : Y0 + L - 1; } }
        public int XMax { get { return Horiz ? X0 + L - 1 : X0 + Dp - 1; } }
    }

    internal sealed partial class PlanBuild
    {
        static readonly int[] DX = { 1, -1, 0, 0 };
        static readonly int[] DY = { 0, 0, 1, -1 };

        static readonly PIntent[] OfficeT =
        {
            new PIntent(RoomFunction.OpenOffice, 22, 8, 14), new PIntent(RoomFunction.PrivateOffice, 28, 3, 7),
            new PIntent(RoomFunction.Conference, 8, 5, 9), new PIntent(RoomFunction.Conference, 2, 10, 14), new PIntent(RoomFunction.BreakRoom, 4, 5, 8),
            new PIntent(RoomFunction.Restroom, 5, 4, 6), new PIntent(RoomFunction.Closet, 8, 3, 4),
            new PIntent(RoomFunction.Storeroom, 5, 4, 7), new PIntent(RoomFunction.ServerRoom, 3, 3, 6),
            new PIntent(RoomFunction.Mailroom, 3, 4, 7), new PIntent(RoomFunction.Cafeteria, 3, 10, 16),
            new PIntent(RoomFunction.Archive, 3, 5, 8),
        };
        static readonly PIntent[] CommonsT =
        {
            new PIntent(RoomFunction.Cafeteria, 40, 10, 18), new PIntent(RoomFunction.BreakRoom, 15, 5, 9),
            new PIntent(RoomFunction.Restroom, 8, 4, 6), new PIntent(RoomFunction.Closet, 6, 3, 4),
            new PIntent(RoomFunction.Mailroom, 6, 4, 7), new PIntent(RoomFunction.Conference, 8, 8, 14),
            new PIntent(RoomFunction.PrivateOffice, 10, 3, 6),
        };
        static readonly PIntent[] MedicalT =
        {
            new PIntent(RoomFunction.Ward, 42, 5, 7), new PIntent(RoomFunction.OperatingRoom, 6, 6, 8),
            new PIntent(RoomFunction.Pharmacy, 5, 5, 7), new PIntent(RoomFunction.Morgue, 4, 5, 7),
            new PIntent(RoomFunction.Lab, 6, 5, 8), new PIntent(RoomFunction.Restroom, 5, 4, 6),
            new PIntent(RoomFunction.Closet, 6, 3, 4), new PIntent(RoomFunction.BreakRoom, 3, 5, 8),
            new PIntent(RoomFunction.ServerRoom, 2, 4, 6), new PIntent(RoomFunction.PrivateOffice, 10, 3, 6),
            new PIntent(RoomFunction.Storeroom, 4, 5, 7), new PIntent(RoomFunction.Conference, 2, 6, 9),
        };
        static readonly PIntent[] BarracksT =
        {
            new PIntent(RoomFunction.Bunkroom, 46, 8, 10), new PIntent(RoomFunction.Cafeteria, 6, 10, 14),
            new PIntent(RoomFunction.Storeroom, 6, 6, 8), new PIntent(RoomFunction.SecurityPost, 6, 5, 8),
            new PIntent(RoomFunction.Closet, 8, 3, 4), new PIntent(RoomFunction.Restroom, 6, 4, 6),
            new PIntent(RoomFunction.Conference, 4, 6, 9), new PIntent(RoomFunction.PrivateOffice, 8, 3, 6),
            new PIntent(RoomFunction.MachineHall, 3, 14, 20),
        };
        static readonly PIntent[] ResidentialT =
        {
            new PIntent(RoomFunction.Apartment, 62, 4, 6), new PIntent(RoomFunction.Mailroom, 4, 4, 6),
            new PIntent(RoomFunction.Storeroom, 5, 5, 7), new PIntent(RoomFunction.Cafeteria, 3, 10, 14),
            new PIntent(RoomFunction.BreakRoom, 4, 5, 8), new PIntent(RoomFunction.Restroom, 5, 4, 5),
            new PIntent(RoomFunction.Closet, 5, 3, 4), new PIntent(RoomFunction.Atrium, 2, 8, 12),
        };
        static readonly PIntent[] ResearchT =
        {
            new PIntent(RoomFunction.Lab, 44, 5, 8), new PIntent(RoomFunction.ServerRoom, 10, 4, 6),
            new PIntent(RoomFunction.Containment, 7, 4, 6), new PIntent(RoomFunction.Archive, 5, 5, 8),
            new PIntent(RoomFunction.MachineHall, 3, 12, 20), new PIntent(RoomFunction.PrivateOffice, 8, 3, 6),
            new PIntent(RoomFunction.BreakRoom, 4, 5, 7), new PIntent(RoomFunction.Conference, 4, 6, 9),
            new PIntent(RoomFunction.Restroom, 4, 4, 6), new PIntent(RoomFunction.Closet, 5, 3, 4),
            new PIntent(RoomFunction.Storeroom, 3, 5, 7),
        };
        static readonly PIntent[] LabT =
        {
            new PIntent(RoomFunction.Lab, 55, 6, 10), new PIntent(RoomFunction.ServerRoom, 6, 4, 6),
            new PIntent(RoomFunction.Storeroom, 10, 5, 8), new PIntent(RoomFunction.PrivateOffice, 10, 4, 6),
            new PIntent(RoomFunction.Closet, 8, 3, 4), new PIntent(RoomFunction.Restroom, 6, 4, 6),
            new PIntent(RoomFunction.Mechanical, 3, 5, 6),
        };
        static readonly PIntent[] WarehouseT =
        {
            new PIntent(RoomFunction.Storeroom, 55, 8, 14), new PIntent(RoomFunction.Archive, 40, 8, 14),
            new PIntent(RoomFunction.Closet, 3, 4, 4),
        };
        static readonly PIntent[] PlantLowT =
        {
            new PIntent(RoomFunction.Mechanical, 40, 6, 10), new PIntent(RoomFunction.Boiler, 40, 6, 10),
            new PIntent(RoomFunction.Closet, 8, 4, 5), new PIntent(RoomFunction.ServerRoom, 4, 4, 6),
        };
        static readonly PIntent[] PlantHighT = { new PIntent(RoomFunction.MachineHall, 100, 14, 28) };
        static readonly PIntent[] SecurityT =
        {
            new PIntent(RoomFunction.SecurityPost, 30, 5, 8), new PIntent(RoomFunction.PrivateOffice, 20, 4, 6),
            new PIntent(RoomFunction.Closet, 20, 4, 4), new PIntent(RoomFunction.BreakRoom, 10, 5, 7),
            new PIntent(RoomFunction.Restroom, 10, 5, 5),
        };

        readonly int W, H;
        readonly LayoutRng rng;
        readonly int[] lab;
        readonly bool[] foot, atr, fire;
        readonly int[] bid;
        readonly PlanTile[] tile;
        readonly List<PRoom> rooms = new List<PRoom>();
        readonly List<PBar> bars = new List<PBar>();
        readonly List<string> archs = new List<string>();
        readonly List<int> extDoors = new List<int>();
        readonly List<int> extBid = new List<int>();
        bool[] trim;
        bool[] notchTrim;
        int courtFilled, courtOpen;
        int entX, entY;
        int w0, h0;
        PlanProfile profile;
        string principal = "Office";
        string style = "campus";
        bool tiny;   // sites under 100 cells: one principal building, optionally one small building or hut
        int startX = -1, startY = -1;

        PlanBuild(int W, int H, LayoutRng rng, int w0, int h0)
        {
            this.W = W; this.H = H; this.rng = rng; this.w0 = w0; this.h0 = h0;
            lab = new int[W * H]; foot = new bool[W * H]; atr = new bool[W * H]; fire = new bool[W * H];
            bid = new int[W * H]; tile = new PlanTile[W * H];
            for (int i = 0; i < lab.Length; i++) { lab[i] = -1; bid[i] = -1; }
            notchTrim = new bool[W * H];
        }

        // ------------------------------------------------------------------ entry
        public static BuildingPlan Build(int w, int h, int seed, int attempt, bool simple, out string err, PlanProfile profile = null)
        {
            err = null;
            LayoutRng rng = new LayoutRng(seed);
            bool transpose = !simple && rng.Next(2) == 1;
            bool fx = rng.Next(2) == 1, fy = rng.Next(2) == 1;
            int cw = transpose ? h : w, ch = transpose ? w : h;
            PlanBuild b = new PlanBuild(cw, ch, rng, w, h);
            b.profile = profile;
            if (profile != null && !string.IsNullOrEmpty(profile.PrincipalArchetype)) b.principal = profile.PrincipalArchetype;
            if (profile != null && profile.Style != null)
            {
                string st = profile.Style.Trim().ToLowerInvariant();
                if (st == "scattered" || (st == "compound" && Math.Min(w, h) >= 100)) b.style = st;
            }

            b.tiny = Math.Min(w, h) < 100;
            if (!b.Plan(simple, attempt)) { err = "plan " + b.Why; return null; }

            b.PaintAll();
            b.DeriveTiles();
            if (!b.PlaceEntranceAndExterior()) { err = "entrance"; return null; }
            if (!b.CheckIsolatedDoors()) { err = "isolated doors"; return null; }
            if (!b.PlaceRoomDoors()) { err = "room doors"; return null; }
            b.EnsureFunctions();
            b.FireCuts();
            b.ExtraDoors();
            b.Windows();
            BuildingPlan plan = b.Assemble();
            if (plan == null) { err = "assemble"; return null; }
            plan.ProfileName = profile == null ? "OfficeCampus" : profile.Name;
            plan.Style = b.style == "scattered" ? "Scattered" : (b.style == "compound" ? "Compound" : "Campus");
            if (transpose || fx || fy) plan = BuildingPlanner.Transform(plan, transpose, fx, fy);
            return plan;
        }

        // ------------------------------------------------------------------ helpers
        bool InB(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
        PlanTile T(int x, int y) { return InB(x, y) ? tile[y * W + x] : PlanTile.Outside; }
        bool Flr(int x, int y) { return InB(x, y) && tile[y * W + x] == PlanTile.Floor; }
        int Lab(int x, int y) { return InB(x, y) && tile[y * W + x] == PlanTile.Floor ? lab[y * W + x] : -1; }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        static void ParamsFor(string arch, bool front, LayoutRng rng, out int cw, out int lb, out int hb)
        {
            switch (arch)
            {
                case "Office":
                    if (front) { cw = 4; lb = 7; hb = 6; }
                    else { cw = rng.Next(4) == 0 ? 2 : 3; lb = 5 + rng.Next(5); hb = 5 + rng.Next(5); }
                    break;
                case "Commons": cw = front ? 4 : 3; lb = 6 + rng.Next(3); hb = 6 + rng.Next(3); break;
                case "Lab": cw = 3; lb = 6 + (front ? 1 : rng.Next(2)); hb = 6 + (front ? 1 : rng.Next(2)); break;
                case "Warehouse": cw = 5; lb = 8; hb = 8; break;
                case "Plant": cw = 2; lb = 5; hb = 10; break;
                case "Medical": cw = front ? 4 : (rng.Next(4) == 0 ? 2 : 3); lb = 6 + rng.Next(2); hb = 6 + rng.Next(2); break;
                case "Barracks": cw = front ? 4 : 3; lb = 6 + rng.Next(2); hb = 6 + rng.Next(2); break;
                case "Residential": cw = front ? 4 : 3; lb = 6; hb = 6 + rng.Next(2); break;
                case "Research": cw = front ? 4 : 3; lb = 7; hb = 7 + rng.Next(2); break;
                default: cw = 3; lb = 4; hb = 4; break;
            }
        }

        static int NomDp(string arch)
        {
            switch (arch)
            {
                case "Office": return 21;
                case "Commons": return 21;
                case "Lab": return 21;
                case "Warehouse": return 25;
                case "Plant": return 21;
                case "Medical": case "Barracks": case "Research": return 21;
                case "Residential": return 19;
                default: return 15;
            }
        }

        int[] Split(int total, int parts, int min)
        {
            int[] v = new int[parts];
            int extra = total - parts * min;
            for (int i = 0; i < parts; i++) v[i] = min + extra / parts;
            int rem = extra - (extra / parts) * parts;
            for (int i = 0; i < rem; i++) v[rng.Next(parts)]++;
            for (int k = 0; k < parts * 2 && parts > 1; k++)
            {
                int a = rng.Next(parts), b = rng.Next(parts);
                int mv = rng.Next(3);
                if (a != b && v[a] - mv >= min) { v[a] -= mv; v[b] += mv; }
            }

            return v;
        }

        // ------------------------------------------------------------------ painting
        bool ChamRemoved(PBar b, bool high, int u, int d)
        {
            int depth = high ? b.Hb : b.Lb;
            int cap = depth - 2;
            for (int e = 0; e < 2; e++)
            {
                int c = b.Cham[e * 2 + (high ? 1 : 0)];
                if (c <= 0) continue;
                int uEnd = e == 0 ? u : b.L - 1 - u;
                int rem = c - (uEnd - 1);
                if (rem > cap) rem = cap;
                if (rem >= 1 && d <= rem) return true;
            }

            return false;
        }

        void MarkTrim(PBar b)
        {
            for (int e = 0; e < 2; e++)
                for (int sd = 0; sd < 2; sd++)
                {
                    bool high = sd == 1;
                    int c = b.Cham[e * 2 + sd];
                    if (c <= 0) continue;
                    int cap = (high ? b.Hb : b.Lb) - 2;
                    for (int k = 0; k <= c + 1; k++)
                    {
                        int rem = k == 0 ? c + 1 : c - (k - 1);
                        if (rem > cap + 1) rem = cap + 1;
                        for (int d = 0; d <= rem; d++)
                        {
                            int u = e == 0 ? k : b.L - 1 - k;
                            int v = high ? b.Dp - 1 - d : d;
                            trim[b.Y(u, v) * W + b.X(u, v)] = true;
                        }
                    }
                }
        }

        void RingCell(int x, int y)
        {
            foreach (int[] g in gateRects) if (x >= g[0] && x <= g[2] && y >= g[1] && y <= g[3]) return;
            foot[y * W + x] = true;
        }

        PRoom NewRoomRect(int x0, int y0, int x1, int y1, RoomFunction f, int bidv, int cut = 0)
        {
            PRoom r = new PRoom();
            r.F = f; r.Bid = bidv; r.Locked = true; r.Len = Math.Min(x1 - x0, y1 - y0) + 1;
            rooms.Add(r);
            int label = rooms.Count;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * W + x;
                    foot[i] = true;
                    if (cut > 0 && (x - x0 + (y - y0) < cut || (x1 - x) + (y - y0) < cut || x - x0 + (y1 - y) < cut || (x1 - x) + (y1 - y) < cut)) continue;
                    lab[i] = label; bid[i] = bidv;
                    r.Cells.Add(i);
                }

            r.X0 = x0; r.X1 = x1; r.Y0 = y0; r.Y1 = y1;
            return r;
        }

        // keep (optional): shape mask indexed [s * hh + t] with s along u from u0 and t away from the corridor (hh = v1 - v0 + 1).
        PRoom NewRoom(PBar b, bool high, int u0, int u1, int v0, int v1, RoomFunction f, bool band = false, bool[] keep = null, int bandLo = 0, int bandHi = -1)
        {
            PRoom r = new PRoom();
            r.F = f; r.Bid = b.Bid; r.High = high; r.Len = u1 - u0 + 1;
            rooms.Add(r);
            int label = rooms.Count;
            int hh = v1 - v0 + 1;
            for (int u = u0; u <= u1; u++)
            {
                for (int v = v0; v <= v1; v++)
                {
                    if (keep != null && !keep[(u - u0) * hh + (high ? v - v0 : v1 - v)]) continue;
                    if (band && ChamRemoved(b, high, u, high ? b.Dp - 1 - v : v)) continue;
                    int x = b.X(u, v), y = b.Y(u, v);
                    int i = y * W + x;
                    lab[i] = label; bid[i] = b.Bid;
                    r.Cells.Add(i);
                }
            }

            if (band) KeepLargestPiece(r, label);
            if (keep != null && bandHi >= bandLo)
            {
                bool farEdge = high ? v1 == bandHi : v0 == bandLo;
                if (farEdge)
                {
                    int outerV = high ? bandHi + 1 : bandLo - 1;
                    for (int u = u0; u <= u1; u++)
                    {
                        int tmax = -1;
                        for (int t = 0; t < hh; t++) if (keep[(u - u0) * hh + t]) tmax = t;
                        if (tmax >= hh - 1) continue;
                        for (int t = tmax + 1; t < hh; t++)
                        {
                            int v = high ? v0 + t : v1 - t;
                            notchTrim[b.Y(u, v) * W + b.X(u, v)] = true;
                        }

                        notchTrim[b.Y(u, outerV) * W + b.X(u, outerV)] = true;
                    }
                }
            }

            foreach (int c in r.Cells)
            {
                int x = c % W, y = c / W;
                if (x < r.X0) r.X0 = x; if (x > r.X1) r.X1 = x;
                if (y < r.Y0) r.Y0 = y; if (y > r.Y1) r.Y1 = y;
            }

            return r;
        }

        void Corr(PBar b, int u0, int u1, int v0, int v1)
        {
            for (int u = u0; u <= u1; u++)
                for (int v = v0; v <= v1; v++)
                {
                    int x = b.X(u, v), y = b.Y(u, v);
                    if (!InB(x, y)) continue;
                    lab[y * W + x] = 0; bid[y * W + x] = b.Bid;
                }
        }

        void PaintAll()
        {
            foreach (PBar b in bars)
                for (int u = 0; u < b.L; u++)
                    for (int v = 0; v < b.Dp; v++)
                    {
                        int i = b.Y(u, v) * W + b.X(u, v);
                        foot[i] = true;
                        if (bid[i] < 0) bid[i] = b.Bid;
                    }

            foreach (PBar b in bars)
            {
                Corr(b, b.Ua, b.Ub, b.Cv0, b.Cv1);
                if (b.IsHub)
                    for (int u = b.Ua; u <= b.Ub; u++)
                        for (int v = b.Cv0; v <= b.Cv1; v++) atr[b.Y(u, v) * W + b.X(u, v)] = true;
                if (!b.IsHub)
                {
                    if (b.Ua >= 2) NewRoom(b, false, 1, b.Ua - 2, b.Cv0, b.Cv1, RoomFunction.Stairwell).IsCap = true;
                    if (b.Ub + 2 <= b.L - 2) NewRoom(b, false, b.Ub + 2, b.L - 2, b.Cv0, b.Cv1, RoomFunction.Stairwell).IsCap = true;
                    if (b.Ua == 1 && b.ExtLow) { extDoors.Add(b.Y(0, b.Mid) * W + b.X(0, b.Mid)); extBid.Add(b.Bid); }
                    if (b.Ub == b.L - 2 && b.ExtHigh) { extDoors.Add(b.Y(b.L - 1, b.Mid) * W + b.X(b.L - 1, b.Mid)); extBid.Add(b.Bid); }
                }

                for (int i = 0; i < b.LowBlocks.Count; i += 2) Corr(b, b.LowBlocks[i], b.LowBlocks[i + 1], 1, b.Cv0 - 1);
                for (int i = 0; i < b.HighBlocks.Count; i += 2) Corr(b, b.HighBlocks[i], b.HighBlocks[i + 1], b.Cv1 + 1, b.Dp - 2);
            }

            foreach (int[] r in linkRects)
            {
                for (int y = r[1] - 1; y <= r[3] + 1; y++)
                    for (int x = r[0] - 1; x <= r[2] + 1; x++)
                    {
                        foot[y * W + x] = true;
                        if (x >= r[0] && x <= r[2] && y >= r[1] && y <= r[3]) lab[y * W + x] = 0;
                    }
            }

            foreach (PBar b in bars)
            {
                if (b.IsHub) { PaintBands(b, false); PaintBands(b, true); continue; }
                if (b.LobbyU0 >= 0)
                {
                    if (b.Atrium)
                    {
                        for (int u = b.LobbyU0; u <= b.LobbyU1; u++)
                            for (int v = 1; v <= b.Lb + 1; v++)
                            {
                                int i = b.Y(u, v) * W + b.X(u, v);
                                lab[i] = 0; atr[i] = true; bid[i] = b.Bid;
                            }
                    }
                    else
                    {
                        bool[] lobbyMask = null;
                        if (b.LobbyU1 - b.LobbyU0 + 1 >= 7 && b.Lb >= 6 && rng.Next(100) < 65) lobbyMask = MakeMask(KOct, b.LobbyU1 - b.LobbyU0 + 1, b.Lb);
                        PRoom lobby = NewRoom(b, false, b.LobbyU0, b.LobbyU1, 1, b.Lb, RoomFunction.Lobby, false, lobbyMask, 1, b.Lb);
                        lobby.Locked = true; lobby.IsLobby = true;
                    }
                }

                PaintBands(b, false);
                PaintBands(b, true);
            }

            foreach (int[] bo in booths)
            {
                for (int y = bo[1]; y <= bo[3]; y++)
                    for (int x = bo[0]; x <= bo[2]; x++) { foot[y * W + x] = true; bid[y * W + x] = bo[6]; }
                PRoom br = NewRoomRect(bo[0] + 1, bo[1] + 1, bo[2] - 1, bo[3] - 1, (RoomFunction)bo[7], bo[6]);
                br.Standalone = true;
            }

            if (style == "compound")
            {
                for (int x = px0; x <= px1; x++) { RingCell(x, py0); RingCell(x, py1); }
                for (int y = py0; y <= py1; y++) { RingCell(px0, y); RingCell(px1, y); }
            }

            foreach (PBldg bl in blds)
                foreach (int[] ct in bl.Courts)
                    NewRoomRect(ct[0], ct[1], ct[2], ct[3], (RoomFunction)ct[4], bl.Bid, ct[5]);

            for (int ri = 0; ri < rooms.Count; ri++)
            {
                PRoom r = rooms[ri];
                int label = ri + 1;
                List<int> still = new List<int>();
                foreach (int c in r.Cells) if (lab[c] == label) still.Add(c);
                r.Cells = still;
                if (r.IsCap || r.IsLobby) continue;
                PruneLeaves(r, label);
                KeepLargestPieceInner(r, label);
                PruneLeaves(r, label);
                r.X0 = int.MaxValue; r.Y0 = int.MaxValue; r.X1 = -1; r.Y1 = -1;
                foreach (int c in r.Cells)
                {
                    int x = c % W, y = c / W;
                    if (x < r.X0) r.X0 = x; if (x > r.X1) r.X1 = x;
                    if (y < r.Y0) r.Y0 = y; if (y > r.Y1) r.Y1 = y;
                }
            }

            trim = new bool[W * H];
            foreach (PBar b in bars) if (!b.IsHub) MarkTrim(b);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!trim[i] && !notchTrim[i]) continue;
                    if (!foot[i] || lab[i] >= 0) continue;
                    bool nearFloor = false;
                    for (int dy = -1; dy <= 1 && !nearFloor; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (InB(nx, ny) && lab[ny * W + nx] >= 0) { nearFloor = true; break; }
                        }

                    if (!nearFloor) foot[i] = false;
                }
        }

        void PaintBands(PBar b, bool high)
        {
            int depth = high ? b.Hb : b.Lb;
            if (depth <= 0) return;
            int vlo = high ? b.Cv1 + 2 : 1, vhi = vlo + depth - 1;
            List<int> blocks = high ? b.HighBlocks : b.LowBlocks;
            List<int[]> forb = new List<int[]>();
            for (int i = 0; i < blocks.Count; i += 2) forb.Add(new[] { blocks[i] - 1, blocks[i + 1] + 1 });
            if (!high && b.LobbyU0 >= 0) forb.Add(new[] { b.LobbyU0 - 1, b.LobbyU1 + 1 });
            if (!b.IsHub && b.L > 24)
            {
                // corridor nodes: small plazas at junctions and bulges along the corridor
                List<int[]> nodes = new List<int[]>();
                for (int i = 0; i < blocks.Count; i += 2)
                {
                    if (rng.Next(100) >= 32) continue;
                    int bc = (blocks[i] + blocks[i + 1]) / 2, hw = 2 + rng.Next(2);
                    TryNode(b, forb, nodes, bc - hw, bc + hw, 3, blocks[i] - 1, blocks[i + 1] + 1);
                }

                int nb = b.L / 70 + (rng.Next(100) < 25 ? 1 : 0);
                for (int k = 0; k < nb; k++)
                {
                    int wdt = 6 + rng.Next(5);
                    int a0 = 6 + rng.Next(Math.Max(1, b.L - 12 - wdt));
                    TryNode(b, forb, nodes, a0, a0 + wdt - 1, 2, -1, -1);
                }

                foreach (int[] nd in nodes)
                {
                    if (high) Corr(b, nd[0], nd[1], b.Cv1 + 1, b.Cv1 + nd[2]);
                    else Corr(b, nd[0], nd[1], b.Cv0 - nd[2], b.Cv0 - 1);
                }
            }

            forb.Sort(delegate (int[] a, int[] c) { return a[0].CompareTo(c[0]); });
            int cursor = 1;
            foreach (int[] fb in forb)
            {
                if (fb[0] - 1 >= cursor) SliceSegment(b, high, cursor, fb[0] - 1, vlo, vhi);
                cursor = Math.Max(cursor, fb[1] + 1);
            }

            if (b.L - 2 >= cursor) SliceSegment(b, high, cursor, b.L - 2, vlo, vhi);
        }

        void TryNode(PBar b, List<int[]> forb, List<int[]> nodes, int a, int e, int depth, int ownLo, int ownHi)
        {
            if (a < b.Ua + 4 || e > b.Ub - 4 || a < 4 || e > b.L - 5) return;
            int lo = a - 1, hi = e + 1;
            foreach (int[] fb in forb)
            {
                bool own = fb[0] == ownLo && fb[1] == ownHi;
                if (own) continue;
                if (lo <= fb[1] + 1 && hi >= fb[0] - 1) return;
            }

            forb.Add(new[] { lo, hi });
            nodes.Add(new[] { a, e, depth });
        }

        PIntent Pick(string arch, bool high)
        {
            PIntent[] t;
            switch (arch)
            {
                case "Office": t = OfficeT; break;
                case "Lab": t = LabT; break;
                case "Commons": t = CommonsT; break;
                case "Medical": t = MedicalT; break;
                case "Barracks": t = BarracksT; break;
                case "Residential": t = ResidentialT; break;
                case "Research": t = ResearchT; break;
                case "Warehouse": t = WarehouseT; break;
                case "Plant": t = high ? PlantHighT : PlantLowT; break;
                default: t = SecurityT; break;
            }

            int sum = 0;
            for (int i = 0; i < t.Length; i++) sum += t[i].Wt;
            int r = rng.Next(sum);
            for (int i = 0; i < t.Length; i++)
            {
                r -= t[i].Wt;
                if (r < 0) return t[i];
            }

            return t[0];
        }

        // ------------------------------------------------------------------ tiles and doors
        void DeriveTiles()
        {
            for (int i = 0; i < tile.Length; i++)
                tile[i] = lab[i] >= 0 ? PlanTile.Floor : (foot[i] ? PlanTile.Wall : PlanTile.Outside);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (tile[y * W + x] != PlanTile.Floor) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (InB(nx, ny) && tile[ny * W + nx] == PlanTile.Outside) tile[ny * W + nx] = PlanTile.Wall;
                        }
                }
        }

        bool NoDoorAround(int x, int y)
        {
            for (int d = 0; d < 4; d++) if (T(x + DX[d], y + DY[d]) == PlanTile.Door) return false;
            return true;
        }

        // 0 none; 1 floors west/east; 2 floors north/south
        int InnerDoorKind(int x, int y)
        {
            if (T(x, y) != PlanTile.Wall || !NoDoorAround(x, y)) return 0;
            if (Flr(x - 1, y) && Flr(x + 1, y) && T(x, y - 1) == PlanTile.Wall && T(x, y + 1) == PlanTile.Wall) return 1;
            if (Flr(x, y - 1) && Flr(x, y + 1) && T(x - 1, y) == PlanTile.Wall && T(x + 1, y) == PlanTile.Wall) return 2;
            return 0;
        }

        bool ExtDoorOk(int x, int y)
        {
            if (T(x, y) != PlanTile.Wall || !NoDoorAround(x, y)) return false;
            if (Flr(x, y + 1) && T(x, y - 1) == PlanTile.Outside && T(x - 1, y) == PlanTile.Wall && T(x + 1, y) == PlanTile.Wall) return true;
            if (Flr(x, y - 1) && T(x, y + 1) == PlanTile.Outside && T(x - 1, y) == PlanTile.Wall && T(x + 1, y) == PlanTile.Wall) return true;
            if (Flr(x + 1, y) && T(x - 1, y) == PlanTile.Outside && T(x, y - 1) == PlanTile.Wall && T(x, y + 1) == PlanTile.Wall) return true;
            if (Flr(x - 1, y) && T(x + 1, y) == PlanTile.Outside && T(x, y - 1) == PlanTile.Wall && T(x, y + 1) == PlanTile.Wall) return true;
            return false;
        }

        bool PlaceEntranceAndExterior()
        {
            if (!ExtDoorOk(entX, entY)) return false;
            tile[entY * W + entX] = PlanTile.Door;
            foreach (int i in extDoors)
            {
                int x = i % W, y = i / W;
                if (!ExtDoorOk(x, y)) return false;
                tile[i] = PlanTile.Door;
            }

            return true;
        }

        // Isolated buildings must be reachable outdoors: their exterior doors open onto the Outside component of Start.
        bool CheckIsolatedDoors()
        {
            bool any = false;
            foreach (PBldg bl in blds) if (bl.Isolated) any = true;
            if (!any) return true;
            bool[] os = new bool[W * H];
            List<int> q = new List<int>();
            int si = (startY >= 0 ? startY : entY - 6) * W + (startY >= 0 ? startX : entX);
            os[si] = true; q.Add(si);
            for (int h = 0; h < q.Count; h++)
            {
                int c = q[h]; int cx = c % W, cy = c / W;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (!InB(nx, ny)) continue;
                    int ni = ny * W + nx;
                    if (!os[ni] && tile[ni] == PlanTile.Outside) { os[ni] = true; q.Add(ni); }
                }
            }

            foreach (PBldg bl in blds)
            {
                if (!bl.Isolated) continue;
                int good = 0;
                for (int k = 0; k < extDoors.Count; k++)
                {
                    if (extBid[k] != bl.Bid) continue;
                    int x = extDoors[k] % W, y = extDoors[k] / W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + DX[d], ny = y + DY[d];
                        if (InB(nx, ny) && os[ny * W + nx]) { good++; break; }
                    }
                }

                if (good < (Math.Max(bl.W, bl.H) > 40 ? 2 : 1)) return false;
            }

            return true;
        }

        // Places a door from room ri to a corridor cell. avoid >= 0: keep Manhattan distance >= 6 from that cell.
        bool PlaceDoor(int ri, int avoid)
        {
            PRoom r = rooms[ri];
            int label = ri + 1;
            int mx = (r.X0 + r.X1) / 2, my = (r.Y0 + r.Y1) / 2;
            int best = -1, bestScore = int.MaxValue;
            foreach (int ci in r.Cells)
            {
                int x = ci % W, y = ci / W;
                for (int d = 0; d < 4; d++)
                {
                    int cx = x + DX[d], cy = y + DY[d];
                    if (!InB(cx, cy) || tile[cy * W + cx] != PlanTile.Wall) continue;
                    if (Lab(cx + DX[d], cy + DY[d]) != 0) continue;
                    if (InnerDoorKind(cx, cy) == 0) continue;
                    if (avoid >= 0 && Math.Abs(cx - avoid % W) + Math.Abs(cy - avoid / W) < 6) continue;
                    int score = (Math.Abs(cx - mx) + Math.Abs(cy - my)) * 4 + rng.Next(3);
                    if (score < bestScore) { bestScore = score; best = cy * W + cx; }
                }
            }

            if (best < 0) return false;
            tile[best] = PlanTile.Door;
            doorOf[ri] = best;
            return true;
        }

        int[] doorOf;

        bool PlaceRoomDoors()
        {
            doorOf = new int[rooms.Count];
            for (int i = 0; i < rooms.Count; i++) doorOf[i] = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                PRoom r = rooms[i];
                if (r.Parent >= 0 || r.Standalone) continue;
                if (PlaceDoor(i, -1)) continue;
                if (r.IsCap || r.IsLobby) return false;
                KillRoom(r);
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                PRoom r = rooms[i];
                if (r.Parent < 0 || r.Dead) continue;
                if (rooms[r.Parent].Dead || !PlaceInnerDoor(i, r.Parent)) KillRoom(r);
            }

            return true;
        }

        void KillRoom(PRoom r)
        {
            r.Dead = true;
            foreach (int ci in r.Cells) { tile[ci] = PlanTile.Wall; lab[ci] = -1; }
        }

        void ExtraDoors()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                PRoom r = rooms[i];
                if (r.Dead || r.IsCap || doorOf[i] < 0 || r.F == RoomFunction.Containment) continue;
                if (r.Cells.Count >= 48 || (r.IsLobby && r.Cells.Count >= 40)) PlaceDoor(i, doorOf[i]);
            }
        }

        // ------------------------------------------------------------------ functions
        int Count(int b, RoomFunction f)
        {
            int c = 0;
            foreach (PRoom r in rooms) if (!r.Dead && r.Bid == b && r.F == f) c++;
            return c;
        }

        void Ensure(int b, RoomFunction f, int count, int minW, int maxW)
        {
            int have = Count(b, f);
            while (have < count)
            {
                List<PRoom> pref = new List<PRoom>(), any = new List<PRoom>();
                bool[] isParent = new bool[rooms.Count];
                foreach (PRoom q in rooms) if (q.Parent >= 0) isParent[q.Parent] = true;
                for (int ri = 0; ri < rooms.Count; ri++)
                {
                    PRoom r = rooms[ri];
                    if (r.Dead || r.Bid != b || r.Locked || r.IsCap || r.IsLobby || isParent[ri]) continue;
                    any.Add(r);
                    if (r.Len >= minW && r.Len <= maxW) pref.Add(r);
                }

                List<PRoom> src = pref.Count > 0 ? pref : any;
                if (src.Count == 0) return;
                PRoom pick = src[rng.Next(src.Count)];
                pick.F = f; pick.Locked = true;
                have++;
            }
        }

        void EnsureFunctions()
        {
            PBar f0 = entryBar;
            if (f0.Atrium && f0.LobbyU0 >= 0)
            {
                // a gallery lobby across the corridor from the atrium
                int cu = (f0.LobbyU0 + f0.LobbyU1) / 2;
                PRoom bestRoom = null; int bestD = int.MaxValue;
                foreach (PRoom r in rooms)
                {
                    if (r.Dead || r.Bid != 0 || r.IsCap || !r.High) continue;
                    int rx = (r.X0 + r.X1) / 2 - f0.X0;
                    int d = Math.Abs(rx - cu);
                    if (r.Y0 > f0.Y0 && r.Y1 < f0.Y1 && d < bestD) { bestD = d; bestRoom = r; }
                }

                if (bestRoom != null) { bestRoom.F = RoomFunction.Lobby; bestRoom.Locked = true; }
            }

            if (tiny)
            {
                Ensure(0, RoomFunction.Reception, 1, 5, 9);
                Ensure(0, RoomFunction.Restroom, 1, 4, 6);
                Ensure(0, RoomFunction.Lobby, 1, 6, 12);
                if (principal == "Office")
                {
                    Ensure(0, RoomFunction.PrivateOffice, 1, 4, 6);
                    Ensure(0, RoomFunction.Conference, 1, 6, 10);
                    Ensure(0, RoomFunction.ServerRoom, 1, 4, 6);
                    Ensure(0, RoomFunction.Storeroom, 1, 4, 8);
                    Ensure(0, RoomFunction.OpenOffice, 1, 8, 16);
                }
                else if (principal == "Medical")
                {
                    Ensure(0, RoomFunction.Ward, 3, 5, 7);
                    Ensure(0, RoomFunction.OperatingRoom, 1, 6, 8);
                    Ensure(0, RoomFunction.Pharmacy, 1, 5, 7);
                }
                else if (principal == "Barracks")
                {
                    Ensure(0, RoomFunction.Bunkroom, 3, 6, 10);
                    Ensure(0, RoomFunction.SecurityPost, 1, 5, 8);
                }
                else if (principal == "Residential")
                {
                    Ensure(0, RoomFunction.Apartment, 6, 4, 6);
                    Ensure(0, RoomFunction.Mailroom, 1, 4, 6);
                }
                else if (principal == "Research")
                {
                    Ensure(0, RoomFunction.Lab, 4, 5, 8);
                    Ensure(0, RoomFunction.Containment, 1, 4, 6);
                    Ensure(0, RoomFunction.ServerRoom, 1, 4, 6);
                }
                else if (principal == "Plant")
                {
                    Ensure(0, RoomFunction.MachineHall, 1, 12, 28);
                    Ensure(0, RoomFunction.Mechanical, 1, 5, 10);
                    Ensure(0, RoomFunction.Storeroom, 1, 5, 8);
                }

                return;
            }

            if (principal == "Office")
            {
                Ensure(0, RoomFunction.Reception, 1, 6, 9);
                Ensure(0, RoomFunction.Conference, 1, 6, 10);
                Ensure(0, RoomFunction.ServerRoom, 1, 4, 6);
                Ensure(0, RoomFunction.Restroom, 1, 4, 6);
                Ensure(0, RoomFunction.Storeroom, 2, 4, 8);
                Ensure(0, RoomFunction.Mechanical, 1, 5, 9);
                Ensure(0, RoomFunction.OpenOffice, 1, 8, 16);
                Ensure(0, RoomFunction.PrivateOffice, 1, 4, 6);
                Ensure(0, RoomFunction.Stairwell, 1, 4, 6);
                Ensure(0, RoomFunction.Lobby, 1, 6, 12);
                if (w0 >= 150 && h0 >= 150)
                {
                    bool hall = false;
                    foreach (PRoom r in rooms) if (!r.Dead && r.F == RoomFunction.MachineHall) hall = true;
                    if (!hall) Ensure(0, RoomFunction.MachineHall, 1, 10, 30);
                }
            }
            else
            {
                Ensure(0, RoomFunction.Reception, 1, 6, 9);
                Ensure(0, RoomFunction.Restroom, 1, 4, 6);
                Ensure(0, RoomFunction.Stairwell, 1, 4, 6);
                Ensure(0, RoomFunction.Lobby, 1, 6, 12);
            }

            for (int bi = 0; bi < blds.Count; bi++)
            {
                bool pr = bi == 0;
                switch (blds[bi].Arch)
                {
                    case "Medical":
                        Ensure(bi, RoomFunction.Ward, pr ? 8 : 3, 5, 7);
                        Ensure(bi, RoomFunction.OperatingRoom, pr ? 2 : 0, 6, 8);
                        Ensure(bi, RoomFunction.Pharmacy, pr ? 1 : 0, 5, 7);
                        Ensure(bi, RoomFunction.Morgue, pr ? 1 : 0, 5, 7);
                        Ensure(bi, RoomFunction.Lab, 1, 5, 8);
                        Ensure(bi, RoomFunction.Restroom, 1, 4, 6);
                        Ensure(bi, RoomFunction.ServerRoom, 1, 4, 6);
                        Ensure(bi, RoomFunction.Storeroom, 1, 5, 7);
                        Ensure(bi, RoomFunction.PrivateOffice, 1, 3, 6);
                        break;
                    case "Barracks":
                        Ensure(bi, RoomFunction.Bunkroom, pr ? 6 : 4, 8, 10);
                        Ensure(bi, RoomFunction.Cafeteria, 1, 10, 14);
                        Ensure(bi, RoomFunction.SecurityPost, 1, 5, 8);
                        Ensure(bi, RoomFunction.Storeroom, 1, 6, 8);
                        Ensure(bi, RoomFunction.Conference, 1, 6, 9);
                        Ensure(bi, RoomFunction.PrivateOffice, 1, 3, 6);
                        Ensure(bi, RoomFunction.Restroom, 1, 4, 6);
                        break;
                    case "Residential":
                        Ensure(bi, RoomFunction.Apartment, pr ? 14 : 6, 4, 6);
                        Ensure(bi, RoomFunction.Mailroom, 1, 4, 6);
                        Ensure(bi, RoomFunction.Storeroom, 1, 5, 7);
                        Ensure(bi, RoomFunction.Cafeteria, 1, 10, 14);
                        Ensure(bi, RoomFunction.Restroom, 1, 4, 5);
                        break;
                    case "Research":
                        Ensure(bi, RoomFunction.Lab, pr ? 8 : 4, 5, 8);
                        Ensure(bi, RoomFunction.ServerRoom, 2, 4, 6);
                        Ensure(bi, RoomFunction.Containment, 2, 4, 6);
                        Ensure(bi, RoomFunction.MachineHall, 1, 12, 20);
                        Ensure(bi, RoomFunction.Archive, 1, 5, 8);
                        Ensure(bi, RoomFunction.PrivateOffice, 1, 3, 6);
                        Ensure(bi, RoomFunction.Conference, 1, 6, 9);
                        Ensure(bi, RoomFunction.Restroom, 1, 4, 6);
                        Ensure(bi, RoomFunction.BreakRoom, 1, 5, 7);
                        break;
                    case "Plant":
                        if (pr && principal == "Plant")
                        {
                            Ensure(bi, RoomFunction.MachineHall, 2, 14, 28);
                            Ensure(bi, RoomFunction.Mechanical, 1, 6, 10);
                            Ensure(bi, RoomFunction.Boiler, 1, 6, 10);
                            Ensure(bi, RoomFunction.Storeroom, 2, 5, 8);
                            Ensure(bi, RoomFunction.PrivateOffice, 1, 3, 6);
                            Ensure(bi, RoomFunction.ServerRoom, 1, 4, 6);
                        }

                        break;
                }
            }
        }

        // ------------------------------------------------------------------ fire doors
        const int CutLimit = 22;
        int[] memb;
        int token;
        int[] mark;
        int markVer;

        List<int> Flood(int start)
        {
            markVer++;
            List<int> res = new List<int>();
            res.Add(start); mark[start] = markVer;
            for (int h = 0; h < res.Count; h++)
            {
                int c = res[h]; int cx = c % W, cy = c / W;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (!InB(nx, ny)) continue;
                    int ni = ny * W + nx;
                    if (mark[ni] != markVer && tile[ni] == PlanTile.Floor && lab[ni] == 0) { mark[ni] = markVer; res.Add(ni); }
                }
            }

            return res;
        }

        int cutLimit = CutLimit, cutMargin = 5, cutRad = 3;

        void FireCuts()
        {
            memb = new int[W * H]; mark = new int[W * H];
            cutLimit = CutLimit; cutMargin = 5; cutRad = 3;
            CutPass();
            cutLimit = 27; cutMargin = 3; cutRad = 1;
            CutPass();
        }

        void CutPass()
        {
            bool[] done = new bool[W * H];
            List<List<int>> stack = new List<List<int>>();
            for (int i = 0; i < W * H; i++)
            {
                if (tile[i] == PlanTile.Floor && lab[i] == 0 && !done[i])
                {
                    List<int> comp = Flood(i);
                    foreach (int c in comp) done[c] = true;
                    stack.Add(comp);
                }
            }

            while (stack.Count > 0)
            {
                List<int> comp = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (!TryCut(comp)) continue;
                List<int> remaining = new List<int>();
                foreach (int c in comp) if (tile[c] == PlanTile.Floor && lab[c] == 0) remaining.Add(c);
                token++;
                foreach (int c in remaining) memb[c] = token;
                int tk = token;
                foreach (int c in remaining)
                {
                    if (memb[c] != tk) continue;
                    List<int> part = Flood(c);
                    foreach (int p in part) memb[p] = -1;
                    stack.Add(part);
                }
            }
        }

        bool TryCut(List<int> comp)
        {
            token++;
            int tk = token;
            int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
            foreach (int c in comp)
            {
                memb[c] = tk;
                int x = c % W, y = c / W;
                if (x < minx) minx = x; if (x > maxx) maxx = x;
                if (y < miny) miny = y; if (y > maxy) maxy = y;
            }

            int bw = maxx - minx + 1, bh = maxy - miny + 1;
            if (Math.Max(bw, bh) <= cutLimit) return false;
            bool colFirst = bw >= bh;
            for (int pass = 0; pass < 2; pass++)
            {
                bool col = pass == 0 ? colFirst : !colFirst;
                int plo = col ? minx + cutMargin : miny + cutMargin, phi = col ? maxx - cutMargin : maxy - cutMargin;
                int pmid = col ? (minx + maxx) / 2 : (miny + maxy) / 2;
                int clo = col ? miny : minx, chi = col ? maxy : maxx;
                for (int off = 0; off <= 200; off++)
                {
                    for (int sg = 0; sg < 2; sg++)
                    {
                        if (off == 0 && sg == 1) continue;
                        int p = pmid + (sg == 0 ? off : -off);
                        if (p < plo || p > phi) continue;
                        if (TryCutLine(col, p, clo, chi, tk)) return true;
                    }
                }
            }

            return false;
        }

        int CI(bool col, int p, int c) { return col ? c * W + p : p * W + c; }

        bool TryCutLine(bool col, int p, int clo, int chi, int tk)
        {
            int c = clo;
            while (c <= chi)
            {
                if (memb[CI(col, p, c)] != tk) { c++; continue; }
                int ya = c;
                while (c + 1 <= chi && memb[CI(col, p, c + 1)] == tk) c++;
                int yb = c;
                c++;
                if (TryCutRun(col, p, ya, yb, tk)) return true;
            }

            return false;
        }

        bool TryCutRun(bool col, int p, int ya, int yb, int tk)
        {
            int width = yb - ya + 1;
            if (width < 2 || width > 6) return false;
            for (int dp = -cutRad; dp <= cutRad; dp++)
            {
                for (int c = ya; c <= yb; c++)
                {
                    int i = CI(col, p + dp, c);
                    if (memb[i] != tk || atr[i]) return false;
                }

                if (memb[CI(col, p + dp, ya - 1)] == tk || memb[CI(col, p + dp, yb + 1)] == tk) return false;
                PlanTile a = tile[CI(col, p + dp, ya - 1)], b = tile[CI(col, p + dp, yb + 1)];
                if (dp == 0) { if (a != PlanTile.Wall || b != PlanTile.Wall) return false; }
                else if ((a != PlanTile.Wall && a != PlanTile.Door) || (b != PlanTile.Wall && b != PlanTile.Door)) return false;
            }

            int mid = ya + (width - 1) / 2;
            for (int c = ya; c <= yb; c++)
            {
                int i = CI(col, p, c);
                lab[i] = -1;
                if (c == mid) { tile[i] = PlanTile.Door; fire[i] = true; }
                else tile[i] = PlanTile.Wall;
            }

            return true;
        }

        // ------------------------------------------------------------------ windows
        static bool Glazed(RoomFunction f)
        {
            return f == RoomFunction.Conference || f == RoomFunction.OpenOffice || f == RoomFunction.Lobby || f == RoomFunction.Reception ||
                   f == RoomFunction.Lab || f == RoomFunction.Cafeteria || f == RoomFunction.SecurityPost;
        }

        static bool Frontage(RoomFunction f)
        {
            return Glazed(f) || f == RoomFunction.PrivateOffice || f == RoomFunction.BreakRoom || f == RoomFunction.Mailroom ||
                   f == RoomFunction.Ward || f == RoomFunction.Apartment || f == RoomFunction.Bunkroom;
        }

        bool WinOk(int x, int y, bool horizLine, int innerLab, bool toCorridor)
        {
            // horizLine: the wall runs along x; the sides are north/south
            if (T(x, y) != PlanTile.Wall || !NoDoorAround(x, y)) return false;
            PlanTile a = horizLine ? T(x - 1, y) : T(x, y - 1), b = horizLine ? T(x + 1, y) : T(x, y + 1);
            if ((a != PlanTile.Wall && a != PlanTile.Window) || (b != PlanTile.Wall && b != PlanTile.Window)) return false;
            return true;
        }

        void Windows()
        {
            for (int ri = 0; ri < rooms.Count; ri++)
            {
                PRoom r = rooms[ri];
                if (r.Dead || r.IsCap) continue;
                bool glaze = Glazed(r.F), front = Frontage(r.F);
                if (!glaze && !front) continue;
                for (int side = 0; side < 4; side++)
                {
                    // side: 0 south wall, 1 north wall, 2 west wall, 3 east wall
                    bool horizLine = side < 2;
                    int lo = horizLine ? r.X0 : r.Y0, hi = horizLine ? r.X1 : r.Y1;
                    int fixedC = side == 0 ? r.Y0 - 1 : (side == 1 ? r.Y1 + 1 : (side == 2 ? r.X0 - 1 : r.X1 + 1));
                    int dirIn = (side == 0 || side == 2) ? 1 : -1;
                    int run0 = -1;
                    for (int c = lo; c <= hi + 1; c++)
                    {
                        bool ok = false;
                        bool corridorSide = false, outsideSide = false;
                        if (c <= hi)
                        {
                            int x = horizLine ? c : fixedC, y = horizLine ? fixedC : c;
                            int ix = horizLine ? x : x + dirIn, iy = horizLine ? y + dirIn : y;
                            int ox = horizLine ? x : x - dirIn, oy = horizLine ? y - dirIn : y;
                            if (lab[iy * W + ix] == ri + 1 && tile[iy * W + ix] == PlanTile.Floor && WinOk(x, y, horizLine, ri + 1, false))
                            {
                                if (Lab(ox, oy) == 0) { corridorSide = true; ok = true; }
                                else if (T(ox, oy) == PlanTile.Outside) { outsideSide = true; ok = true; }
                            }

                            if (ok && ((corridorSide && !glaze) || (outsideSide && !front))) ok = false;
                            if (ok) { if (run0 < 0) run0 = c; lastKind = corridorSide ? 1 : 2; }
                        }

                        if (!ok && run0 >= 0)
                        {
                            int n = c - run0;
                            PlaceWindowRun(horizLine, fixedC, run0, n, lastKind);
                            run0 = -1;
                        }
                    }
                }
            }
        }

        int lastKind;

        void PlaceWindowRun(bool horizLine, int fixedC, int start, int n, int kind)
        {
            int k;
            if (kind == 1)
            {
                if (n < 3) return;
                if (rng.Next(100) >= 85) return;
                k = Math.Min(n - 1, 2 + rng.Next(4));
                if (k < 2) return;
            }
            else
            {
                if (n < 4) return;
                if (rng.Next(100) >= 55) return;
                k = Math.Min(n - 2, 2 + rng.Next(2));
                if (k < 2) return;
            }

            int off = (n - k) / 2 + (n - k > 1 ? rng.Next(3) - 1 : 0);
            off = Clamp(off, 0, n - k);
            for (int j = 0; j < k; j++)
            {
                int c = start + off + j;
                int x = horizLine ? c : fixedC, y = horizLine ? fixedC : c;
                if (T(x, y) == PlanTile.Wall && NoDoorAround(x, y)) tile[y * W + x] = PlanTile.Window;
            }
        }

        // ------------------------------------------------------------------ output
        BuildingPlan Assemble()
        {
            BuildingPlan p = new BuildingPlan(W, H);
            for (int i = 0; i < tile.Length; i++) p.Tiles[i] = tile[i];
            int[] queue = new int[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (tile[i] != PlanTile.Floor || p.RoomIds[i] >= 0) continue;
                    int l = lab[i];
                    int id = p.Rooms.Count;
                    PlanRoom room = new PlanRoom();
                    room.Id = id;
                    int head = 0, tail = 0;
                    queue[tail++] = i; p.RoomIds[i] = id;
                    bool atrium = false;
                    int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
                    long sx = 0, sy = 0;
                    while (head < tail)
                    {
                        int c = queue[head++]; int cx = c % W, cy = c / W;
                        room.Cells.Add(new LayoutCell(cx, cy));
                        if (atr[c]) atrium = true;
                        if (cx < minx) minx = cx; if (cx > maxx) maxx = cx;
                        if (cy < miny) miny = cy; if (cy > maxy) maxy = cy;
                        sx += cx; sy += cy;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + DX[d], ny = cy + DY[d];
                            if (!InB(nx, ny)) continue;
                            int ni = ny * W + nx;
                            if (tile[ni] == PlanTile.Floor && lab[ni] == l && p.RoomIds[ni] < 0) { p.RoomIds[ni] = id; queue[tail++] = ni; }
                        }
                    }

                    if (l == 0)
                    {
                        room.IsCorridor = true;
                        room.Function = atrium ? RoomFunction.Atrium : RoomFunction.Corridor;
                    }
                    else room.Function = rooms[l - 1].F;
                    room.BuildingId = bid[i] >= 0 ? bid[i] : 0;
                    if (l > 0) room.BuildingId = rooms[l - 1].Bid;
                    room.Concave = (maxx - minx + 1) * (maxy - miny + 1) > room.Cells.Count;
                    double mx = (double)sx / room.Cells.Count, my = (double)sy / room.Cells.Count;
                    double bd = double.MaxValue; LayoutCell best = room.Cells[0];
                    foreach (LayoutCell c in room.Cells)
                    {
                        double d2 = (c.X - mx) * (c.X - mx) + (c.Y - my) * (c.Y - my);
                        if (d2 < bd) { bd = d2; best = c; }
                    }

                    room.Anchor = best;
                    p.Rooms.Add(room);
                }
            }

            for (int i = 0; i < tile.Length; i++)
            {
                if (tile[i] != PlanTile.Door) continue;
                int x = i % W, y = i / W;
                int ra, rb;
                if (Flr(x - 1, y) || Flr(x + 1, y)) { ra = p.RoomAt(x - 1, y); rb = p.RoomAt(x + 1, y); }
                else { ra = p.RoomAt(x, y - 1); rb = p.RoomAt(x, y + 1); }
                if (ra < 0 && rb < 0) return null;
                if (ra < 0) { ra = rb; rb = -1; }
                else if (rb >= 0 && ra > rb) { int t = ra; ra = rb; rb = t; }
                PlanLink link = new PlanLink();
                link.RoomA = ra; link.RoomB = rb; link.Cell = new LayoutCell(x, y); link.Fire = fire[i];
                p.Links.Add(link);
            }

            for (int b = 0; b < archs.Count; b++)
            {
                PlanBuilding pb = new PlanBuilding();
                pb.Id = b; pb.Archetype = archs[b]; pb.Isolated = blds[b].Isolated;
                pb.MinX = int.MaxValue; pb.MinY = int.MaxValue; pb.MaxX = -1; pb.MaxY = -1;
                p.Buildings.Add(pb);
            }

            for (int i = 0; i < foot.Length; i++)
            {
                if (!foot[i] || bid[i] < 0 || bid[i] >= p.Buildings.Count) continue;
                PlanBuilding pb = p.Buildings[bid[i]];
                int x = i % W, y = i / W;
                if (x < pb.MinX) pb.MinX = x; if (x > pb.MaxX) pb.MaxX = x;
                if (y < pb.MinY) pb.MinY = y; if (y > pb.MaxY) pb.MaxY = y;
            }

            foreach (PlanBuilding pb in p.Buildings) if (pb.MaxX < 0) return null;
            p.CourtyardsFilled = courtFilled; p.CourtyardsOpen = courtOpen;
            p.MainEntrance = new LayoutCell(entX, entY);
            p.Start = startY >= 0 ? new LayoutCell(startX, startY) : new LayoutCell(entX, entY - 6);
            p.GateCount = gateRects.Count;
            foreach (int[] gr in gateRects)
                for (int y = gr[1]; y <= gr[3]; y++)
                    for (int x = gr[0]; x <= gr[2]; x++) p.Gates.Add(new LayoutCell(x, y));
            if (style == "scattered")
            {
                int rx0 = entX - 14, rx1 = entX + 13, ry0 = Math.Max(0, entY - 20), ry1 = entY - 1;
                bool Open(int x, int y0, int y1)
                {
                    if (x < 0 || x >= W) return false;
                    for (int yy = y0; yy <= y1; yy++) if (tile[yy * W + x] != PlanTile.Outside) return false;
                    return true;
                }

                bool ok = true;
                for (int x = rx0; x <= rx1 && ok; x++) if (!Open(x, ry0, ry1)) ok = false;
                if (ok)
                {
                    while (Open(rx0 - 1, ry0, ry1)) rx0--;
                    while (Open(rx1 + 1, ry0, ry1)) rx1++;
                    int low = ry0;
                    while (low > 0)
                    {
                        bool row = true;
                        for (int x = rx0; x <= rx1 && row; x++) if (tile[(low - 1) * W + x] != PlanTile.Outside) row = false;
                        if (!row) break;
                        low--;
                    }

                    p.ParadeW = rx1 - rx0 + 1; p.ParadeH = ry1 - low + 1;
                }
            }
            return p;
        }
    }
}
