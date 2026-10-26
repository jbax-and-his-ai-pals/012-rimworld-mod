using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Site44
{
    public class JobDriver_HackTerminal : JobDriver
    {
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
    }
}
