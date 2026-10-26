using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    internal static class GameProfiles
    {
        static PlanProfile Make(string name, string principal, int min, int max, params object[] weights)
        {
            var p = new PlanProfile { Name = name, PrincipalArchetype = principal, MinBuildings = min, MaxBuildings = max };
            for (int i = 0; i + 1 < weights.Length; i += 2) p.Weights[(string)weights[i]] = Convert.ToSingle(weights[i + 1]);
            return p;
        }

        public static List<PlanProfile> All()
        {
            return new List<PlanProfile>
            {
                Make("OfficeCampus", "Office", 0, 0),
                Make("MedicalCenter", "Medical", 0, 0, "Medical", 3, "Lab", 2, "Commons", 2, "Warehouse", 1, "Security", 1, "Office", 1),
                Make("Barracks", "Barracks", 0, 0, "Barracks", 3, "Security", 2, "Warehouse", 2, "Plant", 1, "Commons", 2),
                Make("ResearchComplex", "Research", 0, 0, "Research", 3, "Lab", 2, "Plant", 1, "Warehouse", 1, "Security", 1, "Office", 1),
                Make("IndustrialPlant", "Plant", 0, 0, "Plant", 3, "Warehouse", 3, "Office", 1, "Security", 1, "Commons", 1),
                Make("ResidentialBlock", "Residential", 3, 6, "Residential", 4, "Commons", 2, "Warehouse", 1, "Office", 1),
            };
        }
    }

    public class BuildingPlannerProfileTests
    {
        private readonly ITestOutputHelper output;
        public BuildingPlannerProfileTests(ITestOutputHelper output) { this.output = output; }

        static readonly int[] Sizes = { 150, 200, 226, 260 };
        const int Seeds = 25;

        static int Count(BuildingPlan p, RoomFunction f) { return p.Rooms.Count(r => r.Function == f); }

        static void CheckProgram(BuildingPlan p, PlanProfile prof, string tag)
        {
            Assert.Equal(prof.PrincipalArchetype, p.Buildings[0].Archetype);
            Assert.Equal(prof.Name, p.ProfileName);
            if (prof.MinBuildings > 0) Assert.True(p.Buildings.Count >= prof.MinBuildings, tag + " too few buildings " + p.Buildings.Count);
            if (prof.MaxBuildings > 0) Assert.True(p.Buildings.Count <= prof.MaxBuildings, tag + " too many buildings " + p.Buildings.Count);
            switch (prof.PrincipalArchetype)
            {
                case "Medical":
                    Assert.True(Count(p, RoomFunction.Ward) >= 6, tag + " wards");
                    Assert.True(Count(p, RoomFunction.OperatingRoom) >= 1, tag + " operating");
                    Assert.True(Count(p, RoomFunction.Pharmacy) >= 1, tag + " pharmacy");
                    Assert.True(Count(p, RoomFunction.Morgue) >= 1, tag + " morgue");
                    break;
                case "Barracks":
                    Assert.True(Count(p, RoomFunction.Bunkroom) >= 4, tag + " bunkrooms");
                    Assert.True(Count(p, RoomFunction.Cafeteria) >= 1, tag + " mess");
                    Assert.True(Count(p, RoomFunction.SecurityPost) >= 1, tag + " security");
                    break;
                case "Residential":
                    Assert.True(Count(p, RoomFunction.Apartment) >= 10, tag + " apartments");
                    break;
                case "Research":
                    Assert.True(Count(p, RoomFunction.Lab) >= 5, tag + " labs");
                    Assert.True(Count(p, RoomFunction.ServerRoom) >= 2, tag + " servers");
                    Assert.True(Count(p, RoomFunction.Containment) >= 1, tag + " containment");
                    Assert.True(Count(p, RoomFunction.MachineHall) >= 1, tag + " reactor");
                    break;
                case "Plant":
                    Assert.True(Count(p, RoomFunction.MachineHall) >= 2, tag + " machine halls");
                    Assert.True(Count(p, RoomFunction.Mechanical) + Count(p, RoomFunction.Boiler) >= 2, tag + " mechanical");
                    Assert.True(Count(p, RoomFunction.Storeroom) >= 2, tag + " storerooms");
                    break;
                default:
                    Assert.True(Count(p, RoomFunction.MachineHall) >= 1 && Count(p, RoomFunction.ServerRoom) >= 1, tag);
                    break;
            }

            // Containment rooms: one door, no windows
            foreach (var r in p.Rooms.Where(r => r.Function == RoomFunction.Containment))
            {
                int doors = p.Links.Count(l => l.RoomA == r.Id || l.RoomB == r.Id);
                Assert.True(doors == 1, tag + " containment doors " + doors);
            }

            // game-assigned functions are never emitted
            foreach (var f in new[] { RoomFunction.RecRoom, RoomFunction.Kitchen, RoomFunction.Dormitory, RoomFunction.Infirmary, RoomFunction.LockerRoom, RoomFunction.Library, RoomFunction.Workshop, RoomFunction.Armory, RoomFunction.CommsRoom, RoomFunction.Classroom })
                Assert.Equal(0, Count(p, f));
        }

        [Fact]
        public void EveryProfileSatisfiesInvariantsAndPrograms()
        {
            foreach (var prof in GameProfiles.All())
                foreach (int size in Sizes)
                    for (int seed = 1; seed <= Seeds; seed++)
                    {
                        string tag = prof.Name + " " + size + " seed " + seed;
                        var p = BuildingPlanner.Generate(size, size, seed, prof);
                        BuildingPlannerTests.CheckPlan(p, size, size, tag);
                        CheckProgram(p, prof, tag);
                    }
        }

        [Fact]
        public void ProfilesAreDeterministicAndFast()
        {
            var sw = new Stopwatch();
            long worst = 0;
            foreach (var prof in GameProfiles.All())
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    var a = BuildingPlanner.Generate(226, 226, seed, prof).ToAscii();
                    var b = BuildingPlanner.Generate(226, 226, seed, prof).ToAscii();
                    Assert.Equal(a, b);
                }

                BuildingPlanner.Generate(260, 260, 99, prof);
                for (int seed = 1; seed <= 5; seed++)
                {
                    sw.Restart();
                    BuildingPlanner.Generate(260, 260, seed, prof);
                    worst = Math.Max(worst, sw.ElapsedMilliseconds);
                }
            }

            output.WriteLine("worst profile 260x260 generate: " + worst + " ms");
            Assert.True(worst < 500, "worst " + worst + " ms");
        }

        [Fact]
        public void NullProfileMatchesThreeArgumentGenerate()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                Assert.Equal(BuildingPlanner.Generate(200, 200, seed).ToAscii(), BuildingPlanner.Generate(200, 200, seed, null).ToAscii());
                Assert.Equal(BuildingPlanner.Generate(200, 200, seed).ToAscii(), BuildingPlanner.Generate(200, 200, seed, GameProfiles.All()[0]).ToAscii());
            }
        }

        [Fact]
        public void PrintProfileSamples()
        {
            foreach (var prof in GameProfiles.All())
            {
                var p = BuildingPlanner.Generate(226, 226, 1, prof);
                output.WriteLine("=== " + prof.Name + " 226x226 seed 1 ===");
                output.WriteLine(BuildingPlanner.Summary(p));
            }
        }
    }
}
