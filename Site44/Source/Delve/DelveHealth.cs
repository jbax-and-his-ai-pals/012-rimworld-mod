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

        // What the extraction gate does for the party on the way down, as the settings ask. Returns a one-line report.
        public static string OnDescent(IEnumerable<Pawn> party, Site44Settings settings)
        {
            List<Pawn> pawns = party.Where(p => p.health != null).ToList();
            List<string> done = new List<string>();
            if (settings.delveDescentCare == 1)
            {
                int dressed = TendParty(pawns);
                if (dressed > 0)
                {
                    done.Add("dresses " + dressed + " wound(s)");
                }
            }
            else if (settings.delveDescentCare == 2)
            {
                int removed = 0;
                HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("WoundInfection");
                foreach (Pawn pawn in pawns)
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => (h is Hediff_Injury && !h.IsPermanent()) || (infection != null && h.def == infection)).ToList())
                    {
                        pawn.health.RemoveHediff(hediff);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    done.Add("clears " + removed + " injur" + (removed == 1 ? "y" : "ies") + " and infection(s)");
                }
            }
            else if (settings.delveDescentCare >= 3)
            {
                int fixedCount = 0;
                foreach (Pawn pawn in pawns)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        string result = HealthUtility.FixWorstHealthCondition(pawn);
                        if (result.NullOrEmpty())
                        {
                            break;
                        }

                        fixedCount++;
                    }
                }

                if (fixedCount > 0)
                {
                    done.Add("treats " + fixedCount + " condition(s)");
                }
            }

            if (settings.delveDescentRest)
            {
                foreach (Pawn pawn in pawns)
                {
                    if (pawn.needs != null)
                    {
                        if (pawn.needs.food != null)
                        {
                            pawn.needs.food.CurLevelPercentage = 1f;
                        }

                        if (pawn.needs.rest != null)
                        {
                            pawn.needs.rest.CurLevelPercentage = 1f;
                        }
                    }
                }

                done.Add("refills food and rest");
            }

            return done.Count == 0 ? null : "The lift's med bay " + string.Join(" and ", done.ToArray()) + " on the way down.";
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

    // The blood pack: replaces lost blood at once.
    public class IngestionOutcomeDoer_BloodPack : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Hediff blood = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (blood != null)
            {
                pawn.health.RemoveHediff(blood);
            }

            Messages.Message(pawn.LabelShort + (blood != null ? ": blood restored." : ": no blood loss to treat."), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // The panacea pill: closes every wound that can still heal, clears infections and restores blood. Scars and missing parts stay.
    public class IngestionOutcomeDoer_Panacea : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("WoundInfection");
            int healed = 0;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => (h is Hediff_Injury && !h.IsPermanent())
                || h.def == HediffDefOf.BloodLoss || (infection != null && h.def == infection)).ToList())
            {
                pawn.health.RemoveHediff(hediff);
                healed++;
            }

            Messages.Message(pawn.LabelShort + ": " + (healed > 0 ? "everything that can heal, heals." : "nothing needed treating."), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // Regrowth nanites: rebuild the most important missing part (the limb or organ whose loss hurts most). With nothing missing, they
    // close the worst old scar instead.
    public class IngestionOutcomeDoer_Regrowth : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Hediff_MissingPart missing = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .OrderByDescending(m => m.Part.def.GetMaxHealth(pawn) * m.Part.coverageAbs)
                .FirstOrDefault();
            if (missing != null)
            {
                BodyPartRecord part = missing.Part;
                pawn.health.RestorePart(part);
                Messages.Message(pawn.LabelShort + ": the nanites rebuild the " + part.Label + ".", pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            Hediff scar = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault(h => h.IsPermanent());
            if (scar != null)
            {
                pawn.health.RemoveHediff(scar);
                Messages.Message(pawn.LabelShort + ": an old scar fades.", pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            Messages.Message(pawn.LabelShort + ": nothing for the nanites to rebuild.", pawn, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // Upgrade nanites: replace one natural part with its bionic version, no surgery needed. The part is the first of arm, leg, eye, heart,
    // spine, stomach and ear that is still natural and whole; a colonist with all of them already bionic gets nothing from it.
    public class IngestionOutcomeDoer_BionicUpgrade : IngestionOutcomeDoer
    {
        private static readonly string[][] Upgrades =
        {
            new[] { "Shoulder", "BionicArm" },
            new[] { "Leg", "BionicLeg" },
            new[] { "Eye", "BionicEye" },
            new[] { "Heart", "BionicHeart" },
            new[] { "Spine", "BionicSpine" },
            new[] { "Stomach", "BionicStomach" },
            new[] { "Ear", "BionicEar" }
        };

        // True when part is the other part or sits somewhere beneath it.
        private static bool Within(BodyPartRecord part, BodyPartRecord other)
        {
            for (BodyPartRecord p = part; p != null; p = p.parent)
            {
                if (p == other)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            List<Hediff_AddedPart> added = pawn.health.hediffSet.hediffs.OfType<Hediff_AddedPart>().ToList();
            foreach (string[] upgrade in Upgrades)
            {
                HediffDef bionic = DefDatabase<HediffDef>.GetNamedSilentFail(upgrade[1]);
                if (bionic == null)
                {
                    continue;
                }

                BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(p => p.def.defName == upgrade[0] && !added.Any(h => Within(p, h.Part) || Within(h.Part, p)));
                if (part == null)
                {
                    continue;
                }

                pawn.health.RestorePart(part);
                pawn.health.AddHediff(bionic, part);
                Messages.Message(pawn.LabelShort + ": the nanites replace the " + part.Label + " with a bionic one.", pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            Messages.Message(pawn.LabelShort + ": nothing left for the nanites to upgrade.", pawn, MessageTypeDefOf.NeutralEvent, false);
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
