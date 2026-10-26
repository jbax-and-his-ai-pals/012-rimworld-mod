using System.Collections.Generic;
using System.Linq;
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
        public bool hotZonesEnabled = true;

        // Special rooms
        public float recRoomFrequency = 1f;
        public float secureRoomDensity = 1f;
        public bool droidGuards = true;
        public float roomVariety = 1f;
        public string scenarioSiteType = "Site44_Type_Office";
        public string siteSizeMode = "Random";
        public string scenarioComplexSize = "XL";
        public float propDensity = 1f;

        // Delivery
        public float questFrequency = 1f;

        public void Reset()
        {
            Site44Settings defaults = new Site44Settings();
            lootAmount = defaults.lootAmount;
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
            propDensity = defaults.propDensity;
            questFrequency = defaults.questFrequency;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lootAmount, "lootAmount", 1f);
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
            Scribe_Values.Look(ref hotZonesEnabled, "hotZonesEnabled", true);
            Scribe_Values.Look(ref recRoomFrequency, "recRoomFrequency", 1f);
            Scribe_Values.Look(ref secureRoomDensity, "secureRoomDensity", 1f);
            Scribe_Values.Look(ref droidGuards, "droidGuards", true);
            Scribe_Values.Look(ref roomVariety, "roomVariety", 1f);
            Scribe_Values.Look(ref scenarioSiteType, "scenarioSiteType", "Site44_Type_Office");
            Scribe_Values.Look(ref siteSizeMode, "siteSizeMode", "Random");
            Scribe_Values.Look(ref scenarioComplexSize, "scenarioComplexSize", "XL");
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

        private static void ChooseSize(System.Action<string> set, bool allowRandom)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (string size in allowRandom ? new[] { "Random", "Extra small", "Small", "Medium", "Large", "XL" } : new[] { "Extra small", "Small", "Medium", "Large", "XL" })
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

