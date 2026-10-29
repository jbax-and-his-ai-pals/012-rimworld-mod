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
        public const int JustPlay = 0, Tinker = 1, FullVanilla = 2;

        private static int path;

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

        private static readonly string[] PathNames = { "Just play", "Let me tinker", "Full RimWorld setup" };
        private static readonly string[] PathText =
        {
            "Start with these choices. The rest has sensible defaults.",
            "One more page for the party, events, biomes and saving mode.",
            "RimWorld's own pages for storyteller, difficulty, world and landing site. Its difficulty adds to this mod's."
        };

        public override string PageTitle => "Difficulty and pace";

        // The rest of the pages depend on the choice made here, so they are put together when the player moves on.
        protected override void DoNext()
        {
            Site44Scenario scenario = Find.Scenario as Site44Scenario;
            if (scenario != null)
            {
                next = scenario.BuildPages(path);
                if (next != null)
                {
                    next.prev = this;
                }
            }

            Site44Settings.SaveGlobal();
            base.DoNext();
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            Site44Settings settings = Site44Settings.Global;

            float total = Mathf.Min(main.width, 1100f);
            float x = main.x + (main.width - total) / 2f;
            float gap = 28f;
            float column = (total - gap * 2f) / 3f;
            float height = main.height - 50f;

            DrawColumn(new Rect(x, main.y, column, height), "Difficulty", DifficultyNames, DifficultyText, System.Array.IndexOf(DifficultyNames, SettingsScreen.CurrentBalance()), index =>
            {
                settings.ApplyPreset(index);
            });

            string pace = settings.delveSize == "Grows with depth" ? DelveDifficulty.Steady : settings.delveSize;
            DrawColumn(new Rect(x + column + gap, main.y, column, height), "Pace", PaceNames, PaceText, System.Array.IndexOf(PaceNames, pace), index =>
            {
                settings.delveSize = PaceNames[index];
            });

            DrawColumn(new Rect(x + (column + gap) * 2f, main.y, column, height), "Setup", PathNames, PathText, path, index => path = index);

            Rect all = new Rect(x + (total - 260f) / 2f, main.yMax - 40f, 260f, 36f);
            if (Widgets.ButtonText(all, "All settings"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<Site44ModEntry>()));
            }

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

        public static void DrawColumn(Rect rect, string title, string[] names, string[] text, int selected, System.Action<int> choose)
        {
            Listing_Standard list = new Listing_Standard { maxOneColumn = true };
            list.Begin(rect);
            Text.Font = GameFont.Medium;
            list.Label(title);
            Text.Font = GameFont.Small;
            list.GapLine(4f);
            list.Gap(6f);
            for (int i = 0; i < names.Length; i++)
            {
                if (Widgets.RadioButtonLabeled(list.GetRect(28f), names[i], i == selected))
                {
                    choose(i);
                }

                GUI.color = Ui.Muted;
                list.Label(text[i]);
                GUI.color = Color.white;
                list.Gap(10f);
            }

            if (selected < 0)
            {
                GUI.color = Ui.Muted;
                list.Label("Your custom settings are in use.");
                GUI.color = Color.white;
            }

            list.End();
        }
    }
}
