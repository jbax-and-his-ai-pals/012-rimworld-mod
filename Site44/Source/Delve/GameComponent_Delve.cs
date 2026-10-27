using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using Verse;

namespace Site44
{
    // Delve mode: each complex is a level. Reaching the extraction gate carries the party to the next one, a little
    // harder than the last. The run ends when the whole party is dead.
    public class GameComponent_Delve : GameComponent
    {
        private const float StashRadius = 8f;

        public bool active;
        public int depth = 1;
        private int lastCheckTick;

        public GameComponent_Delve(Game game)
        {
        }

        public static GameComponent_Delve Instance => Verse.Current.Game?.GetComponent<GameComponent_Delve>();

        // Delve mode is on once a run has started, and also in test runs launched with -site44delve (level 1 is generated
        // before the run formally starts).
        public static bool IsDelve => (Instance != null && Instance.active) || GenCommandLine.CommandLineArgPassed("site44delve");

        public static DelveLevel Level
        {
            get
            {
                if (!IsDelve)
                {
                    return null;
                }

                GameComponent_Delve delve = Instance;
                return DelveDifficulty.For(delve != null && delve.active ? delve.depth : 1);
            }
        }

        public static void StartRun()
        {
            GameComponent_Delve delve = Instance;
            if (delve == null)
            {
                return;
            }

            delve.active = true;
            delve.depth = 1;
            DelveRules.Apply();
            Log.Message("[Site-44] delve run started.");
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref depth, "depth", 1);
        }

        public override void GameComponentTick()
        {
            if (!active || Find.TickManager.TicksGame - lastCheckTick < 600)
            {
                return;
            }

            lastCheckTick = Find.TickManager.TicksGame;
            bool anyAlive = PawnsFinder.AllMaps_FreeColonists.Any()
                            || Find.WorldPawns.AllPawnsAlive.Any(p => p.IsColonist && p.Faction == Faction.OfPlayer);
            if (!anyAlive)
            {
                RecordBest();
                active = false;
            }
        }

        private void RecordBest()
        {
            Site44Settings settings = Site44Settings.Current;
            if (depth > settings.bestDelveDepth)
            {
                settings.bestDelveDepth = depth;
                settings.Write();
            }

            Messages.Message("The run ends at depth " + depth + ". Best so far: " + settings.bestDelveDepth + ".", MessageTypeDefOf.NeutralEvent, false);
        }

        // Carries the party, and the loot stacked beside the gate, down to the next complex.
        public void Descend(Map from, IntVec3 gatePosition)
        {
            if (!active || from == null)
            {
                return;
            }

            List<Pawn> party = from.mapPawns.AllPawnsSpawned.Where(p => p.IsColonist && !p.Dead).ToList();
            List<Thing> stash = from.listerThings.AllThings
                .Where(t => t.def.category == ThingCategory.Item && t.Position.DistanceTo(gatePosition) <= StashRadius)
                .ToList();

            SiteTypeDef type = SiteTypes.All.Where(t => t.questWeight > 0f).RandomElementByWeight(t => t.questWeight);
            PlanetTile tile;
            if (!TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false))
            {
                Log.Error("[Site-44] delve: no tile found for the next level.");
                return;
            }

            depth++;
            Site44Settings settings = Site44Settings.Current;
            if (depth > settings.bestDelveDepth)
            {
                settings.bestDelveDepth = depth;
                settings.Write();
            }

            Site site = SiteMaker.MakeSite(type.sitePart, tile, Find.FactionManager.OfAncients);
            Find.WorldObjects.Add(site);
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.PreferredMapSize, null);

            MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
            IntVec3 spot = facility != null && facility.startCell.IsValid ? facility.startCell : map.Center;

            foreach (Pawn pawn in party)
            {
                pawn.jobs?.StopAll();
                pawn.DeSpawn();
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(spot, map, 6, c => c.Standable(map)), map);
            }

            foreach (Thing item in stash)
            {
                if (item.Spawned)
                {
                    item.DeSpawn();
                    GenPlace.TryPlaceThing(item, CellFinder.RandomClosewalkCellNear(spot, map, 6, c => c.Standable(map)), map, ThingPlaceMode.Near);
                }
            }

            Verse.Current.Game.CurrentMap = map;
            CameraJumper.TryJump(spot, map);
            Messages.Message("Depth " + depth + ": " + type.LabelCap + ".", MessageTypeDefOf.PositiveEvent, false);
            Log.Message("[Site-44] delve: depth " + depth + " (" + type.defName + "), " + party.Count + " colonist(s) and " + stash.Count + " stash stack(s) carried.");

            if (from.Parent is Site)
            {
                Verse.Current.Game.DeinitAndRemoveMap(from, false);
            }
        }
    }
}
