using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Site44
{
    // A colonist lying in a bed closes their tended wounds over a fixed stretch of game time instead of at a fixed rate, so a badly
    // hurt colonist is back on their feet about as quickly as a lightly hurt one. Wounds that are not tended are left to heal at
    // the normal pace, which makes "tend first, then rest" the way to recover.
    public class GameComponent_BedRecovery : GameComponent
    {
        private const int Interval = 60;

        public GameComponent_BedRecovery(Game game)
        {
        }

        // Game hours to close a wound in an ordinary bed, by setting (0 is the base game).
        public static float Hours(int level)
        {
            switch (level)
            {
                case 1: return 6f;
                case 2: return 3f;
                case 3: return 1.5f;
                default: return 0f;
            }
        }

        // Game hours for a full sleep, by setting (0 is the base game).
        public static float SleepHours(int level)
        {
            switch (level)
            {
                case 1: return 4f;
                case 2: return 2f;
                case 3: return 1f;
                default: return 0f;
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval != 0 || !GameComponent_Delve.IsDelve)
            {
                return;
            }

            float hours = Hours(Site44Settings.Current.delveBedRecovery);
            float sleep = SleepHours(Site44Settings.Current.delveSleepLength);
            if (hours <= 0f && sleep <= 0f)
            {
                return;
            }

            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                if (sleep > 0f)
                {
                    TopUpRest(pawn, sleep);
                }

                if (hours > 0f)
                {
                    Pulse(pawn, hours);
                }
            }
        }

        // A colonist who is asleep gains rest faster than the base game gives, so a full sleep takes the chosen time.
        private static void TopUpRest(Pawn pawn, float sleepHours)
        {
            Need_Rest rest = pawn.needs != null ? pawn.needs.rest : null;
            if (rest == null || !rest.Resting)
            {
                return;
            }

            Building_Bed bed = pawn.CurrentBed();
            float quality = Mathf.Clamp(bed != null ? bed.GetStatValue(StatDefOf.BedRestEffectiveness) : 0.8f, 0.5f, 1.5f);
            rest.CurLevel = Mathf.Min(1f, rest.CurLevel + BedHealing.SleepTopUp(sleepHours, quality, Interval));
        }

        private static void Pulse(Pawn pawn, float hours)
        {
            if (pawn.health == null || !pawn.InBed())
            {
                return;
            }

            Building_Bed bed = pawn.CurrentBed();
            float quality = Mathf.Clamp(bed != null ? bed.GetStatValue(StatDefOf.BedRestEffectiveness) : 1f, 0.5f, 1.5f);
            float window = hours * 2500f / quality;

            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => !h.IsPermanent() && h.IsTended()).ToList())
            {
                injury.Heal(BedHealing.Step(injury.Severity, window, Interval));
            }

            Hediff blood = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (blood != null && pawn.health.hediffSet.BleedRateTotal < 0.01f)
            {
                blood.Severity -= Interval / window;
            }
        }
    }
}
