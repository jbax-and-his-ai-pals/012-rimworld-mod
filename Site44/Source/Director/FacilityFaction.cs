using RimWorld;
using Verse;

namespace Site44
{
    // The facility's security units belong to a hidden faction whose relation to the player
    // follows the clearance tier (hostile until the insider tier, then allied).
    public static class FacilityFaction
    {
        public const int AllyTier = 2;

        public static Faction GetOrCreate()
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(Site44DefOf.Site44_Facility);
            if (faction != null)
            {
                return faction;
            }

            faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(Site44DefOf.Site44_Facility, default(IdeoGenerationParms), true));
            Find.FactionManager.Add(faction);

            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other == faction)
                {
                    continue;
                }

                FactionRelationKind kind = other.IsPlayer || other.HostileTo(Faction.OfPlayer)
                    ? FactionRelationKind.Hostile
                    : FactionRelationKind.Neutral;
                faction.SetRelationDirect(other, kind, false);
            }

            return faction;
        }

        public static void ApplyTier(int tier)
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(Site44DefOf.Site44_Facility);
            if (faction == null)
            {
                return;
            }

            FactionRelationKind kind = tier >= AllyTier ? FactionRelationKind.Ally : FactionRelationKind.Hostile;
            faction.SetRelationDirect(Faction.OfPlayer, kind, false);
        }
    }
}
