using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    // Outdoor set dressing around the buildings, different for each kind of complex: sandbag emplacements and barriers
    // at a barracks, silos and generators at a research compound, crate and barrel yards at a plant, benches and
    // planters at a residential block. All vanilla props, placed in the open yards and kept off the paths and doors.
    public class FacilityProps
    {
        private readonly Map map;
        private readonly BuildingPlan plan;
        private readonly IntVec3 origin;
        private readonly Func<IntVec3, bool> isFree;
        private readonly List<IntVec3> doors = new List<IntVec3>();
        private readonly TerrainDef pavedTile;
        private int outsideCells;
        private float scale;
        private int placed;

        public int Placed => placed;

        public FacilityProps(Map map, BuildingPlan plan, IntVec3 origin, Func<IntVec3, bool> isFree)
        {
            this.map = map;
            this.plan = plan;
            this.origin = origin;
            this.isFree = isFree;
            pavedTile = DefDatabase<TerrainDef>.GetNamedSilentFail("PavedTile");
            foreach (PlanLink link in plan.Links)
            {
                doors.Add(new IntVec3(origin.x + link.Cell.X, 0, origin.z + link.Cell.Y));
            }

            for (int i = 0; i < plan.Tiles.Length; i++)
            {
                if (plan.Tiles[i] == PlanTile.Outside)
                {
                    outsideCells++;
                }
            }
        }

        public sealed class FrontDoor
        {
            public IntVec3 Door;
            public IntVec3 Outward;
        }

        public void Place(string propSet, float density, IList<FrontDoor> frontDoors)
        {
            fronts = frontDoors ?? new List<FrontDoor>();
            Place(propSet, density);
        }

        private IList<FrontDoor> fronts = new List<FrontDoor>();

        public void Place(string propSet, float density)
        {
            if (density <= 0f || outsideCells < 200)
            {
                return;
            }

            scale = outsideCells / 2500f * density * Site44Tuning.ClutterFactor;
            switch (propSet)
            {
                case "barracks":
                    // Deliberate rather than scattered: a guard post either side of every front door, a few straight
                    // sandbag walls and barrier runs aligned to the buildings, and a handful of lone nests.
                    GuardPosts();
                    Line("Sandbags", 8, 1.5f, 0f);
                    Line("AncientConcreteBarrier", 6, 2f, 0f);
                    Emplacements(1.5f);
                    Lamps(3);
                    Cluster("AncientBarrel", 3, 1, 1);
                    break;
                case "research":
                    Line("AncientConcreteBarrier", 6, 4);
                    Cluster("AncientStorageCylinder", 3, 3, 3);
                    Scatter("AncientGenerator", 1);
                    Scatter("AncientMachine", 1);
                    Cluster("AncientFuelNode", 2, 2, 1);
                    Lamps(5);
                    break;
                case "industrial":
                    Cluster("AncientCrate", 5, 3, 6);
                    Cluster("AncientBarrel", 6, 3, 5);
                    Cluster("AncientFuelNode", 3, 3, 2);
                    Line("AncientStorageCylinder", 4, 3);
                    Item("ChunkSlagSteel", 5, 8);
                    Item("Steel", 75, 7);
                    Item("ComponentIndustrial", 6, 3);
                    Item("Chemfuel", 40, 3);
                    Line("AncientConcreteBarrier", 5, 3);
                    Scatter("AncientMachine", 4);
                    Scatter("AncientGenerator", 1);
                    Cluster("AncientMachine", 3, 3, 2);
                    Mess("Filth_RubbleBuilding", 10);
                    Mess("Filth_MachineBits", 8);
                    Mess("Filth_OilSmear", 6);
                    Mess("Filth_Dirt", 8);
                    Lamps(3);
                    break;
                case "residential":
                    Lamps(6);
                    Cluster("PlantPot", 4, 3, 5);
                    Scatter("HoopstoneRing", 1);
                    Scatter("HorseshoesPin", 1);
                    Scatter("Couch", 3);
                    break;
                case "medical":
                    Lamps(5);
                    Cluster("PlantPot", 4, 3, 4);
                    Scatter("Couch", 3);
                    Scatter("Armchair", 3);
                    break;
                default:
                    Cluster("PlantPot", 3, 3, 3);
                    Scatter("Couch", 2);
                    Line("AncientConcreteBarrier", 4, 2);
                    Lamps(4);
                    break;
            }
        }

        private int Count(float basePerUnit)
        {
            return Mathf.Max(basePerUnit > 0f ? 1 : 0, Mathf.RoundToInt(basePerUnit * scale));
        }

        // ----- cells -----

        private bool OpenYardCell(IntVec3 c)
        {
            if (!c.InBounds(map))
            {
                return false;
            }

            if (plan.At(c.x - origin.x, c.z - origin.z) != PlanTile.Outside || !isFree(c))
            {
                return false;
            }

            if (pavedTile != null && c.GetTerrain(map) == pavedTile)
            {
                return false;
            }

            foreach (IntVec3 door in doors)
            {
                if (Mathf.Abs(door.x - c.x) <= 3 && Mathf.Abs(door.z - c.z) <= 3)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryOpenCell(out IntVec3 cell)
        {
            for (int tries = 0; tries < 80; tries++)
            {
                IntVec3 candidate = new IntVec3(origin.x + Rand.Range(0, plan.Width), 0, origin.z + Rand.Range(0, plan.Height));
                if (OpenYardCell(candidate))
                {
                    cell = candidate;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        private bool Spawn(string defName, IntVec3 cell, Rot4 rot)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || !OpenYardCell(cell))
            {
                return false;
            }

            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            if (def.useHitPoints && thing.MaxHitPoints > 1 && def.category == ThingCategory.Building)
            {
                thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * Rand.Range(0.3f, 0.9f)));
            }

            GenSpawn.Spawn(thing, cell, map, rot);
            placed++;
            return true;
        }

        // ----- recipes -----

        private void Scatter(string defName, float perUnit)
        {
            int n = Count(perUnit);
            for (int i = 0; i < n; i++)
            {
                IntVec3 cell;
                if (TryOpenCell(out cell))
                {
                    Spawn(defName, cell, Rot4.Random);
                }
            }
        }

        private void Cluster(string defName, int items, int radius, float perUnit)
        {
            int n = Count(perUnit / 3f);
            for (int i = 0; i < n; i++)
            {
                IntVec3 center;
                if (!TryOpenCell(out center))
                {
                    continue;
                }

                for (int k = 0; k < items; k++)
                {
                    IntVec3 cell = center + new IntVec3(Rand.RangeInclusive(-radius, radius), 0, Rand.RangeInclusive(-radius, radius));
                    Spawn(defName, cell, Rot4.Random);
                }
            }
        }

        private void Line(string defName, int length, float perUnit, float gapChance = 0.12f)
        {
            int n = Count(perUnit / 3f);
            for (int i = 0; i < n; i++)
            {
                IntVec3 start;
                if (!TryOpenCell(out start))
                {
                    continue;
                }

                IntVec3 step = Rand.Bool ? IntVec3.East : IntVec3.North;
                for (int k = 0; k < length; k++)
                {
                    // Short gaps make the line look broken and let people through.
                    if (k > 0 && Rand.Chance(gapChance))
                    {
                        continue;
                    }

                    Spawn(defName, start + step * k, Rot4.North);
                }
            }
        }

        // Guard posts: a sandbag nest either side of each front door, open to the rear and facing outward,
        // with a barrel inside and a bright lamp behind.
        private void GuardPosts()
        {
            foreach (FrontDoor front in fronts)
            {
                IntVec3 outward = front.Outward;
                if ((outward.x != 0) == (outward.z != 0))
                {
                    continue;
                }

                IntVec3 lateral = new IntVec3(outward.z != 0 ? 1 : 0, 0, outward.x != 0 ? 1 : 0);
                foreach (int sign in new[] { -1, 1 })
                {
                    IntVec3 center = front.Door + outward * 5 + lateral * (sign * 7);
                    for (int a = -2; a <= 2; a++)
                    {
                        for (int b = 0; b <= 2; b++)
                        {
                            bool frontRow = b == 2;
                            bool flank = Mathf.Abs(a) == 2;
                            if (frontRow || flank)
                            {
                                Spawn("Sandbags", center + lateral * a + outward * b, Rot4.North);
                            }
                        }
                    }

                    Spawn("AncientBarrel", center + outward, Rot4.North);
                    Spawn("Site44_LampBright", center - outward, Rot4.North);
                }
            }
        }
        // Sandbag nests: a horseshoe of sandbags around a firing position, open on one side.
        private void Emplacements(float perUnit)
        {
            int n = Count(perUnit / 3f);
            for (int i = 0; i < n; i++)
            {
                IntVec3 center;
                if (!TryOpenCell(out center))
                {
                    continue;
                }

                int open = Rand.RangeInclusive(0, 3);
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        bool edge = Mathf.Abs(dx) == 2 || Mathf.Abs(dz) == 1;
                        if (!edge)
                        {
                            continue;
                        }

                        bool gap = (open == 0 && dz == -1 && Mathf.Abs(dx) <= 0) || (open == 1 && dz == 1 && Mathf.Abs(dx) <= 0)
                                   || (open == 2 && dx == -2 && dz == 0) || (open == 3 && dx == 2 && dz == 0);
                        if (!gap)
                        {
                            Spawn("Sandbags", center + new IntVec3(dx, 0, dz), Rot4.North);
                        }
                    }
                }

                if (Rand.Chance(0.5f))
                {
                    Spawn("AncientBarrel", center, Rot4.North);
                }
            }
        }

        private void Lamps(float perUnit)
        {
            int n = Count(perUnit);
            for (int i = 0; i < n; i++)
            {
                IntVec3 cell;
                if (TryOpenCell(out cell))
                {
                    Spawn("Site44_LampBright", cell, Rot4.North);
                }
            }
        }

        // Patches of filth: rubble, scrap, oil stains, dirt. Each patch is a small blob of cells.
        private void Mess(string filthName, float perUnit)
        {
            ThingDef filth = DefDatabase<ThingDef>.GetNamedSilentFail(filthName);
            if (filth == null)
            {
                return;
            }

            int patches = Count(perUnit);
            for (int i = 0; i < patches; i++)
            {
                IntVec3 center;
                if (!TryOpenCell(out center))
                {
                    continue;
                }

                int cells = Rand.RangeInclusive(5, 14);
                for (int k = 0; k < cells; k++)
                {
                    IntVec3 cell = center + new IntVec3(Rand.RangeInclusive(-2, 2), 0, Rand.RangeInclusive(-2, 2));
                    if (cell.InBounds(map) && plan.At(cell.x - origin.x, cell.z - origin.z) == PlanTile.Outside && cell.GetEdifice(map) == null)
                    {
                        FilthMaker.TryMakeFilth(cell, map, filth, 1);
                    }
                }

                placed++;
            }
        }

        // Loose items on the ground: slag chunks, steel piles.
        private void Item(string defName, int maxStack, float perUnit)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            int n = Count(perUnit);
            for (int i = 0; i < n; i++)
            {
                IntVec3 cell;
                if (!TryOpenCell(out cell))
                {
                    continue;
                }

                Thing item = ThingMaker.MakeThing(def);
                item.stackCount = Mathf.Clamp(Mathf.RoundToInt(Rand.RangeInclusive(Mathf.Max(1, maxStack / 3), maxStack) * Site44Tuning.LootAmount), 1, def.stackLimit);
                GenPlace.TryPlaceThing(item, cell, map, ThingPlaceMode.Near);
                placed++;
            }
        }
    }
}
