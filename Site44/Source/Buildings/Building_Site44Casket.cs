using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    // A vanilla ancient cryosleep casket whose survivors, if any, join the party when it is opened.
    public class Building_Site44Casket : Building_AncientCryptosleepCasket
    {
        public override void EjectContents()
        {
            Faction ancients = Faction.OfAncients;
            List<Pawn> survivors = ancients == null
                ? new List<Pawn>()
                : innerContainer.OfType<Pawn>().Where(p => !p.Dead && p.Faction == ancients).ToList();

            base.EjectContents();

            foreach (Pawn pawn in survivors)
            {
                if (!pawn.Spawned)
                {
                    continue;
                }

                pawn.SetFaction(Faction.OfPlayer);
                Find.LetterStack.ReceiveLetter(
                    "Survivor revived",
                    pawn.LabelShort + " was sealed in this casket long before the facility fell silent. They are weak and confused, but alive, and willing to follow you out.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(pawn));
            }
        }
    }
}
