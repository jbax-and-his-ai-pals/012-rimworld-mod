using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    public class BuildingPlannerTinyTests
    {
        private readonly ITestOutputHelper output;
        public BuildingPlannerTinyTests(ITestOutputHelper output) { this.output = output; }

        static readonly Dictionary<string, string> IntendedStyle = new Dictionary<string, string>
        {
            { "OfficeCampus", "Campus" }, { "MedicalCenter", "Campus" }, { "Barracks", "Scattered" },
            { "ResearchComplex", "Compound" }, { "IndustrialPlant", "Scattered" }, { "ResidentialBlock", "Campus" },
        };

        static int Count(BuildingPlan p, RoomFunction f) { return p.Rooms.Count(r => r.Function == f); }

        static void CheckTiny(BuildingPlan p, PlanProfile prof, int size, string tag)
        {
            Assert.Equal(prof.PrincipalArchetype, p.Buildings[0].Archetype);
            // Compound degrades to Campus below 100 cells
            Assert.Equal(prof.Style == "Compound" ? "Campus" : prof.Style, p.Style);
            Assert.Equal(0, p.GateCount);

            // building counts: 60 -> one building plus an optional hut; 85 -> one or two
            Assert.True(p.Buildings.Count >= 1 && p.Buildings.Count <= 2, tag + " building count " + p.Buildings.Count);
            if (size <= 70 && p.Buildings.Count == 2)
            {
                var hut = p.Buildings[1];
                Assert.Equal(1, p.Rooms.Count(r => r.BuildingId == hut.Id));
                Assert.True(hut.Isolated);
                Assert.True(hut.MaxX - hut.MinX + 1 <= 14 && hut.MaxY - hut.MinY + 1 <= 14, tag + " hut size");
            }

            var b0 = p.Buildings[0];
            int bw = b0.MaxX - b0.MinX + 1, bh = b0.MaxY - b0.MinY + 1;
            int longDim = Math.Max(bw, bh), shortDim = Math.Min(bw, bh);
            if (size <= 70) Assert.True(longDim >= 30 && longDim <= 44 && shortDim >= 20 && shortDim <= 32, tag + " principal " + bw + "x" + bh);
            else Assert.True(longDim >= 34 && longDim <= 52 && shortDim >= 30, tag + " principal " + bw + "x" + bh);
            if (p.Buildings.Count == 2 && size > 70)
            {
                var b1 = p.Buildings[1];
                Assert.True(Math.Max(b1.MaxX - b1.MinX, b1.MaxY - b1.MinY) + 1 <= 30, tag + " second building");
                Assert.True(p.Buildings.Count(b => !b.Isolated) <= 2);
            }

            // everything lies inside the plan with a yard around it
            for (int i = 0; i < p.Tiles.Length; i++)
            {
                if (p.Tiles[i] == PlanTile.Outside) continue;
                int x = i % p.Width, y = i / p.Width;
                Assert.True(x >= 2 && y >= 2 && x < p.Width - 2 && y < p.Height - 2, tag + " building touches the border");
            }

            // at most one link corridor: the number of linked buildings is 1 or 2
            Assert.True(p.Buildings.Count(b => !b.Isolated) <= 2);

            // relaxed function minima that fit
            switch (prof.PrincipalArchetype)
            {
                case "Medical":
                    Assert.True(Count(p, RoomFunction.Ward) >= 2, tag + " wards");
                    Assert.True(Count(p, RoomFunction.OperatingRoom) + Count(p, RoomFunction.Pharmacy) >= 1, tag + " OR/pharmacy");
                    break;
                case "Barracks": Assert.True(Count(p, RoomFunction.Bunkroom) >= 2, tag + " bunkrooms"); break;
                case "Residential": Assert.True(Count(p, RoomFunction.Apartment) >= 4, tag + " apartments"); break;
                case "Research":
                    Assert.True(Count(p, RoomFunction.Lab) >= 2, tag + " labs");
                    Assert.True(Count(p, RoomFunction.Containment) + Count(p, RoomFunction.ServerRoom) >= 1, tag + " containment");
                    break;
                case "Plant": Assert.True(Count(p, RoomFunction.MachineHall) >= 1, tag + " machine hall"); break;
            }

            Assert.True(p.Rooms.Any(r => r.BuildingId == 0 && r.Function == RoomFunction.Lobby), tag + " no lobby");
        }

        [Fact]
        public void SixProfilesAtSixtyAndEightyFive()
        {
            foreach (var prof in GameProfiles.All())
            {
                prof.Style = IntendedStyle[prof.Name];
                foreach (int size in new[] { 60, 85 })
                    for (int seed = 1; seed <= 25; seed++)
                    {
                        string tag = prof.Name + "/" + prof.Style + " " + size + " seed " + seed;
                        var p = BuildingPlanner.Generate(size, size, seed, prof);
                        BuildingPlannerTests.CheckPlan(p, size, size, tag);
                        CheckTiny(p, prof, size, tag);
                    }
            }
        }

        [Fact]
        public void TinySitesAreDeterministicAndFast()
        {
            var sw = new Stopwatch();
            long worst = 0;
            foreach (var prof in GameProfiles.All())
            {
                prof.Style = IntendedStyle[prof.Name];
                foreach (int size in new[] { 60, 85 })
                    for (int seed = 1; seed <= 5; seed++)
                    {
                        sw.Restart();
                        string a = BuildingPlanner.Generate(size, size, seed, prof).ToAscii();
                        worst = Math.Max(worst, sw.ElapsedMilliseconds);
                        Assert.Equal(a, BuildingPlanner.Generate(size, size, seed, prof).ToAscii());
                    }
            }

            output.WriteLine("worst tiny generate: " + worst + " ms");
            Assert.True(worst < 500);
        }

        [Fact]
        public void DefaultProfileWorksAtTinySizes()
        {
            foreach (int size in new[] { 60, 85 })
                for (int seed = 1; seed <= 10; seed++)
                {
                    var p = BuildingPlanner.Generate(size, size, seed);
                    BuildingPlannerTests.CheckPlan(p, size, size, "default " + size + " seed " + seed);
                    Assert.Equal("Office", p.Buildings[0].Archetype);
                }
        }

        [Fact]
        public void PrintTinySamples()
        {
            foreach (int size in new[] { 60, 85 })
                foreach (var prof in GameProfiles.All())
                {
                    prof.Style = IntendedStyle[prof.Name];
                    var p = BuildingPlanner.Generate(size, size, 1, prof);
                    output.WriteLine("=== " + prof.Name + " " + size + "x" + size + " seed 1 ===");
                    output.WriteLine(BuildingPlanner.Summary(p));
                }

            output.WriteLine("=== OfficeCampus 60x60 seed 2 ===");
            output.WriteLine(BuildingPlanner.Generate(60, 60, 2, GameProfiles.All()[0]).ToAscii());
        }
    }
}
