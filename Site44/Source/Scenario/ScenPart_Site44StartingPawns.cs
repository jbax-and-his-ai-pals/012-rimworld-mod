using RimWorld;

namespace Site44
{
    // The character page, with the party size taken from the mod settings (1 to 5). The scenario files still name a
    // default of three; the setting wins whenever a new game starts.
    public class ScenPart_Site44StartingPawns : ScenPart_ConfigPage_ConfigureStartingPawns
    {
        protected override int TotalPawnCount
        {
            get
            {
                pawnCount = UnityEngine.Mathf.Clamp(Site44Settings.Current.startingColonists, 1, 5);
                pawnChoiceCount = UnityEngine.Mathf.Max(pawnChoiceCount, pawnCount + 4);
                return pawnCount;
            }
        }
    }
}
