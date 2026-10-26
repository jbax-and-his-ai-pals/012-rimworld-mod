using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Site44
{
    // Helpers to force a ruin quest and its map, for debug actions and the -site44quest test flag.
    public static class Site44Quests
    {
        // Debug override of the complex size for the next generated map: Small, Medium, Large or XL.
        public static string SizeOverride;
        public static Site FindRuinSite(SiteTypeDef type = null)
        {
            return Find.WorldObjects.Sites.FirstOrDefault(s => type == null
                ? SiteTypes.ForSite(s) != null
                : s.parts.Any(p => p.def == type.sitePart));
        }

        public static Site GenerateQuest(SiteTypeDef type = null)
        {
            type = type ?? SiteTypes.All.FirstOrDefault(t => t.defName == "Site44_Type_Office") ?? SiteTypes.All.First();
            System.Collections.Generic.HashSet<Site> existing = new System.Collections.Generic.HashSet<Site>(
                Find.WorldObjects.Sites.Where(s => s.parts.Any(p => p.def == type.sitePart)));
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(type.questScript, 500f);
            Site site = Find.WorldObjects.Sites.FirstOrDefault(s => s.parts.Any(p => p.def == type.sitePart) && !existing.Contains(s))
                        ?? FindRuinSite(type);
            Log.Message("[Site-44] ruin quest generated (" + type.defName + "): " + (quest == null ? "none" : quest.name) + ", site tile " + (site == null ? "none" : site.Tile.ToString()) + ".");
            return site;
        }

        public static Map OpenMap(Site site)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.PreferredMapSize, null);
            Log.Message("[Site-44] ruin site map generated: " + map.Size + ".");
            return map;
        }
    }
}
