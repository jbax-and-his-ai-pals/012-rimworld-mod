using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    // What a room is for, pacing-wise. Lore, Hazard and SetPiece are reserved: the planner never picks them yet.
    public enum RoomBeat
    {
        None,       // not a room the planner cares about (corridors, tiny rooms, the entrance zone)
        Empty,      // nothing here on purpose
        Loot,       // a crate with something worth stopping for
        Stash,      // a body with what it carried
        Casket,     // a cryosleep casket (survivor, hostile sleeper, remains or loot)
        Nest,       // hostile creatures guarding the room
        Puzzle,
        Lore,
        Hazard,
        SetPiece
    }

    public sealed class BeatRoom
    {
        public int Id;
        public bool IsCorridor;
        public bool Eligible;               // may receive a generated beat: an ordinary room outside the entrance zone
        public bool Stock;                  // storage-like: leans toward loot
        public int ZoneDepth;               // depth of the room's zone in the sector tree
        public RoomBeat Fixed;              // a beat decided elsewhere (vault, executive office, preserved room); None = free
        public List<int> Neighbours = new List<int>();
    }

    public sealed class BeatSettings
    {
        public float BeatChance = 0.6f;     // chance an eligible free room gets a beat when it is not forced to
        public int MaxEmptyRun = 2;         // most empty rooms in a row on any shortest route from the start
        public float LootWeight = 40f;
        public float StockLootBonus = 25f;
        public float StashWeight = 15f;
        public float CasketWeight = 14f;
        public float NestWeight = 12f;
        public int NestMinHops = 4;         // rooms away from the start before a nest may appear
        public int NestMinZoneDepth = 2;
    }

    // Decides a beat for every room so that no route from the start passes more than a couple of empty rooms
    // in a row. Pure and deterministic for a given seed.
    public static class RoomBeats
    {
        public static RoomBeat[] Plan(int seed, IReadOnlyList<BeatRoom> rooms, int startRoom, BeatSettings settings)
        {
            int n = rooms.Count;
            RoomBeat[] result = new RoomBeat[n];
            int[] hops = new int[n];
            int[] run = new int[n];
            for (int i = 0; i < n; i++)
            {
                hops[i] = -1;
            }

            List<int> order = new List<int>(n);
            Queue<int> queue = new Queue<int>();
            if (startRoom >= 0 && startRoom < n)
            {
                hops[startRoom] = 0;
                queue.Enqueue(startRoom);
            }

            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                order.Add(room);
                List<int> next = new List<int>(rooms[room].Neighbours);
                next.Sort();
                foreach (int other in next)
                {
                    if (other >= 0 && other < n && hops[other] < 0)
                    {
                        hops[other] = hops[room] + 1;
                        queue.Enqueue(other);
                    }
                }
            }

            // Rooms the walk never reached (isolated buildings entered from the yards) follow, in id order.
            for (int i = 0; i < n; i++)
            {
                if (hops[i] < 0)
                {
                    order.Add(i);
                }
            }

            Random rng = new Random(seed);
            foreach (int id in order)
            {
                BeatRoom room = rooms[id];
                int incoming = 0;
                foreach (int other in room.Neighbours)
                {
                    if (other >= 0 && other < n && hops[other] >= 0 && hops[other] == hops[id] - 1)
                    {
                        incoming = Math.Max(incoming, run[other]);
                    }
                }

                if (room.Fixed != RoomBeat.None)
                {
                    result[id] = room.Fixed;
                    run[id] = 0;
                }
                else if (!room.Eligible || room.IsCorridor)
                {
                    result[id] = RoomBeat.None;
                    run[id] = incoming;
                }
                else if (incoming >= settings.MaxEmptyRun || rng.NextDouble() < settings.BeatChance)
                {
                    result[id] = Pick(rng, room, hops[id], settings);
                    run[id] = 0;
                }
                else
                {
                    result[id] = RoomBeat.Empty;
                    run[id] = incoming + 1;
                }
            }

            return result;
        }

        private static RoomBeat Pick(Random rng, BeatRoom room, int hops, BeatSettings s)
        {
            float loot = s.LootWeight + (room.Stock ? s.StockLootBonus : 0f);
            float nest = hops >= s.NestMinHops && room.ZoneDepth >= s.NestMinZoneDepth ? s.NestWeight : 0f;
            float total = loot + s.StashWeight + s.CasketWeight + nest;
            double roll = rng.NextDouble() * total;
            if (roll < loot)
            {
                return RoomBeat.Loot;
            }

            roll -= loot;
            if (roll < s.StashWeight)
            {
                return RoomBeat.Stash;
            }

            roll -= s.StashWeight;
            if (roll < s.CasketWeight)
            {
                return RoomBeat.Casket;
            }

            return nest > 0f ? RoomBeat.Nest : RoomBeat.Loot;
        }
    }
}
