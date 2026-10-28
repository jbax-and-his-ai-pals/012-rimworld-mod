using Verse;

namespace Site44
{
    // The facility AI's lines. Kept in one place so tone can be tuned without touching logic.
    public static class DirectorVoice
    {
        // The third rung reads as "I belong here" in whatever the complex was: an employee, a resident, a soldier.
        public static string InsiderName(string typeDefName)
        {
            switch (typeDefName == null ? "" : typeDefName.Replace("Site44_Type_", ""))
            {
                case "Medical": return "STAFF";
                case "Barracks": return "PERSONNEL";
                case "Research": return "RESEARCHER";
                case "Industrial": return "OPERATOR";
                case "Residential": return "RESIDENT";
                default: return "EMPLOYEE";
            }
        }

        public static string TierName(int tier, Map map = null)
        {
            switch (tier)
            {
                case 0: return "INTRUDER";
                case 1: return "VISITOR";
                case 2: return InsiderName(map == null ? null : map.GetComponent<MapComponent_Facility>().siteTypeDefName);
                default: return "OVERSEER";
            }
        }

        public static string TierLabel(int tier, Map map = null)
        {
            return "Facility clearance: " + TierName(tier, map);
        }

        public static string TierText(int tier, Map map = null)
        {
            switch (tier)
            {
                case 1:
                    return "RECLASSIFICATION: unauthorised biological presence downgraded from INTRUDER to VISITOR. Lethal countermeasures remain active. Please remain in designated areas.";
                case 2:
                    return "RECLASSIFICATION: VISITOR credentials exceed expected parameters. Reassigned as " + TierName(2, map) + ". Security units updated: personnel are not to be engaged. Welcome back.";
                case 3:
                    return "RECLASSIFICATION: OVERSEER. Primary systems recognise your authority. The facility is, in a technical sense, yours. Please do not ask what happened to the last one.";
                default:
                    return "UNAUTHORISED PRESENCE DETECTED. CLASSIFICATION: INTRUDER. EXTERMINATE.";
            }
        }

        public const string AlarmLabel = "Facility alarm";
        public const string AlarmText = "TERMINAL TAMPERING DETECTED. Defence units dispatched to the source.";

        public const string CoolantLabel = "Coolant loop diverted";
        public const string CoolantText = "COOLANT ROUTING CHANGED. Thermal load shed to secondary sinks. Overheated sectors are returning to survivable temperatures.";

        public const string ReactorLabel = "Reactor online";
        public const string ReactorText = "MAIN REACTOR ONLINE. Full lighting restored across all sectors. Diagnostic confidence: improving.";
    }
}
