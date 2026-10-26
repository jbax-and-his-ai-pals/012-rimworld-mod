using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Site44
{
    public enum SecurityMode
    {
        Hostile,
        Allied,
        Disabled
    }

    // A cluster of turrets and patrol droids governed by one security console.
    public class SecurityGroup : IExposable
    {
        public int id;
        public SecurityMode mode;
        public List<Thing> turrets = new List<Thing>();
        public List<Pawn> droids = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref mode, "mode");
            Scribe_Collections.Look(ref turrets, "turrets", LookMode.Reference);
            Scribe_Collections.Look(ref droids, "droids", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                turrets = turrets ?? new List<Thing>();
                droids = droids ?? new List<Pawn>();
                turrets.RemoveAll(t => t == null);
                droids.RemoveAll(p => p == null);
            }
        }

        public void SetMode(SecurityMode newMode)
        {
            mode = newMode;
            switch (newMode)
            {
                case SecurityMode.Allied:
                    foreach (Thing t in turrets)
                    {
                        if (t != null && !t.Destroyed)
                        {
                            t.SetFaction(Faction.OfPlayer);
                        }
                    }

                    foreach (Pawn p in droids)
                    {
                        if (p != null && !p.Dead)
                        {
                            p.GetLord()?.Notify_PawnLost(p, PawnLostCondition.ForcedToJoinOtherLord);
                            p.SetFaction(Faction.OfPlayer);
                        }
                    }

                    break;
                case SecurityMode.Disabled:
                    foreach (Thing t in turrets)
                    {
                        if (t != null && !t.Destroyed && t.Spawned)
                        {
                            FleckMaker.ThrowSmoke(t.DrawPos, t.Map, 1.2f);
                            t.Destroy(DestroyMode.KillFinalize);
                        }
                    }

                    foreach (Pawn p in droids)
                    {
                        if (p != null && !p.Dead && p.Spawned)
                        {
                            p.Kill(null);
                        }
                    }

                    break;
            }
        }
    }
}
