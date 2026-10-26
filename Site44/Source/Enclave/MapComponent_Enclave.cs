using System.Collections.Generic;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    // State of one enclave map: the town plan parameters (the plan itself is rebuilt from them), the player's
    // standing with the residents, and the growth of the town. A project is revealed on the map as an outline once
    // standing reaches its reveal value, started once it reaches the required value (and its prerequisites stand),
    // then raised a few tiles at a time.
    public class MapComponent_Enclave : MapComponent
    {
        private const int GrowthIntervalTicks = 120;
        private const int TilesPerStep = 4;

        public bool active;
        public int standing;
        public int planSeed;
        public int areaWidth;
        public int areaHeight;
        public int plazaX;
        public int plazaY;
        public int projectCount;
        public IntVec3 origin;
        public List<int> built = new List<int>();
        public int activeProject = -1;
        public int activeProgress;
        private int lastDailyTick;

        private TownPlan plan;

        public MapComponent_Enclave(Map map) : base(map)
        {
        }

        public TownPlan Plan
        {
            get
            {
                if (plan == null && active)
                {
                    plan = TownPlan.Generate(areaWidth, areaHeight, plazaX, plazaY, null, planSeed, projectCount);
                }

                return plan;
            }
        }

        public void SetPlan(TownPlan generated)
        {
            plan = generated;
        }

        public bool IsBuilt(int projectId)
        {
            return built.Contains(projectId);
        }

        public void AddStanding(int amount)
        {
            standing = Mathf.Max(0, standing + amount);
            EnclaveFaction.ApplyStanding(standing);
        }

        private TownProject NextProject()
        {
            foreach (TownProject p in Plan.Projects)
            {
                if (IsBuilt(p.Id) || p.StandingRequired > standing)
                {
                    continue;
                }

                bool ready = true;
                foreach (int req in p.Requires)
                {
                    if (!IsBuilt(req))
                    {
                        ready = false;
                        break;
                    }
                }

                if (ready && AreaIsFree(p))
                {
                    return p;
                }
            }

            return null;
        }

        // The player may have built on a planned lot: relocate the project if so (the plan decides where it fits).
        private bool AreaIsFree(TownProject p)
        {
            for (int ly = 0; ly < p.Height; ly++)
            {
                for (int lx = 0; lx < p.Width; lx++)
                {
                    IntVec3 c = new IntVec3(origin.x + p.X + lx, 0, origin.z + p.Y + ly);
                    if (!c.InBounds(map))
                    {
                        continue;
                    }

                    foreach (Thing t in c.GetThingList(map))
                    {
                        if (t.def.category == ThingCategory.Building && t.Faction == Faction.OfPlayer)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void RelocateBlocked()
        {
            bool[] occupied = new bool[areaWidth * areaHeight];
            foreach (Thing t in map.listerBuildings.allBuildingsColonist)
            {
                foreach (IntVec3 c in t.OccupiedRect())
                {
                    int x = c.x - origin.x;
                    int y = c.z - origin.z;
                    if (x >= 0 && y >= 0 && x < areaWidth && y < areaHeight)
                    {
                        occupied[y * areaWidth + x] = true;
                    }
                }
            }

            foreach (TownProject p in new List<TownProject>(Plan.Projects))
            {
                if (IsBuilt(p.Id) || p.Id == activeProject || p.StandingRequired > standing || AreaIsFree(p))
                {
                    continue;
                }

                TownProject moved;
                if (Plan.TryReplace(p, occupied, out moved))
                {
                    Plan.Apply(moved);
                }
            }
        }

        public override void MapComponentTick()
        {
            if (active && Find.TickManager.TicksGame % 1200 == 0 && GenCommandLine.CommandLineArgPassed("site44grow"))
            {
                Log.Message("[Site-44] enclave heartbeat tick " + Find.TickManager.TicksGame + ", standing " + standing + ", active project " + activeProject + ", built " + built.Count + ".");
            }

            if (!active || Find.TickManager.TicksGame % GrowthIntervalTicks != 0)
            {
                return;
            }

            // Standing rises slowly while the colony is present and calm.
            if (Find.TickManager.TicksGame - lastDailyTick >= GenDate.TicksPerDay)
            {
                lastDailyTick = Find.TickManager.TicksGame;
                if (map.mapPawns.FreeColonistsSpawnedCount > 0)
                {
                    AddStanding(2);
                }
            }

            Faction faction = EnclaveFaction.GetOrCreate();
            if (activeProject < 0)
            {
                RelocateBlocked();
                TownProject next = NextProject();
                if (next == null)
                {
                    return;
                }

                Log.Message("[Site-44] enclave starts project " + next.Id + " (" + next.Kind + "), standing " + standing + ".");
                activeProject = next.Id;
                activeProgress = 0;
            }

            TownProject project = Plan.Projects[activeProject];
            int total = project.Width * project.Height;
            for (int i = 0; i < TilesPerStep && activeProgress < total; i++, activeProgress++)
            {
                EnclaveBuilder.BuildTile(map, origin, project, activeProgress % project.Width, activeProgress / project.Width);
            }

            if (activeProgress >= total)
            {
                EnclaveBuilder.Finish(map, origin, project, faction);
                built.Add(project.Id);
                Log.Message("[Site-44] enclave built project " + project.Id + " (" + project.Kind + "), " + built.Count + "/" + Plan.Projects.Count + ".");
                activeProject = -1;
                activeProgress = 0;
                if (project.Kind == TownProjectKind.House || project.Kind == TownProjectKind.Clinic || project.Kind == TownProjectKind.Canteen)
                {
                    Messages.Message("The Hold's people finish a " + project.Kind.ToString().ToLowerInvariant() + ".",
                        new LookTargets(new IntVec3(origin.x + project.X, 0, origin.z + project.Y), map), MessageTypeDefOf.PositiveEvent, false);
                }
            }
        }

        // Outlines of lots the town has decided to build, shown once the player's standing reveals them.
        public override void MapComponentUpdate()
        {
            if (!active || Plan == null)
            {
                return;
            }

            List<IntVec3> cells = new List<IntVec3>();
            foreach (TownProject p in Plan.Projects)
            {
                if (IsBuilt(p.Id) || p.Id == activeProject || p.RevealStanding > standing)
                {
                    continue;
                }

                cells.Clear();
                for (int ly = 0; ly < p.Height; ly++)
                {
                    for (int lx = 0; lx < p.Width; lx++)
                    {
                        IntVec3 c = new IntVec3(origin.x + p.X + lx, 0, origin.z + p.Y + ly);
                        if (c.InBounds(map))
                        {
                            cells.Add(c);
                        }
                    }
                }

                GenDraw.DrawFieldEdges(cells, p.StandingRequired <= standing ? Color.green : Color.yellow);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref standing, "standing");
            Scribe_Values.Look(ref planSeed, "planSeed");
            Scribe_Values.Look(ref areaWidth, "areaWidth");
            Scribe_Values.Look(ref areaHeight, "areaHeight");
            Scribe_Values.Look(ref plazaX, "plazaX");
            Scribe_Values.Look(ref plazaY, "plazaY");
            Scribe_Values.Look(ref projectCount, "projectCount");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref activeProject, "activeProject", -1);
            Scribe_Values.Look(ref activeProgress, "activeProgress");
            Scribe_Values.Look(ref lastDailyTick, "lastDailyTick");
            Scribe_Collections.Look(ref built, "built", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                built = built ?? new List<int>();
            }
        }
    }
}
