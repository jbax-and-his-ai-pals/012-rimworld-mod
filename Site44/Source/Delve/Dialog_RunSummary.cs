using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Site44
{
    // Shown when the whole party is gone: how the run went, and the settings it was played on so runs can be compared.
    public class Dialog_RunSummary : Window
    {
        private readonly List<KeyValuePair<string, string>> lines;

        public override Vector2 InitialSize => new Vector2(520f, 120f + 30f * 7);

        public Dialog_RunSummary(List<KeyValuePair<string, string>> lines)
        {
            this.lines = lines;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "The run is over");
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, 38f, inRect.width);

            float y = 50f;
            foreach (KeyValuePair<string, string> line in lines)
            {
                Widgets.Label(new Rect(0f, y, inRect.width * 0.5f, 28f), line.Key);
                Widgets.Label(new Rect(inRect.width * 0.5f, y, inRect.width * 0.5f, 28f), line.Value);
                y += 30f;
            }

            if (Widgets.ButtonText(new Rect((inRect.width - 160f) / 2f, inRect.height - 38f, 160f, 34f), "Close"))
            {
                Close();
            }
        }
    }
}
