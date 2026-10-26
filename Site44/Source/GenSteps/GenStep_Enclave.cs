using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Site44
{
    // Builds a delvers' enclave: a walled settlement laid out from a town plan, part built already and
    // part planned for the residents to raise as the player earns their trust.
    public class GenStep_Enclave : GenStep
    {
        private const int StartingStanding = 60;

        public override int SeedPart => 884420045;

        public static bool IsEnclaveMap(Map map)
        {
            Site site = map?.Parent as Site;
            return site != null && site.parts.Any(p => p.def == Site44DefOf.Site44_EnclaveSitePart);
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (!IsEnclaveMap(map))
            {
                return;
            }

            int size = Mathf.Min(map.Size.x, map.Size.z);
            int area = Mathf.Min(size - 20, 130);
            IntVec3 origin = new IntVec3((map.Size.x - area) / 2, 0, (map.Size.z - area) / 2);
            int seed = Rand.Int;
            int projectCount = 36;

            EnclaveBuilder.PrepareArea(map, origin, area, area);
            TownPlan plan = TownPlan.Generate(area, area, area / 2, area / 2, null, seed, projectCount);
            EnclaveBuilder.PaintStreets(map, origin, plan);

            Faction faction = EnclaveFaction.GetOrCreate();
            MapComponent_Enclave comp = map.GetComponent<MapComponent_Enclave>();
            comp.active = true;
            comp.standing = StartingStanding;
            comp.planSeed = seed;
            comp.areaWidth = area;
            comp.areaHeight = area;
            comp.plazaX = plan.PlazaX;
            comp.plazaY = plan.PlazaY;
            comp.projectCount = projectCount;
            comp.origin = origin;
            comp.SetPlan(plan);

            int houses = 0;
            foreach (TownProject project in plan.Projects)
            {
                if (project.StandingRequired > StartingStanding)
                {
                    continue;
                }

                EnclaveBuilder.Build(map, origin, project, faction);
                comp.built.Add(project.Id);
                if (project.Kind == TownProjectKind.House)
                {
                    houses++;
                }
            }

            SpawnResidents(map, origin, plan, faction, Mathf.Max(4, houses * 2));

            IntVec3 plaza = new IntVec3(origin.x + plan.PlazaX, 0, origin.z + plan.PlazaY);
            IntVec3 arrival = new IntVec3(plaza.x, 0, origin.z + plan.PlazaY - 6);
            MapGenerator.PlayerStartSpot = arrival.Standable(map) ? arrival : plaza;
            MapGenerator.UsedRects.Add(new CellRect(0, 0, map.Size.x, map.Size.z));
            EnclaveFaction.ApplyStanding(StartingStanding);

            Log.Message("[Site-44] enclave " + area + "x" + area + ": " + plan.Projects.Count + " planned project(s), " + comp.built.Count + " built, "
                        + houses + " house(s), standing " + StartingStanding + ", seed " + seed + ".");
        }

        private static void SpawnResidents(Map map, IntVec3 origin, TownPlan plan, Faction faction, int count)
        {
            IntVec3 plaza = new IntVec3(origin.x + plan.PlazaX, 0, origin.z + plan.PlazaY);
            List<Pawn> residents = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.Villager, faction, PawnGenerationContext.NonPlayer, -1, true);
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                IntVec3 cell;
                if (!CellFinder.TryFindRandomCellNear(plaza, map, 12, c => c.Standable(map) && c.GetEdifice(map) == null, out cell))
                {
                    cell = plaza;
                }

                GenSpawn.Spawn(pawn, cell, map);
                residents.Add(pawn);
            }

            if (residents.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(plaza, null, 30f), map, residents);
            }
        }

        // Nothing hostile at arrival: remove stray enemies other generation steps left on the map.
        public override void PostMapInitialized(Map map, GenStepParams parms)
        {
            if (!IsEnclaveMap(map))
            {
                return;
            }

            int removed = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Faction != null && !pawn.Faction.IsPlayer && pawn.HostileTo(Faction.OfPlayer))
                {
                    pawn.Destroy();
                    removed++;
                }
            }

            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                if (thing is Building_Turret && thing.Faction != null && !thing.Faction.IsPlayer && thing.Faction.HostileTo(Faction.OfPlayer))
                {
                    thing.Destroy();
                    removed++;
                }
            }

            Log.Message("[Site-44] enclave start sweep: removed " + removed + " stray hostile(s).");
        }
    }
}
