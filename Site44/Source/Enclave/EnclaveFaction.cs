using RimWorld;
using Verse;

namespace Site44
{
    // The residents of a delvers' enclave. Neutral to start, friendly once the player has earned standing.
    public static class EnclaveFaction
    {
        public const int AllyStanding = 150;

        public static Faction GetOrCreate()
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(Site44DefOf.Site44_Enclave);
            if (faction != null)
            {
                return faction;
            }

            faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(Site44DefOf.Site44_Enclave, default(IdeoGenerationParms), true));
            Find.FactionManager.Add(faction);
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other != faction)
                {
                    faction.SetRelationDirect(other, FactionRelationKind.Neutral, false);
                }
            }

            return faction;
        }

        public static void ApplyStanding(int standing)
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(Site44DefOf.Site44_Enclave);
            if (faction == null || faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            faction.SetRelationDirect(Faction.OfPlayer, standing >= AllyStanding ? FactionRelationKind.Ally : FactionRelationKind.Neutral, false);
        }
    }
}
