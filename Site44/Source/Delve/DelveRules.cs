using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace Site44
{
    // The intended Delve experience: random events that belong to an outside world are switched off, each group by its
    // own setting. The rules are built once, when a run starts, as ordinary "disable incident" scenario parts, so the
    // game saves them with the run and changing a setting later only affects the next run.
    public static class DelveRules
    {
        private static readonly string[] Raids = { "RaidEnemy" };
        private static readonly string[] CaravanEvents = { "Ambush", "ManhunterAmbush", "CaravanDemand", "CaravanMeeting" };
        private static readonly string[] OtherThreats =
        {
            "Infestation", "DeepDrillInfestation", "ManhunterPack", "AnimalInsanityMass", "AnimalInsanitySingle",
            "DefoliatorShipPartCrash", "PsychicEmanatorShipPartCrash"
        };
        private static readonly string[] OutsideVisitors =
        {
            "TraderCaravanArrival", "OrbitalTraderArrival", "VisitorGroup", "TravelerGroup", "WandererJoin", "WildManWandersIn",
            "RefugeePodCrash", "ResourcePodCrash", "ShipChunkDrop", "RansomDemand", "FarmAnimalsWanderIn", "ThrumboPasses",
            "HerdMigration", "SelfTame", "RaidFriendly", "StrangerInBlackJoin"
        };
        private static readonly string[] Weather =
        {
            "ToxicFallout", "VolcanicWinter", "HeatWave", "ColdSnap", "Flashstorm", "CropBlight", "ShortCircuit", "SolarFlare", "Eclipse"
        };

        private static readonly FieldInfo PartsField = typeof(Scenario).GetField("parts", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IncidentField = typeof(ScenPart_IncidentBase).GetField("incident", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Apply()
        {
            Scenario scenario = Find.Scenario;
            Site44Settings s = Site44Settings.Current;
            if (scenario == null || PartsField == null || IncidentField == null)
            {
                Log.Warning("[Site-44] delve rules: could not reach the scenario's parts; no events were disabled.");
                return;
            }

            List<ScenPart> parts = (List<ScenPart>)PartsField.GetValue(scenario);
            HashSet<IncidentDef> already = new HashSet<IncidentDef>();
            foreach (ScenPart part in parts)
            {
                if (part is ScenPart_DisableIncident existing && existing.Incident != null)
                {
                    already.Add(existing.Incident);
                }
            }

            int added = 0;
            if (s.delveNoRaids) added += Disable(parts, already, Raids);
            if (s.delveNoCaravanEvents) added += Disable(parts, already, CaravanEvents);
            if (s.delveNoOtherThreats) added += Disable(parts, already, OtherThreats);
            if (s.delveNoOutsideVisitors) added += Disable(parts, already, OutsideVisitors);
            if (s.delveNoWeather) added += Disable(parts, already, Weather);
            Log.Message("[Site-44] delve rules: " + added + " incident(s) disabled for this run.");
        }

        private static int Disable(List<ScenPart> parts, HashSet<IncidentDef> already, string[] names)
        {
            int added = 0;
            foreach (string name in names)
            {
                IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
                if (incident == null || already.Contains(incident))
                {
                    continue;
                }

                ScenPart_DisableIncident part = (ScenPart_DisableIncident)ScenarioMaker.MakeScenPart(Site44DelveDefs.DisableIncident);
                IncidentField.SetValue(part, incident);
                parts.Add(part);
                already.Add(incident);
                added++;
            }

            return added;
        }
    }

    [DefOf]
    public static class Site44DelveDefs
    {
        public static ScenPartDef DisableIncident;

        static Site44DelveDefs()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Site44DelveDefs));
        }
    }
}
