using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    // Small deterministic PRNG (xorshift32 with a splitmix-style seed scramble). No System.Random.
    internal sealed class FacilityRng
    {
        private uint s;

        public FacilityRng(int seed)
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
    }

    // Carves a BuildingPlan's rooms into connected zones along its Door links.
    internal static class FacilityPartition
    {
        private sealed class Edge
        {
            public int A, B, Link, Score;
        }

        // Which cuts are good: 3 = fire door or between buildings, 2 = corridor <-> room boundary,
        // 1 = corridor <-> corridor, 0 = inside a room cluster (room <-> room).
        private static int EdgeScore(BuildingPlan plan, PlanLink l)
        {
            PlanRoom a = plan.Rooms[l.RoomA];
            PlanRoom b = plan.Rooms[l.RoomB];
            if (l.Fire || a.BuildingId != b.BuildingId) return 3;
            if (a.IsCorridor != b.IsCorridor) return 2;
            if (a.IsCorridor) return 1;
            return 0;
        }

        public static int FindRootRoom(BuildingPlan plan)
        {
            int rc = plan.Rooms.Count;
            for (int i = 0; i < plan.Links.Count; i++)
            {
                PlanLink l = plan.Links[i];
                if (l.Cell.X == plan.MainEntrance.X && l.Cell.Y == plan.MainEntrance.Y)
                {
                    if (l.RoomA >= 0 && l.RoomA < rc) return l.RoomA;
                    if (l.RoomB >= 0 && l.RoomB < rc) return l.RoomB;
                }
            }
            for (int i = 0; i < rc; i++)
            {
                if (plan.Rooms[i].Function == RoomFunction.Lobby && plan.Rooms[i].BuildingId == 0) return i;
            }
            for (int i = 0; i < rc; i++)
            {
                if (plan.Rooms[i].Function == RoomFunction.Lobby) return i;
            }
            return 0;
        }

        // Returns room -> zone index. Zone 0 is the zone holding rootRoom; zones are otherwise numbered by lowest room id.
        public static int[] Run(BuildingPlan plan, int target, int seed, int rootRoom, out int zoneCount)
        {
            int rc = plan.Rooms.Count;
            var edges = new List<Edge>();
            var adj = new List<int>[rc];
            for (int i = 0; i < rc; i++) adj[i] = new List<int>();
            for (int li = 0; li < plan.Links.Count; li++)
            {
                PlanLink l = plan.Links[li];
                if (l.RoomA < 0 || l.RoomB < 0 || l.RoomA >= rc || l.RoomB >= rc || l.RoomA == l.RoomB) continue;
                var e = new Edge { A = l.RoomA, B = l.RoomB, Link = li, Score = EdgeScore(plan, l) };
                adj[e.A].Add(edges.Count);
                adj[e.B].Add(edges.Count);
                edges.Add(e);
            }

            // interior components (isolated buildings form their own components)
            var comp = new int[rc];
            for (int i = 0; i < rc; i++) comp[i] = -1;
            var comps = new List<List<int>>();
            var starts = new List<int>();
            starts.Add(rootRoom);
            for (int i = 0; i < rc; i++) starts.Add(i);
            foreach (int st in starts)
            {
                if (comp[st] >= 0) continue;
                var list = new List<int>();
                comp[st] = comps.Count;
                list.Add(st);
                for (int qi = 0; qi < list.Count; qi++)
                {
                    foreach (int ei in adj[list[qi]])
                    {
                        Edge e = edges[ei];
                        int o = e.A == list[qi] ? e.B : e.A;
                        if (comp[o] < 0) { comp[o] = comps.Count; list.Add(o); }
                    }
                }
                comps.Add(list);
            }

            // rooms that own a non-main exterior door
            var hasExt = new bool[rc];
            for (int li = 0; li < plan.Links.Count; li++)
            {
                PlanLink l = plan.Links[li];
                int er = FacilityGraph.ExteriorRoom(plan, l);
                if (er >= 0 && !FacilityGraph.IsMainEntrance(plan, l)) hasExt[er] = true;
            }

            long totalCells = 0;
            for (int i = 0; i < rc; i++) totalCells += Math.Max(1, plan.Rooms[i].Cells.Count);

            var result = new int[rc];
            int offset = 0;
            double[] factors = { 1.0, 1.15, 0.85, 1.3, 0.75, 1.0, 1.1, 0.9 };
            const int Attempts = 32;
            for (int c = 0; c < comps.Count; c++)
            {
                List<int> rooms = comps[c];
                int croot = rootRoom;
                long cells = 0;
                int bestExt = -1;
                foreach (int r in rooms)
                {
                    cells += Math.Max(1, plan.Rooms[r].Cells.Count);
                    if (hasExt[r] && (bestExt < 0 || plan.Rooms[r].Cells.Count > plan.Rooms[bestExt].Cells.Count)) bestExt = r;
                }
                if (c > 0) croot = bestExt >= 0 ? bestExt : rooms[0];
                int t = comps.Count == 1 ? target : (int)Math.Round(target * (double)cells / totalCells);
                if (t < 1) t = 1;

                int[] best = null;
                double bestPen = double.MaxValue;
                int bestCount = 1;
                for (int a = 0; a < Attempts; a++)
                {
                    int cnt;
                    double pen;
                    int[] zr = Attempt(plan, edges, adj, croot, t, factors[a % factors.Length], new FacilityRng(unchecked(seed * 7919 + a * 104729 + c * 1299709 + 17)), comps.Count == 1, out cnt, out pen);
                    if (pen < bestPen) { bestPen = pen; best = zr; bestCount = cnt; }
                }
                foreach (int r in rooms) result[r] = offset + best[r];
                offset += bestCount;
            }

            zoneCount = offset;
            return result;
        }

        private static int[] Attempt(BuildingPlan plan, List<Edge> edges, List<int>[] adj, int rootRoom, int target, double factor, FacilityRng rng, bool forceTwo, out int zoneCount, out double penalty)
        {
            int rc = plan.Rooms.Count;

            // ---- random spanning tree of the room graph (randomized Prim, biased to better-scoring edges)
            var parent = new int[rc];
            var parentEdge = new int[rc];
            var inTree = new bool[rc];
            var order = new List<int>();
            for (int i = 0; i < rc; i++) { parent[i] = -1; parentEdge[i] = -1; }
            var frontier = new List<int>();   // edgeIndex * 2 + dir (dir 0: from A, dir 1: from B)
            inTree[rootRoom] = true;
            order.Add(rootRoom);
            PushEdges(rootRoom, edges, adj, inTree, frontier);
            while (frontier.Count > 0)
            {
                int i1 = rng.Next(frontier.Count);
                int i2 = rng.Next(frontier.Count);
                int pick = edges[frontier[i2] >> 1].Score > edges[frontier[i1] >> 1].Score ? i2 : i1;
                int code = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                Edge e = edges[code >> 1];
                int from = (code & 1) == 0 ? e.A : e.B;
                int child = (code & 1) == 0 ? e.B : e.A;
                if (inTree[child]) continue;
                inTree[child] = true;
                parent[child] = from;
                parentEdge[child] = code >> 1;
                order.Add(child);
                PushEdges(child, edges, adj, inTree, frontier);
            }

            var w = new int[rc];
            int reachedCells = 0;
            for (int i = 0; i < order.Count; i++)
            {
                w[order[i]] = Math.Max(1, plan.Rooms[order[i]].Cells.Count);
                reachedCells += w[order[i]];
            }

            int reached = order.Count;
            if (reached < 2 || (target <= 1 && !forceTwo))
            {
                zoneCount = 1;
                penalty = 0;
                return new int[rc];
            }
            int t = target;
            if (t > reached) t = reached;
            if (t < 2) t = 2;
            if (t > reached) t = reached;
            double avg = reachedCells / (double)t / factor;
            int lo2 = Math.Max(1, Math.Min(60, reachedCells / 3));
            double lo = Math.Max(lo2, 0.6 * avg);
            double hi = 1.4 * avg;

            // ---- cut the tree into pieces
            var acc = new int[rc];
            for (int i = 0; i < rc; i++) acc[i] = w[i];
            var cutAbove = new bool[rc];
            for (int i = order.Count - 1; i >= 1; i--)
            {
                int v = order[i];
                int p = parent[v];
                int s = edges[parentEdge[v]].Score;
                int ac = acc[v];
                bool cut = ac >= hi || (ac >= lo && s >= 2) || (ac >= 0.9 * avg && s == 1) || (ac >= lo2 && acc[p] + ac > hi);
                if (cut) cutAbove[v] = true; else acc[p] += ac;
            }

            var zoneOf = new int[rc];
            var head = new List<int>();
            var zpar = new List<int>();
            head.Add(rootRoom);
            zpar.Add(-1);
            zoneOf[rootRoom] = 0;
            for (int i = 1; i < order.Count; i++)
            {
                int v = order[i];
                if (cutAbove[v])
                {
                    zoneOf[v] = head.Count;
                    head.Add(v);
                    zpar.Add(zoneOf[parent[v]]);
                }
                else
                {
                    zoneOf[v] = zoneOf[parent[v]];
                }
            }

            int zc = head.Count;
            var size = new int[zc];
            for (int i = 0; i < order.Count; i++) size[zoneOf[order[i]]] += w[order[i]];
            var alive = new bool[zc];
            for (int i = 0; i < zc; i++) alive[i] = true;
            int aliveCount = zc;

            // ---- merge zones that are too small
            while (aliveCount > 1)
            {
                int z = -1;
                for (int i = 0; i < zc; i++)
                    if (alive[i] && size[i] < lo2 && (z < 0 || size[i] < size[z])) z = i;
                if (z < 0) break;
                if (z != 0)
                {
                    Merge(z, zpar[z], zoneOf, zpar, size, alive, order);
                }
                else
                {
                    int c = -1;
                    for (int i = 1; i < zc; i++)
                        if (alive[i] && zpar[i] == 0 && (c < 0 || size[i] < size[c])) c = i;
                    if (c < 0) break;
                    Merge(c, 0, zoneOf, zpar, size, alive, order);
                }
                aliveCount--;
            }

            // ---- too many zones: merge the cheapest parent/child pairs
            int hiCount = (int)Math.Floor(1.35 * t + 1e-9);
            while (aliveCount > hiCount && aliveCount > 1)
            {
                int z = -1;
                for (int i = 1; i < zc; i++)
                {
                    if (!alive[i]) continue;
                    if (z < 0 || size[i] + size[zpar[i]] < size[z] + size[zpar[z]]) z = i;
                }
                if (z < 0) break;
                Merge(z, zpar[z], zoneOf, zpar, size, alive, order);
                aliveCount--;
            }

            // ---- degenerate: at least two zones when there are at least two reachable rooms
            if (aliveCount == 1 && reached >= 2)
            {
                var sub = new int[rc];
                for (int i = 0; i < order.Count; i++) sub[order[i]] = w[order[i]];
                for (int i = order.Count - 1; i >= 1; i--) sub[parent[order[i]]] += sub[order[i]];
                int bv = -1, bm = -1;
                for (int i = 1; i < order.Count; i++)
                {
                    int v = order[i];
                    int m = Math.Min(sub[v], reachedCells - sub[v]);
                    if (m > bm) { bm = m; bv = v; }
                }
                var inSub = new bool[rc];
                inSub[bv] = true;
                for (int i = 1; i < order.Count; i++)
                {
                    int v = order[i];
                    if (inSub[parent[v]]) inSub[v] = true;
                }
                for (int i = 0; i < order.Count; i++) zoneOf[order[i]] = inSub[order[i]] ? 1 : 0;
                head = new List<int> { rootRoom, bv };
                zpar = new List<int> { -1, 0 };
                zc = 2;
                size = new int[2];
                for (int i = 0; i < order.Count; i++) size[zoneOf[order[i]]] += w[order[i]];
                alive = new bool[] { true, true };
                aliveCount = 2;
            }

            // ---- relabel: zone of rootRoom = 0, others by lowest room id
            var remap = new int[zc];
            for (int i = 0; i < zc; i++) remap[i] = -1;
            int rootZone = zoneOf[rootRoom];
            remap[rootZone] = 0;
            int next = 1;
            var result = new int[rc];
            for (int r = 0; r < rc; r++)
            {
                if (!inTree[r]) { result[r] = 0; continue; }
                int z = zoneOf[r];
                if (remap[z] < 0) remap[z] = next++;
                result[r] = remap[z];
            }

            // ---- score
            double pen = 0;
            int loCount = (int)Math.Ceiling(0.65 * t - 1e-9);
            if (aliveCount < loCount) pen += (loCount - aliveCount) * 1000.0;
            if (aliveCount > hiCount) pen += (aliveCount - hiCount) * 1000.0;
            double avgActual = reachedCells / (double)Math.Max(1, aliveCount);
            var roomsIn = new int[zc];
            for (int i = 0; i < order.Count; i++) roomsIn[zoneOf[order[i]]]++;
            for (int z = 0; z < zc; z++)
            {
                if (!alive[z]) continue;
                if (size[z] > 3.0 * avgActual && roomsIn[z] > 1) pen += 500;
                if (aliveCount > 1 && size[z] < lo2) pen += 300;
                double d = (size[z] - avgActual) / Math.Max(1.0, avgActual);
                pen += 10.0 * d * d;
                if (z != 0 && head[z] != rootRoom && parentEdge[head[z]] >= 0)
                {
                    int s = edges[parentEdge[head[z]]].Score;
                    pen += s >= 2 ? 0 : (s == 1 ? 2 : 5);
                }
            }

            zoneCount = aliveCount;
            penalty = pen;
            return result;
        }

        private static void PushEdges(int room, List<Edge> edges, List<int>[] adj, bool[] inTree, List<int> frontier)
        {
            List<int> l = adj[room];
            for (int i = 0; i < l.Count; i++)
            {
                Edge e = edges[l[i]];
                int dir = e.A == room ? 0 : 1;
                int other = dir == 0 ? e.B : e.A;
                if (!inTree[other]) frontier.Add(l[i] * 2 + dir);
            }
        }

        private static void Merge(int z, int into, int[] zoneOf, List<int> zpar, int[] size, bool[] alive, List<int> order)
        {
            for (int i = 0; i < order.Count; i++)
                if (zoneOf[order[i]] == z) zoneOf[order[i]] = into;
            for (int i = 0; i < zpar.Count; i++)
                if (alive[i] && zpar[i] == z) zpar[i] = into;
            size[into] += size[z];
            size[z] = 0;
            alive[z] = false;
        }
    }
}
