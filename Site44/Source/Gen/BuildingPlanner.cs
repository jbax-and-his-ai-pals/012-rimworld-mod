using System;
using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    // Whole-site office-complex planner. Pure C#, deterministic (LayoutRng), no engine references.
    //
    // Model: the site is a set of BARS (double-loaded corridor + room bands on both sides). A principal "front" bar
    // runs along one edge with the lobby/main entrance; perpendicular RIBS (wings) join it (comb / E / ladder / ring
    // when a top bar closes them), further zones (lab, warehouse, plant, guard annex) are joined by link corridors.
    // Everything is generated in a canonical orientation (front facing -y) and mirrored/transposed at the end.
    public static class BuildingPlanner
    {
        public static BuildingPlan Generate(int width, int height, int seed)
        {
            return Generate(width, height, seed, null);
        }

        // profile == null generates the default office campus.
        public static BuildingPlan Generate(int width, int height, int seed, PlanProfile profile)
        {
            BuildingPlan lastBuilt = null;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int s = unchecked(seed * 7919 + attempt * 104729 + 17);
                bool simple = attempt >= 9;
                BuildingPlan p = null;
                string err;
                try { p = PlanBuild.Build(width, height, s, attempt, simple, out err, profile); }
                catch (Exception) { p = null; }
                if (p == null) continue;
                lastBuilt = p;
                if (Validate(p) == null) return p;
            }

            // last resort: the plainest dense configuration over a few seeds
            for (int k = 0; k < 40; k++)
            {
                BuildingPlan p = null;
                string err;
                try { p = PlanBuild.Build(width, height, 1000 + k, 99, true, out err, profile); }
                catch (Exception) { p = null; }
                if (p == null) continue;
                lastBuilt = p;
                if (Validate(p) == null) return p;
            }

            return lastBuilt ?? new BuildingPlan(width, height);
        }

        // Diagnostic: one attempt, returns the failure reason (or null when the plan is valid).
        internal static string TryOnce(int width, int height, int seed, int attempt, out BuildingPlan plan, PlanProfile profile = null)
        {
            string err;
            bool simple = attempt >= 9;
            plan = PlanBuild.Build(width, height, unchecked(seed * 7919 + attempt * 104729 + 17), attempt, simple, out err, profile);
            if (plan == null) return "build: " + err;
            return Validate(plan);
        }

        // ------------------------------------------------------------------ summary
        public static string Summary(BuildingPlan plan)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Profile: ").Append(plan.ProfileName).Append(" size=").Append(plan.Width).Append('x').Append(plan.Height).Append(" style=").Append(plan.Style)
              .Append(" parade=").Append(plan.ParadeW > 0 ? plan.ParadeW + "x" + plan.ParadeH : "-").Append(" gates=").Append(plan.GateCount).Append('\n');
            sb.Append("Buildings: ");
            for (int i = 0; i < plan.Buildings.Count; i++)
            {
                PlanBuilding b = plan.Buildings[i];
                if (i > 0) sb.Append(", ");
                sb.Append(b.Archetype).Append(" [").Append(b.MaxX - b.MinX + 1).Append('x').Append(b.MaxY - b.MinY + 1).Append(']');
            }

            int[] counts = new int[Enum.GetValues(typeof(RoomFunction)).Length];
            int fire = 0, win = 0, doors = 0, nonOutside = 0;
            foreach (PlanRoom r in plan.Rooms) counts[(int)r.Function]++;
            foreach (PlanLink l in plan.Links) { if (l.Fire) fire++; }
            foreach (PlanTile t in plan.Tiles)
            {
                if (t != PlanTile.Outside) nonOutside++;
                if (t == PlanTile.Window) win++;
                if (t == PlanTile.Door) doors++;
            }

            sb.Append("\nRooms: ").Append(plan.Rooms.Count).Append(" (");
            bool first = true;
            for (int f = 0; f < counts.Length; f++)
            {
                if (counts[f] == 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append((RoomFunction)f).Append('=').Append(counts[f]);
            }

            int iso = 0, rects = 0;
            foreach (PlanBuilding b in plan.Buildings)
            {
                if (b.Isolated) iso++;
                if (IsPlainRectangle(plan, b)) rects++;
            }

            sb.Append(")\nNonRectRooms=").Append((100.0 * NonRectShare(plan)).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("% shapeClasses=").Append(ShapeClasses(plan).Count)
              .Append(" suites=").Append(CountSuites(plan, 2)).Append(" interlocks=").Append(CountSuites(plan, 1));
            sb.Append("\nIsolated=").Append(iso).Append(" plainRectangles=").Append(rects).Append('/').Append(plan.Buildings.Count)
              .Append(" courtyards filled/open=").Append(plan.CourtyardsFilled).Append('/').Append(plan.CourtyardsOpen);
            sb.Append("\nDoors=").Append(doors).Append(" fire=").Append(fire).Append(" windows=").Append(win);
            double cov = 100.0 * nonOutside / (plan.Width * plan.Height);
            sb.Append("\nCoverage: ").Append(cov.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append("% of ").Append(plan.Width).Append('x').Append(plan.Height);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ transform
        static void Map(int x, int y, int nw, int nh, bool transpose, bool fx, bool fy, out int ox, out int oy)
        {
            int a = transpose ? y : x, b = transpose ? x : y;
            if (fx) a = nw - 1 - a;
            if (fy) b = nh - 1 - b;
            ox = a; oy = b;
        }

        internal static BuildingPlan Transform(BuildingPlan p, bool transpose, bool fx, bool fy)
        {
            int nw = transpose ? p.Height : p.Width, nh = transpose ? p.Width : p.Height;
            BuildingPlan q = new BuildingPlan(nw, nh);
            q.CourtyardsFilled = p.CourtyardsFilled; q.CourtyardsOpen = p.CourtyardsOpen; q.ProfileName = p.ProfileName;
            q.Style = p.Style; q.GateCount = p.GateCount;
            q.ParadeW = transpose ? p.ParadeH : p.ParadeW; q.ParadeH = transpose ? p.ParadeW : p.ParadeH;
            foreach (LayoutCell gc in p.Gates)
            {
                int gx, gy;
                Map(gc.X, gc.Y, nw, nh, transpose, fx, fy, out gx, out gy);
                q.Gates.Add(new LayoutCell(gx, gy));
            }
            int ox, oy;
            for (int y = 0; y < p.Height; y++)
            {
                for (int x = 0; x < p.Width; x++)
                {
                    Map(x, y, nw, nh, transpose, fx, fy, out ox, out oy);
                    q.Tiles[oy * nw + ox] = p.Tiles[y * p.Width + x];
                }
            }

            foreach (PlanRoom r in p.Rooms)
            {
                PlanRoom n = new PlanRoom();
                n.Id = r.Id; n.Function = r.Function; n.IsCorridor = r.IsCorridor; n.BuildingId = r.BuildingId; n.Concave = r.Concave;
                foreach (LayoutCell c in r.Cells)
                {
                    Map(c.X, c.Y, nw, nh, transpose, fx, fy, out ox, out oy);
                    n.Cells.Add(new LayoutCell(ox, oy));
                    q.RoomIds[oy * nw + ox] = r.Id;
                }

                Map(r.Anchor.X, r.Anchor.Y, nw, nh, transpose, fx, fy, out ox, out oy);
                n.Anchor = new LayoutCell(ox, oy);
                q.Rooms.Add(n);
            }

            foreach (PlanLink l in p.Links)
            {
                Map(l.Cell.X, l.Cell.Y, nw, nh, transpose, fx, fy, out ox, out oy);
                PlanLink n = new PlanLink();
                n.RoomA = l.RoomA; n.RoomB = l.RoomB; n.Fire = l.Fire; n.Cell = new LayoutCell(ox, oy);
                q.Links.Add(n);
            }

            foreach (PlanBuilding b in p.Buildings)
            {
                PlanBuilding n = new PlanBuilding();
                n.Id = b.Id; n.Archetype = b.Archetype; n.Isolated = b.Isolated;
                int ax, ay, bx, by;
                Map(b.MinX, b.MinY, nw, nh, transpose, fx, fy, out ax, out ay);
                Map(b.MaxX, b.MaxY, nw, nh, transpose, fx, fy, out bx, out by);
                n.MinX = Math.Min(ax, bx); n.MaxX = Math.Max(ax, bx);
                n.MinY = Math.Min(ay, by); n.MaxY = Math.Max(ay, by);
                q.Buildings.Add(n);
            }

            Map(p.Start.X, p.Start.Y, nw, nh, transpose, fx, fy, out ox, out oy);
            q.Start = new LayoutCell(ox, oy);
            Map(p.MainEntrance.X, p.MainEntrance.Y, nw, nh, transpose, fx, fy, out ox, out oy);
            q.MainEntrance = new LayoutCell(ox, oy);
            return q;
        }

        // ------------------------------------------------------------------ validation
        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

        // Returns null when the plan satisfies every structural invariant, else a short reason.
        internal static string Validate(BuildingPlan p)
        {
            int W = p.Width, H = p.Height, n = W * H;
            if (p.Tiles == null || p.Tiles.Length != n || p.RoomIds.Length != n) return "array size";
            if (p.Buildings.Count < 1) return "no buildings";

            // 1. rooms
            int[] cnt = new int[p.Rooms.Count];
            int floors = 0;
            for (int i = 0; i < n; i++)
            {
                int id = p.RoomIds[i];
                if (p.Tiles[i] == PlanTile.Floor)
                {
                    floors++;
                    if (id < 0 || id >= p.Rooms.Count) return "floor without room";
                    cnt[id]++;
                }
                else if (id != -1) return "non-floor with room";
            }

            bool[] seen = new bool[n];
            int[] queue = new int[n];
            for (int r = 0; r < p.Rooms.Count; r++)
            {
                PlanRoom room = p.Rooms[r];
                if (room.Id != r) return "room id";
                if (room.Cells.Count != cnt[r] || room.Cells.Count == 0) return "room cell count " + r;
                if (room.Function == RoomFunction.Unspecified) return "unspecified function";
                if (room.BuildingId < 0 || room.BuildingId >= p.Buildings.Count) return "building id";
                int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
                foreach (LayoutCell c in room.Cells)
                {
                    if (c.X < 0 || c.Y < 0 || c.X >= W || c.Y >= H) return "cell oob";
                    if (p.RoomIds[c.Y * W + c.X] != r) return "cell not in room";
                    minx = Math.Min(minx, c.X); maxx = Math.Max(maxx, c.X); miny = Math.Min(miny, c.Y); maxy = Math.Max(maxy, c.Y);
                }

                if (room.Concave != ((maxx - minx + 1) * (maxy - miny + 1) > room.Cells.Count)) return "concave flag " + r;
                if (p.RoomIds[room.Anchor.Y * W + room.Anchor.X] != r) return "anchor";
                int head = 0, tail = 0, start = room.Anchor.Y * W + room.Anchor.X;
                queue[tail++] = start; seen[start] = true;
                while (head < tail)
                {
                    int c = queue[head++]; int cx = c % W, cy = c / W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + Dx[d], ny = cy + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int ni = ny * W + nx;
                        if (!seen[ni] && p.RoomIds[ni] == r) { seen[ni] = true; queue[tail++] = ni; }
                    }
                }

                if (tail != room.Cells.Count) return "room not connected " + r;
                if (room.IsCorridor && room.Function != RoomFunction.Corridor && room.Function != RoomFunction.Atrium && room.Function != RoomFunction.Lobby) return "corridor function";
            }

            // 3/4. doors, windows, links
            int[] linkAt = new int[n];
            for (int i = 0; i < n; i++) linkAt[i] = -1;
            for (int li = 0; li < p.Links.Count; li++)
            {
                PlanLink l = p.Links[li];
                int idx = l.Cell.Y * W + l.Cell.X;
                if (!p.InBounds(l.Cell.X, l.Cell.Y) || p.Tiles[idx] != PlanTile.Door) return "link not on door";
                if (linkAt[idx] >= 0) return "two links one door";
                linkAt[idx] = li;
            }

            int exteriorDoors = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    PlanTile t = p.Tiles[y * W + x];
                    if (t != PlanTile.Door && t != PlanTile.Window) continue;
                    PlanTile e = p.At(x + 1, y), w = p.At(x - 1, y), no = p.At(x, y + 1), so = p.At(x, y - 1);
                    bool ew = (e == PlanTile.Floor || e == PlanTile.Outside) && (w == PlanTile.Floor || w == PlanTile.Outside) && IsSolid(no) && IsSolid(so);
                    bool ns = (no == PlanTile.Floor || no == PlanTile.Outside) && (so == PlanTile.Floor || so == PlanTile.Outside) && IsSolid(e) && IsSolid(w);
                    if (!ew && !ns) return (t == PlanTile.Door ? "bad door " : "bad window ") + x + "," + y;
                    if (e == PlanTile.Outside && w == PlanTile.Outside) return "door/window between outsides";
                    if (no == PlanTile.Outside && so == PlanTile.Outside) return "door/window between outsides";
                    if (t == PlanTile.Door)
                    {
                        if (e == PlanTile.Door || no == PlanTile.Door) return "adjacent doors";
                        int li = linkAt[y * W + x];
                        if (li < 0) return "door without link";
                        PlanLink l = p.Links[li];
                        int ra, rb;
                        if (ew) { ra = p.RoomAt(x - 1, y); rb = p.RoomAt(x + 1, y); }
                        else { ra = p.RoomAt(x, y - 1); rb = p.RoomAt(x, y + 1); }
                        if (ra < 0 && rb >= 0) { ra = rb; rb = -1; }
                        else if (ra >= 0 && rb >= 0 && ra > rb) { int tmp = ra; ra = rb; rb = tmp; }
                        if (l.RoomA != ra || l.RoomB != rb) return "link rooms mismatch";
                        if (rb == -1) exteriorDoors++;
                        if (ra == rb && ra >= 0) return "door within one room";
                        if (l.Fire && !(p.Rooms[ra].IsCorridor && rb >= 0 && p.Rooms[rb].IsCorridor)) return "fire door not corridor-corridor";
                    }
                    else
                    {
                        if (e == PlanTile.Door || w == PlanTile.Door || no == PlanTile.Door || so == PlanTile.Door) return "window beside door";
                    }
                }
            }

            if (exteriorDoors < 2) return "too few exterior doors";

            // 2. connectivity through Floor + Door: the principal building's component, plus one component per isolated building
            LayoutCell me0 = p.MainEntrance;
            if (!p.InBounds(me0.X, me0.Y)) return "entrance oob";
            int first = -1;
            for (int d = 0; d < 4; d++)
            {
                int fx = me0.X + Dx[d], fy = me0.Y + Dy[d];
                if (p.InBounds(fx, fy) && p.Tiles[fy * W + fx] == PlanTile.Floor) first = fy * W + fx;
            }

            if (first < 0) return "entrance without floor";
            Array.Clear(seen, 0, n);
            int h2 = 0, t2 = 0;
            queue[t2++] = first; seen[first] = true;
            while (h2 < t2)
            {
                int c = queue[h2++]; int cx = c % W, cy = c / W;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int ni = ny * W + nx;
                    PlanTile t = p.Tiles[ni];
                    if (!seen[ni] && (t == PlanTile.Floor || t == PlanTile.Door)) { seen[ni] = true; queue[t2++] = ni; }
                }
            }

            int[] isoComp = new int[p.Buildings.Count];
            for (int bi = 0; bi < isoComp.Length; bi++) isoComp[bi] = -1;
            bool[] mainSeen = (bool[])seen.Clone();
            for (int i = 0; i < n; i++)
            {
                if (p.Tiles[i] != PlanTile.Floor) continue;
                PlanBuilding pb = p.Buildings[p.Rooms[p.RoomIds[i]].BuildingId];
                if (mainSeen[i])
                {
                    if (pb.Isolated) return "isolated building linked";
                    continue;
                }

                if (!pb.Isolated) return "floor disconnected " + p.Rooms[p.RoomIds[i]].Function + " at " + (i % W) + "," + (i / W) + " cells " + p.Rooms[p.RoomIds[i]].Cells.Count;
                if (seen[i]) continue;
                if (isoComp[pb.Id] >= 0) return "isolated building split";
                isoComp[pb.Id] = 1;
                h2 = 0; t2 = 0;
                queue[t2++] = i; seen[i] = true;
                while (h2 < t2)
                {
                    int c = queue[h2++]; int cx = c % W, cy = c / W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + Dx[d], ny = cy + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int ni = ny * W + nx;
                        PlanTile t = p.Tiles[ni];
                        if (!seen[ni] && (t == PlanTile.Floor || t == PlanTile.Door)) { seen[ni] = true; queue[t2++] = ni; }
                    }
                }
            }

            // entrance and start
            LayoutCell me = p.MainEntrance, st = p.Start;
            if (!p.InBounds(me.X, me.Y) || p.Tiles[me.Y * W + me.X] != PlanTile.Door) return "entrance not door";
            int ent = linkAt[me.Y * W + me.X];
            if (ent < 0 || p.Links[ent].RoomB != -1) return "entrance not exterior";
            RoomFunction ef = p.Rooms[p.Links[ent].RoomA].Function;
            if (p.Rooms[p.Links[ent].RoomA].BuildingId != 0 || (ef != RoomFunction.Lobby && ef != RoomFunction.Atrium)) return "entrance room";
            if (!p.InBounds(st.X, st.Y) || p.Tiles[st.Y * W + st.X] != PlanTile.Outside) return "start not outside";
            int outsideCell = -1;
            for (int d = 0; d < 4; d++)
            {
                if (p.At(me.X + Dx[d], me.Y + Dy[d]) == PlanTile.Outside) outsideCell = (me.Y + Dy[d]) * W + me.X + Dx[d];
            }

            if (outsideCell < 0) return "entrance without outside";
            bool[] os = new bool[n];
            h2 = 0; t2 = 0;
            queue[t2++] = st.Y * W + st.X; os[st.Y * W + st.X] = true;
            bool border = false;
            while (h2 < t2)
            {
                int c = queue[h2++]; int cx = c % W, cy = c / W;
                if (cx == 0 || cy == 0 || cx == W - 1 || cy == H - 1) border = true;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int ni = ny * W + nx;
                    if (!os[ni] && p.Tiles[ni] == PlanTile.Outside) { os[ni] = true; queue[t2++] = ni; }
                }
            }

            if (!border) return "start yard closed";
            if (!os[outsideCell]) return "entrance not in start yard";
            // an 8x8 open block that contains Start
            bool block = false;
            for (int ox = st.X - 7; ox <= st.X && !block; ox++)
            {
                for (int oy = st.Y - 7; oy <= st.Y && !block; oy++)
                {
                    if (ox < 0 || oy < 0 || ox + 7 >= W || oy + 7 >= H) continue;
                    bool ok = true;
                    for (int yy = oy; yy < oy + 8 && ok; yy++)
                        for (int xx = ox; xx < ox + 8; xx++)
                            if (p.Tiles[yy * W + xx] != PlanTile.Outside) { ok = false; break; }
                    if (ok) block = true;
                }
            }

            if (!block) return "start yard small";

            // exterior doors: every building has one; isolated buildings are reachable outdoors from Start
            int[] extCount = new int[p.Buildings.Count], extGood = new int[p.Buildings.Count];
            foreach (PlanLink l in p.Links)
            {
                if (l.RoomB != -1) continue;
                int bd = p.Rooms[l.RoomA].BuildingId;
                extCount[bd]++;
                for (int d = 0; d < 4; d++)
                {
                    int nx = l.Cell.X + Dx[d], ny = l.Cell.Y + Dy[d];
                    if (p.InBounds(nx, ny) && os[ny * W + nx]) { extGood[bd]++; break; }
                }
            }

            for (int bi = 0; bi < p.Buildings.Count; bi++)
            {
                PlanBuilding pb = p.Buildings[bi];
                if (extCount[bi] < 1) return "building without exterior door";
                if (pb.Isolated && extGood[bi] < (Math.Max(pb.MaxX - pb.MinX, pb.MaxY - pb.MinY) + 1 > 40 ? 2 : 1)) return "isolated building not reachable outdoors";
            }

            // 5/6. corridors
            int[] linkCount = new int[p.Rooms.Count];
            foreach (PlanLink l in p.Links)
            {
                linkCount[l.RoomA]++;
                if (l.RoomB >= 0) linkCount[l.RoomB]++;
            }

            bool[] corrLinked = new bool[p.Rooms.Count];
            foreach (PlanLink l in p.Links)
            {
                if (l.RoomB < 0) continue;
                if (p.Rooms[l.RoomB].IsCorridor) corrLinked[l.RoomA] = true;
                if (p.Rooms[l.RoomA].IsCorridor) corrLinked[l.RoomB] = true;
            }

            bool[] direct = (bool[])corrLinked.Clone();
            foreach (PlanLink l in p.Links)
            {
                if (l.RoomB < 0) continue;
                if (direct[l.RoomA] && !p.Rooms[l.RoomA].IsCorridor) corrLinked[l.RoomB] = true;
                if (direct[l.RoomB] && !p.Rooms[l.RoomB].IsCorridor) corrLinked[l.RoomA] = true;
            }

            int[] roomsIn = new int[p.Buildings.Count];
            foreach (PlanRoom r in p.Rooms) roomsIn[r.BuildingId]++;
            int big = 0, bigLinked = 0;
            foreach (PlanRoom r in p.Rooms)
            {
                if (r.IsCorridor)
                {
                    int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
                    foreach (LayoutCell c in r.Cells)
                    {
                        minx = Math.Min(minx, c.X); maxx = Math.Max(maxx, c.X); miny = Math.Min(miny, c.Y); maxy = Math.Max(maxy, c.Y);
                    }

                    if (Math.Max(maxx - minx + 1, maxy - miny + 1) > 30) return "corridor too long " + r.Id + " at " + minx + "," + miny;
                    if (r.Cells.Count > 15 && linkCount[r.Id] < 2) return "dead-end corridor " + r.Id;
                    if (r.Cells.Count > 12)
                    {
                        foreach (LayoutCell c in r.Cells)
                        {
                            bool ok = false;
                            for (int sx = -1; sx <= 0 && !ok; sx++)
                                for (int sy = -1; sy <= 0 && !ok; sy++)
                                {
                                    if (p.RoomAt(c.X + sx, c.Y + sy) == r.Id && p.RoomAt(c.X + sx + 1, c.Y + sy) == r.Id &&
                                        p.RoomAt(c.X + sx, c.Y + sy + 1) == r.Id && p.RoomAt(c.X + sx + 1, c.Y + sy + 1) == r.Id) ok = true;
                                }

                            if (!ok) return "corridor narrower than 2 at " + c.X + "," + c.Y;
                        }
                    }
                }
                else if (r.Cells.Count >= 10)
                {
                    big++;
                    if (corrLinked[r.Id] || (p.Buildings[r.BuildingId].Isolated && roomsIn[r.BuildingId] == 1)) bigLinked++;
                }
            }

            if (big > 0 && bigLinked * 100 < big * 97) return "rooms without corridor link " + bigLinked + "/" + big;

            // 7. coverage
            int nonOutside = 0;
            for (int i = 0; i < n; i++) if (p.Tiles[i] != PlanTile.Outside) nonOutside++;
            bool campusStyle = p.Style == null || p.Style == "Campus";
            double need = Math.Min(W, H) < 100 ? 0.25 : (campusStyle ? ((W >= 180 && H >= 180) ? 0.50 : 0.40) : ((W >= 180 && H >= 180) ? 0.30 : 0.25));
            if (nonOutside < need * n) return "coverage " + (100.0 * nonOutside / n).ToString("0.0");

            // 8. functions of the principal building
            bool[] has = new bool[Enum.GetValues(typeof(RoomFunction)).Length];
            foreach (PlanRoom r in p.Rooms) if (r.BuildingId == 0) has[(int)r.Function] = true;
            string arch0 = p.Buildings[0].Archetype;
            int[] total = new int[Enum.GetValues(typeof(RoomFunction)).Length];
            foreach (PlanRoom r in p.Rooms) total[(int)r.Function]++;
            bool tinySite = Math.Min(W, H) < 100;
            if (tinySite)
            {
                if (!has[(int)RoomFunction.Lobby]) return "missing Lobby";
                string tm = ProfileShortfall(arch0, total, true);
                if (tm != null) return tm;
            }
            else if (arch0 == "Office")
            {
                RoomFunction[] need0 = { RoomFunction.Lobby, RoomFunction.OpenOffice, RoomFunction.PrivateOffice, RoomFunction.Conference, RoomFunction.Restroom, RoomFunction.Stairwell, RoomFunction.ServerRoom, RoomFunction.Storeroom };
                foreach (RoomFunction f in need0) if (!has[(int)f]) return "missing " + f;
                if (!has[(int)RoomFunction.Mechanical] && !has[(int)RoomFunction.Boiler]) return "missing Mechanical";
            }
            else
            {
                RoomFunction[] need0 = { RoomFunction.Lobby, RoomFunction.Restroom, RoomFunction.Stairwell };
                foreach (RoomFunction f in need0) if (!has[(int)f]) return "missing " + f;
                string miss = ProfileShortfall(arch0, total, false);
                if (miss != null) return miss;
            }

            return null;
        }

        // Whole-plan function requirements of each principal archetype (null when satisfied).
        public static string ProfileShortfall(string principal, int[] total, bool tiny = false)
        {
            if (tiny)
            {
                switch (principal)
                {
                    case "Medical":
                        if (total[(int)RoomFunction.Ward] < 2) return "wards";
                        if (total[(int)RoomFunction.OperatingRoom] + total[(int)RoomFunction.Pharmacy] < 1) return "operating room or pharmacy";
                        break;
                    case "Barracks":
                        if (total[(int)RoomFunction.Bunkroom] < 2) return "bunkrooms";
                        break;
                    case "Residential":
                        if (total[(int)RoomFunction.Apartment] < 4) return "apartments";
                        break;
                    case "Research":
                        if (total[(int)RoomFunction.Lab] < 2) return "labs";
                        if (total[(int)RoomFunction.Containment] + total[(int)RoomFunction.ServerRoom] < 1) return "containment or servers";
                        break;
                    case "Plant":
                        if (total[(int)RoomFunction.MachineHall] < 1) return "machine hall";
                        break;
                }

                return null;
            }

            switch (principal)
            {
                case "Medical":
                    if (total[(int)RoomFunction.Ward] < 6) return "wards";
                    if (total[(int)RoomFunction.OperatingRoom] < 1) return "operating room";
                    if (total[(int)RoomFunction.Pharmacy] < 1) return "pharmacy";
                    if (total[(int)RoomFunction.Morgue] < 1) return "morgue";
                    break;
                case "Barracks":
                    if (total[(int)RoomFunction.Bunkroom] < 4) return "bunkrooms";
                    if (total[(int)RoomFunction.Cafeteria] < 1) return "mess";
                    if (total[(int)RoomFunction.SecurityPost] < 1) return "security post";
                    break;
                case "Residential":
                    if (total[(int)RoomFunction.Apartment] < 10) return "apartments";
                    break;
                case "Research":
                    if (total[(int)RoomFunction.Lab] < 5) return "labs";
                    if (total[(int)RoomFunction.ServerRoom] < 2) return "servers";
                    if (total[(int)RoomFunction.Containment] < 1) return "containment";
                    if (total[(int)RoomFunction.MachineHall] < 1) return "reactor";
                    break;
                case "Plant":
                    if (total[(int)RoomFunction.MachineHall] < 2) return "machine halls";
                    if (total[(int)RoomFunction.Mechanical] + total[(int)RoomFunction.Boiler] < 2) return "mechanical";
                    if (total[(int)RoomFunction.Storeroom] < 2) return "storerooms";
                    break;
            }

            return null;
        }

        // Shape class of a room from its cells: Rect, L, T/U, Z, Chamfer, Octagon, Cross, Stepped, Bay, Pillared.
        public static string ShapeClass(BuildingPlan p, PlanRoom r)
        {
            int minx = int.MaxValue, miny = int.MaxValue, maxx = -1, maxy = -1;
            foreach (LayoutCell c in r.Cells)
            {
                if (c.X < minx) minx = c.X; if (c.X > maxx) maxx = c.X;
                if (c.Y < miny) miny = c.Y; if (c.Y > maxy) maxy = c.Y;
            }

            int missing = (maxx - minx + 1) * (maxy - miny + 1) - r.Cells.Count;
            if (missing == 0) return "Rect";
            bool edge = false;
            for (int y = miny; y <= maxy && !edge; y++)
                for (int x = minx; x <= maxx; x++)
                    if (p.RoomAt(x, y) != r.Id && (x == minx || x == maxx || y == miny || y == maxy)) { edge = true; break; }
            if (!edge) return "Pillared";
            bool c1 = p.RoomAt(minx, miny) != r.Id, c2 = p.RoomAt(maxx, miny) != r.Id, c3 = p.RoomAt(minx, maxy) != r.Id, c4 = p.RoomAt(maxx, maxy) != r.Id;
            int mc = (c1 ? 1 : 0) + (c2 ? 1 : 0) + (c3 ? 1 : 0) + (c4 ? 1 : 0);
            if (mc == 0) return "Bay";
            if (mc == 1) return missing <= 6 ? "Chamfer" : "L";
            if (mc == 2)
            {
                if (missing <= 8) return "Chamfer";
                return ((c1 && c4) || (c2 && c3)) ? "Z" : "T/U";
            }

            if (mc == 3) return "Stepped";
            return missing <= 24 ? "Octagon" : "Cross";
        }

        public static HashSet<string> ShapeClasses(BuildingPlan p)
        {
            HashSet<string> set = new HashSet<string>();
            foreach (PlanRoom r in p.Rooms) if (!r.IsCorridor && r.Cells.Count >= 12) set.Add(ShapeClass(p, r));
            return set;
        }

        // Share of non-corridor rooms with at least 20 cells that are not rectangles.
        public static double NonRectShare(BuildingPlan p)
        {
            int n = 0, non = 0;
            foreach (PlanRoom r in p.Rooms)
            {
                if (r.IsCorridor || r.Cells.Count < 20) continue;
                n++;
                if (ShapeClass(p, r) != "Rect") non++;
            }

            return n == 0 ? 0 : (double)non / n;
        }

        // Rooms whose only door(s) lead into one parent room: parents with at least `minInner` such inner rooms.
        public static int CountSuites(BuildingPlan p, int minInner)
        {
            int[] linkCount = new int[p.Rooms.Count];
            int[] parent = new int[p.Rooms.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = -1;
            foreach (PlanLink l in p.Links)
            {
                linkCount[l.RoomA]++;
                if (l.RoomB >= 0) linkCount[l.RoomB]++;
            }

            foreach (PlanLink l in p.Links)
            {
                if (l.RoomB < 0) continue;
                if (linkCount[l.RoomA] == 1 && !p.Rooms[l.RoomA].IsCorridor && !p.Rooms[l.RoomB].IsCorridor) parent[l.RoomA] = l.RoomB;
                if (linkCount[l.RoomB] == 1 && !p.Rooms[l.RoomB].IsCorridor && !p.Rooms[l.RoomA].IsCorridor) parent[l.RoomB] = l.RoomA;
            }

            int[] inner = new int[p.Rooms.Count];
            for (int i = 0; i < parent.Length; i++) if (parent[i] >= 0) inner[parent[i]]++;
            int count = 0;
            for (int i = 0; i < inner.Length; i++)
            {
                if (minInner >= 2 ? inner[i] >= 2 : inner[i] == 1) count++;
            }

            return count;
        }

        // True when the building's footprint fills its whole bounding box (no steps, notches or courtyards).
        public static bool IsPlainRectangle(BuildingPlan plan, PlanBuilding b)
        {
            for (int y = b.MinY; y <= b.MaxY; y++)
                for (int x = b.MinX; x <= b.MaxX; x++)
                    if (plan.At(x, y) == PlanTile.Outside) return false;
            return true;
        }

        static bool IsSolid(PlanTile t) { return t == PlanTile.Wall || t == PlanTile.Window; }
    }
}
