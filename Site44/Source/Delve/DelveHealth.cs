using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    // Delve and vanilla health: a small crew with scarce medicine cannot afford the full infection rate, so a Delve game
    // scales how often a fresh wound goes septic. Other games always get the vanilla numbers back.
    public static class DelveHealth
    {
        private static Dictionary<HediffCompProperties_Infecter, float> originals;

        public static float InfectionFactor(Site44Settings settings)
        {
            switch (settings.delveInfection)
            {
                case 0: return 1f;
                case 2: return 0f;
                default: return 0.5f;
            }
        }

        // Called whenever a game starts or loads; scales from the captured vanilla values, so it never compounds.
        public static void Apply(float factor)
        {
            if (originals == null)
            {
                originals = new Dictionary<HediffCompProperties_Infecter, float>();
                foreach (HediffDef def in DefDatabase<HediffDef>.AllDefs)
                {
                    if (def.comps == null)
                    {
                        continue;
                    }

                    foreach (HediffCompProperties_Infecter props in def.comps.OfType<HediffCompProperties_Infecter>())
                    {
                        originals[props] = props.infectionChance;
                    }
                }
            }

            foreach (KeyValuePair<HediffCompProperties_Infecter, float> pair in originals)
            {
                pair.Key.infectionChance = pair.Value * factor;
            }
        }

        // The gate's med bay: every wound that can still be tended is dressed on the way down.
        public static int TendParty(IEnumerable<Pawn> party)
        {
            int tended = 0;
            foreach (Pawn pawn in party)
            {
                if (pawn.health == null)
                {
                    continue;
                }

                foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.TendableNow(true)).ToList())
                {
                    hediff.Tended(0.75f, 0.75f);
                    tended++;
                }
            }

            return tended;
        }
    }

    // The antiseptic injector: clears wound infections outright and dresses what is left.
    public class IngestionOutcomeDoer_Antiseptic : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("WoundInfection");
            int cured = 0;
            if (infection != null)
            {
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.def == infection).ToList())
                {
                    pawn.health.RemoveHediff(hediff);
                    cured++;
                }
            }

            int tended = DelveHealth.TendParty(new[] { pawn });
            Messages.Message(pawn.LabelShort + ": " + (cured > 0 ? cured + " infection(s) cleared" : "no infection found") + (tended > 0 ? ", " + tended + " wound(s) dressed." : "."), pawn,
                cured > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
