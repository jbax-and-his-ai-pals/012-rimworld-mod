using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Site44
{
    // F9 (rebindable) shows the whole map without fog. It only changes drawing, never the fog state itself,
    // so toggling back restores exactly what the colony had explored.
    // Also hosts the dev test flags: -site44test, -site44quest[=all|Medical|...].
    public class GameComponent_Site44Hotkeys : GameComponent
    {
        private readonly Queue<SiteTypeDef> questQueue = new Queue<SiteTypeDef>();
        private float nextQuestAt = -1f;
#if ENCLAVE
        private float enclaveAt = -1f;
#endif
        private float descendAt = -1f;
        private int descendLeft;
        private float heroAt = -1f;
        private List<Pawn> heroList = new List<Pawn>();

        public HashSet<Pawn> Heroes { get; private set; } = new HashSet<Pawn>();

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                heroList = Heroes.Where(p => p != null && !p.Destroyed).ToList();
            }

            Scribe_Collections.Look(ref heroList, "heroes", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Heroes = new HashSet<Pawn>((heroList ?? new List<Pawn>()).Where(p => p != null));
            }
        }

        public override void GameComponentTick()
        {
            if (Heroes.Count > 0 && Find.TickManager.TicksGame % 6 == 0)
            {
                foreach (Pawn hero in Heroes.ToList())
                {
                    HeroMode.Restore(hero);
                }
            }
        }

        public GameComponent_Site44Hotkeys(Game game)
        {
        }

        // Dev test runs (-quicktest -site44test) use another scenario, so hand out the Site-44 starting weapons here.
        public override void StartedNewGame()
        {
            if (GenCommandLine.CommandLineArgPassed("site44delve"))
            {
                GameComponent_Delve.StartRun();
            }

            // -site44descend[=N]: take the extraction gate N times (default 1), to check the level chain from the log.
            if (GenCommandLine.CommandLineArgPassed("site44delve") && (GenCommandLine.CommandLineArgPassed("site44descend") || SiteTypes.CommandLineValue("site44descend") != null))
            {
                int.TryParse(SiteTypes.CommandLineValue("site44descend"), out descendLeft);
                descendLeft = System.Math.Max(1, descendLeft);
                descendAt = Time.realtimeSinceStartup + 8f;
            }

            if (GenCommandLine.CommandLineArgPassed("site44test"))
            {
                ScenPart_Site44Facility.EquipUnarmedColonists();
                foreach (Pawn colonist in PawnsFinder.AllMaps_FreeColonists)
                {
                    GameComponent_Site44Morale.GiveStartingKit(colonist);
                    Log.Message("[Site-44] kit: " + colonist.LabelShort + " carries " + string.Join(", ", colonist.inventory.innerContainer.Select(t => t.stackCount + " " + t.def.defName).ToArray()) + "; wears " + string.Join(", ", colonist.apparel.WornApparel.Select(w => w.def.defName).ToArray()) + ".");
                }
            }

            // -site44hero: put the first colonist in hero mode a few seconds in, to check it from the log.
            if (GenCommandLine.CommandLineArgPassed("site44hero"))
            {
                heroAt = Time.realtimeSinceStartup + 4f;
            }

            // -site44quest[=type|all]: force ruin quests and generate their maps a moment after the game starts
            // (as in real play, when the game is no longer in new-game setup), to check the chain from the log.
            if (GenCommandLine.CommandLineArgPassed("site44quest") || SiteTypes.CommandLineValue("site44quest") != null)
            {
                string wanted = SiteTypes.CommandLineValue("site44quest");
#if ENCLAVE
                if (wanted == "Enclave")
                {
                    enclaveAt = Time.realtimeSinceStartup + 6f;
                    return;
                }
#endif

                IEnumerable<SiteTypeDef> types = SiteTypes.All;
                if (string.IsNullOrEmpty(wanted))
                {
                    types = SiteTypes.All.Where(t => t.defName == "Site44_Type_Office");
                }
                else if (wanted != "all")
                {
                    types = SiteTypes.All.Where(t => t.defName == wanted || t.defName == "Site44_Type_" + wanted);
                }

                foreach (SiteTypeDef type in types)
                {
                    questQueue.Enqueue(type);
                }

                nextQuestAt = Time.realtimeSinceStartup + 6f;
            }
        }

        // Frame-based so it also runs while the game is paused.
        public override void GameComponentUpdate()
        {
            if (heroAt > 0f && Time.realtimeSinceStartup >= heroAt)
            {
                heroAt = -1f;
                Pawn first = PawnsFinder.AllMaps_FreeColonists.FirstOrDefault();
                if (first != null)
                {
                    HeroMode.Toggle(first);
                    Log.Message("[Site-44] hero mode on for " + first.LabelShort + ": " + (first.equipment?.Primary?.LabelCap.ToString() ?? "unarmed") + ", " + first.apparel.WornApparel.Count + " apparel.");
                }
            }

            if (descendAt > 0f && Time.realtimeSinceStartup >= descendAt)
            {
                descendAt = -1f;
                Map current = Find.CurrentMap;
                Building_ExtractionGate gate = current?.listerThings.ThingsOfDef(Site44DefOf.Site44_ExtractionGate).OfType<Building_ExtractionGate>().FirstOrDefault();
                if (gate == null)
                {
                    Log.Warning("[Site-44] delve test: no extraction gate on the current map.");
                }
                else
                {
                    gate.unlocked = true;
                    GameComponent_Delve.Instance.Descend(current, gate.Position);
                    descendLeft--;
                    if (descendLeft > 0)
                    {
                        descendAt = Time.realtimeSinceStartup + 6f;
                    }
                }
            }

#if ENCLAVE
            if (enclaveAt > 0f && Time.realtimeSinceStartup >= enclaveAt)
            {
                enclaveAt = -1f;
                Site enclave = Site44Quests.GenerateEnclaveQuest();
                if (enclave != null)
                {
                    Site44Quests.OpenMap(enclave);
                    if (GenCommandLine.CommandLineArgPassed("site44grow"))
                    {
                        Map enclaveMap = Find.Maps.Find(m => m.GetComponent<MapComponent_Enclave>().active);
                        enclaveMap?.GetComponent<MapComponent_Enclave>().AddStanding(400); Log.Message("[Site-44] grow test: enclave map " + (enclaveMap != null) + ", maps " + Find.Maps.Count + ", speed set.");
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                        MapComponent_Enclave growing = enclaveMap?.GetComponent<MapComponent_Enclave>();
                        for (int step = 0; step < 6000 && growing != null; step++)
                        {
                            growing.GrowthStep();
                        }

                        Log.Message("[Site-44] grow test done: " + growing.built.Count + " built.");
                    }
                }
            }
#endif

            if (questQueue.Count > 0 && nextQuestAt > 0f && Time.realtimeSinceStartup >= nextQuestAt)
            {
                SiteTypeDef type = questQueue.Dequeue();
                nextQuestAt = Time.realtimeSinceStartup + 4f;
                var site = Site44Quests.GenerateQuest(type);
                if (site != null)
                {
                    Site44Quests.OpenMap(site);
                }
            }
        }

        private static readonly string[] StartingPileDefs = { "Silver", "MealSurvivalPack", "MedicineIndustrial", "Steel", "WoodLog", "ComponentIndustrial", "Chemfuel",
            "Gun_BoltActionRifle", "Gun_Revolver", "MeleeWeapon_Knife", "Apparel_FlakPants", "Apparel_FlakVest", "Apparel_AdvancedHelmet" };
        private static readonly string[] ScatteredAnywhereDefs = { "ShipChunk", "Steel", "MealSurvivalPack" };

        // Run before the first frame (from the generation step's PostMapInitialized), so nothing visibly pops out.
        public static void RemoveCrashLandingExtras(Map map, IntVec3 center)
        {
            int items = 0;
            int animals = 0;
            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                bool nearPile = thing.def.category == ThingCategory.Item && thing.Position.DistanceTo(center) <= 20f && System.Array.IndexOf(StartingPileDefs, thing.def.defName) >= 0;
                if (nearPile || System.Array.IndexOf(ScatteredAnywhereDefs, thing.def.defName) >= 0)
                {
                    thing.Destroy();
                    items++;
                }
            }

            foreach (Pawn animal in map.mapPawns.SpawnedColonyAnimals.ToList())
            {
                animal.Destroy();
                animals++;
            }

            Log.Message("[Site-44] test start cleanup: removed " + items + " starting item stack(s) and " + animals + " pet(s).");
        }

        public static void ToggleFogDrawing()
        {
            DebugViewSettings.drawFog = !DebugViewSettings.drawFog;
            Messages.Message("[Site-44] fog of war " + (DebugViewSettings.drawFog ? "shown" : "hidden"), MessageTypeDefOf.NeutralEvent, false);
        }

        public override void GameComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            if (Site44KeyBindingDefOf.Site44_ToggleFog.KeyDownEvent)
            {
                ToggleFogDrawing();
                Event.current.Use();
            }
            else if (Site44KeyBindingDefOf.Site44_HeroMode.KeyDownEvent)
            {
                HeroMode.ToggleSelected();
                Event.current.Use();
            }
            else if (Site44KeyBindingDefOf.Site44_UnsealAll.KeyDownEvent)
            {
                Site44DebugActions.UnsealAllSectors();
                Event.current.Use();
            }
        }
    }

    [DefOf]
    public static class Site44KeyBindingDefOf
    {
        public static KeyBindingDef Site44_ToggleFog;
        public static KeyBindingDef Site44_UnsealAll;
        public static KeyBindingDef Site44_HeroMode;

        static Site44KeyBindingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Site44KeyBindingDefOf));
        }
    }
}
