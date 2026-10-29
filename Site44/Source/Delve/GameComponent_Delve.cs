using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using UnityEngine;
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
        private int runSeed;
        private int startTick;
        private Dictionary<int, string> sizeByDepth = new Dictionary<int, string>();

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
                level.SizeName = delve != null ? delve.ResolveSize(level.Depth) : DelveDifficulty.SizeFor(Site44Settings.Current.delveSize, level.Depth, null, 0);
                if (QuickTest)
                {
                    level.SizeName = "XS";
                }

                return level;
            }
        }

        // The size of a level is settled once and kept, so the map and everything that asks about it agree.
        private string ResolveSize(int levelDepth)
        {
            string size;
            if (sizeByDepth.TryGetValue(levelDepth, out size))
            {
                return size;
            }

            string previous;
            sizeByDepth.TryGetValue(levelDepth - 1, out previous);
            size = DelveDifficulty.SizeFor(Site44Settings.Current.delveSize, levelDepth, previous, runSeed);
            if (active)
            {
                sizeByDepth[levelDepth] = size;
            }

            return size;
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
            delve.runSeed = Rand.Int;
            delve.startTick = Find.TickManager.TicksGame;
            delve.sizeByDepth.Clear();
            BiomeChoice.EnsureDefault(Site44Settings.Current);
            delve.biomePool = new List<string>(Site44Settings.Current.biomePool);
            DelveRules.Apply();
            Log.Message("[Site-44] delve run started.");
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref depth, "depth", 1);
            Scribe_Values.Look(ref runSeed, "runSeed");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Collections.Look(ref sizeByDepth, "sizeByDepth", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref biomePool, "biomePool", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                biomePool = biomePool ?? new List<string>();
                sizeByDepth = sizeByDepth ?? new Dictionary<int, string>();
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
            Site44Settings settings = Site44Settings.Global;
            if (depth > settings.bestDelveDepth)
            {
                settings.bestDelveDepth = depth;
                Site44Settings.SaveGlobal();
            }

            bool newBest = depth >= settings.bestDelveDepth && depth > 1;
            int survived = Find.TickManager.TicksGame - startTick;
            Site44Settings run = Site44Settings.Current;
            List<KeyValuePair<string, string>> lines = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Depth reached", depth.ToString()),
                new KeyValuePair<string, string>("Levels completed", Mathf.Max(0, depth - 1).ToString()),
                new KeyValuePair<string, string>("Best depth", settings.bestDelveDepth + (newBest ? " (a new best)" : "")),
                new KeyValuePair<string, string>("Time in the ruins", (survived / 60000) + " days, " + ((survived % 60000) / 2500) + " hours"),
                new KeyValuePair<string, string>("Difficulty", SettingsScreen.CurrentBalance()),
                new KeyValuePair<string, string>("Pace", run.delveSize == "Grows with depth" ? "Steady" : run.delveSize),
                new KeyValuePair<string, string>("Focus", new[] { "Balanced", "Explorer", "Power trip" }[Mathf.Clamp(run.focus, 0, 2)])
            };
            Find.WindowStack.Add(new Dialog_RunSummary(lines));
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
            Predicate<PlanetTile> favoured = t => inPool(t) && BiomeChoice.Favoured(Site44Settings.Current, Find.WorldGrid[t].PrimaryBiome);
            if (!(early && TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: t => mildPool(t) && favoured(t)))
                && !(early && TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: mildPool))
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: favoured)
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false, validator: inPool)
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 24, false, validator: inPool)
                && !TileFinder.TryFindNewSiteTile(out tile, from.Tile, 2, 12, false))
            {
                Log.Error("[Site-44] delve: no tile found for the next level.");
                return;
            }

            depth++;
            Site44Settings settings = Site44Settings.Global;
            if (depth > settings.bestDelveDepth)
            {
                settings.bestDelveDepth = depth;
                Site44Settings.SaveGlobal();
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

            string care = DelveHealth.OnDescent(party, Site44Settings.Current);
            if (!care.NullOrEmpty())
            {
                Messages.Message(care, MessageTypeDefOf.PositiveEvent, false);
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
