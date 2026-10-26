using System.Collections.Generic;
using RimWorld;
using Site44.Gen;
using Verse;

namespace Site44
{
    // Turns planned town projects into vanilla buildings on the map. Pure placement: when and by whom a project
    // is built is decided elsewhere.
    public static class EnclaveBuilder
    {
        private static TerrainDef Terrain(string defName, TerrainDef fallback)
        {
            return DefDatabase<TerrainDef>.GetNamedSilentFail(defName) ?? fallback;
        }

        private static ThingDef Thing(string defName)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        }

        public static void PrepareArea(Map map, IntVec3 origin, int width, int height)
        {
            CellRect area = new CellRect(origin.x, origin.z, width, height);
            foreach (IntVec3 c in area.ExpandedBy(4).ClipInsideMap(map))
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    ThingCategory category = things[i].def.category;
                    if (category == ThingCategory.Building || category == ThingCategory.Filth || category == ThingCategory.Item
                        || (category == ThingCategory.Plant && area.Contains(c)))
                    {
                        things[i].Destroy();
                    }
                }

                if (map.roofGrid.RoofAt(c) != null)
                {
                    map.roofGrid.SetRoof(c, null);
                }

                MapGenerator.Elevation[c] = 0f;
                MapGenerator.Caves[c] = 0f;
                TerrainDef existing = c.GetTerrain(map);
                if (area.Contains(c) || existing.IsWater || existing.IsRiver || existing.defName.Contains("Rough") || existing.defName.Contains("Smooth"))
                {
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                }
            }
        }

        public static void PaintStreets(Map map, IntVec3 origin, TownPlan plan)
        {
            TerrainDef gravel = Terrain("Gravel", TerrainDefOf.Soil);
            TerrainDef paved = Terrain("PavedTile", gravel);
            foreach (int index in plan.StreetCells)
            {
                int x = index % plan.Width;
                int y = index / plan.Width;
                IntVec3 c = new IntVec3(origin.x + x, 0, origin.z + y);
                bool plaza = System.Math.Abs(x - plan.PlazaX) <= 4 && System.Math.Abs(y - plan.PlazaY) <= 4;
                map.terrainGrid.SetTerrain(c, plaza ? paved : gravel);
            }

            IntVec3 center = new IntVec3(origin.x + plan.PlazaX, 0, origin.z + plan.PlazaY);
            ThingDef lamp = Thing("TorchLamp");
            if (lamp != null)
            {
                foreach (IntVec3 offset in new[] { new IntVec3(-3, 0, -3), new IntVec3(3, 0, -3), new IntVec3(-3, 0, 3), new IntVec3(3, 0, 3) })
                {
                    TrySpawn(map, lamp, center + offset, null);
                }
            }
        }

        public static void Build(Map map, IntVec3 origin, TownProject p, Faction faction)
        {
            ThingDef wall = ThingDefOf.Wall;
            ThingDef door = ThingDefOf.Door;
            ThingDef wood = ThingDefOf.WoodLog;
            TerrainDef floor = Terrain("WoodPlankFloor", TerrainDefOf.Soil);
            bool roofed = p.Kind != TownProjectKind.Garden && p.Kind != TownProjectKind.Stall && p.Kind != TownProjectKind.WallSegment;

            for (int ly = 0; ly < p.Height; ly++)
            {
                for (int lx = 0; lx < p.Width; lx++)
                {
                    IntVec3 c = new IntVec3(origin.x + p.X + lx, 0, origin.z + p.Y + ly);
                    if (!c.InBounds(map))
                    {
                        continue;
                    }

                    PlanTile tile = p.Tiles[ly * p.Width + lx];
                    switch (tile)
                    {
                        case PlanTile.Wall:
                        case PlanTile.Window:
                            TrySpawn(map, wall, c, wood);
                            if (roofed)
                            {
                                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                            }

                            break;
                        case PlanTile.Door:
                            map.terrainGrid.SetTerrain(c, floor);
                            TrySpawn(map, door, c, wood);
                            if (roofed)
                            {
                                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                            }

                            break;
                        case PlanTile.Floor:
                            map.terrainGrid.SetTerrain(c, p.Kind == TownProjectKind.Stall ? Terrain("Gravel", TerrainDefOf.Soil) : floor);
                            if (roofed)
                            {
                                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                            }

                            break;
                    }
                }
            }

            Furnish(map, origin, p, faction);
        }

        private static void Furnish(Map map, IntVec3 origin, TownProject p, Faction faction)
        {
            List<IntVec3> floors = new List<IntVec3>();
            for (int ly = 0; ly < p.Height; ly++)
            {
                for (int lx = 0; lx < p.Width; lx++)
                {
                    IntVec3 c = new IntVec3(origin.x + p.X + lx, 0, origin.z + p.Y + ly);
                    if (p.Tiles[ly * p.Width + lx] == PlanTile.Floor && c.InBounds(map))
                    {
                        floors.Add(c);
                    }
                }
            }

            if (floors.Count == 0 && p.Kind != TownProjectKind.Garden)
            {
                return;
            }

            // Interior cells (all eight neighbours are floor) host furniture so beds do not block doors or walls.
            List<IntVec3> interior = floors.FindAll(c => AllNeighboursFloor(map, c));
            interior.SortStable((a, b) => (a.x + a.z * 7) - (b.x + b.z * 7));
            ThingDef wood = ThingDefOf.WoodLog;

            switch (p.Kind)
            {
                case TownProjectKind.House:
                    PlaceSome(map, Thing("Bed"), wood, faction, interior, 1 + (p.Width * p.Height) / 40);
                    PlaceSome(map, Thing("TorchLamp"), null, null, interior, 1);
                    PlaceSome(map, Thing("Table1x2c"), wood, faction, interior, 1);
                    break;
                case TownProjectKind.Canteen:
                    PlaceSome(map, Thing("FueledStove"), null, faction, interior, 1);
                    PlaceSome(map, Thing("Table2x2c"), wood, faction, interior, 2);
                    PlaceSome(map, Thing("Stool"), wood, faction, interior, 4);
                    PlaceSome(map, Thing("TorchLamp"), null, null, interior, 1);
                    break;
                case TownProjectKind.Clinic:
                    PlaceSome(map, Thing("HospitalBed"), null, faction, interior, 3);
                    PlaceSome(map, Thing("TorchLamp"), null, null, interior, 1);
                    break;
                case TownProjectKind.Workshop:
                    PlaceSome(map, Thing("Table2x2c"), wood, faction, interior, 1);
                    PlaceSome(map, Thing("TorchLamp"), null, null, interior, 1);
                    break;
                case TownProjectKind.Stall:
                    PlaceSome(map, Thing("Table1x2c"), wood, faction, floors, 1);
                    break;
                case TownProjectKind.Watchtower:
                    PlaceSome(map, Thing("Sandbags"), null, faction, floors, 2);
                    break;
                case TownProjectKind.Garden:
                    Sow(map, origin, p);
                    break;
            }
        }

        private static bool AllNeighboursFloor(Map map, IntVec3 c)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    IntVec3 n = new IntVec3(c.x + dx, 0, c.z + dz);
                    if (!n.InBounds(map) || n.GetEdifice(map) != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void PlaceSome(Map map, ThingDef def, ThingDef stuff, Faction faction, List<IntVec3> cells, int count)
        {
            if (def == null)
            {
                return;
            }

            int placed = 0;
            for (int i = 0; i < cells.Count && placed < count; i++)
            {
                IntVec3 c = cells[(i * 3 + placed) % cells.Count];
                if (TrySpawn(map, def, c, stuff, faction))
                {
                    placed++;
                }
            }
        }

        private static void Sow(Map map, IntVec3 origin, TownProject p)
        {
            ThingDef crop = Thing("Plant_Potato");
            if (crop == null)
            {
                return;
            }

            for (int ly = 0; ly < p.Height; ly++)
            {
                for (int lx = 0; lx < p.Width; lx++)
                {
                    IntVec3 c = new IntVec3(origin.x + p.X + lx, 0, origin.z + p.Y + ly);
                    if (!c.InBounds(map) || p.Tiles[ly * p.Width + lx] != PlanTile.Outside || (lx + ly) % 2 != 0)
                    {
                        continue;
                    }

                    if (c.GetEdifice(map) == null && c.GetPlant(map) == null && c.GetTerrain(map).fertility > 0f)
                    {
                        Plant plant = (Plant)ThingMaker.MakeThing(crop);
                        plant.Growth = Rand.Range(0.3f, 1f);
                        GenSpawn.Spawn(plant, c, map);
                    }
                }
            }
        }

        private static bool TrySpawn(Map map, ThingDef def, IntVec3 c, ThingDef stuff, Faction faction = null)
        {
            if (def == null || !c.InBounds(map))
            {
                return false;
            }

            foreach (Thing existing in c.GetThingList(map))
            {
                if (existing.def.category == ThingCategory.Building)
                {
                    return false;
                }
            }

            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? (stuff ?? GenStuff.DefaultStuffFor(def)) : null);
            if (faction != null && thing.def.CanHaveFaction)
            {
                thing.SetFaction(faction);
            }

            GenSpawn.Spawn(thing, c, map, Rot4.Random);
            return true;
        }
    }
}
