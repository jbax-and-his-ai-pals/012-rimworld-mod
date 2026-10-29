using System.Linq;
using Site44.Gen;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // The two choices that shape a new game: how hard it is and, for a Delve run, how long its levels are.
    // Everything else lives behind "Custom settings".
    public class Page_Site44Setup : Page
    {
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

        private bool IsDelve => Find.Scenario != null && Find.Scenario.AllParts.Any(p => p is ScenPart_Site44Delve);

        // The storyteller page is skipped in this flow. The game still needs a storyteller and a saving mode, so they come from the
        // settings: Cassandra by default, and vanilla's neutral difficulty so that this mod's own difficulty is the only scaling
        // (vanilla's difficulty would otherwise multiply wound infection and mood a second time). Vanilla's difficulty and storyteller
        // can still be changed during play from the game's own options.
        protected override void DoNext()
        {
            Site44Settings settings = Site44Settings.Global;
            StorytellerDef teller = DefDatabase<StorytellerDef>.GetNamedSilentFail(settings.storyteller)
                                    ?? DefDatabase<StorytellerDef>.AllDefs.FirstOrDefault(d => d.listVisible && !d.tutorialMode)
                                    ?? DefDatabase<StorytellerDef>.AllDefsListForReading.First();
            DifficultyDef difficulty = DifficultyDefOf.Rough;
            Current.Game.storyteller = new Storyteller(teller, difficulty, new Difficulty(difficulty));
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = settings.commitment;
            base.DoNext();
        }

        public override string PageTitle => IsDelve ? "Difficulty and pace" : "Difficulty";

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            Site44Settings settings = Site44Settings.Global;
            bool delve = IsDelve;

            float total = Mathf.Min(main.width, delve ? 900f : 460f);
            float x = main.x + (main.width - total) / 2f;
            float column = delve ? (total - 30f) / 2f : total;

            DrawColumn(new Rect(x, main.y, column, main.height - 50f), "Difficulty", DifficultyNames, DifficultyText, SelectedDifficulty(), index =>
            {
                settings.ApplyPreset(index);
                Site44Settings.SaveGlobal();
            });

            if (delve)
            {
                int pace = System.Array.IndexOf(PaceNames, settings.delveSize == "Grows with depth" ? DelveDifficulty.Steady : settings.delveSize);
                DrawColumn(new Rect(x + column + 30f, main.y, column, main.height - 50f), "Pace", PaceNames, PaceText, pace, index =>
                {
                    settings.delveSize = PaceNames[index];
                    Site44Settings.SaveGlobal();
                });
            }

            Rect custom = new Rect(x + (total - 260f) / 2f, main.yMax - 40f, 260f, 36f);
            if (Widgets.ButtonText(custom, "Custom settings"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<Site44ModEntry>()));
            }

            DoBottomButtons(inRect, "Next");
        }

        private static int SelectedDifficulty()
        {
            return System.Array.IndexOf(DifficultyNames, SettingsScreen.CurrentBalance());
        }

        private static void DrawColumn(Rect rect, string title, string[] names, string[] text, int selected, System.Action<int> choose)
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
