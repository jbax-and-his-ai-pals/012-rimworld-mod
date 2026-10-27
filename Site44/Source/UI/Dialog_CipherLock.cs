using System;
using System.Text;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    public class Dialog_CipherLock : Window
    {
        private readonly CipherPuzzle puzzle;
        private readonly string title;
        private readonly Action<CipherOutcome> onFinished;
        private Vector2 scroll;
        private bool reported;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public Dialog_CipherLock(CipherPuzzle puzzle, string title, Action<CipherOutcome> onFinished)
        {
            this.puzzle = puzzle;
            this.title = title;
            this.onFinished = onFinished;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
        }

        public override void PreClose()
        {
            base.PreClose();
            Report(puzzle.State);
        }

        private void Report(CipherOutcome outcome)
        {
            if (reported)
            {
                return;
            }

            reported = true;
            onFinished?.Invoke(outcome);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), title);
            Text.Font = GameFont.Small;

            string status = "Select the access code. Each wrong code reports how many characters sit in the correct position.\n"
                + "Attempts left: " + puzzle.AttemptsLeft + "    Purges left: " + puzzle.PurgesLeft;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 62f), status);

            float top = 106f;
            float bottom = inRect.height - 44f;
            float halfW = inRect.width * 0.58f;

            // Candidate grid
            Rect gridRect = new Rect(0f, top, halfW, bottom - top);
            Widgets.DrawMenuSection(gridRect);
            Rect inner = gridRect.ContractedBy(8f);
            int cols = 2;
            float cellW = inner.width / cols;
            float cellH = 30f;
            bool live = puzzle.State == CipherOutcome.Continue;
            Text.Font = GameFont.Small;
            string clicked = null;
            for (int i = 0; i < puzzle.Candidates.Count; i++)
            {
                Rect r = new Rect(inner.x + (i % cols) * cellW, inner.y + (i / cols) * cellH, cellW - 4f, cellH - 4f);
                if (live)
                {
                    if (Widgets.ButtonText(r, puzzle.Candidates[i]))
                    {
                        clicked = puzzle.Candidates[i];
                    }
                }
                else
                {
                    Widgets.DrawBoxSolid(r, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(r, puzzle.Candidates[i]);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            // History
            Rect logRect = new Rect(halfW + 10f, top, inRect.width - halfW - 10f, bottom - top);
            Widgets.DrawMenuSection(logRect);
            Rect logInner = logRect.ContractedBy(8f);
            StringBuilder sb = new StringBuilder();
            foreach (CipherGuessResult g in puzzle.History)
            {
                sb.AppendLine(g.Code + "   " + g.Matches + "/" + puzzle.CodeLength);
            }

            if (puzzle.State == CipherOutcome.Solved)
            {
                sb.AppendLine().AppendLine("ACCESS GRANTED");
            }
            else if (puzzle.State == CipherOutcome.Failed)
            {
                sb.AppendLine().AppendLine("LOCKED OUT");
                sb.AppendLine("Code was " + puzzle.Answer);
            }

            string log = sb.ToString();
            Rect view = new Rect(0f, 0f, logInner.width - 16f, Text.CalcHeight(log, logInner.width - 16f) + 4f);
            Widgets.BeginScrollView(logInner, ref scroll, view);
            Widgets.Label(new Rect(0f, 0f, view.width, view.height), log);
            Widgets.EndScrollView();

            // Buttons
            Rect purgeRect = new Rect(0f, inRect.height - 36f, 180f, 32f);
            if (live)
            {
                if (puzzle.PurgesLeft > 0)
                {
                    if (Widgets.ButtonText(purgeRect, "Purge a decoy (" + puzzle.PurgesLeft + ")"))
                    {
                        puzzle.TryPurge(out string _);
                    }
                }
                else
                {
                    Widgets.DrawBoxSolid(purgeRect, new Color(0f, 0f, 0f, 0.25f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(purgeRect, "No purges");
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            Rect closeRect = new Rect(inRect.width - 140f, inRect.height - 36f, 140f, 32f);
            if (Widgets.ButtonText(closeRect, live ? "Abort" : "Close"))
            {
                Close();
            }

            if (clicked != null)
            {
                puzzle.Guess(clicked);
                if (puzzle.State == CipherOutcome.Solved)
                {
                    Report(CipherOutcome.Solved);
                    Close();
                }
                else if (puzzle.State == CipherOutcome.Failed)
                {
                    Report(CipherOutcome.Failed);
                }
            }
        }
    }
}
