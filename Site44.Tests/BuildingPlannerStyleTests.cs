using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Site44.Gen;
using Xunit;
using Xunit.Abstractions;

namespace Site44.Tests
{
    public class BuildingPlannerStyleTests
    {
        private readonly ITestOutputHelper output;
        public BuildingPlannerStyleTests(ITestOutputHelper output) { this.output = output; }

        static readonly int[] Sizes = { 100, 130, 170, 230 };
        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

        static readonly Dictionary<string, string> IntendedStyle = new Dictionary<string, string>
        {
            { "OfficeCampus", "Campus" }, { "MedicalCenter", "Campus" }, { "Barracks", "Scattered" },
            { "ResearchComplex", "Compound" }, { "IndustrialPlant", "Scattered" }, { "ResidentialBlock", "Campus" },
        };

        static PlanProfile Prof(string style)
        {
            var p = new PlanProfile { Name = "Test" + style, PrincipalArchetype = "Office", Style = style };
            p.Weights["Lab"] = 1; p.Weights["Warehouse"] = 1; p.Weights["Commons"] = 1; p.Weights["Office"] = 1;
            return p;
        }

        static bool OpenRect(BuildingPlan p, int x0, int y0, int w, int h)
        {
            if (x0 < 0 || y0 < 0 || x0 + w > p.Width || y0 + h > p.Height) return false;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (p.At(x, y) != PlanTile.Outside) return false;
            return true;
        }

        static bool ParadeYard(BuildingPlan p)
        {
            foreach (var dim in new[] { new[] { 28, 20 }, new[] { 20, 28 } })
                for (int ox = p.Start.X - dim[0] + 1; ox <= p.Start.X; ox++)
                    for (int oy = p.Start.Y - dim[1] + 1; oy <= p.Start.Y; oy++)
                        if (OpenRect(p, ox, oy, dim[0], dim[1])) return true;
            return false;
        }

        static double AreaCv(BuildingPlan p)
        {
            var areas = new List<double>();
            foreach (var b in p.Buildings)
            {
                int c = 0;
                for (int y = b.MinY; y <= b.MaxY; y++) for (int x = b.MinX; x <= b.MaxX; x++) if (p.At(x, y) != PlanTile.Outside) c++;
                areas.Add(c);
            }

            double mean = areas.Average();
            double sd = Math.Sqrt(areas.Sum(a => (a - mean) * (a - mean)) / areas.Count);
            return sd / mean;
        }

        // Outside flood from Start; the gate cells are treated as walls when blockGates is set.
        static bool[] OutsideFrom(BuildingPlan p, bool blockGates)
        {
            var seen = new bool[p.Width * p.Height];
            var blocked = new HashSet<int>(p.Gates.Select(g => g.Y * p.Width + g.X));
            var st = new Stack<int>();
            st.Push(p.Start.Y * p.Width + p.Start.X); seen[st.Peek()] = true;
            while (st.Count > 0)
            {
                int c = st.Pop();
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % p.Width + Dx[d], ny = c / p.Width + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= p.Width || ny >= p.Height) continue;
                    int ni = ny * p.Width + nx;
                    if (seen[ni] || p.Tiles[ni] != PlanTile.Outside) continue;
                    if (blockGates && blocked.Contains(ni)) continue;
                    seen[ni] = true; st.Push(ni);
                }
            }

