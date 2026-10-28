using Verse;

namespace Site44
{
    // A keycard opens one sealed hatch, on the level it was found. It is spent when swiped.
    public class Thing_Keycard : ThingWithComps
    {
        public string sectorId;
        public int mapId = -1;

        public bool Opens(Building_SealedHatch hatch)
        {
            return hatch.Map != null && mapId == hatch.Map.uniqueID && sectorId == hatch.sectorId;
        }

        public override string LabelNoCount
        {
            get { return base.LabelNoCount + (sectorId.NullOrEmpty() ? "" : " (sector " + sectorId + ")"); }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sectorId, "sectorId");
            Scribe_Values.Look(ref mapId, "mapId", -1);
        }
    }
}
