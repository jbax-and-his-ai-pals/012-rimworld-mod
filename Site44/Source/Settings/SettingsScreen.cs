using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // The settings screen: tabs of rows. Every row knows its default (so changed rows are marked and can be reset one by one),
    // which game modes it affects, whether it applies right now, and what its value means in plain terms.
    internal abstract class SettingRow
    {
        public string Label;
        public string Tip;
        public string Scope;                    // "Delve", "Breach", "Quest sites" or null for everything
        public bool Advanced;                   // hidden until "Show advanced settings" is ticked
        public Func<bool> Enabled;
        public string DisabledNote;
        public Func<string> Effect;             // plain-terms reading of the current value

        public abstract bool IsDefault { get; }
        public abstract void Reset();
        protected abstract void DrawControl(Listing_Standard list, Rect row);
        protected virtual float ControlHeight => 0f;

        private const float ResetWidth = 56f;
        private static readonly Color ChangedColour = new Color(0.55f, 0.8f, 1f);

        public void Draw(Listing_Standard list)
        {
            bool enabled = Enabled == null || Enabled();
            Rect row = list.GetRect(Text.LineHeight + 2f);
            bool changed = !IsDefault;

            Rect resetRect = new Rect(row.xMax - ResetWidth, row.y, ResetWidth, row.height);
            Rect controlRect = new Rect(row.x, row.y, row.width - ResetWidth - 6f, row.height);
            if (changed && enabled && Widgets.ButtonText(resetRect, "Reset"))
            {
                Reset();
            }

            if (!enabled)
            {
                GUI.color = Color.gray;
            }

            DrawControl(list, controlRect);
            TooltipHandler.TipRegion(row, Tip + (Scope != null ? "\nApplies to: " + Scope + "." : ""));
            GUI.color = Color.white;

            string note = !enabled ? DisabledNote : (Effect != null ? Effect() : null);
            if (enabled && this is FloatRow percentRow && !percentRow.Integer)
            {
                // A percentage here is a share of the normal amount, not a chance: say so on every such row.
                note = (note.NullOrEmpty() ? "" : note + "  ") + "Percent of normal: 100% is the usual amount, 0% removes it.";
            }

            if (!note.NullOrEmpty())
            {
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                list.Label(note);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            list.Gap(4f);
        }

        protected string Title => Label + (Scope != null ? "  [" + Scope + "]" : "");

        protected Color LabelColour(bool enabled)
        {
            return !enabled ? Color.gray : (IsDefault ? Color.white : ChangedColour);
        }
    }

    // A number setting. Multipliers are shown and typed as whole percentages (100% is the mod's normal amount);
    // counts are whole numbers. Both text boxes only accept digits, and an empty or out-of-range entry snaps back.
    internal sealed class FloatRow : SettingRow
    {
        public Func<float> Get;
        public Action<float> Set;
        public float Default, Min, Max;
        public bool Integer;
        private string buffer;

        public override bool IsDefault => Mathf.Approximately(Get(), Default);
        public override void Reset() { Set(Default); buffer = null; }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            GUI.color = LabelColour(enabled);
            Widgets.Label(new Rect(row.x, row.y, row.width - 100f, row.height), Title);
            GUI.color = enabled ? Color.white : Color.gray;

            float scale = Integer ? 1f : 100f;
            float min = Min * scale, max = Max * scale;
            int shown = Mathf.RoundToInt(Mathf.Clamp(Get() * scale, min, max));
            Rect field = new Rect(row.xMax - (Integer ? 70f : 88f), row.y, 70f, row.height);
            int typed = shown;
            Widgets.TextFieldNumeric(field, ref typed, ref buffer, min, max);
            if (!Integer)
            {
                Widgets.Label(new Rect(row.xMax - 16f, row.y, 16f, row.height), "%");
            }

            int clamped = Mathf.Clamp(typed, Mathf.RoundToInt(min), Mathf.RoundToInt(max));
            int slid = Mathf.RoundToInt(list.Slider(clamped, min, max));
            if (slid != clamped)
            {
                buffer = null;
            }

            Set(slid / scale);
        }
    }

    internal sealed class ToggleRow : SettingRow
    {
        public Func<bool> Get;
        public Action<bool> Set;
        public bool Default;

        public override bool IsDefault => Get() == Default;
        public override void Reset() { Set(Default); }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            bool value = Get();
            GUI.color = LabelColour(enabled);
            Widgets.CheckboxLabeled(row, Title, ref value);
            Set(value);
        }
    }

    internal sealed class ChoiceRow : SettingRow
    {
        public Func<int> Get;
        public Action<int> Set;
        public int Default;
        public string[] Names;

        public override bool IsDefault => Get() == Default;
        public override void Reset() { Set(Default); }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            GUI.color = LabelColour(enabled);
            int value = Mathf.Clamp(Get(), 0, Names.Length - 1);
            Widgets.Label(row, Title + ":  " + Names[value]);
            GUI.color = enabled ? Color.white : Color.gray;
            Set(Mathf.RoundToInt(list.Slider(value, 0f, Names.Length - 1)));
        }
    }

    internal sealed class PickRow : SettingRow
    {
        public Func<string> Get;
        public Func<string> Default;
        public Action Pick;
        public Action ResetValue;
        public string Button;

        public override bool IsDefault => Get() == Default();
        public override void Reset() { ResetValue(); }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            GUI.color = LabelColour(enabled);
            Widgets.Label(new Rect(row.x, row.y, row.width - 170f, row.height), Title + ":  " + Get());
            GUI.color = enabled ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(row.xMax - 164f, row.y, 164f, row.height), Button))
            {
                Pick();
            }
        }
    }

    internal static class SettingsScreen
    {
        private sealed class Section
        {
            public string Title;
            public string Subtitle;
            public List<SettingRow> Rows;
        }

        private static string query = "";
        private static bool changedOnly;
        private static Vector2 scroll;
        private static float viewHeight = 3000f;
        private static float pendingScroll = -1f;
        private static List<Section> sections;
        private static List<SettingRow> allRows;
        private static readonly Dictionary<string, float> sectionTop = new Dictionary<string, float>();

        private static Site44Settings S => Site44Settings.Current;
        private static readonly Site44Settings D = new Site44Settings();

        private static readonly string[] PresetFields =
        {
            "supplyX", "lootAmount", "crateDensity", "hostileCasketX", "delveInfection", "delveDressWounds", "puzzleDifficulty",
            "keycardX", "nestDensity", "startingColonists", "drugFindX", "securityEnabled", "missionFocusLevel"
        };

        private static readonly string[] VanillaFields =
        {
            "delveAllowRaids", "delveAllowCaravanEvents", "delveAllowOtherThreats", "delveAllowOutsideVisitors", "delveAllowWeather",
            "delveInfection", "delveDressWounds", "missionFocusLevel", "hardenedToBodies", "startingKit", "supplyX", "drugFindX",
            "delverGearX", "hackMinigame", "securityEnabled", "hotZonesEnabled"
        };

        private static bool Same(Site44Settings a, Site44Settings b, string[] names)
        {
            foreach (string name in names)
            {
                FieldInfo field = typeof(Site44Settings).GetField(name);
                if (field == null || !Equals(field.GetValue(a), field.GetValue(b)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CurrentBalance()
        {
            Site44Settings vanilla = new Site44Settings();
            vanilla.ApplyVanillaRules();
            if (Same(S, vanilla, VanillaFields))
            {
                return "Vanilla rules";
            }

            for (int i = 0; i < Site44Settings.PresetNames.Length; i++)
            {
                Site44Settings preset = new Site44Settings();
                preset.ApplyPreset(i);
                if (Same(S, preset, PresetFields))
                {
                    return Site44Settings.PresetNames[i];
                }
            }

            return "Custom";
        }

        private static string Pct(float share)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(share) * 100f) + "%";
        }

        // ------------------------------------------------------------------ rows

        private static FloatRow Mult(string label, string tip, Func<float> get, Action<float> set, float max, string scope = null, bool advanced = false,
                                     Func<string> effect = null, Func<bool> enabled = null, string disabledNote = null, float min = 0f, float def = 1f, bool integer = false)
        {
            return new FloatRow { Integer = integer, Label = label, Tip = tip, Scope = scope, Advanced = advanced, Effect = effect, Enabled = enabled, DisabledNote = disabledNote, Get = get, Set = set, Default = def, Min = min, Max = max };
        }

        private static ToggleRow Toggle(string label, string tip, Func<bool> get, Action<bool> set, bool def, string scope = null, bool advanced = false,
                                        Func<bool> enabled = null, string disabledNote = null)
        {
            return new ToggleRow { Label = label, Tip = tip, Scope = scope, Advanced = advanced, Enabled = enabled, DisabledNote = disabledNote, Get = get, Set = set, Default = def };
        }

        private static ChoiceRow Choice(string label, string tip, Func<int> get, Action<int> set, int def, string[] names, string scope = null,
                                        Func<string> effect = null, Func<bool> enabled = null, string disabledNote = null)
        {
            return new ChoiceRow { Label = label, Tip = tip, Scope = scope, Effect = effect, Enabled = enabled, DisabledNote = disabledNote, Get = get, Set = set, Default = def, Names = names };
        }

        private static PickRow Size(string label, string tip, Func<string> get, Action<string> set, string def, string scope, bool random, bool growing)
        {
            return new PickRow
            {
                Label = label, Tip = tip, Scope = scope, Button = "Change", Get = get, Default = () => def, ResetValue = () => set(def),
                Pick = () =>
                {
                    List<string> sizes = new List<string> { "Extra small", "Small", "Medium", "Large", "XL" };
                    if (random) sizes.Insert(0, "Random");
                    if (growing) sizes.Add("Grows with depth");
                    Find.WindowStack.Add(new FloatMenu(sizes.Select(size => new FloatMenuOption(size, () => set(size))).ToList()));
                }
            };
        }

        private static void Build()
        {
            Func<bool> open = () => S.openLayout;
            Func<bool> gated = () => !S.openLayout;
            Func<bool> hacking = () => S.hackMinigame;

            var delve = new List<SettingRow>
            {
                Toggle("Enable enemy raids", "Ticked: ordinary enemy raids can happen during a run. Unticked (the default): none do.",
                    () => S.delveAllowRaids, v => S.delveAllowRaids = v, false, "Delve"),
                Toggle("Enable caravan events", "Ticked: caravan ambushes, manhunter ambushes, caravan demands and caravan meetings can happen. Unticked (the default): none do.",
                    () => S.delveAllowCaravanEvents, v => S.delveAllowCaravanEvents = v, false, "Delve"),
                Toggle("Enable other threats", "Ticked: infestations, manhunter packs, animal insanity and crashed threat ship parts can happen. Unticked (the default): none do.",
                    () => S.delveAllowOtherThreats, v => S.delveAllowOtherThreats = v, false, "Delve"),
                Toggle("Enable visitors and outside arrivals", "Ticked: traders, visitors, travelers, wanderers, refugee and resource pods and migrating herds can happen. Unticked (the default): none do.",
                    () => S.delveAllowOutsideVisitors, v => S.delveAllowOutsideVisitors = v, false, "Delve"),
                Toggle("Enable weather disasters", "Ticked: toxic fallout, volcanic winter, heat waves, cold snaps, flash storms, solar flares and eclipses can happen. Unticked (the default): none do.",
                    () => S.delveAllowWeather, v => S.delveAllowWeather = v, false, "Delve"),
                Mult("Starting colonists", "How many colonists a new Site-44 game starts with, chosen from a larger pool on the character page. A larger party survives longer; a smaller one makes every loss count.\nThe character page requires one colonist who can shoot and one who can do intellectual work.",
                    () => S.startingColonists, v => S.startingColonists = Mathf.RoundToInt(v), 5f, null, false, null, null, null, 1f, 3f, true),
                Choice("Wound infection", "How often a fresh wound goes septic during a Delve run. A small crew with scarce medicine can be overwhelmed by the vanilla rate. Other scenarios always use vanilla.",
                    () => S.delveInfection, v => S.delveInfection = v, 1, new[] { "vanilla", "reduced (half)", "none" }, "Delve"),
                Toggle("Dress wounds on descent", "Passing through an extraction gate dresses every wound that can still be tended. Off by default, so wounds carry down with the party.",
                    () => S.delveDressWounds, v => S.delveDressWounds = v, false, "Delve"),
                Choice("Mission focus (mood bonus in a complex)", "A steady mood lift for colonists working inside a ruined complex. Raise it if mental breaks are too frequent.",
                    () => S.missionFocusLevel, v => S.missionFocusLevel = v, 2, new[] { "off", "low (+3)", "normal (+6)", "high (+10)" }),
                Toggle("Delvers ignore plain corpses", "Colonists inside a complex stop being upset by ordinary corpses. Rotting corpses still bother them.",
                    () => S.hardenedToBodies, v => S.hardenedToBodies = v, true, null, true),
                Size("Size of Delve complexes", "Every level of a Delve run uses this size. 'Grows with depth' starts small and ramps up to XL.",
                    () => S.delveSize, v => S.delveSize = v, "Small", "Delve", false, true)
            };

            var loot = new List<SettingRow>
            {
                Mult("Food and medicine supply", "How often medicine and meals turn up in crates, caskets and on shelves, and how large their stacks are.\n0% = almost none, 400% = plentiful. Packed supplies from the starting kit are unaffected.",
                    () => S.supplyX, v => S.supplyX = v, 4f, null, false, () => "Keeps about " + Pct(Site44Tuning.SupplyShare) + " of medicine and meal picks."),
                Mult("Loot amount", "Scales the size of every loot stack in a complex: crates, caskets, storerooms and intact rooms.", () => S.lootAmount, v => S.lootAmount = v, 3f, min: 0.1f),
                Mult("Loot crate density", "How many sealed hermetic crates are scattered through the ruins. They hold a few stacks of loot each. 0% = none.", () => S.crateDensity, v => S.crateDensity = v, 3f),
                Mult("Focus tabs in loot", "Chance that an ordinary loot item is a stack of focus tabs, a non-addictive stimulant that slows hunger and fatigue.",
                    () => S.drugFindX, v => S.drugFindX = v, 3f, null, true, () => "About " + Pct(Site44Tuning.DrugFindChance) + " of ordinary loot items."),
                Mult("Delver gear in rich loot", "Chance that a rich loot item is a delver's cap or vest, which lower the chance of mental breaks.",
                    () => S.delverGearX, v => S.delverGearX = v, 4f, null, true, () => "About " + Pct(Site44Tuning.DelverGearChance) + " of rich loot items."),
                Mult("Intact rooms", "Chance that an ordinary room survived intact: clean sterile floor, undamaged furniture and better loot. Storage, server and reactor wings always have at least one.",
                    () => S.preservedRoomX, v => S.preservedRoomX = v, 7f, null, true, () => "About " + Pct(Site44Tuning.PreservedRoomChance) + " of ordinary rooms."),
                Mult("Cryosleep caskets", "How many sealed cryosleep caskets there are. Each holds a friendly survivor, a hostile sleeper, a body, or just loot. 0% = none.",
                    () => S.casketDensity, v => S.casketDensity = v, 3f, null, false, () => "At 100%, about one casket per 35 rooms."),
                Mult("Survivor caskets per complex", "Caskets holding a friendly sleeper who joins you when opened.", () => S.survivorCaskets, v => S.survivorCaskets = Mathf.RoundToInt(v), 3f, null, true, null,
                    () => S.casketDensity > 0f, "Needs cryosleep caskets above 0%.", 0f, 1f, true),
                Mult("Hostile caskets", "Share of the remaining caskets that hold a hostile sleeper. They only appear deep inside, never near the entrance.",
                    () => S.hostileCasketX, v => S.hostileCasketX = v, 3f, null, true, () => "About " + Pct(0.25f * S.hostileCasketX) + " of the remaining caskets.",
                    () => S.casketDensity > 0f, "Needs cryosleep caskets above 0%."),
                Mult("Dried-out corpses", "Chance that a room holds the remains of an earlier scavenger, sometimes with their loot.",
                    () => S.strayCorpseX, v => S.strayCorpseX = v, 6f, null, true, () => "About " + Pct(Site44Tuning.StrayCorpseChance) + " of rooms.")
            };

            var hazards = new List<SettingRow>
            {
                Toggle("Cipher-lock hacking", "On: gate and diagnostic terminals open a code-guessing puzzle; skill gives more attempts and fewer decoys.\nOff: terminals are hacked with a plain timed job.",
                    () => S.hackMinigame, v => S.hackMinigame = v, true),
                Choice("Puzzle difficulty", "Easy: two extra attempts and fewer decoys. Normal: four attempts, plus one per five skill levels. Hard: four attempts at most, more decoys and more look-alike words.",
                    () => S.puzzleDifficulty, v => S.puzzleDifficulty = v, 1, new[] { "easy", "normal", "hard" }, null, null, hacking, "Needs cipher-lock hacking."),
                Mult("Keycards for locked gates", "Chance that a terminal-locked gate also has a keycard on the near side, usually guarded by a nest. A second way past the gate besides hacking.",
                    () => S.keycardX, v => S.keycardX = v, 2f, null, false, () => "About " + Pct(Site44Tuning.KeycardChance) + " of locked gates."),
                Mult("Clearance-gated wings", "Chance that a complex holds an advanced wing with better loot, behind a terminal that wants clearance. A badge or a granting node on the near side raises your clearance.",
                    () => S.restrictedWingX, v => S.restrictedWingX = v, 1.3f, null, false, () => "About " + Pct(Site44Tuning.RestrictedWingChance) + " of complexes."),
                Mult("Creature nests", "How many rooms are guarded by a hostile nest. 0% = none.", () => S.nestDensity, v => S.nestDensity = v, 3f),
                Toggle("Facility security (experimental)", "Turrets, patrol mechanoids and alarm squads inside the complex. Off by default so the first exploration is safe.",
                    () => S.securityEnabled, v => S.securityEnabled = v, false),
                Toggle("Open layout (explore freely)", "On: every door is a plain door and the whole complex can be explored in any order.\nOff: sectors are sealed behind terminal gates and clearance levels.",
                    () => S.openLayout, v => S.openLayout = v, true),
                Mult("Locked vaults", "In the open layout, the share of terminal gates that stay locked as optional vaults, opened by hacking a terminal.",
                    () => S.lockedVaultX, v => S.lockedVaultX = v, 4f, null, true, () => "About " + Pct(Site44Tuning.OpenLayoutLockedShare) + " of gates.", open, "Needs the open layout."),
                Toggle("Overheated sectors", "Some sectors in the gated layout are dangerously hot until the coolant valve is turned.",
                    () => S.hotZonesEnabled, v => S.hotZonesEnabled = v, true, null, true, gated, "Needs the open layout switched off."),
                Mult("Secure rooms", "Locked dead-end rooms opened by hacking a nearby terminal. Each holds one surprise: a treasure vault, a prototype droid, or a volatile cache. 0% = none.",
                    () => S.secureRoomDensity, v => S.secureRoomDensity = v, 3f, null, true),
                Toggle("Prototype droid guards", "Lets a secure room hold a hostile prototype droid. It only wakes when you open the room, so it never ambushes the entrance.",
                    () => S.droidGuards, v => S.droidGuards = v, true, null, true, () => S.secureRoomDensity > 0f, "Needs secure rooms above 0%.")
            };

            var looks = new List<SettingRow>
            {
                Mult("Dust and debris", "How much dust, trash and scattered paper covers the floors. 0% = spotless.", () => S.dustAmount, v => S.dustAmount = v, 2f, null, true),
                Mult("Outdoor props", "Sandbags, barriers, silos, crates, planters and lamp posts scattered through the yards, different for each kind of complex. 0% = none.",
                    () => S.propDensity, v => S.propDensity = v, 3f, null, true),
                Mult("Furniture damage", "How wrecked the furniture is. 0% = undamaged, higher = barely holding together.", () => S.ruinX, v => S.ruinX = v, 1.3f, null, true),
                Mult("Rec rooms", "How often a break room or cafeteria turns out to be a rec room, with a billiards or card table, a television and couches. 0% = none.",
                    () => S.recRoomFrequency, v => S.recRoomFrequency = v, 3f, null, true),
                Mult("Specialty rooms", "How many ordinary rooms turn out to be something more specific: kitchens, dormitories, clinics, locker rooms, libraries, workshops, armories, comms rooms and classrooms.",
                    () => S.roomVariety, v => S.roomVariety = v, 3f, null, true)
            };

            var setup = new List<SettingRow>
            {
                new PickRow
                {
                    Label = "Complex used by the Breach scenario", Tip = "Which kind of ruined complex a new Site-44 Breach colony starts in. Quest sites on the world map each have their own type.", Scope = "Breach",
                    Button = "Change", Get = () => { SiteTypeDef t = SiteTypes.All.FirstOrDefault(x => x.defName == S.scenarioSiteType); return t != null ? t.LabelCap.ToString() : S.scenarioSiteType; },
                    Default = () => { SiteTypeDef t = SiteTypes.All.FirstOrDefault(x => x.defName == D.scenarioSiteType); return t != null ? t.LabelCap.ToString() : D.scenarioSiteType; },
                    ResetValue = () => S.scenarioSiteType = D.scenarioSiteType,
                    Pick = () => Find.WindowStack.Add(new FloatMenu(SiteTypes.All.Select(type =>
                    {
                        SiteTypeDef captured = type;
                        return new FloatMenuOption(type.LabelCap + " - " + type.description, () => S.scenarioSiteType = captured.defName);
                    }).ToList()))
                },
                Size("Size of the Breach scenario complex", "How much of the starting map the complex covers. XL is the whole map.", () => S.scenarioComplexSize, v => S.scenarioComplexSize = v, "Small", "Breach", false, false),
                Size("Size of quest-site complexes", "Extra small (about 60 cells across), Small (85), Medium (120), Large (160) or XL (the whole site map). Random picks one each time.",
                    () => S.siteSizeMode, v => S.siteSizeMode = v, "Random", "Quest sites", true, false),
                Toggle("Starting kit", "Starting colonists begin fed and rested, in good spirits, with survival meals and focus tabs.", () => S.startingKit, v => S.startingKit = v, true, "Breach"),
                Toggle("Skip world and site selection", "The Site-44 scenarios use nothing from the world except the climate and biome, so a default world and a start tile are chosen for you. Turn off to use the ordinary world and landing-site pages. Not used when the Odyssey expansion is active.",
                    () => S.skipWorldSetup, v => S.skipWorldSetup = v, true),
                Mult("Ruin quest frequency", "How often the storyteller offers a ruined-complex expedition. 0% = never. Takes effect after restarting the game.",
                    () => S.questFrequency, v => S.questFrequency = v, 3f, "Quest sites")
            };

            allRows = delve.Concat(loot).Concat(hazards).Concat(looks).Concat(setup).ToList();
            Func<string[], List<SettingRow>> pick = labels => labels.Select(l => allRows.First(r => r.Label == l)).ToList();

            sections = new List<Section>
            {
                new Section { Title = "The run", Subtitle = "Who goes in, what can happen to them from outside, and how wounds and moods behave.", Rows = pick(new[] {
                    "Starting colonists", "Enable enemy raids", "Enable caravan events", "Enable other threats", "Enable visitors and outside arrivals", "Enable weather disasters",
                    "Wound infection", "Dress wounds on descent", "Mission focus (mood bonus in a complex)", "Delvers ignore plain corpses" }) },
                new Section { Title = "Loot and supplies", Subtitle = "What the ruins hold. Medicine and meals are scarce by default; everything else follows the loot amount.", Rows = pick(new[] {
                    "Food and medicine supply", "Loot amount", "Loot crate density", "Focus tabs in loot", "Delver gear in rich loot", "Intact rooms" }) },
                new Section { Title = "Creatures and survivors", Subtitle = "Nests, sleepers in cryosleep caskets, and the remains of earlier scavengers.", Rows = pick(new[] {
                    "Creature nests", "Cryosleep caskets", "Survivor caskets per complex", "Hostile caskets", "Dried-out corpses" }) },
                new Section { Title = "Hazards and security", Subtitle = "Things in the ruins that fight back.", Rows = pick(new[] {
                    "Facility security (experimental)", "Overheated sectors", "Prototype droid guards" }) },
                new Section { Title = "Hacking, keys and clearance", Subtitle = "How the party gets past sealed gates: hack the terminal, find a keycard, or earn clearance.", Rows = pick(new[] {
                    "Cipher-lock hacking", "Puzzle difficulty", "Keycards for locked gates", "Clearance-gated wings", "Secure rooms" }) },
                new Section { Title = "Complexes and sizes", Subtitle = "How a complex is laid out and how big it is.", Rows = pick(new[] {
                    "Open layout (explore freely)", "Locked vaults", "Size of Delve complexes", "Complex used by the Breach scenario", "Size of the Breach scenario complex", "Size of quest-site complexes" }) },
                new Section { Title = "Scenario setup", Subtitle = "How new games start.", Rows = pick(new[] { "Starting kit", "Skip world and site selection", "Ruin quest frequency" }) },
                new Section { Title = "Look and fine detail", Subtitle = "Atmosphere and room variety. Safe to leave alone.", Rows = pick(new[] {
                    "Dust and debris", "Outdoor props", "Furniture damage", "Rec rooms", "Specialty rooms" }) }
            };
        }

        // ------------------------------------------------------------------ drawing

        private const float TocWidth = 190f;
        private const string AboutTitle = "About this version";
        private const string StartTitle = "Starting points";

        public static void Draw(Rect inRect)
        {
            if (sections == null)
            {
                Build();
            }

            Rect toc = new Rect(inRect.x, inRect.y, TocWidth, inRect.height);
            Rect main = new Rect(inRect.x + TocWidth + 12f, inRect.y, inRect.width - TocWidth - 12f, inRect.height);

            // Search and filter, always at the top of the list.
            Rect searchRow = new Rect(main.x, main.y, main.width, 28f);
            Widgets.Label(new Rect(searchRow.x, searchRow.y + 3f, 60f, 24f), "Search");
            query = Widgets.TextField(new Rect(searchRow.x + 62f, searchRow.y, searchRow.width - 62f - 190f, 26f), query);
            bool changedBox = changedOnly;
            Widgets.CheckboxLabeled(new Rect(searchRow.xMax - 184f, searchRow.y + 1f, 184f, 26f), "Changed settings only", ref changedBox);
            changedOnly = changedBox;
            bool filtering = !query.NullOrEmpty() || changedOnly;

            DrawContents(toc, filtering);

            Rect listRect = new Rect(main.x, main.y + 34f, main.width, main.height - 34f);
            Rect view = new Rect(0f, 0f, listRect.width - 20f, viewHeight);
            if (pendingScroll >= 0f)
            {
                scroll.y = pendingScroll;
                pendingScroll = -1f;
            }

            Widgets.BeginScrollView(listRect, ref scroll, view);
            Listing_Standard list = new Listing_Standard { maxOneColumn = true };
            list.Begin(view);
            sectionTop.Clear();
            try
            {

            if (!filtering)
            {
                sectionTop[AboutTitle] = list.CurHeight;
                DrawAbout(list);
                sectionTop[StartTitle] = list.CurHeight;
                DrawStartingPoints(list);
            }

            int shown = 0;
            foreach (Section section in sections)
            {
                List<SettingRow> rows = section.Rows.Where(r => Matches(r)).ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                sectionTop[section.Title] = list.CurHeight;
                list.Gap(10f);
                Text.Font = GameFont.Medium;
                list.Label(section.Title);
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                list.Label(section.Subtitle);
                GUI.color = Color.white;
                list.GapLine(4f);
                foreach (SettingRow row in rows)
                {
                    DrawRowSafely(list, row);
                    shown++;
                }
            }

            if (filtering && shown == 0)
            {
                GUI.color = Color.gray;
                list.Label("No settings match.");
                GUI.color = Color.white;
            }

            viewHeight = Mathf.Max(200f, list.CurHeight + 30f);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Site-44] settings screen failed to draw: " + e, 482113);
            }
            finally
            {
                GUI.color = Color.white;
                list.End();
                Widgets.EndScrollView();
            }
        }

        private static readonly HashSet<string> reported = new HashSet<string>();

        private static void DrawRowSafely(Listing_Standard list, SettingRow row)
        {
            try
            {
                row.Draw(list);
            }
            catch (Exception e)
            {
                if (reported.Add(row.Label))
                {
                    Log.Error("[Site-44] settings row '" + row.Label + "' failed to draw: " + e);
                }

                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 0.5f, 0.4f);
                list.Label(row.Label + ": this setting could not be drawn (see the log).");
                GUI.color = Color.white;
            }
        }

        private static bool Matches(SettingRow row)
        {
            if (changedOnly && row.IsDefault)
            {
                return false;
            }

            return query.NullOrEmpty() || row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (row.Tip != null && row.Tip.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // The contents column: jump to a section, and see which one is showing.
        private static void DrawContents(Rect toc, bool filtering)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(toc.x, toc.y, toc.width, 30f), "Contents");
            Text.Font = GameFont.Small;
            float y = toc.y + 34f;
            List<string> titles = new List<string>();
            if (!filtering)
            {
                titles.Add(AboutTitle);
                titles.Add(StartTitle);
            }

            titles.AddRange(sections.Where(sec => sec.Rows.Any(Matches)).Select(sec => sec.Title));
            string current = null;
            foreach (string title in titles)
            {
                float top;
                if (sectionTop.TryGetValue(title, out top) && top <= scroll.y + 24f)
                {
                    current = title;
                }
            }

            foreach (string title in titles)
            {
                Rect entry = new Rect(toc.x, y, toc.width, 26f);
                bool active = title == current;
                if (active)
                {
                    Widgets.DrawHighlightSelected(entry);
                }

                if (Widgets.ButtonText(entry, title, false))
                {
                    float top;
                    if (sectionTop.TryGetValue(title, out top))
                    {
                        pendingScroll = top;
                    }
                }

                y += 27f;
            }

            if (!filtering)
            {
                y += 10f;
                int changed = allRows.Count(r => !r.IsDefault);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(toc.x, y, toc.width, 60f), changed == 0 ? "Every setting is at its default." : changed + " setting(s) changed from their defaults.");
                GUI.color = Color.white;
            }
        }

        private static void DrawAbout(Listing_Standard list)
        {
            Text.Font = GameFont.Medium;
            list.Label(AboutTitle);
            Text.Font = GameFont.Small;
            list.GapLine(4f);
            list.Label("Site-44 turns ruined complexes into places you explore, not places you build around. This page sets how hard that is. A few things work differently from ordinary RimWorld, so it helps to know them before you change anything:");
            list.Gap(4f);
            foreach (string line in new[]
            {
                "A run is a chain of complexes. In Delve, every complex is a level with an extraction gate to the next one, and the run ends when the whole party dies. There is no colony to build; only what your colonists carry goes down with them.",
                "The outside world is switched off. Raids, caravans, visitors, traders and weather disasters are off by default during a Delve run. The danger comes from the ruins: creature nests, hostile sleepers, security and sealed doors. You can turn the outside world back on under \"The run\".",
                "Supplies are scarce on purpose. Medicine and meals are rare, wounds can turn septic, and a small crew cannot treat everything. Expect to ration, and expect to find a rare antiseptic injector worth keeping.",
                "Some jobs are not optional. Terminals need someone who can hack, and the ruins need someone who can shoot, so the character page requires at least one of each. Doors can be opened by hacking a terminal, finding a keycard, or earning clearance from a badge or a node.",
                "Changes here apply to complexes generated from now on, and they follow you into every game mode. Settings tagged Delve, Breach or Quest sites affect only that mode."
            })
            {
                list.Label("\u2022  " + line);
                list.Gap(2f);
            }

            GUI.color = Color.gray;
            list.Label("Use the contents list on the left to jump around, search to find a setting, and \"Changed settings only\" to see what you have altered. A changed setting turns blue and shows its own Reset button.");
            GUI.color = Color.white;
        }

        private static void DrawStartingPoints(Listing_Standard list)
        {
            list.Gap(10f);
            Text.Font = GameFont.Medium;
            list.Label(StartTitle);
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            list.Label("These buttons overwrite the balance settings below with a ready-made set of values. Pick one, then adjust anything you like. Sizes, biomes and scenario choices are kept.");
            GUI.color = Color.white;
            list.GapLine(4f);
            Rect presetRow = list.GetRect(30f);
            float buttonWidth = presetRow.width / Site44Settings.PresetNames.Length;
            for (int i = 0; i < Site44Settings.PresetNames.Length; i++)
            {
                Rect button = new Rect(presetRow.x + i * buttonWidth + 2f, presetRow.y, buttonWidth - 4f, presetRow.height);
                TooltipHandler.TipRegion(button, "Sets loot, supplies, infection, puzzles, keycards, nests and party size to the " + Site44Settings.PresetNames[i].ToLower() + " preset.");
                if (Widgets.ButtonText(button, Site44Settings.PresetNames[i]))
                {
                    S.ApplyPreset(i);
                }
            }

            if (list.ButtonText("Vanilla rules: switch off the mod's gameplay changes"))
            {
                S.ApplyVanillaRules();
            }

            list.Gap(6f);
            Text.Font = GameFont.Medium;
            list.Label("Current balance: " + CurrentBalance());
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            list.Label(Summary());
            GUI.color = Color.white;
            list.Gap(6f);
            if (list.ButtonText("Reset every setting to its default"))
            {
                S.Reset();
            }
        }

        private static string Summary()
        {
            List<string> parts = new List<string>();
            parts.Add(S.startingColonists + " colonist(s)");
            parts.Add(new[] { "vanilla", "reduced", "no" }[Mathf.Clamp(S.delveInfection, 0, 2)] + " wound infection in Delve");
            parts.Add("Delve events: " + (new[] { S.delveAllowRaids, S.delveAllowCaravanEvents, S.delveAllowOtherThreats, S.delveAllowOutsideVisitors, S.delveAllowWeather }.Count(b => b) == 0 ? "all off" : "some enabled"));
            parts.Add(S.hackMinigame ? "cipher-lock hacking, " + new[] { "easy", "normal", "hard" }[Mathf.Clamp(S.puzzleDifficulty, 0, 2)] : "timed hacking");
            parts.Add("medicine and meals at " + Pct(Site44Tuning.SupplyShare) + " supply");
            return string.Join(", ", parts.ToArray()) + ".";
        }
    }
}