            return seen;
        }

        static void CheckCompound(BuildingPlan p, string tag, bool exactBooths)
        {
            Assert.Equal(2, p.GateCount);
            Assert.Equal(8, p.Gates.Count);
            foreach (var g in p.Gates) Assert.Equal(PlanTile.Outside, p.At(g.X, g.Y));
            // each gate is 4 collinear adjacent cells; the two gates lie on different walls
            var lines = new List<string>();
            for (int k = 0; k < 2; k++)
            {
                var cells = p.Gates.Skip(k * 4).Take(4).ToList();
                bool vertical = cells.All(c => c.X == cells[0].X), horizontal = cells.All(c => c.Y == cells[0].Y);
                Assert.True(vertical || horizontal, tag + " gate shape");
                if (vertical) { Assert.Equal(4, cells.Max(c => c.Y) - cells.Min(c => c.Y) + 1); lines.Add("x" + cells[0].X); }
                else { Assert.Equal(4, cells.Max(c => c.X) - cells.Min(c => c.X) + 1); lines.Add("y" + cells[0].Y); }
            }

            Assert.NotEqual(lines[0], lines[1]);

            // the perimeter is closed except for the gates, and Start lies outside it
            var open = OutsideFrom(p, false);
            var closed = OutsideFrom(p, true);
            int enc = -1;
            for (int d = 0; d < 4; d++)
                if (p.At(p.MainEntrance.X + Dx[d], p.MainEntrance.Y + Dy[d]) == PlanTile.Outside) enc = (p.MainEntrance.Y + Dy[d]) * p.Width + p.MainEntrance.X + Dx[d];
            Assert.True(enc >= 0 && open[enc], tag + " inside not reachable through the gates");
            Assert.False(closed[enc], tag + " perimeter is not closed");
            foreach (var g in p.Gates) Assert.True(open[g.Y * p.Width + g.X], tag + " gate not reachable");
            Assert.True(closed[p.Start.Y * p.Width + p.Start.X]);

            // four guard booths: single-room Security buildings whose doors open near a gate
            var booths = p.Buildings.Where(b => b.Archetype == "Security" && p.Rooms.Count(r => r.BuildingId == b.Id) == 1 &&
                                                p.Rooms.First(r => r.BuildingId == b.Id).Function == RoomFunction.SecurityPost &&
                                                p.Rooms.First(r => r.BuildingId == b.Id).Cells.Count <= 64).ToList();
            if (exactBooths) Assert.Equal(4, booths.Count); else Assert.True(booths.Count >= 4, tag + " booths");
            int nearGate = 0;
            foreach (var b in booths)
            {
                var room = p.Rooms.First(r => r.BuildingId == b.Id);
                Assert.True(room.Cells.Count >= 25 && room.Cells.Count <= 64, tag + " booth room");
                Assert.True(b.Isolated);
                var door = p.Links.Single(l => l.RoomB == -1 && l.RoomA == room.Id);
                if (p.Gates.Any(g => Math.Abs(g.X - door.Cell.X) + Math.Abs(g.Y - door.Cell.Y) <= 14)) nearGate++;
            }

            Assert.True(nearGate >= 4, tag + " booth doors near gates " + nearGate);
        }

        static void CheckStyle(BuildingPlan p, string style, int size, string tag, bool exactBooths)
        {
            Assert.Equal(style, p.Style);
            var b0 = p.Buildings[0];
            Assert.True(b0.MaxX - b0.MinX + 1 >= 25 && b0.MaxY - b0.MinY + 1 >= 25 && Math.Max(b0.MaxX - b0.MinX, b0.MaxY - b0.MinY) + 1 >= 35, tag + " principal too small");
            if (style == "Scattered")
            {
                if (size >= 170) Assert.True(ParadeYard(p), tag + " no parade yard");
                Assert.True(p.Buildings.Count(b => !b.Isolated) <= 3, tag + " too many linked buildings");
            }

            if (style == "Compound") CheckCompound(p, tag, exactBooths);
        }

        [Fact]
        public void EveryStyleAtEverySize()
        {
            foreach (string style in new[] { "Campus", "Scattered", "Compound" })
                foreach (int size in Sizes)
                    for (int seed = 1; seed <= 20; seed++)
                    {
                        string tag = style + " " + size + " seed " + seed;
                        var p = BuildingPlanner.Generate(size, size, seed, Prof(style));
                        BuildingPlannerTests.CheckPlan(p, size, size, tag);
                        Assert.Equal("Office", p.Buildings[0].Archetype);
                        CheckStyle(p, style, size, tag, true);
                    }
        }

        [Fact]
        public void ScatteredBuildingsVaryInSize()
        {
            int good = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var p = BuildingPlanner.Generate(230, 230, seed, Prof("Scattered"));
                if (AreaCv(p) >= 0.35) good++;
            }

            output.WriteLine("scattered 230x230: CV>=0.35 in " + good + "/20");
            Assert.True(good >= 16);
        }

        [Fact]
        public void StylesAreDeterministicAndFast()
        {
            var sw = new Stopwatch();
            long worst = 0;
            foreach (string style in new[] { "Campus", "Scattered", "Compound" })
            {
                var prof = Prof(style);
                for (int seed = 1; seed <= 3; seed++)
                    Assert.Equal(BuildingPlanner.Generate(170, 170, seed, prof).ToAscii(), BuildingPlanner.Generate(170, 170, seed, prof).ToAscii());
                BuildingPlanner.Generate(260, 260, 77, prof);
                for (int seed = 1; seed <= 8; seed++)
                {
                    sw.Restart();
                    BuildingPlanner.Generate(260, 260, seed, prof);
                    worst = Math.Max(worst, sw.ElapsedMilliseconds);
                }
            }

            output.WriteLine("worst styled 260x260 generate: " + worst + " ms");
            Assert.True(worst < 500, "worst " + worst + " ms");
        }

        [Fact]
        public void UnknownStyleIsCampus()
        {
            var a = new PlanProfile { Style = "nonsense" };
            var b = new PlanProfile { Style = null };
            for (int seed = 1; seed <= 4; seed++)
            {
                string expected = BuildingPlanner.Generate(200, 200, seed).ToAscii();
                Assert.Equal(expected, BuildingPlanner.Generate(200, 200, seed, a).ToAscii());
                Assert.Equal(expected, BuildingPlanner.Generate(200, 200, seed, b).ToAscii());
            }
        }

        [Fact]
        public void GameProfilesWithTheirStyles()
        {
            foreach (var prof in GameProfiles.All())
            {
                prof.Style = IntendedStyle[prof.Name];
                foreach (int size in Sizes)
                    for (int seed = 1; seed <= 6; seed++)
                    {
                        string tag = prof.Name + "/" + prof.Style + " " + size + " seed " + seed;
                        var p = BuildingPlanner.Generate(size, size, seed, prof);
                        BuildingPlannerTests.CheckPlan(p, size, size, tag);
                        Assert.Equal(prof.PrincipalArchetype, p.Buildings[0].Archetype);
                        CheckStyle(p, prof.Style, size, tag, false);
                    }
            }
        }

        [Fact]
        public void PrintStyleSamples()
        {
            foreach (var prof in GameProfiles.All())
            {
                prof.Style = IntendedStyle[prof.Name];
                var p = BuildingPlanner.Generate(226, 226, 1, prof);
                output.WriteLine("=== " + prof.Name + " (" + prof.Style + ") 226x226 seed 1 ===");
                output.WriteLine(BuildingPlanner.Summary(p));
            }

            var comp = GameProfiles.All()[3];
            comp.Style = "Compound";
            var cp = BuildingPlanner.Generate(130, 130, 1, comp);
            output.WriteLine("=== Compound 130x130 seed 1 ===");
            output.WriteLine(BuildingPlanner.Summary(cp));
            output.WriteLine(cp.ToAscii());
        }
    }
}
