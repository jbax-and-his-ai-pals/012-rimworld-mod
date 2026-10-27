using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Zone = Site44.Gen.Zone;

namespace Site44
{
    // Builds the whole complex from an architectural plan: buildings with real hallways, carved into
    // progression zones along their doors, ringed by a cleared strip of ordinary terrain.
    public class GenStep_Facility : GenStep
    {
        private const int RingMargin = 10;
        private const int CellsPerZone = 1200;

        private Map map;
        private BuildingPlan plan;
        private FacilityGraph graph;
        private IntVec3 origin;
        private CellRect rect;
        private Faction faction;
        private IntVec3 startSpot;
        private readonly HashSet<IntVec3> reserved = new HashSet<IntVec3>();
        private readonly List<IntVec3> hotAnchors = new List<IntVec3>();
        private int preservedTotal;
        private readonly List<KeyValuePair<PlanRoom, Zone>> containerRooms = new List<KeyValuePair<PlanRoom, Zone>>();
        private string containerSummary = "";
        private string propSummary = "";
        private readonly HashSet<Pawn> ownedHostiles = new HashSet<Pawn>();
        private int waterFlattened;
        private PlanRoom welcomeRoom;
        private readonly HashSet<int> executiveRooms = new HashSet<int>();
        private IntVec3 startSpotForSafety;
        private IntVec3 welcomePos = IntVec3.Invalid;
        private int recRoomCount;
        private readonly List<SecureRoom> secureRooms = new List<SecureRoom>();
        private readonly List<Foyer> foyers = new List<Foyer>();
        private readonly HashSet<int> lockedGateZones = new HashSet<int>();

        public override int SeedPart => 884420044;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (!Site44Mod.ShouldGenerateFacility(map))
            {
                return;
            }

            this.map = map;
            SiteTypeDef siteType = SiteTypes.Resolve(map);
            SiteTypes.Current = siteType;
            reserved.Clear();
            hotAnchors.Clear();
            containerRooms.Clear();
            containerSummary = "";
            waterFlattened = 0;
            welcomeRoom = null;
            executiveRooms.Clear();
            secureRooms.Clear();
            ownedHostiles.Clear();
            foyers.Clear();
            welcomePos = IntVec3.Invalid;
            recRoomCount = 0;
            preservedTotal = 0;

            int available = Mathf.Min(map.Size.x, map.Size.z) - 2 * RingMargin;
            string sizeName;
            int width = FootprintFor(siteType, map, available, out sizeName);
            int height = width;
            origin = new IntVec3((map.Size.x - width) / 2, 0, (map.Size.z - height) / 2);
            rect = new CellRect(origin.x, origin.z, width, height);

            int planSeed = Rand.Int;
            int graphSeed = Rand.Int;
            int targetZones = Mathf.Clamp(width * height / CellsPerZone, 8, 60);
            plan = siteType != null
                ? BuildingPlanner.Generate(width, height, planSeed, siteType.ToProfile())
                : BuildingPlanner.Generate(width, height, planSeed);
            graph = FacilityGraph.Build(plan, targetZones, graphSeed);
            faction = Site44Tuning.SecurityEnabled ? FacilityFaction.GetOrCreate() : null;

            Reposition(width, height);
            startSpotForSafety = World(plan.Start);
            foreach (PlanRoom exec in plan.Rooms.Where(r => r.Function.ToString() == "PrivateOffice" && r.Cells.Count >= 20)
                         .OrderByDescending(r => r.Cells.Count).Take(2))
            {
                executiveRooms.Add(exec.Id);
            }

            PlanRecRooms();
            PlanSecureRooms();
            PlanFoyers();
            PlanSpecialtyRooms();

            ClearRing();
            PrepareCells();
            DigForecourts();
            BuildTiles();
            PaintPaths();
            foreach (Zone zone in graph.Zones)
            {
                BuildZone(zone);
            }

            PlaceContainers();
            PlaceExtraction();
            PopulateSecureRooms();
            MarkEntrances();
            PlaceDoorMarkers();
            FacilityProps props = new FacilityProps(map, plan, origin, IsFree);
            List<FacilityProps.FrontDoor> frontDoors = foyers
                .Select(f => new FacilityProps.FrontDoor { Door = World(f.door), Outward = World(f.door) - f.inside })
                .ToList();
            props.Place(siteType != null ? siteType.props : "office", Site44Tuning.PropDensity, frontDoors);
            propSummary = "outdoor props=" + props.Placed;

            startSpot = World(plan.Start);
            // The whole map counts as used, so vanilla ruin, ancient-danger and mech-cluster steps that run after this
            // one find no room anywhere, including the natural land around a small complex.
            MapGenerator.UsedRects.Add(new CellRect(0, 0, map.Size.x, map.Size.z));
            MapGenerator.PlayerStartSpot = startSpot;

            MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
            facility.Initialize(width, height, planSeed, graphSeed, targetZones, hotAnchors);
            facility.startCell = startSpot;

            FacilityFaction.ApplyTier(0);

            Log.Message("[Site-44] facility (" + (siteType != null ? siteType.defName : "default") + ", " + sizeName + ") " + width + "x" + height + ", " + plan.Buildings.Count + " building(s) ["
                        + string.Join(", ", plan.Buildings.Select(b => b.Archetype).ToArray()) + "], " + plan.Rooms.Count + " rooms, "
                        + graph.Zones.Count + " zones, start " + startSpot + ", seeds " + planSeed + "/" + graphSeed + ".");
            Validate();
        }

