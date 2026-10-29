using System.Linq;
using UnityEngine;
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

        private int KeyIndex
        {
            get
            {
                Map home = Map ?? Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
                MapComponent_Facility facility = home != null ? home.GetComponent<MapComponent_Facility>() : null;
                return facility != null && !sectorId.NullOrEmpty() ? facility.KeyColourIndex(sectorId) : -1;
            }
        }

        public override Color DrawColor
        {
            get
            {
                int index = KeyIndex;
                return index >= 0 ? KeyColours.Colour(index) : base.DrawColor;
            }
        }

        public override string LabelNoCount
        {
            get
            {
                int index = KeyIndex;
                return index >= 0 ? KeyColours.Name(index) + " " + base.LabelNoCount : base.LabelNoCount;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sectorId, "sectorId");
            Scribe_Values.Look(ref mapId, "mapId", -1);
        }
    }
}
