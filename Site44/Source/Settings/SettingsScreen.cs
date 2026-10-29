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
    internal static class Ui
    {
        // Secondary text: light enough to read comfortably on the dark panel, still quieter than the labels.
        public static readonly UnityEngine.Color Muted = new UnityEngine.Color(0.78f, 0.78f, 0.78f);
        public static readonly UnityEngine.Color Disabled = new UnityEngine.Color(0.62f, 0.62f, 0.62f);
    }

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

        private static readonly Color ChangedColour = new Color(0.55f, 0.8f, 1f);

        public string ZeroNote = "None.";       // what the live reading says when the value is 0%
        protected virtual bool AtZero => false;

        public SettingRow Zero(string note)
        {
            ZeroNote = note;
            return this;
        }

        // The first sentence of the tip is always on screen; the full tip is a hover only when there is more than that.
        private static string FirstSentence(string text, out bool hasMore)
        {
            hasMore = false;
            if (text.NullOrEmpty())
            {
                return null;
            }

            int end = text.IndexOf(". ", StringComparison.Ordinal);
            int newline = text.IndexOf((char)10);
            if (newline >= 0 && (end < 0 || newline < end))
            {
                end = newline - 1;
            }

            if (end < 0)
            {
                return text;
            }

            hasMore = true;
            return text.Substring(0, end + 1);
        }

        public void Draw(Listing_Standard list)
        {
            bool enabled = Enabled == null || Enabled();
            Rect row = list.GetRect(Text.LineHeight + 2f);

            Rect controlRect = new Rect(row.x + 12f, row.y, row.width - 12f, row.height);

            if (!enabled)
            {
                GUI.color = Ui.Disabled;
            }

            // A modified setting gets a bar at its left edge as well as the blue label, so the mark never depends on colour alone.
            if (enabled && !IsDefault)
            {
                Widgets.DrawBoxSolid(new Rect(row.x, row.y + 1f, 4f, row.height - 2f), ChangedColour);
            }

            DrawControl(list, controlRect);
            GUI.color = Color.white;

            bool hasMore;
            string description = FirstSentence(Tip, out hasMore);
            string reading = !enabled ? DisabledNote : (Effect != null ? (AtZero ? ZeroNote : Effect()) : null);
            GUI.color = Ui.Muted;
            if (!description.NullOrEmpty())
            {
                list.Label(description);
            }

            if (!reading.NullOrEmpty())
            {
                list.Label(reading);
            }

            GUI.color = Color.white;
            if (hasMore)
            {
                Rect hover = new Rect(row.x, row.y, row.width, list.CurHeight - row.y);
                TooltipHandler.TipRegion(hover, Tip);
            }

            list.Gap(10f);
        }

        protected string Title => Label + (Scope != null ? "  [" + Scope + "]" : "");

        protected Color LabelColour(bool enabled)
        {
            return !enabled ? Ui.Disabled : (IsDefault ? Color.white : ChangedColour);
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
        private float lastModel = float.NaN;

        public override bool IsDefault => Mathf.Approximately(Get(), Default);
        protected override bool AtZero => !Integer && Mathf.Approximately(Get(), 0f);
        public override void Reset() { Set(Default); buffer = null; }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            GUI.color = LabelColour(enabled);
            Widgets.Label(new Rect(row.x, row.y, row.width - 100f, row.height), Title);
            GUI.color = enabled ? Color.white : Ui.Disabled;

            // A preset or another row changed this value behind the text box's back: show the real number again.
            if (!float.IsNaN(lastModel) && !Mathf.Approximately(Get(), lastModel))
            {
                buffer = null;
            }

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
            lastModel = Get();
        }
    }

    // A multiplier chosen from named steps instead of a bare number: "dust: heavy" says more than "150%".
    internal sealed class LevelRow : SettingRow
    {
        public Func<float> Get;
        public Action<float> Set;
        public float Default;
        public string[] Names;
        public float[] Values;

        private int Index()
        {
            float value = Get();
            int best = 0;
            for (int i = 1; i < Values.Length; i++)
            {
                if (Mathf.Abs(Values[i] - value) < Mathf.Abs(Values[best] - value))
                {
                    best = i;
                }
            }

            return best;
        }

        public override bool IsDefault => Mathf.Approximately(Get(), Default);
        protected override bool AtZero => Mathf.Approximately(Get(), 0f);
        public override void Reset() { Set(Default); }

        protected override void DrawControl(Listing_Standard list, Rect row)
        {
            bool enabled = Enabled == null || Enabled();
            GUI.color = LabelColour(enabled);
            int index = Index();
            Widgets.Label(row, Title + ":  " + Names[index]);
            GUI.color = enabled ? Color.white : Ui.Disabled;
            int slid = Mathf.RoundToInt(list.Slider(index, 0f, Names.Length - 1));
            if (slid != index)
            {
                Set(Values[slid]);
            }
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
            GUI.color = enabled ? Color.white : Ui.Disabled;
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
            GUI.color = enabled ? Color.white : Ui.Disabled;
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
        private static HashSet<string> pinned = new HashSet<string>();
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
            "supplyX", "lootAmount", "crateDensity", "hostileCasketX", "delveInfection", "delveDescentCare", "delveDescentRest", "puzzleDifficulty",
            "keycardX", "nestDensity", "startingColonists", "drugFindX", "securityLevel", "dangerX", "missionFocusLevel",
            "delveAllowRaids", "delveAllowCaravanEvents", "delveAllowOtherThreats", "delveAllowOutsideVisitors", "delveAllowWeather"
        };

        private static readonly string[] VanillaFields =
        {
            "delveAllowRaids", "delveAllowCaravanEvents", "delveAllowOtherThreats", "delveAllowOutsideVisitors", "delveAllowWeather",
            "delveInfection", "delveDescentCare", "delveDescentRest", "missionFocusLevel", "hardenedToBodies", "startingKit", "supplyX", "drugFindX",
            "delverGearX", "hackMinigame", "securityLevel", "hotZonesEnabled"
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

        internal static string CurrentBalance()
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

        private static LevelRow Level(string label, string tip, Func<float> get, Action<float> set, string[] names, float[] values, bool advanced = false, Func<string> effect = null)
        {
            return new LevelRow { Label = label, Tip = tip, Advanced = advanced, Effect = effect, Get = get, Set = set, Default = 1f, Names = names, Values = values };
        }

        // Settings read only while a new game is being set up are locked once a game is running.
        private static SettingRow NewGameOnly(SettingRow row)
        {
            Func<bool> original = row.Enabled;
            row.Enabled = () => !Site44Settings.InRun && (original == null || original());
            row.DisabledNote = row.DisabledNote ?? "Only used when a new game starts; change it from the main menu.";
            return row;
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
                Toggle("Vanilla enemy raids", "",
                    () => S.delveAllowRaids, v => S.delveAllowRaids = v, false, "Delve"),
                Toggle("Vanilla caravan events", "Caravan ambushes, manhunter ambushes, caravan demands, and caravan meetings.",
                    () => S.delveAllowCaravanEvents, v => S.delveAllowCaravanEvents = v, false, "Delve"),
                Toggle("Vanilla other threats", "Infestations, manhunter packs, animal insanity, and crashed threat ship parts.",
                    () => S.delveAllowOtherThreats, v => S.delveAllowOtherThreats = v, false, "Delve"),
                Toggle("Vanilla visitors and outside arrivals", "Traders, visitors, travelers, wanderers, refugee and resource pods, and migrating herds.",
                    () => S.delveAllowOutsideVisitors, v => S.delveAllowOutsideVisitors = v, false, "Delve"),
                Toggle("Vanilla weather disasters", "Toxic fallout, volcanic winter, heat waves, cold snaps, flash storms, solar flares, and eclipses.",
                    () => S.delveAllowWeather, v => S.delveAllowWeather = v, false, "Delve"),
                Mult("Starting colonists", "Your group needs at least one colonist who can fight and one who can do intellectual work.",
                    () => S.startingColonists, v => S.startingColonists = Mathf.RoundToInt(v), 5f, null, false, null, null, null, 1f, 3f, true),
                Choice("Wound infection risk", "Chance that a fresh wound becomes infected, compared with the base game.",
                    () => S.delveInfection, v => S.delveInfection = v, 1, new[] { "same as vanilla", "half of vanilla", "none" }, "Delve"),
                Choice("Wounds on descent", "What the extraction gate does for the party's injuries on the way down.",
                    () => S.delveDescentCare, v => S.delveDescentCare = v, 0, new[] { "carried down", "dressed", "healed", "fully restored" }, "Delve",
                    () => new[] { "Wounds carry down with the party.", "Every wound that can still be tended is dressed.", "Injuries and infections are removed; scars, illnesses and missing parts stay.", "Everything the game can fix is fixed, including missing parts and scars." }[Mathf.Clamp(S.delveDescentCare, 0, 3)]),
                Toggle("Restore hunger and rest on descent", "Fills food and rest for the whole party at each extraction gate.",
                    () => S.delveDescentRest, v => S.delveDescentRest = v, false, "Delve"),
                Choice("Mission focus (mood bonus in a complex)", "A steady mood lift for colonists working inside a ruined complex. Raise it if mental breaks are too frequent.",
                    () => S.missionFocusLevel, v => S.missionFocusLevel = v, 2, new[] { "off", "low (+3)", "normal (+6)", "high (+10)" }),
                Toggle("Delvers ignore plain corpses", "Colonists inside a complex stop being upset by ordinary corpses. Rotting corpses still bother them.",
                    () => S.hardenedToBodies, v => S.hardenedToBodies = v, true, null, true),
                new PickRow
                {
                    Label = "Delve pace", Scope = "Delve", Button = "Change",
                    Tip = "Quick delves are always Small, Steady varies and tends to get larger, Expeditions are Medium to Large, or fix every level to one size.",
                    Get = () => S.delveSize == "Grows with depth" ? "Steady" : S.delveSize,
                    Default = () => D.delveSize,
                    ResetValue = () => S.delveSize = D.delveSize,
                    Pick = () => Find.WindowStack.Add(new FloatMenu(new[] { "Quick delves", "Steady", "Expeditions", "Extra small", "Small", "Medium", "Large", "XL" }
                        .Select(name => new FloatMenuOption(name, () => S.delveSize = name)).ToList()))
                }
            };

            var loot = new List<SettingRow>
            {
                Mult("Food and medicine supply", "How often medicine and meals turn up in crates, caskets and on shelves, and how large their stacks are.\n0% = almost none, 400% = plentiful. Packed supplies from the starting kit are unaffected.",
                    () => S.supplyX, v => S.supplyX = v, 4f, null, false, () => "Keeps " + Pct(Site44Tuning.SupplyShare) + " of medicine and meal picks.").Zero("No medicine or meals turn up in loot, caskets or the welcome crate."),
                Mult("Number of loot crates", "Sealed hermetic crates scattered through the ruins, each holding a few stacks of loot.", () => S.crateDensity, v => S.crateDensity = v, 3f),
                Mult("Loot stack size", "The size of every loot stack in a complex: crates, caskets, storerooms and intact rooms.", () => S.lootAmount, v => S.lootAmount = v, 3f, min: 0.1f),
                Mult("Focus tabs in loot", "Chance that an ordinary loot item is a stack of focus tabs, a non-addictive stimulant that slows hunger and fatigue.",
                    () => S.drugFindX, v => S.drugFindX = v, 3f, null, true, () => "About " + Pct(Site44Tuning.DrugFindChance) + " of ordinary loot items.").Zero("No focus tabs in loot."),
                Mult("Delver gear in rich loot", "Chance that a rich loot item is a delver's cap or vest, which lower the chance of mental breaks.",
                    () => S.delverGearX, v => S.delverGearX = v, 4f, null, true, () => "About " + Pct(Site44Tuning.DelverGearChance) + " of rich loot items."),
                Mult("Intact rooms", "Chance that an ordinary room survived intact: clean sterile floor, undamaged furniture and better loot. Storage, server and reactor wings always have at least one, unless this is 0%.",
                    () => S.preservedRoomX, v => S.preservedRoomX = v, 7f, null, true, () => "About " + Pct(Site44Tuning.PreservedRoomChance) + " of ordinary rooms.").Zero("No ordinary room survives intact; secure rooms stay pristine."),
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
                Mult("Creature nests", "How much of each level's danger is spent on creature nests.", () => S.nestDensity, v => S.nestDensity = v, 3f),
                Mult("Danger", "How much danger each level is given to spend on creature nests and security.",
                    () => S.dangerX, v => S.dangerX = v, 3f),
                Choice("Facility security", "Turrets and mechanoid guards in security sectors; a security terminal or staff clearance stands them down.",
                    () => S.securityLevel, v => S.securityLevel = v, 2, new[] { "off", "light", "normal", "heavy" }),
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
                Level("Dust and debris", "How much dust, trash and scattered paper covers the floors.",
                    () => S.dustAmount, v => S.dustAmount = v, new[] { "spotless", "light", "normal", "heavy", "filthy" }, new[] { 0f, 0.4f, 1f, 1.5f, 2f }, true,
                    () => "About " + Mathf.RoundToInt(Site44Tuning.DustChanceRoom * 100f) + " of every 100 room floor cells carry dust or debris, and " + Mathf.RoundToInt(Site44Tuning.DustChanceCorridor * 100f) + " in corridors."),
                Level("Outdoor props", "Sandbags, barriers, silos, crates, planters and lamp posts scattered through the yards, different for each kind of complex.",
                    () => S.propDensity, v => S.propDensity = v, new[] { "none", "sparse", "normal", "dense", "cluttered" }, new[] { 0f, 0.5f, 1f, 2f, 3f }, true),
                Level("Furniture damage", "Every piece of furniture is left at a random share of its full health, so even the worst setting leaves every piece standing, just barely. Intact rooms are exempt.",
                    () => S.ruinX, v => S.ruinX = v, new[] { "intact", "worn", "damaged", "wrecked" }, new[] { 0f, 0.5f, 1f, 1.33f }, true,
                    () => "Each piece is left at " + Mathf.RoundToInt(Site44Tuning.RuinedFurnitureMinHealth * 100f) + "% to " + Mathf.RoundToInt(Site44Tuning.RuinedFurnitureMaxHealth * 100f) + "% of its full health."),
                Level("Rec rooms", "How often a break room or cafeteria turns out to be a rec room, with a billiards or card table, a television and couches.",
                    () => S.recRoomFrequency, v => S.recRoomFrequency = v, new[] { "none", "rare", "normal", "common", "frequent" }, new[] { 0f, 0.5f, 1f, 2f, 3f }, true),
                Level("Specialty rooms", "How many ordinary rooms turn out to be something more specific: kitchens, dormitories, clinics, locker rooms, libraries, workshops, armories, comms rooms and classrooms.",
                    () => S.roomVariety, v => S.roomVariety = v, new[] { "none", "few", "normal", "many", "most" }, new[] { 0f, 0.5f, 1f, 2f, 3f }, true)
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

            NewGameOnly(delve.First(r => r.Label == "Starting colonists"));
            NewGameOnly(setup.First(r => r.Label == "Starting kit"));
            NewGameOnly(setup.First(r => r.Label == "Skip world and site selection"));
            NewGameOnly(setup.First(r => r.Label == "Ruin quest frequency"));
            NewGameOnly(setup.First(r => r.Label == "Complex used by the Breach scenario"));
            NewGameOnly(setup.First(r => r.Label == "Size of the Breach scenario complex"));
            allRows = delve.Concat(loot).Concat(hazards).Concat(looks).Concat(setup).ToList();
            Func<string[], List<SettingRow>> pick = labels => labels.Select(l => allRows.First(r => r.Label == l)).ToList();

            sections = new List<Section>
            {
                new Section { Title = "General settings", Subtitle = "", Rows = pick(new[] {
                    "Starting colonists", "Vanilla enemy raids", "Vanilla caravan events", "Vanilla other threats", "Vanilla visitors and outside arrivals", "Vanilla weather disasters",
                    "Wound infection risk", "Wounds on descent", "Restore hunger and rest on descent", "Mission focus (mood bonus in a complex)", "Delvers ignore plain corpses" }) },
                new Section { Title = "Loot and supplies", Subtitle = "", Rows = pick(new[] {
                    "Food and medicine supply", "Number of loot crates", "Loot stack size", "Focus tabs in loot", "Delver gear in rich loot", "Intact rooms" }) },
                new Section { Title = "Creatures and survivors", Subtitle = "", Rows = pick(new[] {
                    "Cryosleep caskets", "Survivor caskets per complex", "Hostile caskets", "Dried-out corpses" }) },
                new Section { Title = "Hazards and security", Subtitle = "", Rows = pick(new[] {
                    "Danger", "Creature nests", "Facility security", "Overheated sectors", "Prototype droid guards" }) },
                new Section { Title = "Hacking, keys and clearance", Subtitle = "", Rows = pick(new[] {
                    "Cipher-lock hacking", "Puzzle difficulty", "Keycards for locked gates", "Clearance-gated wings", "Secure rooms" }) },
                new Section { Title = "Complexes and sizes", Subtitle = "", Rows = pick(new[] {
                    "Open layout (explore freely)", "Locked vaults", "Delve pace", "Complex used by the Breach scenario", "Size of the Breach scenario complex", "Size of quest-site complexes" }) },
                new Section { Title = "Scenario setup", Subtitle = "", Rows = pick(new[] { "Starting kit", "Skip world and site selection", "Ruin quest frequency" }) },
                new Section { Title = "Look and fine detail", Subtitle = "", Rows = pick(new[] {
                    "Dust and debris", "Outdoor props", "Furniture damage", "Rec rooms", "Specialty rooms" }) }
            };
        }

        // ------------------------------------------------------------------ drawing

        private const float TocWidth = 190f;
        private const string AboutTitle = "About this version";
        private const string StartTitle = "Difficulty presets";

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
            Widgets.CheckboxLabeled(new Rect(searchRow.xMax - 184f, searchRow.y + 1f, 184f, 26f), "Modified settings only", ref changedBox);
            if (changedBox && !changedOnly)
            {
                // Remember which rows are modified right now: they stay listed until the filter is switched off, even if one is set back to its default.
                pinned = new HashSet<string>(allRows.Where(row => !row.IsDefault).Select(row => row.Label));
            }

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
            GUI.color = Ui.Muted;
            list.Label(Site44Settings.InRun
                ? "Editing the settings for this game. The defaults for new games are changed from the main menu."
                : "Editing the defaults that new games start from. Games already running keep their own copy.");
            GUI.color = Color.white;
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
                list.Gap(28f);
                Text.Font = GameFont.Medium;
                list.Label(section.Title);
                Text.Font = GameFont.Small;
                list.GapLine(4f);
                list.Gap(8f);
                foreach (SettingRow row in rows)
                {
                    DrawRowSafely(list, row);
                    shown++;
                }
            }

            if (filtering && shown == 0)
            {
                GUI.color = Ui.Muted;
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
            if (changedOnly && !pinned.Contains(row.Label))
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
                GUI.color = Ui.Muted;
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
                "The outside world is switched off. Raids, caravans, visitors, traders and weather disasters are off by default during a Delve run. The danger comes from the ruins: creature nests, hostile sleepers, security and sealed doors. You can turn the outside world back on under \"General settings\".",
                "Supplies are scarce on purpose. Medicine and meals are rare, wounds can turn septic, and a small crew cannot treat everything. Expect to ration, and expect to find a rare antiseptic injector worth keeping.",
                "Some jobs are not optional. Terminals need someone who can hack, and the ruins need someone who can shoot, so the character page requires at least one of each. Doors can be opened by hacking a terminal, finding a keycard, or earning clearance from a badge or a node.",
                "Changes here apply to complexes generated from now on, and they follow you into every game mode. Settings tagged Delve, Breach or Quest sites affect only that mode."
            })
            {
                list.Label("\u2022  " + line);
                list.Gap(2f);
            }

        }

        private static void DrawStartingPoints(Listing_Standard list)
        {
            list.Gap(28f);
            Text.Font = GameFont.Medium;
            list.Label(StartTitle);
            Text.Font = GameFont.Small;
            list.GapLine(4f);
            list.Gap(6f);
            const float gap = 6f, height = 30f;
            Rect block = list.GetRect(height * 2f + gap);
            string current = CurrentBalance();
            float buttonWidth = (block.width - gap * (Site44Settings.PresetNames.Length - 1)) / Site44Settings.PresetNames.Length;
            for (int i = 0; i < Site44Settings.PresetNames.Length; i++)
            {
                string name = Site44Settings.PresetNames[i];
                Rect button = new Rect(block.x + i * (buttonWidth + gap), block.y, buttonWidth, height);
                TooltipHandler.TipRegion(button, i == 1
                    ? "Resets every setting in this mod to its default, including sizes and scenario choices."
                    : i == 4
                        ? "Not balanced to be won: one colonist, no medicine, vanilla infection, nests everywhere, no keycards, and raids, threats and weather switched on. Sizes and scenario choices are kept."
                        : "Sets loot, supplies, infection, puzzles, keycards, nests and party size to the " + name.ToLower() + " preset. Sizes and scenario choices are kept.");
                DrawPresetButton(button, name, current == name, () =>
                {
                    if (i == 1) S.Reset(); else S.ApplyPreset(i);
                });
            }

            Rect vanilla = new Rect(block.x, block.y + height + gap, block.width, height);
            TooltipHandler.TipRegion(vanilla, "Switches off every change this mod makes to how the game plays: Delve events, infection, moods, supplies and hacking behave as in the base game.");
            DrawPresetButton(vanilla, "Vanilla rules", current == "Vanilla rules", () => S.ApplyVanillaRules());

            list.Gap(8f);
            list.Label(Summary());
            List<string> harsh = HarshnessWarnings();
            if (harsh.Count > 0)
            {
                list.Gap(6f);
                GUI.color = new Color(1f, 0.6f, 0.35f);
                list.Label("Warning: this combination is very harsh. " + string.Join(" ", harsh.Take(3).ToArray()));
                GUI.color = Color.white;
            }
        }

        // Combinations that make a run close to unwinnable. They are allowed (nothing is ever blocked), but worth a warning.
        private static List<string> HarshnessWarnings()
        {
            var reasons = new List<string>();
            int score = 0;
            bool noMedicine = Site44Tuning.SupplyShare <= 0.0001f;
            if (noMedicine && S.delveInfection == 0)
            {
                reasons.Add("No medicine and vanilla wound infection means most serious wounds are fatal.");
                score += 4;
            }
            else if (Site44Tuning.SupplyShare <= 0.05f)
            {
                reasons.Add("Medicine is almost absent.");
                score += 2;
            }

            if (S.startingColonists == 1)
            {
                reasons.Add("A single colonist ends the run with the first death.");
                score += 2;
            }
            else if (S.startingColonists == 2)
            {
                score += 1;
            }

            if (S.nestDensity >= 2.5f)
            {
                reasons.Add("Nests are in nearly every room.");
                score += 2;
            }
            else if (S.nestDensity >= 1.5f)
            {
                score += 1;
            }

            if (S.delveAllowRaids)
            {
                reasons.Add("Raids are enabled" + (S.startingColonists <= 2 ? " against a very small party." : "."));
                score += S.startingColonists <= 2 ? 3 : 1;
            }

            if (S.securityLevel >= 3)
            {
                score += 1;
            }

            if (S.dangerX >= 2f)
            {
                reasons.Add("Danger is set very high.");
                score += 2;
            }

            if (S.hostileCasketX >= 2f)
            {
                score += 1;
            }

            if (S.lootAmount <= 0.6f)
            {
                score += 1;
            }

            if (score >= 4 && reasons.Count == 0)
            {
                reasons.Add("Several difficulty settings stack up.");
            }

            return score >= 4 ? reasons : new List<string>();
        }

        // A button that stays lit while the settings match what it sets.
        private static void DrawPresetButton(Rect rect, string label, bool pressed, Action click)
        {
            if (pressed)
            {
                GUI.color = new Color(1f, 0.92f, 0.55f);
            }

            bool clicked = Widgets.ButtonText(rect, label);
            GUI.color = Color.white;
            if (pressed)
            {
                Widgets.DrawHighlightSelected(rect);
            }

            if (clicked)
            {
                click();
            }
        }

        private static string Summary()
        {
            List<string> parts = new List<string>();
            parts.Add(CurrentBalance());
            parts.Add(S.startingColonists == 1 ? "1 colonist" : S.startingColonists + " colonists");
            parts.Add("wound infection risk " + new[] { "same as vanilla", "half of vanilla", "removed" }[Mathf.Clamp(S.delveInfection, 0, 2)]);
            int events = new[] { S.delveAllowRaids, S.delveAllowCaravanEvents, S.delveAllowOtherThreats, S.delveAllowOutsideVisitors, S.delveAllowWeather }.Count(b => b);
            parts.Add(events == 0 ? "all Delve events disabled" : events == 5 ? "all Delve events enabled" : "some Delve events enabled");
            parts.Add(S.hackMinigame ? "cipher-lock hacking at " + new[] { "easy", "normal", "hard" }[Mathf.Clamp(S.puzzleDifficulty, 0, 2)] + " difficulty" : "timed hacking");
            parts.Add("medicine and meals at " + Pct(Site44Tuning.SupplyShare) + " supply");
            return "Current settings summary: " + string.Join(", ", parts.ToArray()) + ".";
        }
    }
}
