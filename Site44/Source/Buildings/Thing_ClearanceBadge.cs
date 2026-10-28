using System.Linq;
using Verse;

namespace Site44
{
    // An access badge. A colonist carrying one raises the clearance of the level it was found on; the badge is spent.
    public class Thing_ClearanceBadge : ThingWithComps
    {
        public int tier = 1;
        public int mapId = -1;

        public override string LabelNoCount
        {
            get
            {
                Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
                return base.LabelNoCount + " (" + DirectorVoice.TierName(tier, map) + ")";
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tier, "tier", 1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
        }
    }
}
