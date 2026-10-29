using System.Linq;
using Site44.Gen;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // The first page of a Delve game: how hard it is, how long its levels are, and how much more the player wants to set up.
    // Everything not asked here has a sensible default and lives in the settings.
    public class Page_Site44Setup : Page
    {
        // More setup: neither box ticked is just play; either or both can be ticked.
        public static bool Customize;
        public static bool Full;

        private static readonly string[] DifficultyNames = { "Easier", "Default", "Harder", "Brutal", "Unfair" };
        private static readonly string[] DifficultyText =
        {
            "Gentler threats, more medicine and supplies, and the lift heals the party between levels.",
            "The intended balance. Medicine is scarce and the ruins are the danger.",
            "More danger and fewer supplies.",
            "Two colonists, no medicine, and heavy defences.",
            "Built to be lost: one colonist, no medicine, and raids, threats and weather switched on."
        };

        private static readonly string[] PaceNames = { DelveDifficulty.QuickDelves, DelveDifficulty.Steady, DelveDifficulty.Expeditions };
        private static readonly string[] PaceText =
        {
            "Short levels, one after another.",
            "Levels vary in length and tend to get larger the deeper you go.",
            "Long levels worth exploring thoroughly."
        };

        private static readonly string[] PathNames = { "Just play", "Customize Site-44 settings", "Full RimWorld setup" };
        private static readonly string[] PathText =
        {
            "",
            "Party size, events, healing, saving mode and biomes.",
            "Choose storyteller, difficulty, world, and landing site."
        };

        // Said only when the full setup is picked: RimWorld's own difficulty is a separate scale from this mod's.
        private const string FullSetupNote = "RimWorld's difficulty mostly shapes vanilla events, which are off in Delve unless you turn them on. It also changes wound infection and mood, on top of Site-44's difficulty.";

        public override string PageTitle => "Difficulty and pace";

        // The rest of the pages depend on the choice made here, so they are put together when the player moves on.
        protected override void DoNext()
        {
            Site44Scenario scenario = Find.Scenario as Site44Scenario;
            if (scenario != null)
            {
                next = scenario.BuildPages(Customize, Full);
                if (next != null)
                {
                    next.prev = this;
                }
            }

            Site44Settings.SaveGlobal();
            base.DoNext();
        }

        private static readonly string[] FocusNames = { "Balanced", "Explorer", "Power trip" };
        private static readonly string[] FocusText =
        {
            "Exploring, fighting and rationing all matter.",
            "Combat takes a backseat: few, weak threats, the facility treats you as staff, and puzzles are easier.",
            "Fight swarms of weak foes with a tougher, faster-learning party and better gear as you go deeper."
        };

        private Vector2 scroll;
        private float contentHeight = 900f;

        public override void DoWindowContents(Rect inRect)
        {
            // The column headings say what the page is, so it has no title of its own.
            Rect main = GetMainRect(inRect, 0f, true);
            Site44Settings settings = Site44Settings.Global;

            float total = Mathf.Min(main.width, 1200f);
            float x = main.x + (main.width - total) / 2f;
            float gap = 28f;
            float width = total - 16f;
            float column = (width - gap * 2f) / 3f;

            Rect scrollRect = new Rect(x, main.y, total, main.height);
            Widgets.BeginScrollView(scrollRect, ref scroll, new Rect(0f, 0f, width, contentHeight));

            float top = 0f;
            top = Mathf.Max(top, DrawColumn(new Rect(0f, 0f, column, 4000f), "Difficulty", "Site-44's own features: danger, supplies and healing.", DifficultyNames, DifficultyText, System.Array.IndexOf(DifficultyNames, SettingsScreen.CurrentBalance()), index =>
            {
                settings.ApplyPreset(index);
            }));

            string pace = settings.delveSize == "Grows with depth" ? DelveDifficulty.Steady : settings.delveSize;
            top = Mathf.Max(top, DrawColumn(new Rect(column + gap, 0f, column, 4000f), "Pace", null, PaceNames, PaceText, System.Array.IndexOf(PaceNames, pace), index =>
            {
                settings.delveSize = PaceNames[index];
            }));

            top = Mathf.Max(top, DrawColumn(new Rect((column + gap) * 2f, 0f, column, 4000f), "Focus", null, FocusNames, FocusText, Mathf.Clamp(settings.focus, 0, 2), index =>
            {
                settings.focus = index;
            }));

            float bottom = DrawMoreSetup(new Rect(0f, top + 24f, width, 4000f));
            contentHeight = top + 24f + bottom + 16f;
            Widgets.EndScrollView();

            DoBottomButtons(inRect, "Next");
        }

        // The game still needs a storyteller and a saving mode when the pages do not ask for them: Cassandra by default, and
        // vanilla's neutral difficulty so this mod's own difficulty is the only scaling (vanilla's would multiply wound infection
        // and mood a second time). Vanilla's difficulty and storyteller can still be changed during play from the game's options.
        public static void ApplyDefaultStoryteller()
        {
            Site44Settings settings = Site44Settings.Global;
            StorytellerDef teller = DefDatabase<StorytellerDef>.GetNamedSilentFail(settings.storyteller)
                                    ?? DefDatabase<StorytellerDef>.AllDefs.FirstOrDefault(d => d.listVisible && !d.tutorialMode)
                                    ?? DefDatabase<StorytellerDef>.AllDefsListForReading.First();
            DifficultyDef difficulty = DifficultyDefOf.Rough;
            Current.Game.storyteller = new Storyteller(teller, difficulty, new Difficulty(difficulty));
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = settings.commitment;
        }

        public static float DrawColumn(Rect rect, string title, string subtitle, string[] names, string[] text, int selected, System.Action<int> choose, string selectedNote = null)
        {
            Listing_Standard list = new Listing_Standard { maxOneColumn = true };
            list.Begin(rect);
            Text.Font = GameFont.Medium;
            list.Label(title);
            Text.Font = GameFont.Small;
            list.GapLine(4f);
            if (!subtitle.NullOrEmpty())
            {
                GUI.color = Ui.Muted;
                list.Label(subtitle);
                GUI.color = Color.white;
            }

            list.Gap(6f);
            for (int i = 0; i < names.Length; i++)
            {
                if (Widgets.RadioButtonLabeled(list.GetRect(28f), names[i], i == selected))
                {
                    choose(i);
                }

                GUI.color = Ui.Muted;
                if (!text[i].NullOrEmpty())
                {
                    list.Label(text[i]);
                }

                if (i == selected && !selectedNote.NullOrEmpty())
                {
                    list.Label(selectedNote);
                }

                GUI.color = Color.white;
                list.Gap(10f);
            }

            if (selected < 0)
            {
                GUI.color = Ui.Muted;
                list.Label("Your custom settings are in use.");
                GUI.color = Color.white;
            }

            float used = list.CurHeight;
            list.End();
            return used;
        }

        // Customizing the Site-44 settings and using RimWorld's own setup pages are independent; with neither, the game just starts.
        private static float DrawMoreSetup(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "More setup");
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 34f, rect.width);
            float gap = 24f;
            float cell = (rect.width - gap * 2f) / 3f;
            float tallest = 0f;
            for (int i = 0; i < 3; i++)
            {
                Listing_Standard list = new Listing_Standard { maxOneColumn = true };
                list.Begin(new Rect(rect.x + i * (cell + gap), rect.y + 44f, cell, 4000f));
                bool on = i == 0 ? !Customize && !Full : i == 1 ? Customize : Full;
                bool value = on;
                Widgets.CheckboxLabeled(list.GetRect(28f), PathNames[i], ref value);
                if (value != on)
                {
                    if (i == 0)
                    {
                        Customize = false;
                        Full = false;
                    }
                    else if (i == 1)
                    {
                        Customize = value;
                    }
                    else
                    {
                        Full = value;
                    }
                }

                GUI.color = Ui.Muted;
                if (!PathText[i].NullOrEmpty())
                {
                    list.Label(PathText[i]);
                }

                GUI.color = Color.white;
                tallest = Mathf.Max(tallest, list.CurHeight);
                list.End();
            }

            float used = 44f + tallest;
            if (Full)
            {
                GUI.color = Ui.Muted;
                float noteHeight = Text.CalcHeight(FullSetupNote, rect.width);
                Widgets.Label(new Rect(rect.x, rect.y + used + 6f, rect.width, noteHeight), FullSetupNote);
                GUI.color = Color.white;
                used += noteHeight + 6f;
            }

            return used;
        }

        // The same choices laid out side by side under one heading.
        public static float DrawRow(Rect rect, string title, string[] names, string[] text, int selected, System.Action<int> choose, string selectedNote = null)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), title);
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 34f, rect.width);
            float gap = 24f;
            float cell = (rect.width - gap * (names.Length - 1)) / names.Length;
            float tallest = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                Listing_Standard list = new Listing_Standard { maxOneColumn = true };
                list.Begin(new Rect(rect.x + i * (cell + gap), rect.y + 44f, cell, 4000f));
                if (Widgets.RadioButtonLabeled(list.GetRect(28f), names[i], i == selected))
                {
                    choose(i);
                }

                GUI.color = Ui.Muted;
                if (!text[i].NullOrEmpty())
                {
                    list.Label(text[i]);
                }

                GUI.color = Color.white;
                tallest = Mathf.Max(tallest, list.CurHeight);
                list.End();
            }

            float used = 44f + tallest;
            if (!selectedNote.NullOrEmpty())
            {
                GUI.color = Ui.Muted;
                float noteHeight = Text.CalcHeight(selectedNote, rect.width);
                Widgets.Label(new Rect(rect.x, rect.y + used + 6f, rect.width, noteHeight), selectedNote);
                GUI.color = Color.white;
                used += noteHeight + 6f;
            }

            return used;
        }
    }
}
