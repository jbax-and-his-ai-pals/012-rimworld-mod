using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class RoomBeatsTests
    {
        // A chain of rooms 0-1-2-...-(n-1), optionally with every third room a corridor.
        private static List<BeatRoom> Chain(int n, bool corridors = false)
        {
            List<BeatRoom> rooms = new List<BeatRoom>();
            for (int i = 0; i < n; i++)
            {
                rooms.Add(new BeatRoom { Id = i, Eligible = true, ZoneDepth = 3, IsCorridor = corridors && i % 3 == 1 });
            }

            for (int i = 0; i + 1 < n; i++)
            {
                rooms[i].Neighbours.Add(i + 1);
                rooms[i + 1].Neighbours.Add(i);
            }

            return rooms;
        }

        // A random-ish tree-plus-extra-links graph so shortest paths branch and merge.
        private static List<BeatRoom> Web(int seed, int n)
        {
            System.Random rng = new System.Random(seed);
            List<BeatRoom> rooms = Enumerable.Range(0, n).Select(i => new BeatRoom { Id = i, Eligible = true, ZoneDepth = 3 }).ToList();
            for (int i = 1; i < n; i++)
            {
                Link(rooms, i, rng.Next(0, i));
                if (rng.NextDouble() < 0.4)
                {
                    Link(rooms, i, rng.Next(0, i));
                }
            }

            return rooms;
        }

        private static void Link(List<BeatRoom> rooms, int a, int b)
        {
            if (a != b && !rooms[a].Neighbours.Contains(b))
            {
                rooms[a].Neighbours.Add(b);
                rooms[b].Neighbours.Add(a);
            }
        }

        [Fact]
        public void Same_seed_gives_the_same_plan()
        {
            List<BeatRoom> rooms = Web(5, 40);
            Assert.Equal(RoomBeats.Plan(9, rooms, 0, new BeatSettings()), RoomBeats.Plan(9, rooms, 0, new BeatSettings()));
        }

        [Fact]
        public void No_route_from_the_start_has_more_than_the_allowed_empty_rooms_in_a_row()
        {
            BeatSettings s = new BeatSettings { BeatChance = 0.1f };
            for (int seed = 0; seed < 30; seed++)
            {
                List<BeatRoom> rooms = Web(seed, 60);
                RoomBeat[] plan = RoomBeats.Plan(seed, rooms, 0, s);
                Assert.True(LongestEmptyRun(rooms, plan, 0) <= s.MaxEmptyRun, "seed " + seed);
            }
        }

        // Walk every shortest route and report the longest stretch of Empty rooms (corridors and None rooms are transparent).
        private static int LongestEmptyRun(List<BeatRoom> rooms, RoomBeat[] plan, int start)
        {
            int[] hops = Enumerable.Repeat(-1, rooms.Count).ToArray();
            int[] run = new int[rooms.Count];
            Queue<int> queue = new Queue<int>();
            hops[start] = 0;
            queue.Enqueue(start);
            int worst = 0;
            List<int> order = new List<int>();
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                order.Add(r);
                foreach (int o in rooms[r].Neighbours.OrderBy(x => x))
                {
                    if (hops[o] < 0)
                    {
                        hops[o] = hops[r] + 1;
                        queue.Enqueue(o);
                    }
                }
            }

            foreach (int r in order)
            {
                int incoming = rooms[r].Neighbours.Where(o => hops[o] == hops[r] - 1).Select(o => run[o]).DefaultIfEmpty(0).Max();
                run[r] = plan[r] == RoomBeat.Empty ? incoming + 1 : plan[r] == RoomBeat.None ? incoming : 0;
                worst = System.Math.Max(worst, run[r]);
            }

            return worst;
        }

        [Fact]
        public void Corridors_and_ineligible_rooms_get_no_beat_and_do_not_break_runs()
        {
            List<BeatRoom> rooms = Chain(30, corridors: true);
            rooms[5].Eligible = false;
            RoomBeat[] plan = RoomBeats.Plan(3, rooms, 0, new BeatSettings { BeatChance = 0f });
            Assert.All(rooms.Where(r => r.IsCorridor || !r.Eligible), r => Assert.Equal(RoomBeat.None, plan[r.Id]));
            Assert.True(LongestEmptyRun(rooms, plan, 0) <= 2);
        }

        [Fact]
        public void Fixed_beats_are_kept_and_reset_the_run()
        {
            List<BeatRoom> rooms = Chain(8);
            rooms[2].Fixed = RoomBeat.Loot;
            RoomBeat[] plan = RoomBeats.Plan(1, rooms, 0, new BeatSettings { BeatChance = 0f });
            Assert.Equal(RoomBeat.Loot, plan[2]);
            Assert.Equal(RoomBeat.Empty, plan[3]);
        }

        [Fact]
        public void Nests_only_appear_far_and_deep()
        {
            BeatSettings s = new BeatSettings { BeatChance = 1f, StashWeight = 0f, CasketWeight = 0f, LootWeight = 1f, StockLootBonus = 0f, NestWeight = 1000f };
            List<BeatRoom> rooms = Chain(12);
            RoomBeat[] plan = RoomBeats.Plan(2, rooms, 0, s);
            for (int i = 0; i < rooms.Count; i++)
            {
                if (plan[i] == RoomBeat.Nest)
                {
                    Assert.True(i >= s.NestMinHops);
                }
            }

            Assert.Contains(RoomBeat.Nest, plan);

            foreach (BeatRoom room in rooms)
            {
                room.ZoneDepth = 0;
            }

            Assert.DoesNotContain(RoomBeat.Nest, RoomBeats.Plan(2, rooms, 0, s));
        }

        [Fact]
        public void Unreachable_rooms_still_get_planned()
        {
            List<BeatRoom> rooms = Chain(4);
            rooms.Add(new BeatRoom { Id = 4, Eligible = true, ZoneDepth = 3 });
            RoomBeat[] plan = RoomBeats.Plan(7, rooms, 0, new BeatSettings { BeatChance = 1f });
            Assert.NotEqual(RoomBeat.None, plan[4]);
        }

        [Fact]
        public void Overall_share_of_beats_follows_the_chance()
        {
            List<BeatRoom> rooms = Web(11, 300);
            RoomBeat[] plan = RoomBeats.Plan(4, rooms, 0, new BeatSettings { BeatChance = 0.6f });
            double share = plan.Count(b => b != RoomBeat.Empty && b != RoomBeat.None) / (double)rooms.Count;
            Assert.InRange(share, 0.5, 0.8);
        }
    }
}
