using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Site44
{
    public static class FacilityMechs
    {
        // Squad composition grows with "strength" (sector depth, or the alarm size). All kinds are Core mechanoids.
        public static List<string> SquadFor(int strength)
        {
            List<string> squad = new List<string>();
            if (strength < 3)
            {
                squad.Add("Mech_Scyther");
            }
            else if (strength < 6)
            {
                squad.Add("Mech_Lancer");
                squad.Add("Mech_Scyther");
            }
            else if (strength < 9)
            {
                squad.Add("Mech_Pikeman");
                squad.Add("Mech_Lancer");
                squad.Add("Mech_Scyther");
            }
            else
            {
                squad.Add("Mech_CentipedeBlaster");
                squad.Add("Mech_Pikeman");
                squad.Add("Mech_Lancer");
                squad.Add("Mech_Scyther");
            }

            return squad;
        }

        public static Pawn Make(string kindName, Faction faction)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName);
            if (kind == null)
            {
                Log.Warning("[Site-44] unknown mech kind " + kindName);
                return null;
            }

            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
        }
    }
}
