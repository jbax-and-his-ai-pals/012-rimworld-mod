using System;
using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    public enum SectorKind { Courtyard, Hall, Storage, Security, HotZone, Diagnostic, Valve, Reactor }
    public enum GateKind { Open, Terminal }

    public static class Progression
    {
        public const int GatePoints = 1;        // hacking a terminal-gated edge
        public const int DiagnosticPoints = 2;  // using a Diagnostic sector's node
        public const int ValvePoints = 1;       // using the Valve sector's valve
        public const int ReactorPoints = 4;     // bringing the Reactor online
        public const int MaxTier = 3;

        public static int TierForPoints(int points)
        {
            if (points < 2) return 0;
            if (points < 5) return 1;
            if (points < 9) return 2;
            return 3;
        }
    }

    public sealed class SectorBlock
    {
        public int Id;                 // index in graph.Blocks, == Gy*Nx+Gx
        public int Gx, Gy;             // grid coordinates, (0,0) is bottom-left (south-west)
        public int Depth;              // tree depth, root = 0
        public int ParentId;           // -1 for root
        public List<int> Children = new List<int>();
        public SectorKind Kind;
        public GateKind Gate;          // gate on the doorway from the parent INTO this block
        public int RequiredTier;       // clearance tier needed to hack this block's gate
        public bool EvaCache;          // true on exactly one Storage block that holds the EVA suits
    }

    public sealed class SimulationResult
    {
        public bool AllReachable;
        public int FinalPoints;
        public int FinalTier;
        public List<int> UnreachableIds = new List<int>();
        public string Report;
    }

    public sealed class SectorGraph
    {
        public int Nx, Ny, Seed;
        private readonly List<SectorBlock> blocks = new List<SectorBlock>();
        public IReadOnlyList<SectorBlock> Blocks { get { return blocks; } }
        public SectorBlock Root { get { return blocks[(Ny / 2) * Nx]; } }

        public SectorBlock At(int gx, int gy)
        {
            if (gx < 0 || gy < 0 || gx >= Nx || gy >= Ny) return null;
            return blocks[gy * Nx + gx];
        }

        // ---------------------------------------------------------------- PRNG
        private sealed class Rng
        {
            private uint s;
            public Rng(int seed)
            {
                // splitmix32-style scramble
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
            public void Shuffle<T>(List<T> l)
            {
                for (int i = l.Count - 1; i > 0; i--)
                {
                    int j = Next(i + 1);
                    T t = l[i]; l[i] = l[j]; l[j] = t;
                }
            }
        }

        // ---------------------------------------------------------------- generation
        public static SectorGraph Generate(int nx, int ny, int seed)
        {
            if (nx < 2) throw new ArgumentOutOfRangeException("nx", "nx must be >= 2");
            if (ny < 2) throw new ArgumentOutOfRangeException("ny", "ny must be >= 2");

            SectorGraph last = null;
            for (int attempt = 0; attempt <= 50; attempt++)
            {
                int s = attempt == 0 ? seed : unchecked(seed * 31 + attempt);
                last = Build(nx, ny, s);
                last.Seed = seed;
                if (last.Simulate().AllReachable) return last;
            }

            // last resort: lower every terminal tier until solvable
            for (int pass = 0; pass < Progression.MaxTier; pass++)
            {
                bool any = false;
                foreach (var b in last.blocks)
                    if (b.Gate == GateKind.Terminal && b.RequiredTier > 0) { b.RequiredTier--; any = true; }
                if (last.Simulate().AllReachable) return last;
                if (!any) break;
            }

            // absolute fallback: remove heat hazard and valve
            foreach (var b in last.blocks)
                if (b.Kind == SectorKind.HotZone || b.Kind == SectorKind.Valve) b.Kind = SectorKind.Hall;
            return last;
        }

        private static SectorGraph Build(int nx, int ny, int seed)
        {
            var g = new SectorGraph { Nx = nx, Ny = ny, Seed = seed };
            var rng = new Rng(seed);
            int n = nx * ny;
            for (int i = 0; i < n; i++)
                g.blocks.Add(new SectorBlock { Id = i, Gx = i % nx, Gy = i / nx, ParentId = -1, Kind = SectorKind.Hall, Gate = GateKind.Open });

            g.BuildTree(rng);
            g.AssignGates(rng);
            g.AssignRoles(rng);
            return g;
        }

        private void BuildTree(Rng rng)
        {
            int n = blocks.Count;
            var visited = new bool[n];
            var active = new List<int>();
            int rootId = (Ny / 2) * Nx;
            visited[rootId] = true;
            active.Add(rootId);
            blocks[rootId].Depth = 0;
            blocks[rootId].ParentId = -1;
            int[] dx = { 1, 0, -1, 0 };
            int[] dy = { 0, 1, 0, -1 };
            var nb = new List<int>();
            while (active.Count > 0)
            {
                int idx = rng.Next(100) < 70 ? active.Count - 1 : rng.Next(active.Count);
                var b = blocks[active[idx]];
                nb.Clear();
                for (int d = 0; d < 4; d++)
                {
                    var o = At(b.Gx + dx[d], b.Gy + dy[d]);
                    if (o != null && !visited[o.Id]) nb.Add(o.Id);
                }
                if (nb.Count == 0) { active.RemoveAt(idx); continue; }
                var c = blocks[nb[rng.Next(nb.Count)]];
                visited[c.Id] = true;
                c.ParentId = b.Id;
                c.Depth = b.Depth + 1;
                b.Children.Add(c.Id);
                active.Add(c.Id);
            }
        }

        private void AssignGates(Rng rng)
        {
            foreach (var b in blocks)
            {
                if (b.ParentId < 0) { b.Gate = GateKind.Open; b.RequiredTier = 0; continue; }
                if (rng.Next(100) < 45) { b.Gate = GateKind.Open; b.RequiredTier = 0; continue; }
                b.Gate = GateKind.Terminal;
                int t = b.Depth <= 2 ? 0 : b.Depth <= 5 ? 1 : b.Depth <= 9 ? 2 : 3;
                b.RequiredTier = Math.Min(t, Progression.MaxTier);
            }
        }

        private bool PathHas(int id, Func<SectorBlock, bool> pred)
        {
            while (id >= 0)
            {
                var b = blocks[id];
                if (pred(b)) return true;
                id = b.ParentId;
            }
            return false;
        }

        private static List<SectorBlock> Take(List<SectorBlock> cand, int count)
        {
            var r = new List<SectorBlock>();
            for (int i = 0; i < cand.Count && r.Count < count; i++) r.Add(cand[i]);
            return r;
        }

        private void AssignRoles(Rng rng)
        {
            int n = blocks.Count;
            foreach (var b in blocks) { b.Kind = SectorKind.Hall; b.EvaCache = false; }
            Root.Kind = SectorKind.Courtyard;

            var leaves = new List<SectorBlock>();
            foreach (var b in blocks) if (b.ParentId >= 0 && b.Children.Count == 0) leaves.Add(b);

            // Reactor: deepest leaf, lowest Id
            SectorBlock reactor = null;
            foreach (var l in leaves) if (reactor == null || l.Depth > reactor.Depth) reactor = l;
            reactor.Kind = SectorKind.Reactor;

            // Diagnostics: ceil(n/8), shallow leaves first
            int diagCount = (n + 7) / 8;
            var diagCand = new List<SectorBlock>();
            foreach (var l in leaves) if (l != reactor) diagCand.Add(l);
            SortByDepth(diagCand);
            var diagNon = new List<SectorBlock>();
            foreach (var b in blocks) if (b.ParentId >= 0 && b.Children.Count > 0) diagNon.Add(b);
            SortByDepth(diagNon);
            diagCand.AddRange(diagNon);
            foreach (var d in Take(diagCand, diagCount)) d.Kind = SectorKind.Diagnostic;

            // HotZone + Valve
            if (n >= 12)
            {
                var valveCand = new List<SectorBlock>();
                foreach (var l in leaves) if (l.Kind == SectorKind.Hall) valveCand.Add(l);
                rng.Shuffle(valveCand);
                foreach (var v in valveCand)
                {
                    var pathIds = new HashSet<int>();
                    for (int id = v.Id; id >= 0; id = blocks[id].ParentId) pathIds.Add(id);
                    var hotCand = new List<SectorBlock>();
                    foreach (var b in blocks)
                        if (b.Depth >= 3 && b.Children.Count > 0 && b.Kind == SectorKind.Hall && !pathIds.Contains(b.Id))
                            hotCand.Add(b);
                    if (hotCand.Count == 0) continue;
                    rng.Shuffle(hotCand);
                    int hotCount = 1 + rng.Next(Math.Max(1, n / 10));
                    foreach (var h in Take(hotCand, hotCount)) h.Kind = SectorKind.HotZone;
                    v.Kind = SectorKind.Valve;
                    break;
                }
            }

            // Security
            var free = new List<SectorBlock>();
            foreach (var b in blocks) if (b.Depth >= 1 && b.Kind == SectorKind.Hall) free.Add(b);
            rng.Shuffle(free);
            foreach (var s in Take(free, n / 6)) s.Kind = SectorKind.Security;

            // Storage: prefer unassigned leaves
            var stLeaves = new List<SectorBlock>();
            var stOthers = new List<SectorBlock>();
            foreach (var b in blocks)
            {
                if (b.Depth < 1 || b.Kind != SectorKind.Hall) continue;
                if (b.Children.Count == 0) stLeaves.Add(b); else stOthers.Add(b);
            }
            rng.Shuffle(stLeaves);
            rng.Shuffle(stOthers);
            stLeaves.AddRange(stOthers);
            var storages = Take(stLeaves, n / 6);
            foreach (var s in storages) s.Kind = SectorKind.Storage;

            Func<SectorBlock, bool> isHot = x => x.Kind == SectorKind.HotZone;
            SectorBlock eva = null;
            var evaCand = new List<SectorBlock>();
            foreach (var s in storages) if (!PathHas(s.Id, isHot)) evaCand.Add(s);
            if (evaCand.Count > 0) eva = evaCand[rng.Next(evaCand.Count)];
            else if (storages.Count > 0 || HasKind(SectorKind.HotZone))
            {
                var conv = new List<SectorBlock>();
                foreach (var b in blocks)
                    if (b.Depth >= 1 && b.Kind == SectorKind.Hall && !PathHas(b.Id, isHot)) conv.Add(b);
                if (conv.Count == 0)
                    foreach (var b in blocks)
                        if (b.Depth >= 1 && b.Kind == SectorKind.Security && !PathHas(b.Id, isHot)) conv.Add(b);
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
            foreach (var b in blocks) if (b.Kind == k) return true;
            return false;
        }

        private static void SortByDepth(List<SectorBlock> l)
        {
            // stable insertion sort by (Depth, Id) -- deterministic across runtimes
            for (int i = 1; i < l.Count; i++)
            {
                var x = l[i];
                int j = i - 1;
                while (j >= 0 && (l[j].Depth > x.Depth || (l[j].Depth == x.Depth && l[j].Id > x.Id)))
                {
                    l[j + 1] = l[j]; j--;
                }
                l[j + 1] = x;
            }
        }

        // ---------------------------------------------------------------- simulation
        public SimulationResult Simulate()
        {
            var visited = new bool[blocks.Count];
            visited[Root.Id] = true;
            int points = 0;
            bool coolant = false, hasEva = false;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var b in blocks)
                {
                    if (visited[b.Id] || b.ParentId < 0 || !visited[b.ParentId]) continue;
                    int tier = Progression.TierForPoints(points);
                    if (b.Gate == GateKind.Terminal && tier < b.RequiredTier) continue;
                    if (b.Kind == SectorKind.HotZone && !(coolant || hasEva)) continue;

                    visited[b.Id] = true;
                    changed = true;
                    if (b.Gate == GateKind.Terminal) points += Progression.GatePoints;
                    switch (b.Kind)
                    {
                        case SectorKind.Diagnostic: points += Progression.DiagnosticPoints; break;
                        case SectorKind.Valve: points += Progression.ValvePoints; coolant = true; break;
                        case SectorKind.Reactor: points += Progression.ReactorPoints; break;
                        case SectorKind.Storage: if (b.EvaCache) hasEva = true; break;
                    }
                }
            }

            var res = new SimulationResult();
            foreach (var b in blocks) if (!visited[b.Id]) res.UnreachableIds.Add(b.Id);
            res.AllReachable = res.UnreachableIds.Count == 0;
            res.FinalPoints = points;
            res.FinalTier = Progression.TierForPoints(points);
            var sb = new StringBuilder();
            sb.AppendLine("Sector graph " + Nx + "x" + Ny + " seed " + Seed + ": " + blocks.Count + " blocks");
            sb.AppendLine("Reachable: " + (blocks.Count - res.UnreachableIds.Count) + "/" + blocks.Count + (res.AllReachable ? " (all)" : ""));
            sb.AppendLine("Points " + points + ", tier " + res.FinalTier + ", coolant " + coolant + ", EVA " + hasEva);
            if (!res.AllReachable) sb.AppendLine("Unreachable: " + string.Join(",", res.UnreachableIds.ConvertAll(x => x.ToString()).ToArray()));
            res.Report = sb.ToString();
            return res;
        }
    }
}