        // The start must be safe: once every generation step has run, remove hostile pawns and turrets that some other
        // step (ancient dangers, mech clusters, bandit camps) placed on the map. The droids guarding secure rooms are ours
        // and stay. Raids and other storyteller events happen later and are not touched.
        public override void PostMapInitialized(Map map, GenStepParams parms)
        {
            if (!Site44Mod.ShouldGenerateFacility(map))
            {
                return;
            }

            Faction player = Faction.OfPlayer;
            List<string> removed = new List<string>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Faction != null && !pawn.Faction.IsPlayer && pawn.HostileTo(player) && !ownedHostiles.Contains(pawn))
                {
                    removed.Add(pawn.LabelShort + " (" + pawn.Faction.def.defName + ")");
                    pawn.Destroy();
                }
            }

            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                if (thing is Building_Turret && thing.Faction != null && !thing.Faction.IsPlayer && thing.Faction.HostileTo(player)
                    && thing.def != Site44DefOf.Site44_SecurityTurret)
                {
                    removed.Add(thing.LabelShort + " (" + thing.Faction.def.defName + ")");
                    thing.Destroy();
                }
            }

            // Test runs use the crash-landing scenario: drop its gear pile and pets so they match the Breach scenario.
            if (Find.GameInitData != null && GenCommandLine.CommandLineArgPassed("site44test"))
            {
                GameComponent_Site44Hotkeys.RemoveCrashLandingExtras(map, startSpot);
            }

            Log.Message("[Site-44] start sweep: removed " + removed.Count + " stray hostile(s)"
                        + (removed.Count > 0 ? ": " + string.Join(", ", removed.Take(12).ToArray()) : "") + ".");
        }

        // Complex size classes: extra small, small, medium and large cover part of the map; XL covers it all.
        private static int FootprintFor(SiteTypeDef type, Map map, int available, out string name)
        {
            string mode = Site44Quests.SizeOverride
                          ?? SiteTypes.CommandLineValue("site44size")
                          ?? GameComponent_Delve.Level?.SizeName
                          ?? (map.Parent is Site ? Site44Settings.Current.siteSizeMode : Site44Settings.Current.scenarioComplexSize);
            if (mode == null || mode == "Random")
            {
                float xs = 0.10f, s = 0.25f, m = 0.30f, l = 0.20f, xl = 0.15f;
                if (type != null && type.sizeWeights != null && type.sizeWeights.Count > 0)
                {
                    xs = Weight(type, "XS"); s = Weight(type, "S"); m = Weight(type, "M"); l = Weight(type, "L"); xl = Weight(type, "XL");
                }

                float roll = Rand.Value * (xs + s + m + l + xl);
                mode = roll < xs ? "XS" : roll < xs + s ? "Small" : roll < xs + s + m ? "Medium" : roll < xs + s + m + l ? "Large" : "XL";
            }

            switch (mode)
            {
                case "XS":
                case "Extra small":
                    name = "XS";
                    return Mathf.Min(available, 60);
                case "S":
                case "Small":
                    name = "S";
                    return Mathf.Min(available, 85);
                case "M":
                case "Medium":
                    name = "M";
                    return Mathf.Min(available, 120);
                case "L":
                case "Large":
                    name = "L";
                    return Mathf.Min(available, 160);
                default:
                    name = "XL";
                    return available;
            }
        }
        private static float Weight(SiteTypeDef type, string key)
        {
            WeightedName entry = type.sizeWeights.FirstOrDefault(w => w.name == key);
            return entry != null ? entry.weight : 0f;
        }

        private IntVec3 World(LayoutCell cell)
        {
            return new IntVec3(origin.x + cell.X, 0, origin.z + cell.Y);
        }

        private IntVec3 World(int x, int y)
        {
            return new IntVec3(origin.x + x, 0, origin.z + y);
        }

        // ----- placement and forecourts -----

        private const int ForecourtDepth = 9;

        private struct ExteriorDoor
        {
            public int X, Y;       // the cell just outside the door, in plan coordinates
            public int Dx, Dy;     // outward direction
        }

        private List<ExteriorDoor> ExteriorDoors()
        {
            List<ExteriorDoor> doors = new List<ExteriorDoor>();
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            foreach (PlanLink link in plan.Links)
            {
                if (link.RoomB >= 0)
                {
                    continue;
                }

                for (int d = 0; d < 4; d++)
                {
                    int nx = link.Cell.X + dx[d];
                    int ny = link.Cell.Y + dy[d];
                    if (plan.InBounds(nx, ny) && plan.At(nx, ny) == PlanTile.Outside)
                    {
                        doors.Add(new ExteriorDoor { X = nx, Y = ny, Dx = dx[d], Dy = dy[d] });
                        break;
                    }
                }
            }

            return doors;
        }

        private static bool IsRock(Map map, IntVec3 c)
        {
            if (!c.InBounds(map))
            {
                return true;
            }

            Building edifice = c.GetEdifice(map);
            return edifice != null && edifice.def.building != null && edifice.def.building.isNaturalRock;
        }

        // Entrances look wrong pressed against a mountain face. When the complex has room to move, slide it to the
        // spot whose forecourts (the ground in front of its doors) hold the least rock.
        private void Reposition(int width, int height)
        {
            List<ExteriorDoor> doors = ExteriorDoors();
            if (doors.Count == 0)
            {
                return;
            }

            int minX = RingMargin;
            int maxX = map.Size.x - width - RingMargin;
            int minZ = RingMargin;
            int maxZ = map.Size.z - height - RingMargin;
            if (maxX <= minX && maxZ <= minZ)
            {
                return;
            }

            int bestScore = int.MaxValue;
            IntVec3 best = origin;
            for (int ox = minX; ox <= Mathf.Max(minX, maxX); ox += 4)
            {
                for (int oz = minZ; oz <= Mathf.Max(minZ, maxZ); oz += 4)
                {
                    int score = 0;
                    foreach (ExteriorDoor door in doors)
                    {
                        score += ForecourtRock(ox, oz, door);
                        if (score >= bestScore)
                        {
                            break;
                        }
                    }

                    // A little noise so equally good spots are not always the corner of the search.
                    score = score * 4 + Rand.RangeInclusive(0, 3);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new IntVec3(ox, 0, oz);
                    }
                }
            }

            origin = best;
            rect = new CellRect(origin.x, origin.z, width, height);
        }

        private int ForecourtRock(int ox, int oz, ExteriorDoor door)
        {
            int rock = 0;
            int px = -door.Dy;   // perpendicular to the outward direction
            int py = door.Dx;
            for (int k = 0; k < ForecourtDepth; k++)
            {
                for (int w = -4; w <= 4; w++)
                {
                    int x = ox + door.X + door.Dx * k + px * w;
                    int z = oz + door.Y + door.Dy * k + py * w;
                    if (IsRock(map, new IntVec3(x, 0, z)))
                    {
                        rock++;
                    }
                }
            }

            return rock;
        }

        // Whatever rock is left in front of a door is dug back into an irregular clearing so every entrance opens onto
        // open ground, not a rock face.
        private void DigForecourts()
        {
            foreach (ExteriorDoor door in ExteriorDoors())
            {
                int px = -door.Dy;
                int py = door.Dx;
                int halfWidth = Rand.RangeInclusive(4, 5);
                for (int k = 0; k < ForecourtDepth; k++)
                {
                    // The clearing narrows toward its far end and wobbles at the edge.
                    int reach = Mathf.Max(2, halfWidth - k / 3 + Rand.RangeInclusive(-1, 1));
                    for (int w = -reach; w <= reach; w++)
                    {
                        IntVec3 c = new IntVec3(origin.x + door.X + door.Dx * k + px * w, 0, origin.z + door.Y + door.Dy * k + py * w);
                        if (c.InBounds(map) && IsRock(map, c))
                        {
                            ClearForBuilding(c);
                        }
                    }
                }
            }
        }

        // ----- preparation -----

        private void ClearRing()
        {
            CellRect clearZone = rect.ExpandedBy(RingMargin);
            foreach (IntVec3 c in map.AllCells)
            {
                if (rect.Contains(c))
                {
                    continue;
                }

                // No cave hives anywhere on the map; the margin around the complex is cleared of vanilla structures.
                MapGenerator.Caves[c] = 0f;
                if (!clearZone.Contains(c))
                {
                    continue;
                }

                // Beyond the complex the land stays natural (rock, trees, water); only generated ruins and clutter go.
                ClearClutter(c);
            }
        }

        private void PrepareCells()
        {
            TerrainDef apron = TerrainDefOf.Concrete;
            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    IntVec3 c = World(x, y);
                    bool outside = plan.At(x, y) == PlanTile.Outside;

                    // Yards away from the buildings keep their natural look: mountains, trees and water stay where they are.
                    bool needed = !outside || NearBuilding(x, y, 2) || Mathf.Max(Mathf.Abs(x - plan.Start.X), Mathf.Abs(y - plan.Start.Y)) <= 3;
                    if (!needed)
                    {
                        ClearClutter(c);
                        continue;
                    }

                    // Rivers, lakes and marsh under the buildings and their apron become dry ground, so nothing is built on water.
                    ClearForBuilding(c);
                    map.roofGrid.SetRoof(c, outside ? null : RoofDefOf.RoofRockThick);
                    if (outside)
                    {
                        // A paved apron hugging the buildings; beyond it the yards stay natural and overgrow.
                        if (NearBuilding(x, y, 2))
                        {
                            map.terrainGrid.SetTerrain(c, apron);
                        }
                    }
                    else
                    {
                        map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                    }
                }
            }
        }

        private bool NearBuilding(int x, int y, int distance)
        {
            for (int dy = -distance; dy <= distance; dy++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    if (plan.InBounds(x + dx, y + dy) && plan.At(x + dx, y + dy) != PlanTile.Outside)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ----- tiles -----

        private void BuildTiles()
        {
            ThingDef doorDef = DefDatabase<ThingDef>.GetNamed("Door");
            Dictionary<int, int> gateZoneOfLink = new Dictionary<int, int>();
            foreach (Zone zone in graph.Zones)
            {
                if (zone.GateLink >= 0)
                {
                    gateZoneOfLink[zone.GateLink] = zone.Id;
                }
            }

            HashSet<int> sealedLinks = new HashSet<int>(graph.SealedLinks);

            // In the open layout most gates become plain doors; a share stays locked as optional vaults.
            lockedGateZones.Clear();
            foreach (Zone gateZone in graph.Zones)
            {
                if (gateZone.GateLink >= 0 && gateZone.Gate == GateKind.Terminal
                    && (!Site44Tuning.OpenLayout || Rand.Chance(Site44Tuning.OpenLayoutLockedShare)))
                {
                    lockedGateZones.Add(gateZone.Id);
                }
            }
            Dictionary<long, int> linkAt = new Dictionary<long, int>();
            for (int i = 0; i < plan.Links.Count; i++)
            {
                linkAt[plan.Links[i].Cell.X + (long)plan.Links[i].Cell.Y * plan.Width] = i;
            }

            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    IntVec3 c = World(x, y);
                    switch (plan.At(x, y))
                    {
                        case PlanTile.Wall:
                            SpawnWall(c);
                            break;
                        case PlanTile.Window:
                            GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_ViewportWall, ThingDefOf.Steel), c, map);
                            break;
                        case PlanTile.Door:
                            int link;
                            if (!linkAt.TryGetValue(x + (long)y * plan.Width, out link))
                            {
                                link = -1;
                            }

                            ReserveAround(c);
                            SecureRoom secureAtDoor = link >= 0 ? secureRooms.FirstOrDefault(sr => sr.linkIndex == link) : null;
                            if (secureAtDoor != null)
                            {
                                Building_SealedHatch vaultHatch = (Building_SealedHatch)ThingMaker.MakeThing(Site44DefOf.Site44_SealedHatch);
                                vaultHatch.sectorId = secureAtDoor.sectorId;
                                vaultHatch.requiredTier = 0;
                                GenSpawn.Spawn(vaultHatch, c, map);
                            }
                            else if (link >= 0 && sealedLinks.Contains(link) && !Site44Tuning.OpenLayout)
                            {
                                SpawnWall(c);
                            }
                            else if (link >= 0 && gateZoneOfLink.ContainsKey(link) && lockedGateZones.Contains(gateZoneOfLink[link]))
                            {
                                Zone zone = graph.Zones[gateZoneOfLink[link]];
                                Building_SealedHatch hatch = (Building_SealedHatch)ThingMaker.MakeThing(Site44DefOf.Site44_SealedHatch);
                                hatch.sectorId = zone.Id.ToString();
                                hatch.requiredTier = zone.RequiredTier;
                                GenSpawn.Spawn(hatch, c, map);
                            }
                            else
                            {
                                GenSpawn.Spawn(ThingMaker.MakeThing(doorDef, ThingDefOf.Steel), c, map);
                            }
                            break;
                    }
                }
            }
        }

        private void ReserveAround(IntVec3 center)
        {
            foreach (IntVec3 around in CellRect.CenteredOn(center, 3, 3))
            {
                reserved.Add(around);
            }
        }

        private void SpawnWall(IntVec3 c)
        {
            GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_BlastWall, ThingDefOf.Steel), c, map);
        }

        // ----- outdoor paths -----

        // A walkway network: each exterior door joins the nearest existing path (starting from the main yard)
        // along a route that prefers straight runs, so paths branch like real ones instead of running in parallel.
        private void PaintPaths()
        {
            int w = plan.Width;
            int h = plan.Height;
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            TerrainDef path = Terrain("PavedTile");

            List<int> targets = new List<int>();
            foreach (PlanLink link in plan.Links)
            {
                if (link.RoomB >= 0)
                {
                    continue;
                }

                for (int d = 0; d < 4; d++)
                {
                    int nx = link.Cell.X + dx[d];
                    int ny = link.Cell.Y + dy[d];
                    if (plan.InBounds(nx, ny) && plan.At(nx, ny) == PlanTile.Outside)
                    {
                        targets.Add(nx + ny * w);
                        break;
                    }
                }
            }

            int start = plan.Start.X + plan.Start.Y * w;
            targets = targets.OrderBy(t => Mathf.Abs(t % w - plan.Start.X) + Mathf.Abs(t / w - plan.Start.Y)).ToList();

            List<int> pathCells = new List<int> { start };
            StampPath(start, path);
            foreach (int target in targets)
            {
                int tx = target % w;
                int ty = target / w;
                if (pathCells.Any(c => Mathf.Max(Mathf.Abs(c % w - tx), Mathf.Abs(c / w - ty)) <= 3))
                {
                    continue;
                }

                List<int> route = FindPathRoute(target, pathCells, w, h);
                if (route == null)
                {
                    continue;
                }

                foreach (int cell in route)
                {
                    pathCells.Add(cell);
                    StampPath(cell, path);
                }
            }
        }

        // Dijkstra over open ground with a penalty for each turn, from a door to the nearest existing path cell.
        private List<int> FindPathRoute(int source, List<int> goals, int w, int h)
        {
            HashSet<int> goalSet = new HashSet<int>(goals);
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            int stateCount = w * h * 5;
            int[] best = new int[stateCount];
            int[] previous = new int[stateCount];
            for (int i = 0; i < stateCount; i++)
            {
                best[i] = int.MaxValue;
                previous[i] = -1;
            }

            SortedSet<long> open = new SortedSet<long>();
            int startState = source * 5 + 4;
            best[startState] = 0;
            open.Add(startState);
            while (open.Count > 0)
            {
                long top = open.Min;
                open.Remove(top);
                int state = (int)(top & 0xFFFFF);
                int cost = (int)(top >> 20);
                if (cost > best[state])
                {
                    continue;
                }

                int cell = state / 5;
                int dir = state % 5;
                if (goalSet.Contains(cell))
                {
                    List<int> route = new List<int>();
                    int walk = state;
                    while (walk >= 0)
                    {
                        route.Add(walk / 5);
                        walk = previous[walk];
                    }

                    return route;
                }

                int cx = cell % w;
                int cy = cell / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];
                    if (!plan.InBounds(nx, ny) || plan.At(nx, ny) != PlanTile.Outside)
                    {
                        continue;
                    }

                    int step = 1 + (dir != 4 && dir != d ? 5 : 0);
                    int nextState = (nx + ny * w) * 5 + d;
                    int nextCost = cost + step;
                    if (nextCost < best[nextState])
                    {
                        best[nextState] = nextCost;
                        previous[nextState] = state;
                        open.Add(((long)nextCost << 20) | (uint)nextState);
                    }
                }
            }

            return null;
        }

        // A 4x4 stamp per path cell leaves walkways four cells wide in any direction.
        private void StampPath(int cell, TerrainDef path)
        {
            int x = cell % plan.Width;
            int y = cell / plan.Width;
            for (int px = -1; px <= 2; px++)
            {
                for (int py = -1; py <= 2; py++)
                {
                    PaintPathCell(x + px, y + py, path);
                }
            }
        }

        private void PaintPathCell(int x, int y, TerrainDef path)
        {
            if (plan.InBounds(x, y) && plan.At(x, y) == PlanTile.Outside)
            {
                IntVec3 c = World(x, y);
                ClearForBuilding(c);
                map.terrainGrid.SetTerrain(c, path);
            }
        }

        // Digs out a cell that the complex needs (natural rock, ruins, rubble, water); cells it leaves alone stay natural.
        private void ClearForBuilding(IntVec3 c)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                if (things[i].def.category != ThingCategory.Pawn)
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
            if (existing != null && (existing.IsWater || existing.IsRiver))
            {
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                waterFlattened++;
            }
        }

        // Clears loose man-made clutter (ruins, rubble, items) but leaves natural rock and terrain alone.
        private void ClearClutter(IntVec3 c)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing t = things[i];
                ThingCategory category = t.def.category;
                bool rock = t.def.building != null && t.def.building.isNaturalRock;
                if ((category == ThingCategory.Building && !rock) || category == ThingCategory.Filth || category == ThingCategory.Item)
                {
                    t.Destroy();
                }
            }
        }
        // ----- door markers -----

        // Steles and bright lamps mark each front door from outside, tinted with the complex type's accent colour and
        // made of its own material. The layout varies per building: a flanking pair, a tall column, a processional
        // avenue or a quad. The principal entrance always gets the grand avenue.
        private void PlaceDoorMarkers()
        {
            SiteTypeDef type = SiteTypes.Current;
            Color accent = type != null ? type.accent : Color.white;
            ThingDef stuff = type != null && !string.IsNullOrEmpty(type.steleStuff)
                ? DefDatabase<ThingDef>.GetNamedSilentFail(type.steleStuff) ?? ThingDefOf.Steel
                : ThingDefOf.Steel;
            foreach (Foyer foyer in foyers)
            {
                IntVec3 door = World(foyer.door);
                IntVec3 outward = door - foyer.inside;
                if ((outward.x != 0) == (outward.z != 0))
                {
                    continue;
                }

                IntVec3 lateral = new IntVec3(outward.z != 0 ? 1 : 0, 0, outward.x != 0 ? 1 : 0);
                int roll = foyer.principal ? 70 : Rand.RangeInclusive(0, 99);
                foreach (int sign in new[] { -1, 1 })
                {
                    if (roll < 40)
                    {
                        // Pair: one stele and one lamp either side.
                        Mark(door, outward, lateral, sign, 2, 2, stuff, accent);
                        Lamp(door, outward, lateral, sign, 3, 2);
                    }
                    else if (roll < 65)
                    {
                        // Column: three steles deep either side, capped by a lamp.
                        for (int depth = 2; depth <= 4; depth++)
                        {
                            Mark(door, outward, lateral, sign, 2, depth, stuff, accent);
                        }

                        Lamp(door, outward, lateral, sign, 2, 5);
                    }
                    else if (roll < 85)
                    {
                        // Avenue: steles every third cell leading out from the door, lamps between them.
                        for (int step = 2; step <= 8; step += 3)
                        {
                            Mark(door, outward, lateral, sign, 2, step, stuff, accent);
                            Lamp(door, outward, lateral, sign, 2, step + 1);
                        }

                        if (foyer.principal)
                        {
                            Mark(door, outward, lateral, sign, 5, 2, stuff, accent);
                            Lamp(door, outward, lateral, sign, 6, 2);
                        }
                    }
                    else
                    {
                        // Quad: four steles around a small forecourt.
                        Mark(door, outward, lateral, sign, 2, 2, stuff, accent);
                        Mark(door, outward, lateral, sign, 2, 5, stuff, accent);
                        Lamp(door, outward, lateral, sign, 3, 3);
                    }
                }
            }
        }

        private void Mark(IntVec3 door, IntVec3 outward, IntVec3 lateral, int sign, int sideways, int forward, ThingDef stuff, Color accent)
        {
            TrySpawnOutside(Site44DefOf.Site44_Stele, door + outward * forward + lateral * (sign * sideways), accent, stuff);
        }

        private void Lamp(IntVec3 door, IntVec3 outward, IntVec3 lateral, int sign, int sideways, int forward)
        {
            TrySpawnOutside(Site44DefOf.Site44_LampBright, door + outward * forward + lateral * (sign * sideways), null, null);
        }
        private void TrySpawnOutside(ThingDef def, IntVec3 cell, Color? tint, ThingDef stuff)
        {
            if (!cell.InBounds(map) || plan.At(cell.x - origin.x, cell.z - origin.z) != PlanTile.Outside || cell.GetEdifice(map) != null)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? (stuff ?? ThingDefOf.Steel) : null);
            if (tint.HasValue)
            {
                thing.SetColor(tint.Value, false);
            }

            GenSpawn.Spawn(thing, cell, map);
        }

        // ----- entrances -----

        // Doors into big special rooms get a marble threshold and bright lamps either side, so they are easy to spot.
        private void MarkEntrances()
        {
            HashSet<string> special = new HashSet<string> { "Auditorium", "Vault", "MachineHall", "Atrium", "ServerRoom", "Cafeteria", "Lab" };
            TerrainDef threshold = Terrain("TileMarble");
            foreach (PlanLink link in plan.Links)
            {
                bool big = false;
                foreach (int roomId in new[] { link.RoomA, link.RoomB })
                {
                    if (roomId < 0)
                    {
                        continue;
                    }

                    PlanRoom room = plan.Rooms[roomId];
                    if (!room.IsCorridor && room.Cells.Count >= 100 && special.Contains(room.Function.ToString()))
                    {
                        big = true;
                    }
                }

                if (!big)
                {
                    continue;
                }

                int x = link.Cell.X;
                int y = link.Cell.Y;
                bool horizontal = plan.At(x - 1, y) == PlanTile.Floor && plan.At(x + 1, y) == PlanTile.Floor;
                int ax = horizontal ? 1 : 0;
                int ay = horizontal ? 0 : 1;
                foreach (int sign in new[] { -1, 1 })
                {
                    for (int step = 1; step <= 2; step++)
                    {
                        int fx = x + ax * sign * step;
                        int fy = y + ay * sign * step;
                        if (plan.At(fx, fy) != PlanTile.Floor)
                        {
                            break;
                        }

                        IntVec3 cell = World(fx, fy);
                        map.terrainGrid.SetTerrain(cell, threshold);
                        if (step == 2 && IsFree(cell))
                        {
                            GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_LampBright), cell, map);
                        }
                    }
                }

                map.terrainGrid.SetTerrain(World(link.Cell), threshold);
            }
        }

        // ----- special rooms -----

        private sealed class SecureRoom
        {
            public PlanRoom room;
            public PlanRoom outer;
            public int linkIndex;
            public string sectorId;
            public int kind; // 0 treasure vault, 1 prototype droid, 2 volatile cache
        }

        private sealed class Foyer
        {
            public int roomId;
            public PlanRoom area;
            public IntVec3 inside;
            public bool principal;
            public LayoutCell door;
        }

        private static bool IsRecCandidate(string function)
        {
            return function == "BreakRoom" || function == "Cafeteria" || function == "OpenOffice" || function == "Auditorium";
        }

        // A few ordinary rooms turn out to be rec rooms: a billiards, card or chess table, a television and couches.
        private void PlanRecRooms()
        {
            float frequency = Site44Tuning.RecRoomFrequency;
            if (frequency <= 0f)
            {
                return;
            }

            List<PlanRoom> candidates = plan.Rooms
                .Where(r => !r.IsCorridor && r.Cells.Count >= 36 && r.Cells.Count <= 220 && IsRecCandidate(r.Function.ToString())
                            && !executiveRooms.Contains(r.Id) && graph.Zones[graph.ZoneOfRoom(r.Id)].Kind != SectorKind.Courtyard)
                .ToList();
            candidates.Shuffle();
            recRoomCount = Mathf.Clamp(Mathf.RoundToInt(candidates.Count / 28f * frequency), 0, 6);
            foreach (PlanRoom room in candidates.Take(recRoomCount))
            {
                room.Function = RoomFunction.RecRoom;
            }
        }

        private sealed class Specialty
        {
            public RoomFunction function;
            public string[] sources;
            public string[] archetypes; // null = any building
            public int minCells;
            public int maxCells;
            public int maxCount;
            public bool nearCafeteria;
        }

        private static readonly Specialty[] Specialties =
        {
            new Specialty { function = RoomFunction.Kitchen, sources = new[] { "BreakRoom", "Cafeteria", "Storeroom" }, archetypes = new[] { "Commons", "Office" }, minCells = 20, maxCells = 90, maxCount = 2, nearCafeteria = true },
            new Specialty { function = RoomFunction.Dormitory, sources = new[] { "OpenOffice", "Storeroom", "BreakRoom" }, archetypes = new[] { "Commons", "Security", "Lab" }, minCells = 40, maxCells = 200, maxCount = 2 },
            new Specialty { function = RoomFunction.Infirmary, sources = new[] { "Lab", "PrivateOffice", "OpenOffice", "Storeroom" }, archetypes = new[] { "Lab", "Office", "Commons", "Security" }, minCells = 30, maxCells = 120, maxCount = 2 },
            new Specialty { function = RoomFunction.LockerRoom, sources = new[] { "Storeroom", "Closet", "Restroom" }, archetypes = new[] { "Security", "Commons", "Lab", "Plant" }, minCells = 16, maxCells = 70, maxCount = 2 },
            new Specialty { function = RoomFunction.Library, sources = new[] { "Archive", "OpenOffice", "Conference" }, archetypes = new[] { "Office", "Lab", "Commons" }, minCells = 40, maxCells = 160, maxCount = 2 },
            new Specialty { function = RoomFunction.Workshop, sources = new[] { "Storeroom", "Mechanical", "Boiler", "OpenOffice" }, archetypes = new[] { "Plant", "Lab", "Warehouse", "Security" }, minCells = 30, maxCells = 150, maxCount = 2 },
            new Specialty { function = RoomFunction.Armory, sources = new[] { "SecurityPost", "Storeroom" }, archetypes = null, minCells = 14, maxCells = 70, maxCount = 1 },
            new Specialty { function = RoomFunction.CommsRoom, sources = new[] { "ServerRoom", "PrivateOffice", "SecurityPost" }, archetypes = new[] { "Office", "Security" }, minCells = 14, maxCells = 60, maxCount = 1 },
            new Specialty { function = RoomFunction.Classroom, sources = new[] { "OpenOffice", "Conference" }, archetypes = new[] { "Office", "Lab", "Commons" }, minCells = 30, maxCells = 120, maxCount = 1 }
        };

        private string specialtySummary = "";

        // Ordinary rooms are repurposed into more specific ones, using only vanilla furniture: kitchens near cafeterias,
        // dormitories, clinics, locker rooms, libraries, workshops, armories, comms rooms and classrooms.
        private void PlanSpecialtyRooms()
        {
            specialtySummary = "";
            float variety = Site44Tuning.RoomVariety;
            if (variety <= 0f)
            {
                return;
            }

            HashSet<int> taken = new HashSet<int>(secureRooms.SelectMany(v => new[] { v.room.Id, v.outer.Id }));
            foreach (Foyer foyer in foyers)
            {
                taken.Add(foyer.roomId);
            }

            foreach (int id in executiveRooms)
            {
                taken.Add(id);
            }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (Specialty specialty in Specialties.OrderBy(x => Rand.Value))
            {
                int allowed = Mathf.RoundToInt(SpecialtyCount(specialty) * variety);
                if (allowed <= 0)
                {
                    continue;
                }

                List<PlanRoom> candidates = plan.Rooms
                    .Where(r => !r.IsCorridor && !taken.Contains(r.Id) && r.Cells.Count >= specialty.minCells && r.Cells.Count <= specialty.maxCells
                                && specialty.sources.Contains(r.Function.ToString())
                                && (specialty.archetypes == null || IsFlexibleArchetype(plan.Buildings[r.BuildingId].Archetype)
                                    || specialty.archetypes.Contains(plan.Buildings[r.BuildingId].Archetype ?? ""))
                                && graph.Zones[graph.ZoneOfRoom(r.Id)].Kind != SectorKind.Courtyard
                                && !plan.Links.Any(l => l.RoomB < 0 && l.RoomA == r.Id))
                    .OrderBy(r => Rand.Value)
                    .ToList();
                if (specialty.nearCafeteria)
                {
                    candidates = candidates.OrderByDescending(r => NextToFunction(r, "Cafeteria")).ToList();
                }

                int made = 0;
                foreach (PlanRoom room in candidates)
                {
                    if (made >= allowed)
                    {
                        break;
                    }

                    room.Function = specialty.function;
                    taken.Add(room.Id);
                    made++;
                }

                if (made > 0)
                {
                    counts[specialty.function.ToString()] = made;
                }
            }

            specialtySummary = string.Join(",", counts.Select(kv => kv.Key + "=" + kv.Value).ToArray());
        }

        // The site type can name how many of each specialty it wants; unnamed ones get a reduced default share.
        private static int SpecialtyCount(Specialty specialty)
        {
            SiteTypeDef type = SiteTypes.Current;
            if (type == null || type.specialties == null || type.specialties.Count == 0)
            {
                return specialty.maxCount;
            }

            WeightedName named = type.specialties.FirstOrDefault(w => w.name == specialty.function.ToString());
            return named != null ? Mathf.RoundToInt(named.weight) : Mathf.RoundToInt(specialty.maxCount * 0.5f);
        }

        // The newer building archetypes accept any specialty room.
        private static bool IsFlexibleArchetype(string archetype)
        {
            return archetype == "Medical" || archetype == "Barracks" || archetype == "Residential" || archetype == "Research";
        }

        private bool NextToFunction(PlanRoom room, string function)
        {
            foreach (PlanLink link in plan.Links)
            {
                if (link.RoomB < 0)
                {
                    continue;
                }

                int other = link.RoomA == room.Id ? link.RoomB : link.RoomB == room.Id ? link.RoomA : -1;
                if (other >= 0 && plan.Rooms[other].Function.ToString() == function)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSecureCandidate(string function)
        {
            return function == "ServerRoom" || function == "Storeroom" || function == "Archive" || function == "PrivateOffice"
                   || function == "Lab" || function == "Mailroom" || function == "Vault" || function == "Containment" || function == "Pharmacy";
        }

        // Locked dead-end rooms: one door, opened by hacking a terminal in the room outside. Each holds a surprise.
        private void PlanSecureRooms()
        {
            float density = Site44Tuning.SecureRoomDensity;
            if (density <= 0f)
            {
                return;
            }

            HashSet<int> gateLinks = new HashSet<int>(graph.Zones.Where(z => z.GateLink >= 0).Select(z => z.GateLink));
            HashSet<int> sealedSet = new HashSet<int>(graph.SealedLinks);
            List<SecureRoom> options = new List<SecureRoom>();
            for (int li = 0; li < plan.Links.Count; li++)
            {
                PlanLink link = plan.Links[li];
                if (link.RoomB < 0 || gateLinks.Contains(li) || sealedSet.Contains(li))
                {
                    continue;
                }

                TryAddSecureOption(options, li, link.RoomA, link.RoomB);
                TryAddSecureOption(options, li, link.RoomB, link.RoomA);
            }

            options.Shuffle();
            int count = Mathf.Clamp(Mathf.RoundToInt(options.Count / 30f * density), 0, 6);
            HashSet<int> used = new HashSet<int>();
            bool droidOk = Site44Tuning.DroidGuards && Faction.OfMechanoids != null;
            int droidCut = 45 + Mathf.RoundToInt(30f * Site44Tuning.DroidGuardChance / 0.35f);
            foreach (SecureRoom option in options)
            {
                if (secureRooms.Count >= count)
                {
                    break;
                }

                if (used.Contains(option.room.Id) || used.Contains(option.outer.Id))
                {
                    continue;
                }

                used.Add(option.room.Id);
                used.Add(option.outer.Id);
                option.sectorId = "vault" + secureRooms.Count;
                int roll = Rand.RangeInclusive(0, 99);
                bool far = World(option.room.Anchor).DistanceTo(startSpotForSafety) >= 80f
                           && graph.Zones[graph.ZoneOfRoom(option.room.Id)].Depth >= 2;
                option.kind = roll < 45 ? 0 : (roll < droidCut && droidOk && far) ? 1 : 2;
                secureRooms.Add(option);
            }
        }

        private void TryAddSecureOption(List<SecureRoom> options, int linkIndex, int innerId, int outerId)
        {
            PlanRoom inner = plan.Rooms[innerId];
            if (inner.IsCorridor || inner.Cells.Count < 14 || inner.Cells.Count > 140 || !IsSecureCandidate(inner.Function.ToString())
                || executiveRooms.Contains(innerId))
            {
                return;
            }

            if (plan.Links.Count(l => l.RoomA == innerId || l.RoomB == innerId) != 1)
            {
                return;
            }

            Zone zone = graph.Zones[graph.ZoneOfRoom(innerId)];
            if (zone.Kind == SectorKind.Courtyard || graph.ZoneOfRoom(outerId) != zone.Id)
            {
                return;
            }

            options.Add(new SecureRoom { room = inner, outer = plan.Rooms[outerId], linkIndex = linkIndex });
        }

        private void PopulateSecureRooms()
        {
            FacilityContainers containers = new FacilityContainers(map, plan, origin, IsFree);
            MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
            foreach (SecureRoom vault in secureRooms)
            {
                // The control terminal stands in the room outside the door.
                IntVec3 spot;
                if (TryFindSpotInRoom(vault.outer, Site44DefOf.Site44_Terminal.size, out spot))
                {
                    Building_FacilityTerminal terminal = SpawnConsole(Site44DefOf.Site44_Terminal, spot, TerminalKind.Gate);
                    terminal.sectorId = vault.sectorId;
                    terminal.requiredTier = 0;
                }
                else
                {
                    // No room for a terminal: never leave a room that cannot be opened.
                    facility.UnsealSector(vault.sectorId);
                }

                switch (vault.kind)
                {
                    case 0:
                        for (int i = 0; i < 3; i++)
                        {
                            containers.TrySpawnCrate(vault.room, true);
                        }
                        break;
                    case 1:
                        containers.TrySpawnCrate(vault.room, true);
                        containers.TrySpawnCrate(vault.room, true);
                        SpawnVaultDroid(vault.room);
                        break;
                    default:
                        containers.TrySpawnCrate(vault.room, true);
                        containers.TrySpawnCrate(vault.room, true);
                        ThingDef fuel = DefDatabase<ThingDef>.GetNamedSilentFail("AncientFuelNode");
                        for (int i = 0; fuel != null && i < 3; i++)
                        {
                            IntVec3 cell;
                            if (TryFindFreeCell(vault.room, out cell))
                            {
                                GenSpawn.Spawn(ThingMaker.MakeThing(fuel), cell, map);
                            }
                        }
                        break;
                }
            }
        }

        private void SpawnVaultDroid(PlanRoom room)
        {
            Pawn droid = FacilityMechs.Make("Mech_Pikeman", Faction.OfMechanoids);
            IntVec3 cell;
            if (droid == null || !TryFindFreeCell(room, out cell))
            {
                return;
            }

            GenSpawn.Spawn(droid, cell, map);
            ownedHostiles.Add(droid);
            LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_DefendPoint(World(room.Anchor), null, 10f), map, new List<Pawn> { droid });
        }

        // The front door of each office building gets a foyer: a marble patch with a reception desk and seating,
        // so every approach to a building announces where its entrance is. The principal one keeps the supply crate.
        private void PlanFoyers()
        {
            foreach (PlanBuilding building in plan.Buildings)
            {
                string archetype = building.Archetype ?? "";
                if (archetype == "Warehouse" || archetype == "Plant")
                {
                    continue;
                }

                List<int> exteriors = new List<int>();
                for (int i = 0; i < plan.Links.Count; i++)
                {
                    if (plan.Links[i].RoomB < 0 && plan.Rooms[plan.Links[i].RoomA].BuildingId == building.Id)
                    {
                        exteriors.Add(i);
                    }
                }

                if (exteriors.Count == 0)
                {
                    continue;
                }

                int pick = -1;
                if (building.Id == 0)
                {
                    foreach (int i in exteriors)
                    {
                        if (plan.Links[i].Cell.X == plan.MainEntrance.X && plan.Links[i].Cell.Y == plan.MainEntrance.Y)
                        {
                            pick = i;
                        }
                    }
                }

                if (pick < 0)
                {
                    pick = exteriors.OrderByDescending(i => plan.Rooms[plan.Links[i].RoomA].Cells.Count).First();
                }

                PlanLink link = plan.Links[pick];
                PlanRoom inside = plan.Rooms[link.RoomA];
                int[] dx = { 1, -1, 0, 0 };
                int[] dy = { 0, 0, 1, -1 };
                LayoutCell insideCell = default(LayoutCell);
                bool found = false;
                for (int d = 0; d < 4 && !found; d++)
                {
                    int nx = link.Cell.X + dx[d];
                    int ny = link.Cell.Y + dy[d];
                    if (plan.RoomAt(nx, ny) == inside.Id)
                    {
                        insideCell = new LayoutCell(nx, ny);
                        found = true;
                    }
                }

                if (!found)
                {
                    continue;
                }

                List<LayoutCell> area = inside.Cells
                    .Where(c => Mathf.Max(Mathf.Abs(c.X - insideCell.X), Mathf.Abs(c.Y - insideCell.Y)) <= 3)
                    .ToList();
                if (area.Count < 8)
                {
                    continue;
                }

                PlanRoom pseudo = new PlanRoom
                {
                    Id = inside.Id,
                    Function = RoomFunction.Reception,
                    IsCorridor = false,
                    BuildingId = inside.BuildingId,
                    Cells = area,
                    Anchor = insideCell
                };
                foyers.Add(new Foyer { roomId = inside.Id, area = pseudo, inside = World(insideCell), principal = building.Id == 0, door = link.Cell });
            }
        }

        private void DecorateFoyer(FacilityDecor decor, Foyer foyer)
        {
            TerrainDef marble = Terrain("TileMarble");
            foreach (LayoutCell cell in foyer.area.Cells)
            {
                map.terrainGrid.SetTerrain(World(cell), marble);
            }

            decor.DecorateReception(foyer.area);

            // A bright lamp at the back of the foyer.
            LayoutCell lampCell = foyer.area.Cells
                .OrderByDescending(c => Mathf.Abs(World(c).x - foyer.inside.x) + Mathf.Abs(World(c).z - foyer.inside.z))
                .FirstOrDefault(c => IsFree(World(c)));
            if (IsFree(World(lampCell)))
            {
                GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_LampBright), World(lampCell), map);
            }

            if (foyer.principal)
            {
                FacilityContainers containers = new FacilityContainers(map, plan, origin, IsFree);
                Thing crate = containers.TrySpawnSupplyCrate(foyer.area);
                if (crate != null)
                {
                    welcomePos = crate.Position;
                }
            }

            foreach (LayoutCell cell in foyer.area.Cells)
            {
                reserved.Add(World(cell));
            }
        }

        // ----- zone contents -----

        private IEnumerable<PlanRoom> RoomsOf(Zone zone)
        {
            return zone.RoomIds.Select(id => plan.Rooms[id]);
        }

        private void BuildZone(Zone zone)
        {
            List<PlanRoom> rooms = RoomsOf(zone).ToList();
            if (rooms.Count == 0)
            {
                return;
            }

            HashSet<int> preserved = ChoosePreservedRooms(zone, rooms);
            preservedTotal += preserved.Count;
            ApplyFloors(zone, rooms, preserved);
            PlaceGateTerminals(zone, rooms);

            PlanRoom main = rooms.Where(r => !r.IsCorridor).OrderByDescending(r => r.Cells.Count).FirstOrDefault() ?? rooms[0];
            switch (zone.Kind)
            {
                case SectorKind.Diagnostic:
                    PlaceConsole(Site44DefOf.Site44_DiagnosticNode, rooms, TerminalKind.Diagnostic, "ServerRoom");
                    break;
                case SectorKind.Valve:
                    PlaceConsole(Site44DefOf.Site44_CoolantValve, rooms, TerminalKind.Valve, "Mechanical", "Boiler", "MachineHall");
                    break;
                case SectorKind.Reactor:
                    PlaceConsole(Site44DefOf.Site44_ReactorConsole, rooms, TerminalKind.Reactor, "MachineHall", "Mechanical", "Boiler");
                    break;
                case SectorKind.Storage:
                    SpawnLoot(rooms, zone);
                    break;
                case SectorKind.Security:
                    SpawnSecurity(rooms, zone, main);
                    break;
                case SectorKind.HotZone:
                    foreach (PlanRoom room in rooms)
                    {
                        hotAnchors.Add(World(room.Anchor));
                    }
                    break;
            }

            SpawnLamps(rooms, zone);
            FacilityDecor decor = new FacilityDecor(map, plan, origin, IsFree);
            foreach (PlanRoom room in rooms)
            {
                foreach (Foyer foyer in foyers.Where(f => f.roomId == room.Id))
                {
                    DecorateFoyer(decor, foyer);
                }

                if (IsReceptionRoom(zone, room))
                {
                    decor.DecorateReception(room);
                    if (room.Function.ToString() == "Lobby" || welcomeRoom == null)
                    {
                        welcomeRoom = room;
                    }
                }
                else if (executiveRooms.Contains(room.Id))
                {
                    decor.DecorateExecutive(room);
                }
                else
                {
                    decor.Decorate(room, preserved.Contains(room.Id));
                }

                // The entrance zone stays free of caskets, corpses and stray crates: the start must be safe.
                if (!room.IsCorridor && room.Cells.Count >= 10 && zone.Kind != SectorKind.Courtyard)
                {
                    containerRooms.Add(new KeyValuePair<PlanRoom, Zone>(room, zone));
                }
            }
        }

        // The first rooms after the main entrance: a polished, lit reception area.
        private static bool IsReceptionRoom(Zone zone, PlanRoom room)
        {
            string function = room.Function.ToString();
            return zone.Kind == SectorKind.Courtyard && (function == "Lobby" || function == "Reception" || function == "Atrium");
        }

        // Crates, caskets and stray corpses go in after every room is furnished, so quotas are map-wide.
        private void PlaceContainers()
        {
            FacilityContainers containers = new FacilityContainers(map, plan, origin, IsFree);
            int crates = 0;

            // Every executive office keeps a sealed crate.
            foreach (int roomId in executiveRooms)
            {
                if (containers.TrySpawnCrate(plan.Rooms[roomId]))
                {
                    crates++;
                }
            }
            int strays = 0;
            foreach (KeyValuePair<PlanRoom, Zone> pair in containerRooms)
            {
                string function = pair.Key.Function.ToString();
                bool stock = function == "Storeroom" || function == "Archive" || function == "Warehouse" || function == "ServerRoom"
                             || function == "Lab" || function == "Mailroom" || pair.Value.Kind == SectorKind.Storage;
                if (Rand.Chance(stock ? Site44Tuning.CrateChanceStock : Site44Tuning.CrateChanceOther) && containers.TrySpawnCrate(pair.Key))
                {
                    crates++;
                }

                if (Rand.Chance(Site44Tuning.StrayCorpseChance) && containers.TrySpawnStrayCorpse(pair.Key))
                {
                    strays++;
                }
            }

            int total = Site44Tuning.CasketDensity <= 0f
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(containerRooms.Count / (float)Site44Tuning.RoomsPerCasket * Site44Tuning.CasketDensity), 1, 40);
            List<CasketKind> kinds = new List<CasketKind>();
            for (int i = 0; i < Mathf.Min(Site44Tuning.SurvivorCaskets, total); i++)
            {
                kinds.Add(CasketKind.Survivor);
            }

            int remaining = Mathf.Max(0, total - kinds.Count);
            int hostile = Mathf.RoundToInt(remaining * Site44Tuning.HostileCasketShare);
            int dead = Mathf.Min(remaining - hostile, Mathf.RoundToInt(remaining * 0.40f));
            for (int i = 0; i < hostile; i++)
            {
                kinds.Add(CasketKind.Hostile);
            }

            for (int i = 0; i < dead; i++)
            {
                kinds.Add(CasketKind.Dead);
            }

            while (kinds.Count < total)
            {
                kinds.Add(CasketKind.Loot);
            }

            int placed = 0;
            foreach (CasketKind kind in kinds)
            {
                // Hostile sleepers lie deep inside and well away from the start; the entrance area is never trapped.
                int minDepth = kind == CasketKind.Hostile ? 3 : kind == CasketKind.Survivor ? 1 : kind == CasketKind.Dead ? 1 : 0;
                List<KeyValuePair<PlanRoom, Zone>> options = containerRooms
                    .Where(p => p.Value.Depth >= minDepth
                                && (kind != CasketKind.Hostile || World(p.Key.Anchor).DistanceTo(startSpotForSafety) >= 70f))
                    .ToList();
                options.Shuffle();
                foreach (KeyValuePair<PlanRoom, Zone> option in options.Take(30))
                {
                    if (containers.TrySpawnCasket(option.Key, kind))
                    {
                        placed++;
                        break;
                    }
                }
            }

            containerSummary = "crates=" + crates + " caskets=" + placed + "/" + total + " strayCorpses=" + strays;
        }

        // A few rooms survived intact. Storage, diagnostic and reactor zones always hold at least one.
        private HashSet<int> ChoosePreservedRooms(Zone zone, List<PlanRoom> rooms)
        {
            HashSet<int> chosen = new HashSet<int>();
            foreach (SecureRoom vault in secureRooms)
            {
                if (rooms.Any(r => r.Id == vault.room.Id))
                {
                    chosen.Add(vault.room.Id);
                }
            }

            if (zone.Kind == SectorKind.Courtyard)
            {
                return chosen;
            }

            List<PlanRoom> candidates = rooms.Where(r => !r.IsCorridor && r.Cells.Count >= 9 && r.Cells.Count <= 160).ToList();
            foreach (PlanRoom room in candidates)
            {
                if (Rand.Chance(Site44Tuning.PreservedRoomChance))
                {
                    chosen.Add(room.Id);
                }
            }

            bool guaranteed = zone.Kind == SectorKind.Storage || zone.Kind == SectorKind.Diagnostic || zone.Kind == SectorKind.Reactor;
            if (guaranteed && chosen.Count == 0 && candidates.Count > 0)
            {
                chosen.Add(candidates.RandomElement().Id);
            }

            return chosen;
        }

        // ----- floors -----

        private void ApplyFloors(Zone zone, List<PlanRoom> rooms, HashSet<int> preserved)
        {
            foreach (PlanRoom room in rooms)
            {
                TerrainDef floor = preserved.Contains(room.Id) ? Terrain("SterileTile") : FloorFor(room);
                bool signature = false;
                if (IsReceptionRoom(zone, room) && room.Function.ToString() == "Reception")
                {
                    floor = DefDatabase<TerrainDef>.GetNamedSilentFail("CarpetBurgundy") ?? floor;
                    signature = true;
                }
                else if (IsReceptionRoom(zone, room))
                {
                    signature = true;
                }
                else if (executiveRooms.Contains(room.Id))
                {
                    floor = DefDatabase<TerrainDef>.GetNamedSilentFail("CarpetBrownDark") ?? floor;
                    signature = true;
                }

                if (!signature && !preserved.Contains(room.Id) && CarpetChance(room.Function.ToString()) > 0f && Rand.Chance(CarpetChance(room.Function.ToString())))
                {
                    // Carpet is a template: the game generates one terrain per colour (CarpetBlueSubtle, CarpetBrownDark, ...).
                    TerrainDef carpet = DefDatabase<TerrainDef>.GetNamedSilentFail("Carpet" + CarpetColors.RandomElement());
                    if (carpet != null)
                    {
                        floor = carpet;
                    }
                }

                if (floor == null)
                {
                    continue;
                }

                foreach (LayoutCell cell in room.Cells)
                {
                    map.terrainGrid.SetTerrain(World(cell), floor);
                }
            }
        }

        private static readonly string[] CarpetColors = { "BlueSubtle", "BrownDark", "GreyDark", "GreenForest", "Burgundy", "RedSubtle", "PurpleMuted", "BrownFaded", "Teal", "GreenFaded", "Slate", "Marine" };

        // Some rooms are carpeted: offices and meeting rooms often, lobbies and halls now and then.
        private static float CarpetChance(string function)
        {
            switch (function)
            {
                case "PrivateOffice":
                case "Conference":
                    return 0.55f;
                case "Reception":
                case "OpenOffice":
                case "Lobby":
                    return 0.40f;
                case "Apartment":
                    return 0.55f;
                case "Library":
                    return 0.60f;
                case "Dormitory":
                    return 0.50f;
                case "Classroom":
                    return 0.35f;
                case "RecRoom":
                    return 0.65f;
                case "Auditorium":
                case "BreakRoom":
                    return 0.30f;
                default:
                    return 0f;
            }
        }

        private static TerrainDef Terrain(string name)
        {
            return DefDatabase<TerrainDef>.GetNamed(name);
        }

        private static TerrainDef FloorFor(PlanRoom room)
        {
            switch (room.Function.ToString())
            {
                case "Lobby":
                case "Reception":
                case "Atrium":
                case "Auditorium":
                    return Terrain("TileMarble");
                case "Vault":
                    return TerrainDefOf.MetalTile;
                case "OpenOffice":
                case "PrivateOffice":
                case "Conference":
                case "BreakRoom":
                case "Cafeteria":
                case "Mailroom":
                case "RecRoom":
                    return Terrain("TileLimestone");
                case "Restroom":
                case "Lab":
                    return Terrain("SterileTile");
                case "ServerRoom":
                case "Mechanical":
                case "MachineHall":
                case "Boiler":
                case "SecurityPost":
                    return TerrainDefOf.MetalTile;
                case "Storeroom":
                case "Archive":
                case "Warehouse":
                    return Terrain("TileGranite");
                case "Kitchen":
                case "Infirmary":
                case "Ward":
                case "OperatingRoom":
                case "Pharmacy":
                case "Morgue":
                    return Terrain("SterileTile");
                case "Apartment":
                    return Terrain("TileLimestone");
                case "Bunkroom":
                case "Cell":
                    return TerrainDefOf.Concrete;
                case "Containment":
                    return TerrainDefOf.MetalTile;
                case "Dormitory":
                case "Library":
                case "Classroom":
                    return Terrain("TileLimestone");
                case "Armory":
                case "CommsRoom":
                    return TerrainDefOf.MetalTile;
                case "LockerRoom":
                case "Workshop":
                    return TerrainDefOf.Concrete;
                case "Corridor":
                    return Terrain("PavedTile");
                default:
                    return room.IsCorridor ? Terrain("PavedTile") : TerrainDefOf.Concrete;
            }
        }

        // ----- consoles -----

        private void PlaceGateTerminals(Zone parent, List<PlanRoom> rooms)
        {
            foreach (int childId in parent.Children)
            {
                Zone child = graph.Zones[childId];
                if (child.Gate != GateKind.Terminal || !lockedGateZones.Contains(child.Id))
                {
                    continue;
                }

                PlanLink link = plan.Links[child.GateLink];
                if (link.RoomB < 0)
                {
                    // An exterior gate: the control pedestal stands outside next to the door.
                    IntVec3 outsideSpot;
                    if (TryFindOutsideSpot(World(link.Cell), Site44DefOf.Site44_Terminal.size, out outsideSpot))
                    {
                        Building_FacilityTerminal pedestal = SpawnConsole(Site44DefOf.Site44_Terminal, outsideSpot, TerminalKind.Gate);
                        pedestal.sectorId = child.Id.ToString();
                        pedestal.requiredTier = child.RequiredTier;
                    }
                    else
                    {
                        Log.Warning("[Site-44] no outside spot for the exterior gate of zone " + child.Id);
                    }

                    continue;
                }

                int roomId = graph.ZoneOfRoom(link.RoomA) == parent.Id ? link.RoomA : link.RoomB;

                List<PlanRoom> order = new List<PlanRoom>();
                if (roomId >= 0)
                {
                    order.Add(plan.Rooms[roomId]);
                }

                order.AddRange(rooms.Where(r => r.Id != roomId).OrderBy(r => r.IsCorridor ? 1 : 0));

                IntVec3 spot = IntVec3.Invalid;
                bool found = false;
                foreach (PlanRoom room in order)
                {
                    if (TryFindSpotInRoom(room, Site44DefOf.Site44_Terminal.size, out spot))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    Building_FacilityTerminal terminal = SpawnConsole(Site44DefOf.Site44_Terminal, spot, TerminalKind.Gate);
                    terminal.sectorId = child.Id.ToString();
                    terminal.requiredTier = child.RequiredTier;
                }
                else
                {
                    Log.Warning("[Site-44] no room for a gate terminal in zone " + parent.Id);
                }
            }
        }

        private bool TryFindOutsideSpot(IntVec3 door, IntVec2 size, out IntVec3 center)
        {
            for (int radius = 2; radius <= 9; radius++)
            {
                foreach (IntVec3 c in CellRect.CenteredOn(door, radius * 2 + 1, radius * 2 + 1).Cells.OrderBy(x => Rand.Value))
                {
                    CellRect occupied = GenAdj.OccupiedRect(c, Rot4.North, size);
                    bool ok = true;
                    foreach (IntVec3 cell in occupied)
                    {
                        if (plan.At(cell.x - origin.x, cell.z - origin.z) != PlanTile.Outside || !IsFree(cell)
                            || NearBuildingWall(cell, 1))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                    {
                        center = c;
                        return true;
                    }
                }
            }

            center = IntVec3.Invalid;
            return false;
        }

        private bool NearBuildingWall(IntVec3 c, int distance)
        {
            return NearBuilding(c.x - origin.x, c.z - origin.z, distance);
        }

        private void PlaceConsole(ThingDef def, List<PlanRoom> rooms, TerminalKind kind, params string[] preferredFunctions)
        {
            List<PlanRoom> order = rooms
                .OrderBy(r => System.Array.IndexOf(preferredFunctions, r.Function.ToString()) is int i && i >= 0 ? i : 99)
                .ThenBy(r => r.IsCorridor ? 1 : 0)
                .ToList();
            foreach (PlanRoom room in order)
            {
                IntVec3 spot;
                if (TryFindSpotInRoom(room, def.size, out spot))
                {
                    SpawnConsole(def, spot, kind);
                    return;
                }
            }

            Log.Warning("[Site-44] no room for " + def.defName);
        }

        private Building_FacilityTerminal SpawnConsole(ThingDef def, IntVec3 center, TerminalKind kind)
        {
            Building_FacilityTerminal terminal = (Building_FacilityTerminal)ThingMaker.MakeThing(def);
            terminal.kind = kind;
            GenSpawn.Spawn(terminal, center, map, Rot4.North);
            return terminal;
        }

        private bool TryFindSpotInRoom(PlanRoom room, IntVec2 size, out IntVec3 center)
        {
            // Cells along the room's edge first: terminals read as wall-mounted.
            foreach (LayoutCell cell in room.Cells.OrderByDescending(c => c.Y).ThenBy(c => c.X))
            {
                IntVec3 candidate = World(cell);
                CellRect occupied = GenAdj.OccupiedRect(candidate, Rot4.North, size);
                bool ok = true;
                foreach (IntVec3 c in occupied)
                {
                    if (plan.RoomAt(c.x - origin.x, c.z - origin.z) != room.Id || !IsFree(c))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    center = candidate;
                    return true;
                }
            }

            center = IntVec3.Invalid;
            return false;
        }

        private bool IsFree(IntVec3 c)
        {
            return c.InBounds(map) && !reserved.Contains(c) && c.GetEdifice(map) == null;
        }

        private bool TryFindFreeCell(PlanRoom room, out IntVec3 cell)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                IntVec3 candidate = World(room.Cells.RandomElement());
                if (IsFree(candidate) && candidate.GetFirstItem(map) == null)
                {
                    cell = candidate;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        // ----- lamps, loot, security -----

        private void SpawnLamps(List<PlanRoom> rooms, Zone zone)
        {
            foreach (PlanRoom room in rooms)
            {
                ThingDef lampDef = IsReceptionRoom(zone, room) ? Site44DefOf.Site44_LampBright : Site44DefOf.Site44_LampEmergency;
                int count = Mathf.Clamp(room.Cells.Count / 45, 1, 6);
                for (int i = 0; i < count; i++)
                {
                    IntVec3 cell = IntVec3.Invalid;
                    bool ok = i == 0 ? IsFree(World(room.Anchor)) : TryFindFreeCell(room, out cell);
                    if (i == 0)
                    {
                        cell = World(room.Anchor);
                    }

                    if (ok)
                    {
                        GenSpawn.Spawn(ThingMaker.MakeThing(lampDef), cell, map);
                    }
                }
            }
        }

        private static readonly string[] LootTable = { "Steel", "ComponentIndustrial", "MedicineIndustrial", "Silver", "Plasteel", "MealSurvivalPack" };

        private void SpawnLoot(List<PlanRoom> rooms, Zone zone)
        {
            List<PlanRoom> stock = rooms.Where(r => !r.IsCorridor).ToList();
            if (stock.Count == 0)
            {
                stock = rooms;
            }

            int stacks = Mathf.RoundToInt((6 + zone.Depth / 2) * Site44Tuning.LootAmount);
            for (int i = 0; i < stacks; i++)
            {
                IntVec3 cell;
                if (!TryFindFreeCell(stock.RandomElement(), out cell))
                {
                    continue;
                }

                ThingDef def = DefDatabase<ThingDef>.GetNamed(LootTable.RandomElement());
                Thing loot = ThingMaker.MakeThing(def);
                loot.stackCount = Mathf.Clamp(Mathf.RoundToInt(Rand.RangeInclusive(def.stackLimit / 4, def.stackLimit) * Site44Tuning.LootAmount), 1, def.stackLimit);
                GenPlace.TryPlaceThing(loot, cell, map, ThingPlaceMode.Near);
            }

            if (zone.EvaCache)
            {
                PlanRoom room = stock.OrderBy(r => r.Cells.Count).First();
                for (int i = 0; i < 3; i++)
                {
                    IntVec3 cell;
                    if (TryFindFreeCell(room, out cell))
                    {
                        GenPlace.TryPlaceThing(ThingMaker.MakeThing(Site44DefOf.Site44_EVASuit), cell, map, ThingPlaceMode.Near);
                        GenPlace.TryPlaceThing(ThingMaker.MakeThing(Site44DefOf.Site44_EVAHelmet), cell, map, ThingPlaceMode.Near);
                    }
                }
            }
        }

        private void SpawnSecurity(List<PlanRoom> rooms, Zone zone, PlanRoom main)
        {
            if (!Site44Tuning.SecurityEnabled || faction == null)
            {
                return;
            }

            MapComponent_Facility facilityComp = map.GetComponent<MapComponent_Facility>();
            SecurityGroup group = new SecurityGroup { id = facilityComp.securityGroups.Count + 1 };
            facilityComp.securityGroups.Add(group);
            int turrets = Mathf.Clamp(2 + zone.Depth / 3, 2, 6);
            for (int i = 0; i < turrets; i++)
            {
                IntVec3 cell;
                if (!TryFindFreeCell(main, out cell))
                {
                    break;
                }

                Thing turret = ThingMaker.MakeThing(Site44DefOf.Site44_SecurityTurret);
                turret.SetFaction(faction);
                GenSpawn.Spawn(turret, cell, map);
                group.turrets.Add(turret);
            }

            List<Pawn> mechs = new List<Pawn>();
            foreach (string kindName in FacilityMechs.SquadFor(zone.Depth))
            {
                Pawn mech = FacilityMechs.Make(kindName, faction);
                IntVec3 cell;
                if (mech != null && TryFindFreeCell(main, out cell))
                {
                    GenSpawn.Spawn(mech, cell, map);
                    mechs.Add(mech);
                    group.droids.Add(mech);
                }
            }

            if (mechs.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(World(main.Anchor), null, 14f), map, mechs);
            }

            IntVec3 consoleSpot;
            if (TryFindSpotInRoom(main, Site44DefOf.Site44_Terminal.size, out consoleSpot))
            {
                SpawnConsole(Site44DefOf.Site44_Terminal, consoleSpot, TerminalKind.Security).groupId = group.id;
            }
        }

        // Delve mode: an extraction gate and its terminal in the room farthest from the start.
        private void PlaceExtraction()
        {
            if (!GameComponent_Delve.IsDelve)
            {
                return;
            }

            PlanRoom target = plan.Rooms
                .Where(r => !r.IsCorridor && r.Cells.Count >= 12)
                .OrderByDescending(r => Mathf.Abs(r.Anchor.X - plan.Start.X) + Mathf.Abs(r.Anchor.Y - plan.Start.Y))
                .FirstOrDefault(r => TryFindSpotInRoom(r, Site44DefOf.Site44_ExtractionGate.size, out IntVec3 probe));
            IntVec3 gateSpot;
            if (target == null || !TryFindSpotInRoom(target, Site44DefOf.Site44_ExtractionGate.size, out gateSpot))
            {
                Log.Warning("[Site-44] delve: no room found for the extraction gate.");
                return;
            }

            GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_ExtractionGate), gateSpot, map, Rot4.North);
            IntVec3 terminalSpot;
            if (TryFindSpotInRoom(target, Site44DefOf.Site44_Terminal.size, out terminalSpot))
            {
                SpawnConsole(Site44DefOf.Site44_Terminal, terminalSpot, TerminalKind.Extraction);
            }

            Log.Message("[Site-44] delve: extraction gate at " + gateSpot + ".");
        }

        // ----- validation -----

        private bool PassableForValidation(IntVec3 c)
        {
            if (!c.InBounds(map))
            {
                return false;
            }

            Building edifice = c.GetEdifice(map);
            return edifice == null || edifice.def.passability != Traversability.Impassable
                   || edifice is Building_SealedHatch || edifice is Building_Door;
        }

        private void Validate()
        {
            bool[] seen = new bool[map.cellIndices.NumGridCells];
            Queue<IntVec3> queue = new Queue<IntVec3>();
            queue.Enqueue(startSpot);
            seen[map.cellIndices.CellToIndex(startSpot)] = true;
            while (queue.Count > 0)
            {
                IntVec3 c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    IntVec3 n = c + GenAdj.CardinalDirections[d];
                    if (PassableForValidation(n))
                    {
                        int index = map.cellIndices.CellToIndex(n);
                        if (!seen[index])
                        {
                            seen[index] = true;
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            int floorTotal = 0;
            int floorReached = 0;
            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    if (plan.At(x, y) != PlanTile.Floor)
                    {
                        continue;
                    }

                    floorTotal++;
                    if (seen[map.cellIndices.CellToIndex(World(x, y))])
                    {
                        floorReached++;
                    }
                }
            }

            List<int> unreachable = new List<int>();
            foreach (Zone zone in graph.Zones)
            {
                bool any = zone.RoomIds.Any(id => seen[map.cellIndices.CellToIndex(World(plan.Rooms[id].Anchor))]);
                if (!any)
                {
                    unreachable.Add(zone.Id);
                }
            }

            SimulationResult sim = graph.Simulate();
            Log.Message("[Site-44] validation: " + floorReached + "/" + floorTotal + " floor cells reachable from the start; zones reached "
                        + (graph.Zones.Count - unreachable.Count) + "/" + graph.Zones.Count
                        + " (unreachable: " + (unreachable.Count == 0 ? "none" : string.Join(",", unreachable.Select(i => i.ToString()).ToArray()))
                        + "); progression " + (sim.AllReachable ? "solvable" : "UNSOLVABLE") + ", final tier " + sim.FinalTier + ".");
            Log.Message("[Site-44] contents: " + BuildingPlanner.Summary(plan).Replace("\n", " | ")
                        + "; zone kinds: " + string.Join(", ", graph.Zones.GroupBy(z => z.Kind).Select(g => g.Key + "=" + g.Count()).ToArray())
                        + "; " + propSummary + "; specialty rooms=" + specialtySummary + "; rec rooms=" + recRoomCount + " secure rooms=" + string.Join("", secureRooms.Select(v => v.kind == 0 ? "T" : v.kind == 1 ? "D" : "V").ToArray()) + " foyers=" + foyers.Count + " supplyCrate=" + welcomePos + "; water cells made dry=" + waterFlattened + "; preserved rooms=" + preservedTotal + "; " + containerSummary
                        + "; hatches=" + map.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch).Count
                        + " sealedLinks=" + graph.SealedLinks.Count
                        + " terminals=" + map.listerThings.AllThings.Count(t => t is Building_FacilityTerminal)
                        + " lamps=" + map.listerThings.ThingsOfDef(Site44DefOf.Site44_LampEmergency).Count + ".");
        }
    }
}





