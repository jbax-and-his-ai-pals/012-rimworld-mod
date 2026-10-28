using System;
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
        public bool active;
        public int depth = 1;
        public List<string> biomePool = new List<string>();   // fixed when the run starts; empty = every biome
        private int lastCheckTick;

        public GameComponent_Delve(Game game)
        {
        }

        public override void FinalizeInit()
        {
            DelveHealth.Apply(IsDelve ? DelveHealth.InfectionFactor(Site44Settings.Current) : 1f);
        }

        public static GameComponent_Delve Instance => Verse.Current.Game?.GetComponent<GameComponent_Delve>();

        // Delve mode is on once a run has started. Level 1 is generated before the run formally starts, so a Delve scenario
        // (or a test run launched with -site44delve) counts as well.
        public static bool IsDelve => (Instance != null && Instance.active)
                                      || GenCommandLine.CommandLineArgPassed("site44delve") || GenCommandLine.CommandLineArgPassed("site44quickdelve")
                                      || (Find.Scenario != null && Find.Scenario.AllParts.Any(p => p is ScenPart_Site44Delve));

        // Quick test: the scenario part asks for it, or the -site44quickdelve launch flag.
        public static bool QuickTest =>
            GenCommandLine.CommandLineArgPassed("site44quickdelve")
            || (Find.Scenario != null && Find.Scenario.AllParts.OfType<ScenPart_Site44Delve>().Any(p => p.quickTest));

        public static DelveLevel Level
        {
            get
            {
                if (!IsDelve)
                {
                    return null;
                }

                GameComponent_Delve delve = Instance;
                DelveLevel level = DelveDifficulty.For(delve != null && delve.active ? delve.depth : 1);
                string chosen = Site44Settings.Current.delveSize;
                if (!string.IsNullOrEmpty(chosen) && chosen != "Grows with depth")
                {
                    level.SizeName = chosen;
                }

                if (QuickTest)
                {
                    level.SizeName = "XS";
                }

                return level;
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
            BiomeChoice.EnsureDefault(Site44Settings.Current);
            delve.biomePool = new List<string>(Site44Settings.Current.biomePool);
            DelveRules.Apply();
            Log.Message("[Site-44] delve run started.");
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref depth, "depth", 1);
            Scribe_Collections.Look(ref biomePool, "biomePool", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                biomePool = biomePool ?? new List<string>();
            }
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

            SiteTypeDef type = SiteTypes.All.Where(t => t.questWeight > 0f).RandomElementByWeight(t => t.questWeight);
            PlanetTile tile;
            Predicate<PlanetTile> inPool = t =>
            {
                BiomeDef biome = Find.WorldGrid[t].PrimaryBiome;
                return biomePool.Count == 0 || (biome != null && biomePool.Contains(biome.defName));
            };
            // The first few levels stay out of the extreme climates (when the pool has anything milder), so the party
            // has a chance to find protection before it needs it.
            bool early = depth <= Site44Tuning.MildLevels && BiomeChoice.PoolHasMild(Site44Settings.Current);
            Predicate<PlanetTile> mildPool = t => inPool(t) && !BiomeChoice.IsExtreme(Find.WorldGrid[t].PrimaryBiome);
            if (!(early && TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: mildPool))
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: inPool)
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 24, false, validator: inPool)
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false))
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

            int dressed = DelveHealth.TendParty(party);
            if (dressed > 0)
            {
                Messages.Message("The lift's med bay dresses " + dressed + " wound(s) on the way down.", MessageTypeDefOf.PositiveEvent, false);
            }

            Verse.Current.Game.CurrentMap = map;
            CameraJumper.TryJump(spot, map);
            Messages.Message("Depth " + depth + ": " + type.LabelCap + ".", MessageTypeDefOf.PositiveEvent, false);
            Log.Message("[Site-44] delve: depth " + depth + " (" + type.defName + "), " + party.Count + " colonist(s) carried down.");

            if (from.Parent is Site)
            {
                Verse.Current.Game.DeinitAndRemoveMap(from, false);
            }
        }
    }
}
