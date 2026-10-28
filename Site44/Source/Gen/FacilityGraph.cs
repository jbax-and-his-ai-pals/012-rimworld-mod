using System;
using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    // A progression sector ("zone"): a connected group of plan rooms.
    public sealed class Zone
    {
        public int Id;
        public List<int> RoomIds = new List<int>();     // plan rooms in this zone
        public int CellCount;                           // total Floor cells
        public int Depth;                               // tree depth, root = 0
        public int ParentId;                            // -1 for root
        public List<int> Children = new List<int>();
        public SectorKind Kind;
        public GateKind Gate;                           // gate on the entry from the parent (root: Open, unused)
        public int RequiredTier;                        // clearance tier for a Terminal gate (0..Progression.MaxTier)
        public int GateLink = -1;                       // index into plan.Links of the Door tile chosen as this zone's gate from its parent (-1 for root)
        public bool EvaCache;                           // exactly one Storage zone (reachable without crossing a HotZone) holds the EVA suits
    }

    // Link classification (PlanLink = a Door tile):
    //  - FREE:   interior link whose two rooms are in the same zone. Untouched by the game.
    //  - GATE:   exactly one link per non-root zone, Zone.GateLink. Either an interior link between the zone and its
    //            tree parent, or (ParentId == Root.Id only) an exterior link (RoomB == -1) of the zone itself.
    //  - SEALED: every other cross-zone interior link, and every exterior link that is not a gate, except the main
    //            entrance. The game turns SealedLinks into walls, so the only ways in are gates and the main entrance.
    //  - The main entrance link (Cell == plan.MainEntrance) belongs to the Root lobby and is never gated or sealed.
    // The outside (yards) is conceptually part of the Root zone, so an exterior link of zone Z is an adjacency Z <-> Root.
    public sealed class FacilityGraph
    {
        public BuildingPlan Plan { get; }
        public IReadOnlyList<Zone> Zones { get { return zones; } }
        public Zone Root { get { return zones[0]; } }
        public IReadOnlyList<int> SealedLinks { get { return sealedLinks; } }
        public int Seed { get; private set; }

        private readonly List<Zone> zones = new List<Zone>();
        private readonly List<int> sealedLinks = new List<int>();
        private readonly int[] roomZone;
        private readonly Dictionary<int, int> doorAt = new Dictionary<int, int>();           // tile index -> link index
        private readonly Dictionary<long, List<int>> pairLinks = new Dictionary<long, List<int>>();
        private readonly List<int>[] neighbours;
        private readonly List<int>[] extLinks;          // per zone: non-main exterior links of the zone's rooms
        private readonly int[] zoneComp;                // interior-connected component per zone (0 = the one holding Root)
        private bool[] hasServer, hasMachine, hasSecurity, hasStore, corridorHeavy;

        // The interior room of an exterior link (exactly one side is -1), else -1.
        internal static int ExteriorRoom(BuildingPlan plan, PlanLink l)
        {
            int rc = plan.Rooms.Count;
            bool a = l.RoomA >= 0 && l.RoomA < rc, b = l.RoomB >= 0 && l.RoomB < rc;
            if (a && l.RoomB < 0) return l.RoomA;
            if (b && l.RoomA < 0) return l.RoomB;
            return -1;
        }

        internal static bool IsMainEntrance(BuildingPlan plan, PlanLink l)
        {
            return l.Cell.X == plan.MainEntrance.X && l.Cell.Y == plan.MainEntrance.Y;
        }

        public int ZoneOfRoom(int roomId)
        {
            if (roomId < 0 || roomId >= roomZone.Length) return -1;
            return roomZone[roomId];
        }

        public Zone ZoneAt(int x, int y)
        {
            if (!Plan.InBounds(x, y)) return null;
            int idx = y * Plan.Width + x;
            switch (Plan.Tiles[idx])
            {
                case PlanTile.Floor:
                {
                    int r = Plan.RoomIds[idx];
                    return r >= 0 && r < roomZone.Length ? zones[roomZone[r]] : null;
                }
                case PlanTile.Door:
                {
                    int li;
                    if (!doorAt.TryGetValue(idx, out li)) return null;
                    PlanLink dl = Plan.Links[li];
                    int r = dl.RoomA >= 0 ? dl.RoomA : dl.RoomB;
                    return r >= 0 && r < roomZone.Length ? zones[roomZone[r]] : null;
                }
                default:
                    return null;
            }
        }

        // ---------------------------------------------------------------- construction
        private FacilityGraph(BuildingPlan plan, int[] roomZone, int zoneCount, int seed)
        {
            Plan = plan;
            Seed = seed;
            this.roomZone = roomZone;
            for (int i = 0; i < zoneCount; i++)
                zones.Add(new Zone { Id = i, ParentId = -1, Kind = SectorKind.Hall, Gate = GateKind.Open });
            for (int r = 0; r < roomZone.Length; r++)
            {
                Zone z = zones[roomZone[r]];
                z.RoomIds.Add(r);
                z.CellCount += plan.Rooms[r].Cells.Count;
            }

            extLinks = new List<int>[zoneCount];
            for (int i = 0; i < zoneCount; i++) extLinks[i] = new List<int>();
            for (int li = 0; li < plan.Links.Count; li++)
            {
                PlanLink l = plan.Links[li];
                int er = ExteriorRoom(plan, l);
                if (er >= 0 && !IsMainEntrance(plan, l)) extLinks[roomZone[er]].Add(li);
                if (plan.InBounds(l.Cell.X, l.Cell.Y))
                {
                    int idx = l.Cell.Y * plan.Width + l.Cell.X;
                    if (!doorAt.ContainsKey(idx)) doorAt[idx] = li;
                }
                if (l.RoomA < 0 || l.RoomB < 0 || l.RoomA >= roomZone.Length || l.RoomB >= roomZone.Length) continue;
                int za = roomZone[l.RoomA], zb = roomZone[l.RoomB];
                if (za == zb) continue;
                long key = PairKey(za, zb);
                List<int> list;
                if (!pairLinks.TryGetValue(key, out list)) { list = new List<int>(); pairLinks[key] = list; }
                list.Add(li);
            }

            neighbours = new List<int>[zoneCount];
            for (int i = 0; i < zoneCount; i++) neighbours[i] = new List<int>();
            for (int a = 0; a < zoneCount; a++)
                for (int b = a + 1; b < zoneCount; b++)
                    if (pairLinks.ContainsKey(PairKey(a, b))) { neighbours[a].Add(b); neighbours[b].Add(a); }

            zoneComp = new int[zoneCount];
            for (int i = 0; i < zoneCount; i++) zoneComp[i] = -1;
            int comps = 0;
            for (int st = 0; st < zoneCount; st++)
            {
                if (zoneComp[st] >= 0) continue;
                var q = new List<int> { st };
                zoneComp[st] = comps;
                for (int qi = 0; qi < q.Count; qi++)
                    foreach (int o in neighbours[q[qi]])
                        if (zoneComp[o] < 0) { zoneComp[o] = comps; q.Add(o); }
                comps++;
            }

            ComputeFeatures();
        }

        private static long PairKey(int a, int b)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        private static bool IsMachine(RoomFunction f) { return f == RoomFunction.Mechanical || f == RoomFunction.MachineHall || f.ToString() == "Boiler"; }
        private static bool IsStore(RoomFunction f) { return f == RoomFunction.Storeroom || f == RoomFunction.Archive || f.ToString() == "Warehouse"; }

        private void ComputeFeatures()
        {
            int n = zones.Count;
            hasServer = new bool[n]; hasMachine = new bool[n]; hasSecurity = new bool[n]; hasStore = new bool[n]; corridorHeavy = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int corridorCells = 0;
                foreach (int r in zones[i].RoomIds)
                {
                    PlanRoom room = Plan.Rooms[r];
                    if (room.Function == RoomFunction.ServerRoom) hasServer[i] = true;
                    if (IsMachine(room.Function)) hasMachine[i] = true;
                    if (room.Function == RoomFunction.SecurityPost) hasSecurity[i] = true;
                    if (IsStore(room.Function)) hasStore[i] = true;
                    if (room.IsCorridor) corridorCells += room.Cells.Count;
                }
                corridorHeavy[i] = zones[i].CellCount > 0 && corridorCells * 100 >= 35 * zones[i].CellCount;
            }
        }

        // ---------------------------------------------------------------- generation
        public static FacilityGraph Build(BuildingPlan plan, int targetZoneCount, int seed)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (plan.Rooms.Count == 0) throw new ArgumentException("plan has no rooms", "plan");

            int rootRoom = FacilityPartition.FindRootRoom(plan);
            int zc;
            int[] rz = FacilityPartition.Run(plan, targetZoneCount, seed, rootRoom, out zc);

            FacilityGraph last = null;
            for (int attempt = 0; attempt <= 50; attempt++)
            {
                int s = attempt == 0 ? seed : unchecked(seed * 31 + attempt);
                last = new FacilityGraph(plan, rz, zc, seed);
                last.Generate(s);
                if (last.Simulate().AllReachable) return last;
            }

            // last resort: lower every terminal tier until solvable
            for (int pass = 0; pass < Progression.MaxTier; pass++)
            {
                bool any = false;
                foreach (Zone z in last.zones)
                    if (z.Gate == GateKind.Terminal && z.RequiredTier > 0) { z.RequiredTier--; any = true; }
                if (last.Simulate().AllReachable) return last;
                if (!any) break;
            }

            // absolute fallback: remove heat hazard and valve
            foreach (Zone z in last.zones)
                if (z.Kind == SectorKind.HotZone || z.Kind == SectorKind.Valve) z.Kind = SectorKind.Hall;
            return last;
        }

        private void Generate(int seed)
        {
            var rng = new FacilityRng(seed);
            BuildTree(rng);
            ChooseGateLinks(rng);
            AssignGates(rng);
            AssignRoles(rng);
            ComputeSealed();
        }

        private void BuildTree(FacilityRng rng)
        {
            int n = zones.Count;
            var visited = new bool[n];
            var attached = new bool[n];          // per interior component: already linked to the tree
            var active = new List<int>();
            visited[0] = true;
            attached[zoneComp[0]] = true;
            active.Add(0);
            var nb = new List<int>();
            while (active.Count > 0)
            {
                int idx = rng.Next(100) < 70 ? active.Count - 1 : rng.Next(active.Count);
                Zone b = zones[active[idx]];
                nb.Clear();
                foreach (int o in neighbours[b.Id]) if (!visited[o]) nb.Add(o);
                if (b.Id == 0)
                {
                    // exterior doors: the yards are part of Root. Each isolated component is entered through exactly one.
                    for (int z = 1; z < n; z++)
                        if (!visited[z] && extLinks[z].Count > 0 && !nb.Contains(z) && (zoneComp[z] == zoneComp[0] || !attached[zoneComp[z]]))
                            nb.Add(z);
                    nb.Sort();
                }
                if (nb.Count == 0) { active.RemoveAt(idx); continue; }
                Zone c = zones[nb[rng.Next(nb.Count)]];
                visited[c.Id] = true;
                attached[zoneComp[c.Id]] = true;
                c.ParentId = b.Id;
                c.Depth = b.Depth + 1;
                b.Children.Add(c.Id);
                active.Add(c.Id);
            }

            // pathological: an interior component with no exterior door at all is hung off Root with no gate link
            for (int z = 1; z < n; z++)
            {
                if (visited[z]) continue;
                var q = new List<int> { z };
                visited[z] = true;
                zones[z].ParentId = 0; zones[z].Depth = 1; zones[0].Children.Add(z);
                for (int qi = 0; qi < q.Count; qi++)
                    foreach (int o in neighbours[q[qi]])
                        if (!visited[o])
                        {
                            visited[o] = true;
                            zones[o].ParentId = q[qi]; zones[o].Depth = zones[q[qi]].Depth + 1; zones[q[qi]].Children.Add(o);
                            q.Add(o);
                        }
            }
        }

        // 75%: a corridor-adjacent door (either room IsCorridor), Fire doors preferred when present; otherwise any door link.
        private void ChooseGateLinks(FacilityRng rng)
        {
            for (int i = 1; i < zones.Count; i++)
            {
                Zone z = zones[i];
                if (z.ParentId < 0) continue;
                var all = new List<int>();
                List<int> inter;
                if (pairLinks.TryGetValue(PairKey(z.ParentId, z.Id), out inter)) all.AddRange(inter);
                // Gates sit on internal doors; an exterior door is only a fallback when a zone has no internal link.
                if (z.ParentId == 0 && all.Count == 0) all.AddRange(extLinks[z.Id]);
                if (all.Count == 0) { z.GateLink = -1; continue; }
                bool preferCorridor = rng.Next(100) < 75;
                List<int> pool = all;
                if (preferCorridor)
                {
                    var corr = new List<int>();
                    foreach (int li in all)
                    {
                        PlanLink l = Plan.Links[li];
                        int er = ExteriorRoom(Plan, l);
                        bool isCorr = er >= 0 ? Plan.Rooms[er].IsCorridor : (Plan.Rooms[l.RoomA].IsCorridor || Plan.Rooms[l.RoomB].IsCorridor);
                        if (isCorr) corr.Add(li);
                    }
                    if (corr.Count > 0)
                    {
                        var fire = new List<int>();
                        foreach (int li in corr) if (Plan.Links[li].Fire) fire.Add(li);
                        pool = fire.Count > 0 ? fire : corr;
                    }
                }
                z.GateLink = pool[rng.Next(pool.Count)];
            }
        }

        private void AssignGates(FacilityRng rng)
        {
            foreach (Zone z in zones)
            {
                if (z.ParentId < 0) { z.Gate = GateKind.Open; z.RequiredTier = 0; continue; }
                if (rng.Next(100) < 45) { z.Gate = GateKind.Open; z.RequiredTier = 0; continue; }
                z.Gate = GateKind.Terminal;
                int t = z.Depth <= 2 ? 0 : z.Depth <= 5 ? 1 : z.Depth <= 9 ? 2 : 3;
                z.RequiredTier = Math.Min(t, Progression.MaxTier);
            }
        }

        private void ComputeSealed()
        {
            sealedLinks.Clear();
            var gates = new HashSet<int>();
            foreach (Zone z in zones) if (z.GateLink >= 0) gates.Add(z.GateLink);
            for (int li = 0; li < Plan.Links.Count; li++)
            {
                PlanLink l = Plan.Links[li];
                if (gates.Contains(li)) continue;
                if (ExteriorRoom(Plan, l) >= 0)
                {
                    if (!IsMainEntrance(Plan, l)) sealedLinks.Add(li);
                    continue;
                }
                if (l.RoomA < 0 || l.RoomB < 0 || l.RoomA >= roomZone.Length || l.RoomB >= roomZone.Length) continue;
                if (roomZone[l.RoomA] == roomZone[l.RoomB]) continue;
                sealedLinks.Add(li);
            }
        }

        // ---------------------------------------------------------------- roles
        private bool PathHas(int id, Func<Zone, bool> pred)
        {
            while (id >= 0)
            {
                Zone z = zones[id];
                if (pred(z)) return true;
                id = z.ParentId;
            }
            return false;
        }

        private static List<Zone> Take(List<Zone> cand, int count)
        {
            var r = new List<Zone>();
            for (int i = 0; i < cand.Count && r.Count < count; i++) r.Add(cand[i]);
            return r;
        }

        private static int B(bool preferred) { return preferred ? 0 : 1; }

        private void AssignRoles(FacilityRng rng)
        {
            int n = zones.Count;
            foreach (Zone z in zones) { z.Kind = SectorKind.Hall; z.EvaCache = false; }
            Root.Kind = SectorKind.Courtyard;
            if (n < 2) return;

            var rk = new int[n];
            for (int i = 0; i < n; i++) rk[i] = rng.Next(1 << 20);

            var leaves = new List<Zone>();
            foreach (Zone z in zones) if (z.ParentId >= 0 && z.Children.Count == 0) leaves.Add(z);
            if (leaves.Count == 0) return;

            // Reactor: deepest leaf; among the deepest few prefer one with machinery
            leaves.Sort((a, b) => a.Depth != b.Depth ? b.Depth.CompareTo(a.Depth) : a.Id.CompareTo(b.Id));
            Zone reactor = leaves[0];
            for (int i = 0; i < leaves.Count && i < 3; i++)
                if (hasMachine[leaves[i].Id]) { reactor = leaves[i]; break; }
            reactor.Kind = SectorKind.Reactor;

            // Diagnostics: ceil(n/8), prefer server rooms, then leaves, then shallow
            int diagCount = (n + 7) / 8;
            var diag = new List<Zone>();
            foreach (Zone z in zones) if (z.ParentId >= 0 && z != reactor) diag.Add(z);
            diag.Sort((a, b) =>
            {
                int c = B(hasServer[a.Id]).CompareTo(B(hasServer[b.Id]));
                if (c != 0) return c;
                c = B(a.Children.Count == 0).CompareTo(B(b.Children.Count == 0));
                if (c != 0) return c;
                c = a.Depth.CompareTo(b.Depth);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            foreach (Zone d in Take(diag, diagCount)) d.Kind = SectorKind.Diagnostic;

            // HotZone + Valve
            if (n >= 12)
            {
                var valveCand = new List<Zone>();
                foreach (Zone l in leaves) if (l.Kind == SectorKind.Hall) valveCand.Add(l);
                valveCand.Sort((a, b) =>
                {
                    int c = B(hasMachine[a.Id]).CompareTo(B(hasMachine[b.Id]));
                    if (c != 0) return c;
                    c = rk[a.Id].CompareTo(rk[b.Id]);
                    return c != 0 ? c : a.Id.CompareTo(b.Id);
                });
                foreach (Zone v in valveCand)
                {
                    var pathIds = new HashSet<int>();
                    for (int id = v.Id; id >= 0; id = zones[id].ParentId) pathIds.Add(id);
                    var hotCand = new List<Zone>();
                    foreach (Zone z in zones)
                        if (z.Depth >= 3 && z.Children.Count > 0 && z.Kind == SectorKind.Hall && !pathIds.Contains(z.Id))
                            hotCand.Add(z);
                    if (hotCand.Count == 0) continue;
                    hotCand.Sort((a, b) =>
                    {
                        int c = B(corridorHeavy[a.Id]).CompareTo(B(corridorHeavy[b.Id]));
                        if (c != 0) return c;
                        c = rk[b.Id].CompareTo(rk[a.Id]);
                        return c != 0 ? c : a.Id.CompareTo(b.Id);
                    });
                    int hotCount = 1 + rng.Next(Math.Max(1, n / 10));
                    foreach (Zone h in Take(hotCand, hotCount)) h.Kind = SectorKind.HotZone;
                    v.Kind = SectorKind.Valve;
                    break;
                }
            }

            // Security
            var free = new List<Zone>();
            foreach (Zone z in zones) if (z.Depth >= 1 && z.Kind == SectorKind.Hall) free.Add(z);
            free.Sort((a, b) =>
            {
                int c = B(hasSecurity[a.Id]).CompareTo(B(hasSecurity[b.Id]));
                if (c != 0) return c;
                c = rk[a.Id].CompareTo(rk[b.Id]);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            foreach (Zone s in Take(free, n / 6)) s.Kind = SectorKind.Security;

            // Storage: prefer zones with store rooms, then leaves
            var st = new List<Zone>();
            foreach (Zone z in zones) if (z.Depth >= 1 && z.Kind == SectorKind.Hall) st.Add(z);
            st.Sort((a, b) =>
            {
                int c = B(hasStore[a.Id]).CompareTo(B(hasStore[b.Id]));
                if (c != 0) return c;
                c = B(a.Children.Count == 0).CompareTo(B(b.Children.Count == 0));
                if (c != 0) return c;
                c = rk[a.Id].CompareTo(rk[b.Id]);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            List<Zone> storages = Take(st, n / 6);
            foreach (Zone s in storages) s.Kind = SectorKind.Storage;

            Func<Zone, bool> isHot = x => x.Kind == SectorKind.HotZone;
            Zone eva = null;
            var evaCand = new List<Zone>();
            foreach (Zone s in storages) if (!PathHas(s.Id, isHot)) evaCand.Add(s);
            if (evaCand.Count > 0) eva = evaCand[rng.Next(evaCand.Count)];
            else if (storages.Count > 0 || HasKind(SectorKind.HotZone))
            {
                var conv = new List<Zone>();
                foreach (Zone z in zones)
                    if (z.Depth >= 1 && z.Kind == SectorKind.Hall && !PathHas(z.Id, isHot)) conv.Add(z);
                if (conv.Count == 0)
                    foreach (Zone z in zones)
                        if (z.Depth >= 1 && z.Kind == SectorKind.Security && !PathHas(z.Id, isHot)) conv.Add(z);
                if (conv.Count > 0)
                {
                    eva = conv[rng.Next(conv.Count)];
                    eva.Kind = SectorKind.Storage;
                }
            }
            if (eva != null) eva.EvaCache = true;
        }

        private bool HasKind(SectorKind k)
        {
            foreach (Zone z in zones) if (z.Kind == k) return true;
            return false;
        }

        // ---------------------------------------------------------------- simulation
        public SimulationResult Simulate()
        {
            var visited = new bool[zones.Count];
            visited[0] = true;
            int points = 0;
            bool coolant = false, hasEva = false;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (Zone z in zones)
                {
                    if (visited[z.Id] || z.ParentId < 0 || !visited[z.ParentId]) continue;
                    int tier = Progression.TierForPoints(points);
                    if (z.Gate == GateKind.Terminal && tier < z.RequiredTier) continue;
                    if (z.Kind == SectorKind.HotZone && !(coolant || hasEva)) continue;

                    visited[z.Id] = true;
                    changed = true;
                    if (z.Gate == GateKind.Terminal) points += Progression.GatePoints;
                    switch (z.Kind)
                    {
                        case SectorKind.Diagnostic: points += Progression.DiagnosticPoints; break;
                        case SectorKind.Valve: points += Progression.ValvePoints; coolant = true; break;
                        case SectorKind.Reactor: points += Progression.ReactorPoints; break;
                        case SectorKind.Storage: if (z.EvaCache) hasEva = true; break;
                    }
                }
            }

            var res = new SimulationResult();
            foreach (Zone z in zones) if (!visited[z.Id]) res.UnreachableIds.Add(z.Id);
            res.AllReachable = res.UnreachableIds.Count == 0;
            res.FinalPoints = points;
            res.FinalTier = Progression.TierForPoints(points);
            var sb = new StringBuilder();
            sb.AppendLine("Facility graph seed " + Seed + ": " + zones.Count + " zones");
            sb.AppendLine("Reachable: " + (zones.Count - res.UnreachableIds.Count) + "/" + zones.Count + (res.AllReachable ? " (all)" : ""));
            sb.AppendLine("Points " + points + ", tier " + res.FinalTier + ", coolant " + coolant + ", EVA " + hasEva);
            if (!res.AllReachable) sb.AppendLine("Unreachable: " + string.Join(",", res.UnreachableIds.ConvertAll(x => x.ToString()).ToArray()));
            res.Report = sb.ToString();
            return res;
        }
    }
}
