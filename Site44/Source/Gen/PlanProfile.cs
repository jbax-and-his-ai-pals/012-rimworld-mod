using System.Collections.Generic;

namespace Site44.Gen
{
    // Describes a kind of complex to the building planner: which archetype is the principal building and
    // how the other buildings in the campus are chosen. Pure data; no RimWorld references.
    public sealed class PlanProfile
    {
        public string Name = "OfficeCampus";

        // Archetype of Buildings[0], which holds the main entrance and the start yard.
        public string PrincipalArchetype = "Office";

        // Relative weights for the archetypes of the other buildings. Empty = the planner's built-in mix.
        public Dictionary<string, float> Weights = new Dictionary<string, float>();

        // Building count bounds; 0 means "use the planner default for the site size".
        public int MinBuildings;
        public int MaxBuildings;

        // Overall site layout: "Campus" (jittered grid of similar buildings, the default), "Scattered" (buildings of widely
        // varying sizes at random positions around an open central yard, few or no link corridors) or "Compound"
        // (a perimeter wall with guarded gates around a tighter cluster of buildings).
        public string Style = "Campus";
    }
}
