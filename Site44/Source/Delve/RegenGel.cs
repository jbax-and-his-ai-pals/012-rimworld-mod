using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    public class HediffCompProperties_RegenGel : HediffCompProperties
    {
        public float healPerHour = 2.5f;

        public HediffCompProperties_RegenGel()
        {
            compClass = typeof(HediffComp_RegenGel);
        }
    }

    // Regeneration gel: for a few hours the worst tended or untended wound keeps closing, whatever the colonist is doing.
    public class HediffComp_RegenGel : HediffComp
    {
        private HediffCompProperties_RegenGel Props => (HediffCompProperties_RegenGel)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            Hediff_Injury worst = Pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(h => !h.IsPermanent())
                .OrderByDescending(h => h.Severity)
                .FirstOrDefault();
            if (worst != null)
            {
                worst.Heal(Props.healPerHour / 2500f * delta);
            }
        }
    }

    // Field surgery kit: stitches the worst wound shut for good and dresses the rest.
    public class IngestionOutcomeDoer_FieldSurgery : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Hediff_Injury worst = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(h => !h.IsPermanent())
                .OrderByDescending(h => h.Severity)
                .FirstOrDefault();
            string place = null;
            if (worst != null)
            {
                place = worst.Part != null ? worst.Part.Label : "body";
                pawn.health.RemoveHediff(worst);
            }

            int dressed = DelveHealth.TendParty(new[] { pawn });
            string text = worst != null ? "the worst wound (" + place + ") is closed" : "no wound needed surgery";
            if (dressed > 0)
            {
                text += ", " + dressed + " other(s) dressed";
            }

            Messages.Message(pawn.LabelShort + ": " + text + ".", pawn, worst != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // Regeneration gel: starts the gel's slow healing.
    public class IngestionOutcomeDoer_RegenGel : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            HediffDef gel = DefDatabase<HediffDef>.GetNamedSilentFail("Site44_RegenGel");
            if (gel == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(gel);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            pawn.health.AddHediff(gel);
            Messages.Message(pawn.LabelShort + ": the gel starts to close the worst wounds.", pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
