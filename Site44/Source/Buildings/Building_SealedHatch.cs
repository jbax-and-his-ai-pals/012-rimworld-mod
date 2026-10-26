using RimWorld;
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

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string status = sectorId != null && sectorId.StartsWith("vault")
                ? "Secure room. Sealed until a nearby terminal is hacked."
                : "Sealed. Needs a terminal override (clearance " + DirectorVoice.TierName(requiredTier) + ").";
            return text.NullOrEmpty() ? status : text + "\n" + status;
        }
    }
}
