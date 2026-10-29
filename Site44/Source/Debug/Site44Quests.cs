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

#if ENCLAVE
        public static Site GenerateEnclaveQuest()
        {
            System.Collections.Generic.HashSet<Site> existing = new System.Collections.Generic.HashSet<Site>(
                Find.WorldObjects.Sites.Where(s => s.parts.Any(p => p.def.defName == "Site44_EnclaveSitePart")));
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(Site44DefOf.OpportunitySite_Site44Enclave, 500f);
            Site site = Find.WorldObjects.Sites.FirstOrDefault(s => s.parts.Any(p => p.def.defName == "Site44_EnclaveSitePart") && !existing.Contains(s));
            Log.Message("[Site-44] enclave quest generated: " + (quest == null ? "none" : quest.name) + ", site tile " + (site == null ? "none" : site.Tile.ToString()) + ".");
            return site;
        }
#endif

        // Debug: move the whole colony to a freshly generated complex, as if it were the starting tile of an excursion.
        public static void WarpColoniesTo(Map target)
        {
            MapComponent_Facility facility = target.GetComponent<MapComponent_Facility>();
            IntVec3 spot = facility != null && facility.startCell.IsValid ? facility.startCell : target.Center;
            int moved = 0;
            foreach (Pawn pawn in Find.Maps.Where(m => m != target).SelectMany(m => m.mapPawns.FreeColonistsSpawned).ToList())
            {
                pawn.jobs?.StopAll();
                pawn.DeSpawn();
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(spot, target, 6, c => c.Standable(target));
                GenSpawn.Spawn(pawn, cell, target);
                moved++;
            }

            Current.Game.CurrentMap = target;
            CameraJumper.TryJump(spot, target);
            Log.Message("[Site-44] warped " + moved + " colonist(s) to " + spot + ".");
        }

        public static Map OpenMap(Site site)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.PreferredMapSize, null);
            Log.Message("[Site-44] ruin site map generated: " + map.Size + ".");
            return map;
        }
    }
}
