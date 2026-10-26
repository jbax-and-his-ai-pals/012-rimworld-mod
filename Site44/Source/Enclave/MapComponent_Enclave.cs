using System.Collections.Generic;
using Site44.Gen;
using Verse;

namespace Site44
{
    // State of one enclave map: the town plan parameters (the plan itself is rebuilt from them), the player's
    // standing with the residents, and which planned projects have been built.
    public class MapComponent_Enclave : MapComponent
    {
        public bool active;
        public int standing;
        public int planSeed;
        public int areaWidth;
        public int areaHeight;
        public int plazaX;
        public int plazaY;
        public int projectCount;
        public IntVec3 origin;
        public List<int> built = new List<int>();

        private TownPlan plan;

        public MapComponent_Enclave(Map map) : base(map)
        {
        }

        public TownPlan Plan
        {
            get
            {
                if (plan == null && active)
                {
                    plan = TownPlan.Generate(areaWidth, areaHeight, plazaX, plazaY, null, planSeed, projectCount);
                }

                return plan;
            }
        }

        public void SetPlan(TownPlan generated)
        {
            plan = generated;
        }

        public bool IsBuilt(int projectId)
        {
            return built.Contains(projectId);
        }

        public void AddStanding(int amount)
        {
            standing = System.Math.Max(0, standing + amount);
            EnclaveFaction.ApplyStanding(standing);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref standing, "standing");
            Scribe_Values.Look(ref planSeed, "planSeed");
            Scribe_Values.Look(ref areaWidth, "areaWidth");
            Scribe_Values.Look(ref areaHeight, "areaHeight");
            Scribe_Values.Look(ref plazaX, "plazaX");
            Scribe_Values.Look(ref plazaY, "plazaY");
            Scribe_Values.Look(ref projectCount, "projectCount");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Collections.Look(ref built, "built", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                built = built ?? new List<int>();
            }
        }
    }
}
