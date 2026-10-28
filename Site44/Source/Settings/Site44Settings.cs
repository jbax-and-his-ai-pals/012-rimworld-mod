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
        public float scarcity = 0.75f;                        // how rarely medicine and meals turn up, and how small the stacks are
        public float crateDensity = 1f;
        public float preservedRoomChance = 0.07f;

        // People and containers
        public float casketDensity = 1f;
        public int survivorCaskets = 1;
        public float hostileCasketShare = 0.25f;
        public float strayCorpseChance = 0.04f;

        // Look
        public float dustAmount = 1f;
        public float ruinLevel = 0.75f;

        // Layout and threats
        public bool openLayout = true;
        public float lockedVaultShare = 0.25f;
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
        public float drugFindChance = 0.15f;
        public float delverGearChance = 0.12f;
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

        public void Reset()
        {
            Site44Settings defaults = new Site44Settings();
            lootAmount = defaults.lootAmount;
            scarcity = defaults.scarcity;
            skipWorldSetup = defaults.skipWorldSetup;
            biomePool = new List<string>();
            biomePoolV2 = false;
            delveNoRaids = defaults.delveNoRaids;
            delveNoCaravanEvents = defaults.delveNoCaravanEvents;
            delveNoOtherThreats = defaults.delveNoOtherThreats;
            delveNoOutsideVisitors = defaults.delveNoOutsideVisitors;
            delveNoWeather = defaults.delveNoWeather;
            crateDensity = defaults.crateDensity;
            preservedRoomChance = defaults.preservedRoomChance;
            casketDensity = defaults.casketDensity;
            survivorCaskets = defaults.survivorCaskets;
            hostileCasketShare = defaults.hostileCasketShare;
            strayCorpseChance = defaults.strayCorpseChance;
            dustAmount = defaults.dustAmount;
            ruinLevel = defaults.ruinLevel;
            openLayout = defaults.openLayout;
            lockedVaultShare = defaults.lockedVaultShare;
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
            Scribe_Values.Look(ref scarcity, "scarcity", 0.75f);
            Scribe_Values.Look(ref crateDensity, "crateDensity", 1f);
            Scribe_Values.Look(ref preservedRoomChance, "preservedRoomChance", 0.07f);
            Scribe_Values.Look(ref casketDensity, "casketDensity", 1f);
            Scribe_Values.Look(ref survivorCaskets, "survivorCaskets", 1);
            Scribe_Values.Look(ref hostileCasketShare, "hostileCasketShare", 0.25f);
            Scribe_Values.Look(ref strayCorpseChance, "strayCorpseChance", 0.04f);
            Scribe_Values.Look(ref dustAmount, "dustAmount", 1f);
            Scribe_Values.Look(ref ruinLevel, "ruinLevel", 0.75f);
            Scribe_Values.Look(ref openLayout, "openLayout", true);
            Scribe_Values.Look(ref lockedVaultShare, "lockedVaultShare", 0.25f);
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
            Scribe_Values.Look(ref drugFindChance, "drugFindChance", 0.15f);
            Scribe_Values.Look(ref delverGearChance, "delverGearChance", 0.12f);
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

            Section(list, "Treasure");
            s.scarcity = Multiplier(list, "Food and medicine scarcity", s.scarcity, 0f, 1f,
                "How rare medicine and meals are in crates, caskets and on shelves, and how small their stacks are.\n0% is generous; 100% is almost none. Packed supplies from the starting kit are unaffected.");
            s.lootAmount = Multiplier(list, "Loot amount", s.lootAmount, 0.1f, 3f,
                "Scales the size of every loot stack in a complex: crates, caskets, storerooms and intact rooms.\nx1 is normal.");
            s.crateDensity = Multiplier(list, "Loot crate density", s.crateDensity, 0f, 3f,
                "How many sealed hermetic crates are scattered through the ruins. They hold a few stacks of loot each.\nx0 = none, x1 = normal.");
            s.preservedRoomChance = Percent(list, "Intact room chance", s.preservedRoomChance, 0f, 0.5f,
                "Chance that an ordinary room survived intact: clean sterile floor, undamaged furniture and better loot.\nStorage, server and reactor wings always have at least one.");

            Section(list, "People and containers");
            s.casketDensity = Multiplier(list, "Cryosleep caskets", s.casketDensity, 0f, 3f,
                "How many sealed cryosleep caskets there are. Each holds a friendly survivor, a hostile sleeper, a body, or just loot.\nx0 = none, x1 = about one per 35 rooms.");
            list.Label("Survivor caskets per complex:  " + s.survivorCaskets, -1f, new TipSignal(
                "Caskets holding a friendly sleeper who joins you when opened. One stranded delver per complex keeps it believable."));
            s.survivorCaskets = Mathf.RoundToInt(list.Slider(s.survivorCaskets, 0, 3));
            s.hostileCasketShare = Percent(list, "Hostile share of caskets", s.hostileCasketShare, 0f, 0.8f,
                "Share of the remaining caskets that hold a hostile sleeper. They only appear deep inside, never near the entrance.");
            s.strayCorpseChance = Percent(list, "Dried-out corpse chance (per room)", s.strayCorpseChance, 0f, 0.25f,
                "Chance that a room holds the remains of an earlier scavenger, sometimes with their loot.");

            Section(list, "Look");
            s.dustAmount = Multiplier(list, "Dust and debris", s.dustAmount, 0f, 2f,
                "How much dust, trash and scattered paper covers the floors. x0 = spotless, x1 = normal.");
            s.propDensity = Multiplier(list, "Outdoor props", s.propDensity, 0f, 3f,
                "Sandbags, barriers, silos, crates, planters and lamp posts scattered through the yards, different for each kind of complex.\nx0 = none, x1 = normal.");
            s.ruinLevel = Percent(list, "Furniture damage", s.ruinLevel, 0f, 1f,
                "How wrecked the furniture is. 0% = undamaged, 100% = barely holding together.");

            Section(list, "Layout and threats");
            list.CheckboxLabeled("Open layout (explore freely)", ref s.openLayout,
                "On: every door is a plain door and the whole complex can be explored in any order.\nOff: sectors are sealed behind terminal gates and clearance levels.");
            s.lockedVaultShare = Percent(list, "Locked vaults (open layout)", s.lockedVaultShare, 0f, 1f,
                "In the open layout, the share of terminal gates that stay locked as optional vaults, opened by hacking a terminal.");
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
            Section(list, "Morale");
            list.Label("Mission focus (mood bonus inside a complex):  " + new[] { "off", "low (+3)", "normal (+6)", "high (+10)" }[Mathf.Clamp(s.missionFocusLevel, 0, 3)], -1f, new TipSignal(
                "A steady mood lift for colonists working inside a ruined complex. Raise it if breaks are too frequent."));
            s.missionFocusLevel = Mathf.RoundToInt(list.Slider(s.missionFocusLevel, 0f, 3f));
            list.CheckboxLabeled("Delvers ignore plain corpses", ref s.hardenedToBodies,
                "Colonists inside a complex stop being upset by ordinary corpses. Rotting corpses still bother them.");
            list.CheckboxLabeled("Starting kit (Breach scenario)", ref s.startingKit,
                "Starting colonists begin fed and rested, in good spirits, with survival meals and focus tabs.");
            s.drugFindChance = Percent(list, "Focus tabs in loot", s.drugFindChance, 0f, 0.5f,
                "Chance that an ordinary loot item is a stack of focus tabs, a non-addictive stimulant that slows hunger and fatigue.");
            s.delverGearChance = Percent(list, "Delver gear in rich loot", s.delverGearChance, 0f, 0.5f,
                "Chance that a rich loot item is a delver's cap or vest, which lower the chance of mental breaks.");
            Section(list, "Hacking and security");
            list.CheckboxLabeled("Cipher-lock hacking", ref s.hackMinigame,
                "On: gate and diagnostic terminals open a code-guessing puzzle; skill gives more attempts and fewer decoys.\nOff: terminals are hacked with a plain timed job.");
            list.CheckboxLabeled("Facility security (experimental)", ref s.securityEnabled,
                "Turrets, patrol mechanoids and alarm squads inside the complex. Off by default so the first exploration is safe.");
            list.CheckboxLabeled("Overheated sectors (gated layout only)", ref s.hotZonesEnabled,
                "Some sectors in the gated layout are dangerously hot until the coolant valve is turned.");

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

