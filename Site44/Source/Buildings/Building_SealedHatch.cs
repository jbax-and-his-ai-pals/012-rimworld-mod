using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    public class Building_SealedHatch : Building
    {
        public string sectorId;
        public int requiredTier;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sectorId, "sectorId");
            Scribe_Values.Look(ref requiredTier, "requiredTier");
        }

        private const float SwipeRange = 6f;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (sectorId != null && !sectorId.StartsWith("vault"))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Swipe keycard",
                    defaultDesc = "Open this hatch with a keycard carried by a colonist standing nearby. The card is spent.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Open", false),
                    action = SwipeKeycard
                };
            }
        }

        private void SwipeKeycard()
        {
            foreach (Pawn pawn in Map.mapPawns.FreeColonistsSpawned.Where(p => p.Position.DistanceTo(Position) <= SwipeRange))
            {
                Thing_Keycard card = pawn.inventory.innerContainer.OfType<Thing_Keycard>().FirstOrDefault(k => k.Opens(this));
                if (card == null)
                {
                    continue;
                }

                string who = pawn.LabelShort;
                card.Destroy();
                Messages.Message(who + " swipes a keycard. The hatch unseals.", this, MessageTypeDefOf.PositiveEvent, false);
                Map.GetComponent<MapComponent_Facility>().UnsealSector(sectorId);
                return;
            }

            Messages.Message("No colonist near the hatch is carrying a keycard for it.", this, MessageTypeDefOf.RejectInput, false);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string status = sectorId != null && sectorId.StartsWith("vault")
                ? "Secure room. Sealed until a nearby terminal is hacked."
                : "Sealed. Needs a terminal override (clearance " + DirectorVoice.TierName(requiredTier, Map) + ") or a keycard.";
            return text.NullOrEmpty() ? status : text + "\n" + status;
        }
    }
}
