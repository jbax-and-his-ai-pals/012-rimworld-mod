using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // The per-game choices for a player who wants to tinker. The rows are the same ones the settings window uses, so they read
    // and behave the same way.
    public class Page_Site44Customize : Page
    {
        public static bool ChooseStoryteller;

        public override string PageTitle => "Customize Site-44 settings";

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            float total = Mathf.Min(main.width, 1100f);
            float x = main.x + (main.width - total) / 2f;
            float gap = 28f;
            float column = (total - gap) / 2f;

            Listing_Standard left = new Listing_Standard { maxOneColumn = true };
            left.Begin(new Rect(x, main.y, column, main.height));
            Text.Font = GameFont.Medium;
            left.Label("Party and events");
            Text.Font = GameFont.Small;
            left.GapLine(4f);
            left.Gap(6f);
            SettingsScreen.DrawRows(left, "Starting colonists", "Vanilla enemy raids", "Vanilla caravan events", "Vanilla other threats", "Vanilla visitors and outside arrivals", "Vanilla weather disasters");
            left.End();

            Listing_Standard right = new Listing_Standard { maxOneColumn = true };
            right.Begin(new Rect(x + column + gap, main.y, column, main.height));
            Text.Font = GameFont.Medium;
            right.Label("Health and saving");
            Text.Font = GameFont.Small;
            right.GapLine(4f);
            right.Gap(6f);
            SettingsScreen.DrawRows(right, "Wound infection risk", "Wounds on descent", "Restore hunger and rest on descent", "Commitment mode");
            right.CheckboxLabeled("Choose my own storyteller and difficulty", ref ChooseStoryteller);
            GUI.color = Ui.Muted;
            right.Label("Adds RimWorld's storyteller page after the biomes. Its difficulty adds to this mod's, and its saving mode replaces the one above.");
            GUI.color = Color.white;
            right.End();

            DoBottomButtons(inRect, "Next");
        }

        protected override void DoNext()
        {
            Site44Settings.SaveGlobal();
            base.DoNext();
        }
    }
}
