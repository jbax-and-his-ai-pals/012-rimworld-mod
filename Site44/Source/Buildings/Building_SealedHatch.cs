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

        // A hatch opened by a keycard has a colour, shared with its card. Restricted wings and secure rooms have none.
        private bool Keyed => sectorId != null && !sectorId.StartsWith("vault") && requiredTier <= 0;

        private int KeyIndex
        {
            get
            {
                MapComponent_Facility facility = Map != null ? Map.GetComponent<MapComponent_Facility>() : null;
                return facility != null ? facility.KeyColourIndex(sectorId) : -1;
            }
        }

        public override Color DrawColor
        {
            get
            {
                int index = Keyed ? KeyIndex : -1;
                return index >= 0 ? KeyColours.Colour(index) : base.DrawColor;
            }
        }

        public override string LabelNoCount
        {
            get
            {
                int index = Keyed ? KeyIndex : -1;
                return index >= 0 ? base.LabelNoCount + " (" + KeyColours.Name(index) + ")" : base.LabelNoCount;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Keyed)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Swipe keycard",
                    defaultDesc = "Open this hatch with its " + KeyColours.Name(Mathf.Max(0, KeyIndex)) + " keycard, carried by a colonist standing nearby. The card is spent.",
                    icon = TexCommand.ForbidOff,
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

            Messages.Message("No colonist near the hatch is carrying its " + KeyColours.Name(Mathf.Max(0, KeyIndex)) + " keycard.", this, MessageTypeDefOf.RejectInput, false);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string status = sectorId != null && sectorId.StartsWith("vault")
                ? "Secure room. Sealed until a nearby terminal is hacked."
                : requiredTier > 0 ? "Restricted wing. Needs clearance " + DirectorVoice.TierName(requiredTier, Map) + " at its terminal." : "Sealed. Needs a terminal override or the " + KeyColours.Name(Mathf.Max(0, KeyIndex)) + " keycard.";
            return text.NullOrEmpty() ? status : text + "\n" + status;
        }
    }
}
