using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // Player-facing options (Options > Mod settings > Site-44). They apply to complexes generated afterwards.
    public class Site44Settings : ModSettings
    {
        // The defaults new games start from; the mod settings window at the main menu edits these.
        public static Site44Settings Global = new Site44Settings();

        // The settings in force: this game's own copy while a Site-44 game is loaded, the global defaults otherwise.
        public static Site44Settings Current
        {
            get
            {
                GameComponent_Site44Run run = Verse.Current.Game != null ? Verse.Current.Game.GetComponent<GameComponent_Site44Run>() : null;
                return run != null && run.run != null ? run.run : Global;
            }
        }

        public static bool InRun => Current != Global;

        public static void SaveGlobal()
        {
            Global.Write();
        }

        public Site44Settings Clone()
        {
            Site44Settings copy = new Site44Settings();
            foreach (System.Reflection.FieldInfo field in typeof(Site44Settings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object value = field.GetValue(this);
                List<string> list = value as List<string>;
                field.SetValue(copy, list != null ? new List<string>(list) : value);
            }

            return copy;
        }

        // Treasure
        public float lootAmount = 1f;
        public string storyteller = "Cassandra";             // used when the new-game pages skip the storyteller choice
        public bool commitment;                               // commitment mode: one automatic save, no reloading
        public int focus;                                     // what the game emphasises: 0 balanced, 1 explorer, 2 power trip
        public int startingColonists = 3;                     // 1-5
        public int puzzleDifficulty = 1;                      // 0 easy, 1 normal, 2 hard
        public float keycardX = 1f;                    // terminal gates that also have a keycard
        public float restrictedWingX = 1f;            // complexes that hold a clearance-gated wing
        public float nestDensity = 1f;
        public int delveDescentCare;                          // what the extraction gate does for injuries: 0 nothing, 1 dress, 2 heal, 3 full restore
        public bool delveDescentRest;                         // the gate also refills food and rest
        //                         // the lift dresses wounds on the way down (off by default)
        public int delveInfection = 1;                        // wound infection in Delve: 0 vanilla, 1 reduced, 2 none
        public float supplyX = 1f;                            // food and medicine supply, x1 = about a quarter of picks kept (see Site44Tuning)
        public float crateDensity = 1f;
        public float preservedRoomX = 1f;

        // People and containers
        public float casketDensity = 1f;
        public int survivorCaskets = 1;
        public float hostileCasketX = 1f;
        public float strayCorpseX = 1f;

        // Look
        public float dustAmount = 1f;
        public float ruinX = 1f;

        // Layout and threats
        public bool openLayout = true;
        public float lockedVaultX = 1f;
        public int securityLevel = 2;                         // facility security: 0 off, 1 light, 2 normal, 3 heavy
        public float dangerX = 1f;                            // how much danger each level is given to spend
        public bool hackMinigame = true;
        public int bestDelveDepth;
        public bool skipWorldSetup = true;
        public bool biomePoolV2;                              // false until the default pool (everything but the extremes) has been applied
        public List<string> biomePool = new List<string>();   // biomes for later complexes in a run; empty = every biome
        public bool delveAllowRaids = false;
        public bool delveAllowCaravanEvents = false;
        public bool delveAllowOtherThreats = false;
        public bool delveAllowOutsideVisitors = false;
        public bool delveAllowWeather = false;
        public int missionFocusLevel = 2;
        public bool hardenedToBodies = true;
        public bool startingKit = true;
        public float drugFindX = 1f;
        public float delverGearX = 1f;
        public bool hotZonesEnabled = true;

        // Special rooms
        public float recRoomFrequency = 1f;
        public float secureRoomDensity = 1f;
        public bool droidGuards = true;
        public float roomVariety = 1f;
        public string scenarioSiteType = "Site44_Type_Office";
        public string siteSizeMode = "Random";
        public string scenarioComplexSize = "Small";
        public string delveSize = "Steady";                   // a pace style (Quick delves, Steady, Expeditions) or a fixed size
        public float propDensity = 1f;

        // Delivery
        public float questFrequency = 1f;

        public static readonly string[] PresetNames = { "Easier", "Default", "Harder", "Brutal", "Unfair" };

        // Balance presets: only the difficulty settings change. Sizes, biomes, scenario choices and the event switches are kept.
        public void ApplyPreset(int preset)
        {
            Site44Settings d = new Site44Settings();
            supplyX = d.supplyX; lootAmount = d.lootAmount; crateDensity = d.crateDensity; hostileCasketX = d.hostileCasketX;
            delveInfection = d.delveInfection; delveDescentCare = d.delveDescentCare; delveDescentRest = d.delveDescentRest; puzzleDifficulty = d.puzzleDifficulty;
            keycardX = d.keycardX; nestDensity = d.nestDensity; startingColonists = d.startingColonists;
            drugFindX = d.drugFindX; securityLevel = d.securityLevel; dangerX = d.dangerX; missionFocusLevel = d.missionFocusLevel;
            delveAllowRaids = d.delveAllowRaids; delveAllowCaravanEvents = d.delveAllowCaravanEvents; delveAllowOtherThreats = d.delveAllowOtherThreats;
            delveAllowOutsideVisitors = d.delveAllowOutsideVisitors; delveAllowWeather = d.delveAllowWeather;
            switch (preset)
            {
                case 0:
                    supplyX = 2.4f; lootAmount = 1.3f; crateDensity = 1.3f; hostileCasketX = 0.4f;
                    delveInfection = 2; delveDescentCare = 2; puzzleDifficulty = 0; keycardX = 1.4f;
                    nestDensity = 0.5f; startingColonists = 4; securityLevel = 1; dangerX = 0.6f; drugFindX = 1.7f; missionFocusLevel = 3;
                    break;
                case 2:
                    supplyX = 0.4f; lootAmount = 0.8f; crateDensity = 0.8f; hostileCasketX = 1.6f;
                    delveInfection = 0; puzzleDifficulty = 2; keycardX = 0.6f; nestDensity = 1.5f; drugFindX = 0.7f; dangerX = 1.4f;
                    break;
                case 3:
                    supplyX = 0f; lootAmount = 0.6f; crateDensity = 0.6f; hostileCasketX = 2.2f;
                    delveInfection = 0; puzzleDifficulty = 2; keycardX = 0.3f; nestDensity = 2.2f; startingColonists = 2;
                    drugFindX = 0.3f; securityLevel = 3; dangerX = 1.8f; missionFocusLevel = 1;
                    break;
                case 4:
                    // Not balanced to be won: a lone colonist, no medicine, vanilla infection, nests and sleepers everywhere,
                    // no keycards, and the outside world switched back on (raids, threats, weather).
                    supplyX = 0f; lootAmount = 0.4f; crateDensity = 0.4f; hostileCasketX = 3f;
                    delveInfection = 0; puzzleDifficulty = 2; keycardX = 0f; nestDensity = 3f; startingColonists = 1;
                    drugFindX = 0f; securityLevel = 3; dangerX = 2.5f; missionFocusLevel = 0;
                    delveAllowRaids = true; delveAllowOtherThreats = true; delveAllowWeather = true;
                    break;
            }
        }

        // Vanilla rules: switches off every change this mod makes to how the game plays, leaving only the ruined complexes
        // themselves. Events, infection, moods, supplies and hacking all behave as in the base game.
        public void ApplyVanillaRules()
        {
            delveAllowRaids = true; delveAllowCaravanEvents = true; delveAllowOtherThreats = true; delveAllowOutsideVisitors = true; delveAllowWeather = true;
            delveInfection = 0; delveDescentCare = 0; delveDescentRest = false;
            missionFocusLevel = 0; hardenedToBodies = false; startingKit = false;
            supplyX = 4f; drugFindX = 0f; delverGearX = 0f;
            hackMinigame = false; securityLevel = 0; hotZonesEnabled = false;
        }

        // Every setting back to its default, whatever has been added since (the best Delve depth is a record, so it stays).
        public void Reset()
        {
            Site44Settings defaults = new Site44Settings();
            foreach (System.Reflection.FieldInfo field in typeof(Site44Settings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (field.Name != "bestDelveDepth")
                {
                    field.SetValue(this, field.GetValue(defaults));
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lootAmount, "lootAmount", 1f);
            Scribe_Values.Look(ref supplyX, "supplyX", 1f);
            Scribe_Values.Look(ref delveInfection, "delveInfection", 1);
            Scribe_Values.Look(ref delveDescentCare, "delveDescentCare");
            Scribe_Values.Look(ref delveDescentRest, "delveDescentRest");
            Scribe_Values.Look(ref storyteller, "storyteller", "Cassandra");
            Scribe_Values.Look(ref commitment, "commitment");
            Scribe_Values.Look(ref focus, "focus");
            Scribe_Values.Look(ref startingColonists, "startingColonists", 3);
            Scribe_Values.Look(ref puzzleDifficulty, "puzzleDifficulty", 1);
            Scribe_Values.Look(ref keycardX, "keycardX", 1f);
            Scribe_Values.Look(ref restrictedWingX, "restrictedWingX", 1f);
            Scribe_Values.Look(ref nestDensity, "nestDensity", 1f);
            Scribe_Values.Look(ref crateDensity, "crateDensity", 1f);
            Scribe_Values.Look(ref preservedRoomX, "preservedRoomX", 1f);
            Scribe_Values.Look(ref casketDensity, "casketDensity", 1f);
            Scribe_Values.Look(ref survivorCaskets, "survivorCaskets", 1);
            Scribe_Values.Look(ref hostileCasketX, "hostileCasketX", 1f);
            Scribe_Values.Look(ref strayCorpseX, "strayCorpseX", 1f);
            Scribe_Values.Look(ref dustAmount, "dustAmount", 1f);
            Scribe_Values.Look(ref ruinX, "ruinX", 1f);
            Scribe_Values.Look(ref openLayout, "openLayout", true);
            Scribe_Values.Look(ref lockedVaultX, "lockedVaultX", 1f);
            Scribe_Values.Look(ref securityLevel, "securityLevel", 2);
            Scribe_Values.Look(ref dangerX, "dangerX", 1f);
            Scribe_Values.Look(ref hackMinigame, "hackMinigame", true);
            Scribe_Values.Look(ref bestDelveDepth, "bestDelveDepth");
            Scribe_Values.Look(ref skipWorldSetup, "skipWorldSetup", true);
            Scribe_Values.Look(ref biomePoolV2, "biomePoolV2");
            Scribe_Collections.Look(ref biomePool, "biomePool", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                biomePool = biomePool ?? new List<string>();
            }
            Scribe_Values.Look(ref delveAllowRaids, "delveAllowRaids", false);
            Scribe_Values.Look(ref delveAllowCaravanEvents, "delveAllowCaravanEvents", false);
            Scribe_Values.Look(ref delveAllowOtherThreats, "delveAllowOtherThreats", false);
            Scribe_Values.Look(ref delveAllowOutsideVisitors, "delveAllowOutsideVisitors", false);
            Scribe_Values.Look(ref delveAllowWeather, "delveAllowWeather", false);
            Scribe_Values.Look(ref missionFocusLevel, "missionFocusLevel", 2);
            Scribe_Values.Look(ref hardenedToBodies, "hardenedToBodies", true);
            Scribe_Values.Look(ref startingKit, "startingKit", true);
            Scribe_Values.Look(ref drugFindX, "drugFindX", 1f);
            Scribe_Values.Look(ref delverGearX, "delverGearX", 1f);
            Scribe_Values.Look(ref hotZonesEnabled, "hotZonesEnabled", true);
            Scribe_Values.Look(ref recRoomFrequency, "recRoomFrequency", 1f);
            Scribe_Values.Look(ref secureRoomDensity, "secureRoomDensity", 1f);
            Scribe_Values.Look(ref droidGuards, "droidGuards", true);
            Scribe_Values.Look(ref roomVariety, "roomVariety", 1f);
            Scribe_Values.Look(ref scenarioSiteType, "scenarioSiteType", "Site44_Type_Office");
            Scribe_Values.Look(ref siteSizeMode, "siteSizeMode", "Random");
            Scribe_Values.Look(ref scenarioComplexSize, "scenarioSize2", "Small");
            Scribe_Values.Look(ref delveSize, "delveSize", "Steady");
            Scribe_Values.Look(ref propDensity, "propDensity", 1f);
            Scribe_Values.Look(ref questFrequency, "questFrequency", 1f);
        }
    }

    public class Site44ModEntry : Mod
    {
        private Vector2 scroll;

        public Site44ModEntry(ModContentPack content) : base(content)
        {
            Site44Settings.Global = GetSettings<Site44Settings>();
        }

        public override string SettingsCategory()
        {
            return "Site-44";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Site44Mod.ApplyQuestFrequency();
        }

        private static void Section(Listing_Standard list, string title)
        {
            list.Gap(6f);
            Text.Font = GameFont.Medium;
            list.Label(title);
            Text.Font = GameFont.Small;
            list.GapLine(4f);
        }

        private static float Multiplier(Listing_Standard list, string label, float value, float min, float max, string tip)
        {
            return Editable(list, label, value, min, max, tip);
        }

        private static readonly Dictionary<string, string> editBuffers = new Dictionary<string, string>();

        // A label, a typed value and a slider, kept in step: type a number, or drag the slider.
        private static float Editable(Listing_Standard list, string label, float value, float min, float max, string tip)
        {
            Rect row = list.GetRect(Text.LineHeight + 2f);
            Rect field = new Rect(row.xMax - 70f, row.y, 70f, row.height);
            Widgets.Label(new Rect(row.x, row.y, row.width - 78f, row.height), label);
            TooltipHandler.TipRegion(row, tip);
            string buffer;
            editBuffers.TryGetValue(label, out buffer);
            float typed = value;
            Widgets.TextFieldNumeric(field, ref typed, ref buffer, min, max);
            editBuffers[label] = buffer;
            float slid = list.Slider(typed, min, max);
            if (!Mathf.Approximately(slid, typed))
            {
                editBuffers[label] = null;   // the slider moved: refresh the typed value from it
            }

            return slid;
        }

        private static int Count(Listing_Standard list, string label, int value, int min, int max, string tip)
        {
            return Mathf.RoundToInt(Editable(list, label, value, min, max, tip));
        }

        private static float Percent(Listing_Standard list, string label, float value, float min, float max, string tip)
        {
            list.Label(label + ":  " + (value * 100f).ToString("0") + "%", -1f, new TipSignal(tip));
            return list.Slider(value, min, max);
        }

        private static void ChooseSize(System.Action<string> set, bool allowRandom, bool allowGrowing = false)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            List<string> sizes = new List<string> { "Extra small", "Small", "Medium", "Large", "XL" };
            if (allowRandom)
            {
                sizes.Insert(0, "Random");
            }

            if (allowGrowing)
            {
                sizes.Add("Grows with depth");
            }

            foreach (string size in sizes)
            {
                string captured = size;
                options.Add(new FloatMenuOption(size, () => set(captured)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SettingsScreen.Draw(inRect);
        }
    }
}

