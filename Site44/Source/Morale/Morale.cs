using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Site44
{
    // Steady lift for colonists inside a ruined complex; the size comes from the mod setting.
    public class ThoughtWorker_MissionFocus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            int level = Site44Settings.Current.missionFocusLevel;
            if (level <= 0 || p.Map == null || !p.IsColonist)
            {
                return ThoughtState.Inactive;
            }

            MapComponent_Facility facility = p.Map.GetComponent<MapComponent_Facility>();
            if (facility == null || !facility.IsFacilityMap)
            {
                return ThoughtState.Inactive;
            }

            return ThoughtState.ActiveAtStage(level - 1);
        }
    }

    // Delvers have seen bodies before: plain corpses stop bothering colonists inside a complex (rot still does).
    public class GameComponent_Site44Morale : GameComponent
    {
        private const int CheckInterval = 150;

        public GameComponent_Site44Morale(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckInterval != 0 || !Site44Settings.Current.hardenedToBodies)
            {
                return;
            }

            foreach (Map map in Find.Maps)
            {
                MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
                if (facility == null || !facility.IsFacilityMap)
                {
                    continue;
                }

                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    pawn.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(Site44MoraleDefOf.ObservedLayingCorpse);
                }
            }
        }

        // The first expedition starts rested, fed, in good spirits and carrying a little kit.
        public static void GiveStartingKit(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                return;
            }

            if (pawn.needs != null)
            {
                if (pawn.needs.food != null) pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
                if (pawn.needs.rest != null) pawn.needs.rest.CurLevel = pawn.needs.rest.MaxLevel;
                if (pawn.needs.joy != null) pawn.needs.joy.CurLevel = pawn.needs.joy.MaxLevel;
                pawn.needs.mood?.thoughts?.memories?.TryGainMemory(Site44MoraleDefOf.Site44_ReadyToDelve);
            }

            if (!Site44Settings.Current.startingKit || pawn.inventory == null)
            {
                return;
            }

            if (pawn.inventory.innerContainer.Contains(Site44MoraleDefOf.Site44_FocusTab))
            {
                return;
            }

            AddToInventory(pawn, ThingDef.Named("MealSurvivalPack"), 6);
            AddToInventory(pawn, Site44MoraleDefOf.Site44_FocusTab, 3);
            AddToInventory(pawn, ThingDef.Named("MedicineIndustrial"), 2);
            WearIfFree(pawn, Site44MoraleDefOf.Site44_DelverCap);
            WearIfFree(pawn, Site44MoraleDefOf.Site44_DelverVest);
        }

        private static void WearIfFree(Pawn pawn, ThingDef def)
        {
            if (def == null || pawn.apparel == null || !ApparelUtility.HasPartsToWear(pawn, def))
            {
                return;
            }

            foreach (Apparel worn in pawn.apparel.WornApparel)
            {
                if (worn.def == def || !ApparelUtility.CanWearTogether(def, worn.def, pawn.RaceProps.body))
                {
                    return;
                }
            }

            Apparel item = (Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            pawn.apparel.Wear(item, false);
        }

        private static void AddToInventory(Pawn pawn, ThingDef def, int count)
        {
            if (def == null)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = count;
            pawn.inventory.innerContainer.TryAdd(thing, count);
        }
    }

    [DefOf]
    public static class Site44MoraleDefOf
    {
        public static ThoughtDef Site44_ReadyToDelve;
        public static ThoughtDef ObservedLayingCorpse;
        public static ThingDef Site44_FocusTab;
        public static ThingDef Site44_DelverCap;
        public static ThingDef Site44_DelverVest;

        static Site44MoraleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Site44MoraleDefOf));
        }
    }
}
