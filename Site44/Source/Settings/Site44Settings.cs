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
        public static Site44Settings Current = new Site44Settings();

        // Treasure
        public float lootAmount = 1f;
        public int startingColonists = 3;                     // 1-5
        public int puzzleDifficulty = 1;                      // 0 easy, 1 normal, 2 hard
        public float keycardX = 1f;                    // terminal gates that also have a keycard
        public float restrictedWingX = 1f;            // complexes that hold a clearance-gated wing
        public float nestDensity = 1f;
        public bool delveDressWounds;                         // the lift dresses wounds on the way down (off by default)
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
        public bool securityEnabled;
        public bool hackMinigame = true;
        public int bestDelveDepth;
        public bool skipWorldSetup = true;
        public bool biomePoolV2;                              // false until the default pool (everything but the extremes) has been applied
        public List<string> biomePool = new List<string>();   // biomes for later complexes in a run; empty = every biome
        public bool delveNoRaids = true;
        public bool delveNoCaravanEvents = true;
        public bool delveNoOtherThreats = true;
        public bool delveNoOutsideVisitors = true;
        public bool delveNoWeather = true;
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
        public string delveSize = "Small";                    // "Grows with depth" uses the delve difficulty ramp
        public float propDensity = 1f;

        // Delivery
        public float questFrequency = 1f;

        public static readonly string[] PresetNames = { "Easier", "Standard", "Harder", "Brutal" };

        // Balance presets: only the difficulty settings change. Sizes, biomes, scenario choices and the event switches are kept.
        public void ApplyPreset(int preset)
        {
            Site44Settings d = new Site44Settings();
            supplyX = d.supplyX; lootAmount = d.lootAmount; crateDensity = d.crateDensity; hostileCasketX = d.hostileCasketX;
            delveInfection = d.delveInfection; delveDressWounds = d.delveDressWounds; puzzleDifficulty = d.puzzleDifficulty;
            keycardX = d.keycardX; nestDensity = d.nestDensity; startingColonists = d.startingColonists;
            drugFindX = d.drugFindX; securityEnabled = d.securityEnabled; missionFocusLevel = d.missionFocusLevel;
            switch (preset)
            {
                case 0:
                    supplyX = 2.4f; lootAmount = 1.3f; crateDensity = 1.3f; hostileCasketX = 0.4f;
                    delveInfection = 2; delveDressWounds = true; puzzleDifficulty = 0; keycardX = 1.4f;
                    nestDensity = 0.5f; startingColonists = 4; drugFindX = 1.7f; missionFocusLevel = 3;
                    break;
                case 2:
                    supplyX = 0.4f; lootAmount = 0.8f; crateDensity = 0.8f; hostileCasketX = 1.6f;
                    delveInfection = 0; puzzleDifficulty = 2; keycardX = 0.6f; nestDensity = 1.5f; drugFindX = 0.7f;
                    break;
                case 3:
                    supplyX = 0f; lootAmount = 0.6f; crateDensity = 0.6f; hostileCasketX = 2.2f;
                    delveInfection = 0; puzzleDifficulty = 2; keycardX = 0.3f; nestDensity = 2.2f; startingColonists = 2;
                    drugFindX = 0.3f; securityEnabled = true; missionFocusLevel = 1;
                    break;
            }
        }

        // Vanilla rules: switches off every change this mod makes to how the game plays, leaving only the ruined complexes
        // themselves. Events, infection, moods, supplies and hacking all behave as in the base game.
        public void ApplyVanillaRules()
        {
            delveNoRaids = false; delveNoCaravanEvents = false; delveNoOtherThreats = false; delveNoOutsideVisitors = false; delveNoWeather = false;
            delveInfection = 0; delveDressWounds = false;
            missionFocusLevel = 0; hardenedToBodies = false; startingKit = false;
            supplyX = 4f; drugFindX = 0f; delverGearX = 0f;
            hackMinigame = false; securityEnabled = false; hotZonesEnabled = false;
        }

        public void Reset()
        {
            Site44Settings defaults = new Site44Settings();
            lootAmount = defaults.lootAmount;
            supplyX = defaults.supplyX;
            delveInfection = defaults.delveInfection;
            delveDressWounds = defaults.delveDressWounds;
            startingColonists = defaults.startingColonists;
            puzzleDifficulty = defaults.puzzleDifficulty;
            keycardX = defaults.keycardX;
            restrictedWingX = defaults.restrictedWingX;
            nestDensity = defaults.nestDensity;
            skipWorldSetup = defaults.skipWorldSetup;
            biomePool = new List<string>();
            biomePoolV2 = false;
            delveNoRaids = defaults.delveNoRaids;
            delveNoCaravanEvents = defaults.delveNoCaravanEvents;
            delveNoOtherThreats = defaults.delveNoOtherThreats;
            delveNoOutsideVisitors = defaults.delveNoOutsideVisitors;
            delveNoWeather = defaults.delveNoWeather;
            crateDensity = defaults.crateDensity;
            preservedRoomX = defaults.preservedRoomX;
            casketDensity = defaults.casketDensity;
            survivorCaskets = defaults.survivorCaskets;
            hostileCasketX = defaults.hostileCasketX;
            strayCorpseX = defaults.strayCorpseX;
            dustAmount = defaults.dustAmount;
            ruinX = defaults.ruinX;
            openLayout = defaults.openLayout;
            lockedVaultX = defaults.lockedVaultX;
            securityEnabled = defaults.securityEnabled;
            hotZonesEnabled = defaults.hotZonesEnabled;
            recRoomFrequency = defaults.recRoomFrequency;
            secureRoomDensity = defaults.secureRoomDensity;
            droidGuards = defaults.droidGuards;
            roomVariety = defaults.roomVariety;
            scenarioSiteType = defaults.scenarioSiteType;
            siteSizeMode = defaults.siteSizeMode;
            scenarioComplexSize = defaults.scenarioComplexSize;
            delveSize = defaults.delveSize;
            propDensity = defaults.propDensity;
            questFrequency = defaults.questFrequency;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lootAmount, "lootAmount", 1f);
            Scribe_Values.Look(ref supplyX, "supplyX", 1f);
            Scribe_Values.Look(ref delveInfection, "delveInfection", 1);
            Scribe_Values.Look(ref delveDressWounds, "delveDressWounds");
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
            Scribe_Values.Look(ref securityEnabled, "securityEnabled");
            Scribe_Values.Look(ref hackMinigame, "hackMinigame", true);
            Scribe_Values.Look(ref bestDelveDepth, "bestDelveDepth");
            Scribe_Values.Look(ref skipWorldSetup, "skipWorldSetup", true);
            Scribe_Values.Look(ref biomePoolV2, "biomePoolV2");
            Scribe_Collections.Look(ref biomePool, "biomePool", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                biomePool = biomePool ?? new List<string>();
            }
            Scribe_Values.Look(ref delveNoRaids, "delveNoRaids", true);
            Scribe_Values.Look(ref delveNoCaravanEvents, "delveNoCaravanEvents", true);
            Scribe_Values.Look(ref delveNoOtherThreats, "delveNoOtherThreats", true);
            Scribe_Values.Look(ref delveNoOutsideVisitors, "delveNoOutsideVisitors", true);
            Scribe_Values.Look(ref delveNoWeather, "delveNoWeather", true);
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
            Scribe_Values.Look(ref delveSize, "delveSize", "Small");
            Scribe_Values.Look(ref propDensity, "propDensity", 1f);
            Scribe_Values.Look(ref questFrequency, "questFrequency", 1f);
        }
    }

    public class Site44ModEntry : Mod
    {
        private Vector2 scroll;

        public Site44ModEntry(ModContentPack content) : base(content)
        {
            Site44Settings.Current = GetSettings<Site44Settings>();
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
            list.Label(label + ":  x" + value.ToString("0.0#"), -1f, new TipSignal(tip));
            return list.Slider(value, min, max);
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
            Site44Settings s = Site44Settings.Current;
            Rect view = new Rect(0f, 0f, inRect.width - 20f, 1500f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            Listing_Standard list = new Listing_Standard();
            list.Begin(view);

            GUI.color = Color.gray;
            list.Label("Ruined complexes you find on the world map, and the Site-44 Breach scenario. Changes apply to complexes generated from now on; hover a setting for details.");
            GUI.color = Color.white;

            Section(list, "Starting points");
            GUI.color = Color.gray;
            list.Label("These buttons overwrite the settings below with a ready-made set of values. Pick one, then adjust anything you like.");
            GUI.color = Color.white;
            Rect presetRow = list.GetRect(30f);
            float buttonWidth = presetRow.width / Site44Settings.PresetNames.Length;
            for (int i = 0; i < Site44Settings.PresetNames.Length; i++)
            {
                Rect button = new Rect(presetRow.x + i * buttonWidth + 2f, presetRow.y, buttonWidth - 4f, presetRow.height);
                TooltipHandler.TipRegion(button, "Sets loot, supplies, infection, puzzles, keycards, nests and party size to the " + Site44Settings.PresetNames[i].ToLower()
                                                  + " preset. Sizes, biomes and scenario choices are kept. Adjust anything afterwards.");
                if (Widgets.ButtonText(button, Site44Settings.PresetNames[i]))
                {
                    s.ApplyPreset(i);
                }
            }

            if (list.ButtonText("Vanilla rules (switch off the mod's gameplay changes)"))
            {
                s.ApplyVanillaRules();
            }

            Section(list, "Treasure");
            s.supplyX = Multiplier(list, "Food and medicine supply", s.supplyX, 0f, 4f,
                "How often medicine and meals turn up in crates, caskets and on shelves, and how large their stacks are.\nx0 = almost none, x1 = a quarter of the picks kept, x4 = plentiful. Packed supplies from the starting kit are unaffected.");
            s.lootAmount = Multiplier(list, "Loot amount", s.lootAmount, 0.1f, 3f,
                "Scales the size of every loot stack in a complex: crates, caskets, storerooms and intact rooms.\nx1 is normal.");
            s.crateDensity = Multiplier(list, "Loot crate density", s.crateDensity, 0f, 3f,
                "How many sealed hermetic crates are scattered through the ruins. They hold a few stacks of loot each.\nx0 = none, x1 = normal.");
            s.preservedRoomX = Multiplier(list, "Intact rooms", s.preservedRoomX, 0f, 7f,
                "Chance that an ordinary room survived intact: clean sterile floor, undamaged furniture and better loot.\nStorage, server and reactor wings always have at least one.\nx1 is normal.");

            Section(list, "People and containers");
            s.casketDensity = Multiplier(list, "Cryosleep caskets", s.casketDensity, 0f, 3f,
                "How many sealed cryosleep caskets there are. Each holds a friendly survivor, a hostile sleeper, a body, or just loot.\nx0 = none, x1 = about one per 35 rooms.");
            list.Label("Survivor caskets per complex:  " + s.survivorCaskets, -1f, new TipSignal(
                "Caskets holding a friendly sleeper who joins you when opened. One stranded delver per complex keeps it believable."));
            s.survivorCaskets = Mathf.RoundToInt(list.Slider(s.survivorCaskets, 0, 3));
            s.hostileCasketX = Multiplier(list, "Hostile caskets", s.hostileCasketX, 0f, 3f,
                "Share of the remaining caskets that hold a hostile sleeper. They only appear deep inside, never near the entrance.\nx1 is normal.");
            s.strayCorpseX = Multiplier(list, "Dried-out corpses", s.strayCorpseX, 0f, 6f,
                "Chance that a room holds the remains of an earlier scavenger, sometimes with their loot.\nx1 is normal.");

            Section(list, "Look");
            s.dustAmount = Multiplier(list, "Dust and debris", s.dustAmount, 0f, 2f,
                "How much dust, trash and scattered paper covers the floors. x0 = spotless, x1 = normal.");
            s.propDensity = Multiplier(list, "Outdoor props", s.propDensity, 0f, 3f,
                "Sandbags, barriers, silos, crates, planters and lamp posts scattered through the yards, different for each kind of complex.\nx0 = none, x1 = normal.");
            s.ruinX = Multiplier(list, "Furniture damage", s.ruinX, 0f, 1.3f,
                "How wrecked the furniture is. x0 = undamaged, x1 = normal, higher = barely holding together.");

            Section(list, "Layout and threats");
            list.CheckboxLabeled("Open layout (explore freely)", ref s.openLayout,
                "On: every door is a plain door and the whole complex can be explored in any order.\nOff: sectors are sealed behind terminal gates and clearance levels.");
            s.lockedVaultX = Multiplier(list, "Locked vaults (open layout)", s.lockedVaultX, 0f, 4f,
                "In the open layout, the share of terminal gates that stay locked as optional vaults, opened by hacking a terminal.\nx1 is normal.");
            Section(list, "New game setup");
            list.CheckboxLabeled("Skip world and site selection", ref s.skipWorldSetup,
                "The Site-44 scenarios use nothing from the world except the climate and biome, so a default world and a start tile are chosen for you. Turn off to use the ordinary world and landing-site pages. Not used when the Odyssey expansion is active.");
            list.Label("The biomes complexes are drawn from are chosen on the new game screen, right after the storyteller.");

            Section(list, "Delve rules (applied when a Delve run starts)");
            list.Label("Delve is about the ruins, not an outside world. These switch off whole groups of random events for the Site-44 Delve scenario only; other scenarios are never touched. Best depth so far: " + s.bestDelveDepth + ".");
            list.CheckboxLabeled("No enemy raids", ref s.delveNoRaids,
                "Switches off ordinary enemy raids for the run.");
            list.CheckboxLabeled("No caravan events", ref s.delveNoCaravanEvents,
                "Switches off caravan ambushes, manhunter ambushes, caravan demands and caravan meetings.");
            list.CheckboxLabeled("No other threats", ref s.delveNoOtherThreats,
                "Switches off infestations, manhunter packs, animal insanity and crashed threat ship parts. The danger should come from the ruins.");
            list.CheckboxLabeled("No visitors or outside arrivals", ref s.delveNoOutsideVisitors,
                "Switches off traders, visitors, travelers, wanderers, refugee and resource pods, migrating herds and similar arrivals.");
            list.CheckboxLabeled("No weather disasters", ref s.delveNoWeather,
                "Switches off toxic fallout, volcanic winter, heat waves, cold snaps, flash storms, solar flares and eclipses.");
            Section(list, "Party");
            list.Label("Starting colonists:  " + s.startingColonists, -1f, new TipSignal(
                "How many colonists a new Site-44 game starts with, chosen from a larger pool on the character page. A larger party survives longer; a smaller one makes every loss count."));
            s.startingColonists = Mathf.RoundToInt(list.Slider(s.startingColonists, 1f, 5f));
            list.Label("The character page requires at least one colonist who can shoot and one who can do intellectual work, since fighting and hacking come up in every complex.");

            list.Label("Wound infection in Delve:  " + new[] { "vanilla", "reduced", "none" }[Mathf.Clamp(s.delveInfection, 0, 2)], -1f, new TipSignal(
                "How often a fresh wound goes septic during a Delve run. A small crew with scarce medicine can be overwhelmed by the vanilla rate. Other scenarios always use vanilla."));
            s.delveInfection = Mathf.RoundToInt(list.Slider(s.delveInfection, 0f, 2f));
            list.CheckboxLabeled("Dress wounds on descent", ref s.delveDressWounds,
                "Passing through an extraction gate dresses every wound that can still be tended. Off by default, so wounds carry down with the party.");

            Section(list, "Morale");
            list.Label("Mission focus (mood bonus inside a complex):  " + new[] { "off", "low (+3)", "normal (+6)", "high (+10)" }[Mathf.Clamp(s.missionFocusLevel, 0, 3)], -1f, new TipSignal(
                "A steady mood lift for colonists working inside a ruined complex. Raise it if breaks are too frequent."));
            s.missionFocusLevel = Mathf.RoundToInt(list.Slider(s.missionFocusLevel, 0f, 3f));
            list.CheckboxLabeled("Delvers ignore plain corpses", ref s.hardenedToBodies,
                "Colonists inside a complex stop being upset by ordinary corpses. Rotting corpses still bother them.");
            list.CheckboxLabeled("Starting kit (Breach scenario)", ref s.startingKit,
                "Starting colonists begin fed and rested, in good spirits, with survival meals and focus tabs.");
            s.drugFindX = Multiplier(list, "Focus tabs in loot", s.drugFindX, 0f, 3f,
                "Chance that an ordinary loot item is a stack of focus tabs, a non-addictive stimulant that slows hunger and fatigue.\nx1 is normal.");
            s.delverGearX = Multiplier(list, "Delver gear in rich loot", s.delverGearX, 0f, 4f,
                "Chance that a rich loot item is a delver's cap or vest, which lower the chance of mental breaks.\nx1 is normal.");
            Section(list, "Hacking and security");
            list.Label("Puzzle difficulty:  " + new[] { "easy", "normal", "hard" }[Mathf.Clamp(s.puzzleDifficulty, 0, 2)], -1f, new TipSignal(
                "Easy: two extra attempts and fewer decoys. Normal: four attempts, plus one per five skill levels. Hard: four attempts at most, more decoys and more look-alike words."));
            s.puzzleDifficulty = Mathf.RoundToInt(list.Slider(s.puzzleDifficulty, 0f, 2f));
            list.CheckboxLabeled("Cipher-lock hacking", ref s.hackMinigame,
                "On: gate and diagnostic terminals open a code-guessing puzzle; skill gives more attempts and fewer decoys.\nOff: terminals are hacked with a plain timed job.");
            list.CheckboxLabeled("Facility security (experimental)", ref s.securityEnabled,
                "Turrets, patrol mechanoids and alarm squads inside the complex. Off by default so the first exploration is safe.");
            list.CheckboxLabeled("Overheated sectors (gated layout only)", ref s.hotZonesEnabled,
                "Some sectors in the gated layout are dangerously hot until the coolant valve is turned.");

            s.keycardX = Multiplier(list, "Keycards for locked gates", s.keycardX, 0f, 2f,
                "Chance that a terminal-locked gate also has a keycard on the near side, usually guarded by a nest. A second way past the gate besides hacking.\nx1 is normal.");
            s.restrictedWingX = Multiplier(list, "Clearance-gated wings", s.restrictedWingX, 0f, 1.3f,
                "Chance that a complex holds an advanced wing with better loot, behind a terminal that wants clearance. A badge or a granting node on the near side raises your clearance.\nx1 is normal.");
            s.nestDensity = Multiplier(list, "Creature nests", s.nestDensity, 0f, 3f,
                "How many rooms are guarded by a hostile nest. x0 = none, x1 = normal.");

            Section(list, "Special rooms");
            s.recRoomFrequency = Multiplier(list, "Rec rooms", s.recRoomFrequency, 0f, 3f,
                "How often a break room or cafeteria turns out to be a rec room, with a billiards or card table, a television and couches. They are uncommon.\nx0 = none, x1 = normal.");
            s.roomVariety = Multiplier(list, "Specialty rooms", s.roomVariety, 0f, 3f,
                "How many ordinary rooms turn out to be something more specific: kitchens, dormitories, clinics, locker rooms, libraries, workshops, armories, comms rooms and classrooms.\nx0 = none, x1 = a handful per complex.");
            s.secureRoomDensity = Multiplier(list, "Secure rooms", s.secureRoomDensity, 0f, 3f,
                "Locked dead-end rooms opened by hacking a nearby terminal. Each holds one surprise: a treasure vault, a prototype droid, or a volatile cache.\nx0 = none, x1 = a few per complex.");
            list.CheckboxLabeled("Prototype droid guards", ref s.droidGuards,
                "Lets a secure room hold a hostile prototype droid. It only wakes when you open the room, so it never ambushes the entrance.");

            Section(list, "Complex type");
            SiteTypeDef current = SiteTypes.All.FirstOrDefault(t => t.defName == s.scenarioSiteType);
            list.Label("Complex used by the Site-44 Breach scenario:  " + (current != null ? current.LabelCap.ToString() : s.scenarioSiteType), -1f,
                new TipSignal("Which kind of ruined complex a new Site-44 Breach colony starts in. Quest sites on the world map each have their own type."));
            if (list.ButtonText("Change complex type"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (SiteTypeDef type in SiteTypes.All)
                {
                    SiteTypeDef captured = type;
                    options.Add(new FloatMenuOption(type.LabelCap + " - " + type.description, () => s.scenarioSiteType = captured.defName));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.Label("Size of quest-site complexes:  " + s.siteSizeMode, -1f,
                new TipSignal("Extra small (about 60 cells across), Small (85), Medium (120), Large (160) or XL (the whole site map). Random picks one each time."));
            if (list.ButtonText("Change quest-site size"))
            {
                ChooseSize(value => s.siteSizeMode = value, true);
            }

            list.Label("Size of the Site-44 Breach scenario complex:  " + s.scenarioComplexSize, -1f,
                new TipSignal("How much of the starting map the complex covers. XL is the whole map."));
            if (list.ButtonText("Change scenario size"))
            {
                ChooseSize(value => s.scenarioComplexSize = value, false);
            }

            list.Label("Size of Delve complexes:  " + s.delveSize, -1f,
                new TipSignal("Every level of a Delve run uses this size. Grows with depth starts small and ramps up to XL."));
            if (list.ButtonText("Change Delve size"))
            {
                ChooseSize(value => s.delveSize = value, false, true);
            }

            Section(list, "Quests");
            s.questFrequency = Multiplier(list, "Ruin quest frequency", s.questFrequency, 0f, 3f,
                "How often the storyteller offers a ruined-complex expedition. x0 = never, x1 = normal.\nTakes effect after restarting the game.");

            list.Gap(12f);
            if (list.ButtonText("Reset to defaults"))
            {
                s.Reset();
            }

            list.End();
            Widgets.EndScrollView();
        }
    }
}

