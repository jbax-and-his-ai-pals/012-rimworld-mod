using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    // The settings this game was started with. They are saved with the game, so a change to the defaults in the main menu
    // never alters a game in progress; the settings window edits this copy while the game is loaded.
    public class GameComponent_Site44Run : GameComponent
    {
        public Site44Settings run;

        public GameComponent_Site44Run(Game game)
        {
        }

        public void Snapshot()
        {
            if (run == null)
            {
                run = Site44Settings.Global.Clone();
            }
        }

        public override void FinalizeInit()
        {
            // An older save has no copy yet: freeze the defaults at the first load.
            if (run == null && Find.Scenario != null && Find.Scenario.AllParts.Any(p => p is ScenPart_Site44Facility))
            {
                Snapshot();
            }
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref run, "run");
        }
    }
}
