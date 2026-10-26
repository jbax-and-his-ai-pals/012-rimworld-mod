using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    public class WeightedName
    {
        public string name;
        public float weight = 1f;
    }

    public class LootWeight
    {
        public ThingDef thingDef;
        public float weight = 1f;
    }

    // One kind of ruined complex: what the buildings are, what is lying around, and how it is offered as a quest.
    // Defined in XML so new types need no code.
    public class SiteTypeDef : Def
    {
        public SitePartDef sitePart;
        public QuestScriptDef questScript;

        // Buildings
        public string principalArchetype = "Office";
        public List<WeightedName> archetypeWeights = new List<WeightedName>();
        public int minBuildings;
        public int maxBuildings;

        // Overall site layout the planner uses ("Campus", "Scattered", "Compound") and the outdoor prop set.
        public string style = "Campus";
        public string props = "office";

        // Accent colour of the door-front steles; lets each kind of complex read at a glance.
        public Color accent = Color.white;

        // Material of the door-front steles (a stuff def name such as BlocksMarble or Plasteel; empty = steel) and how
        // much loose clutter lies about: scales outdoor props and loose goods indoors.
        public string steleStuff = "Steel";
        public float clutterFactor = 1f;

        // Chances of small/medium/large/XL when the size is random (names S, M, L, XL). Empty = built-in weights.
        public List<WeightedName> sizeWeights = new List<WeightedName>();

        // Quest frequency relative to the other types (scaled by the player's frequency setting).
        public float questWeight = 0.4f;

        // Ordinary and rare loot found in this kind of complex; empty = the built-in tables.
        public List<LootWeight> loot = new List<LootWeight>();
        public List<LootWeight> richLoot = new List<LootWeight>();

        // Specialty rooms: function name -> how many to make. Functions not listed get a reduced default share.
        public List<WeightedName> specialties = new List<WeightedName>();

        // Per-type flavour multipliers.
        public float crateFactor = 1f;
        public float casketFactor = 1f;
        public float strayCorpseFactor = 1f;
        public float secureRoomFactor = 1f;
        public float dustFactor = 1f;

        public PlanProfile ToProfile()
        {
            PlanProfile profile = new PlanProfile
            {
                Name = defName,
                PrincipalArchetype = principalArchetype,
                MinBuildings = minBuildings,
                MaxBuildings = maxBuildings,
                Style = style
            };
            foreach (WeightedName entry in archetypeWeights)
            {
                profile.Weights[entry.name] = entry.weight;
            }

            return profile;
        }
    }

    public static class SiteTypes
    {
        // The type being generated right now (set by the generation step, read by loot and decor code).
        public static SiteTypeDef Current;

        public static List<SiteTypeDef> All => DefDatabase<SiteTypeDef>.AllDefsListForReading;

        public static SiteTypeDef ForSitePart(SitePartDef part)
        {
            return All.FirstOrDefault(t => t.sitePart == part);
        }

        public static SiteTypeDef ForSite(Site site)
        {
            return site == null ? null : site.parts.Select(p => ForSitePart(p.def)).FirstOrDefault(t => t != null);
        }

        // Site maps use the site's type; a scenario start uses the chosen setting (or -site44type=Name).
        public static SiteTypeDef Resolve(Map map)
        {
            SiteTypeDef fromSite = ForSite(map?.Parent as Site);
            if (fromSite != null)
            {
                return fromSite;
            }

            string wanted = CommandLineValue("site44type") ?? Site44Settings.Current.scenarioSiteType;
            SiteTypeDef chosen = All.FirstOrDefault(t => t.defName == wanted)
                                 ?? All.FirstOrDefault(t => t.defName == "Site44_Type_" + wanted)
                                 ?? All.FirstOrDefault(t => t.defName == "Site44_Type_Office")
                                 ?? All.FirstOrDefault();
            return chosen;
        }

        public static string CommandLineValue(string key)
        {
            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                string prefix = "-" + key + "=";
                if (arg.StartsWith(prefix))
                {
                    return arg.Substring(prefix.Length);
                }
            }

            return null;
        }
    }
}
