using System.Collections.Generic;
using RimWorld;
using Site44.Gen;
using Verse;
using Verse.AI;

namespace Site44
{
    public class JobDriver_HackTerminal : JobDriver
    {
        private CipherOutcome puzzleOutcome = CipherOutcome.Continue;
        private bool puzzleOpened;
        private Building_FacilityTerminal Terminal => (Building_FacilityTerminal)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Terminal.Hacked);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            if (Terminal.UsesCipherLock)
            {
                yield return CipherToil();
                yield break;
            }

            Toil hack = Toils_General.Wait(Terminal.HackTicks(pawn))
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);
            hack.tickIntervalAction = delegate(int delta)
            {
                pawn.skills?.Learn(Terminal.PrimarySkill, 0.1f * delta);
            };
            yield return hack;

            Toil finish = ToilMaker.MakeToil("Site44_HackFinish");
            finish.initAction = delegate
            {
                Terminal.Hack(pawn);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private Toil CipherToil()
        {
            Toil toil = ToilMaker.MakeToil("Site44_CipherLock");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.initAction = delegate
            {
                int skill = pawn.skills != null ? pawn.skills.GetSkill(Terminal.PrimarySkill).Level : 0;
                int seed = Terminal.thingIDNumber * 7919 + Find.TickManager.TicksGame / 600 + pawn.thingIDNumber;
                CipherPuzzle puzzle = CipherPuzzle.Create(seed, skill, Terminal.PuzzleTier, Site44Tuning.PuzzleDifficulty);
                puzzleOpened = true;
                Find.WindowStack.Add(new Dialog_CipherLock(puzzle, Terminal.LabelCap, outcome => puzzleOutcome = outcome));
            };
            toil.tickAction = delegate
            {
                pawn.skills?.Learn(Terminal.PrimarySkill, 0.05f);
                if (puzzleOutcome == CipherOutcome.Solved)
                {
                    Terminal.Hack(pawn);
                    ReadyForNextToil();
                }
                else if (puzzleOutcome == CipherOutcome.Failed)
                {
                    Terminal.LockOut(pawn);
                    EndJobWith(JobCondition.Incompletable);
                }
                else if (puzzleOpened && !Find.WindowStack.IsOpen<Dialog_CipherLock>())
                {
                    EndJobWith(JobCondition.InterruptForced);
                }
            };
            return toil;
        }
    }
}
