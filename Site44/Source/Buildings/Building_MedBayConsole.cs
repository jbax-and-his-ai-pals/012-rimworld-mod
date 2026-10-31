using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Site44
{
    // A med-bay console. Someone lying in a bed nearby is treated by whoever works the console: every wound that can still heal is
    // closed, infections are cleared and blood is restored. It does not need the patient's own hands, so a downed colonist can be
    // healed while the rest of the party carries on. Scars, missing parts and illnesses stay.
    public class Building_MedBayConsole : Building
    {
        public const float Range = 8f;

        // The occupied bed nearest the console, in range and on the same side of the walls.
        public Pawn Patient()
        {
            if (Map == null)
            {
                return null;
            }

            return Map.listerThings.ThingsInGroup(ThingRequestGroup.Bed)
                .OfType<Building_Bed>()
                .Where(b => b.Spawned && b.Position.DistanceTo(Position) <= Range && b.GetRoom() == this.GetRoom())
                .OrderBy(b => b.Position.DistanceTo(Position))
                .SelectMany(b => b.CurOccupants)
                .FirstOrDefault(p => p.health != null && NeedsTreatment(p));
        }

        public static bool NeedsTreatment(Pawn p)
        {
            return p.health.hediffSet.hediffs.Any(IsTreatable);
        }

        public static bool IsTreatable(Hediff h)
        {
            return (h is Hediff_Injury && !h.IsPermanent()) || h.def == HediffDefOf.BloodLoss || h.def.defName == "WoundInfection";
        }

        public static int Treat(Pawn patient)
        {
            int count = 0;
            foreach (Hediff h in patient.health.hediffSet.hediffs.Where(IsTreatable).ToList())
            {
                patient.health.RemoveHediff(h);
                count++;
            }

            return count;
        }

        public int TreatTicks(Pawn worker)
        {
            int skill = worker.skills != null ? worker.skills.GetSkill(SkillDefOf.Medicine).Level : 0;
            return Mathf.Max(300, Mathf.RoundToInt(1500f / (0.5f + 0.1f * skill)));
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            Pawn patient = Patient();
            if (selPawn.skills != null && selPawn.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled)
            {
                yield return new FloatMenuOption("Run treatment (incapable of medicine)", null);
            }
            else if (patient == null)
            {
                yield return new FloatMenuOption("Run treatment (nobody in a bed nearby needs it)", null);
            }
            else if (patient == selPawn)
            {
                yield return new FloatMenuOption("Run treatment (someone else must work the console)", null);
            }
            else if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("Run treatment (cannot reach)", null);
            }
            else
            {
                yield return new FloatMenuOption("Run treatment on " + patient.LabelShort, () =>
                {
                    Job job = JobMaker.MakeJob(Site44DefOf.Site44_UseMedBay, this, patient);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }

        // With several colonists selected: offer it once, for the one with the best medicine skill who can reach the console.
        public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns)
        {
            List<Pawn> pawns = selPawns.ToList();
            Pawn best = pawns
                .Where(p => p.skills != null && !p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled && p.CanReach(this, PathEndMode.Touch, Danger.Deadly))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level)
                .FirstOrDefault() ?? pawns.FirstOrDefault();
            return best == null ? Enumerable.Empty<FloatMenuOption>() : GetFloatMenuOptions(best);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            Pawn patient = Patient();
            string status = patient != null ? "Ready: " + patient.LabelShort + " is in a bed nearby." : "Idle: needs someone hurt in a bed nearby.";
            return text.NullOrEmpty() ? status : text + "\n" + status;
        }
    }

    public class JobDriver_UseMedBay : JobDriver
    {
        private Building_MedBayConsole Console => (Building_MedBayConsole)job.targetA.Thing;
        private Pawn Patient => job.targetB.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Patient == null || Patient.Dead || !Patient.InBed() || !Building_MedBayConsole.NeedsTreatment(Patient));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil work = Toils_General.Wait(Console.TreatTicks(pawn)).WithProgressBarToilDelay(TargetIndex.A);
            work.tickIntervalAction = delegate(int delta)
            {
                pawn.skills?.Learn(SkillDefOf.Medicine, 0.1f * delta);
            };
            yield return work;
            Toil finish = ToilMaker.MakeToil("Site44_MedBayFinish");
            finish.initAction = delegate
            {
                int treated = Building_MedBayConsole.Treat(Patient);
                Messages.Message(Patient.LabelShort + ": " + (treated > 0 ? treated + " condition(s) treated." : "nothing needed treating.") , Patient, MessageTypeDefOf.PositiveEvent, false);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
