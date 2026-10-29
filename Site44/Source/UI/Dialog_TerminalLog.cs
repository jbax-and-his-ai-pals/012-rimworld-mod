using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // A terminal's stored document. The entries of the same thread read earlier in this complex can be paged back to.
    public class Dialog_TerminalLog : Window
    {
        private readonly StoryThreadDef thread;
        private readonly int latest;
        private int shown;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(620f, 540f);

        public Dialog_TerminalLog(StoryThreadDef thread, int entry)
        {
            this.thread = thread;
            latest = entry;
            shown = entry;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            StoryEntry e = thread.entries[shown];

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), e.title);
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, 36f, inRect.width, 24f), e.kind + "    (" + (shown + 1) + " of " + thread.entries.Count + ", " + thread.LabelCap + ")");
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, 62f, inRect.width);

            string text = (e.header.NullOrEmpty() ? "" : e.header + "\n\n") + e.body;
            Rect outRect = new Rect(0f, 70f, inRect.width, inRect.height - 70f - 44f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f, Text.CalcHeight(text, outRect.width - 18f));
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, viewRect.height), text);
            Widgets.EndScrollView();

            float y = inRect.height - 36f;
            if (shown > 0 && Widgets.ButtonText(new Rect(0f, y, 140f, 34f), "Earlier entry"))
            {
                shown--;
                scroll = Vector2.zero;
            }

            if (shown < latest && Widgets.ButtonText(new Rect(150f, y, 140f, 34f), "Later entry"))
            {
                shown++;
                scroll = Vector2.zero;
            }

            if (Widgets.ButtonText(new Rect(inRect.width - 140f, y, 140f, 34f), "Close"))
            {
                Close();
            }
        }
    }
}
