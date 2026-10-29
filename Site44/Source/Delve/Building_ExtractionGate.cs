using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Site44
{
    // The way down. Locked until the extraction terminal has been hacked.
    public class Building_ExtractionGate : Building
    {
        public bool unlocked;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref unlocked, "unlocked");
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string status = unlocked
                ? "Open. The party goes down carrying only what it holds."
                : "Sealed. The extraction terminal controls it.";
            return text.NullOrEmpty() ? status : text + "\n" + status;
        }

        public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns)
        {
            List<Pawn> pawns = selPawns.ToList();
            Pawn best = pawns.FirstOrDefault(p => p.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly)) ?? pawns.FirstOrDefault();
            return best == null ? Enumerable.Empty<FloatMenuOption>() : GetFloatMenuOptions(best);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            if (!GameComponent_Delve.IsDelve)
            {
                yield break;
            }

            if (!unlocked)
            {
                yield return new FloatMenuOption("Descend (sealed: hack the extraction terminal first)", null);
            }
            else if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("Descend (cannot reach)", null);
            }
            else
            {
                yield return new FloatMenuOption("Descend with the party", () =>
                {
                    Job job = JobMaker.MakeJob(Site44DefOf.Site44_UseGate, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }

    public class JobDriver_UseGate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil use = ToilMaker.MakeToil("Site44_UseGate");
            use.initAction = delegate
            {
                Building_ExtractionGate gate = (Building_ExtractionGate)job.targetA.Thing;
                GameComponent_Delve.Instance?.Descend(gate.Map, gate.Position);
            };
            use.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return use;
        }
    }

    // Marks a scenario as a delve run: starts the depth counter once the game begins.
    public class ScenPart_Site44Delve : ScenPart
    {
        // Testing aid: tiny complexes with the extraction gate near the start and already unlocked.
        public bool quickTest;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref quickTest, "quickTest");
        }

        public override string Summary(Scenario scen)
        {
            return quickTest
                ? "Delve (quick test): tiny complexes, the extraction gate is next to the start and already open."
                : "Delve: each complex is a level; reach the extraction gate to go deeper. Losing the whole party ends the run.";
        }

        public override void PostGameStart()
        {
            GameComponent_Delve.StartRun();
        }
    }
}
