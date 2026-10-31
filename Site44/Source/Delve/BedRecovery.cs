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

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval != 0 || !GameComponent_Delve.IsDelve)
            {
                return;
            }

            float hours = Hours(Site44Settings.Current.delveBedRecovery);
            if (hours <= 0f)
            {
                return;
            }

            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                Pulse(pawn, hours);
            }
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
