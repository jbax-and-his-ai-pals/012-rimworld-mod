using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    // The run at a glance: what the level still asks of the party, where the run stands, and how the party is. Opens from the
    // bottom bar during a Delve run, and the same figures appear on the end screen.
    public class MainButtonWorker_Run : MainButtonWorker_ToggleTab
    {
        public override bool Visible => GameComponent_Delve.IsDelve && base.Visible;
    }

    public class MainTabWindow_Run : MainTabWindow
    {
        public override Vector2 InitialSize => new Vector2(520f, 560f);

        private Vector2 scroll;

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Delve run = GameComponent_Delve.Instance;
            Map map = Find.CurrentMap;
            List<KeyValuePair<string, string>> goals = Goals(map);
            List<KeyValuePair<string, string>> stats = Stats(run, map);
            List<KeyValuePair<string, string>> party = Party(map);

            float y = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 34f), "The run");
            Text.Font = GameFont.Small;
            y += 38f;

            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y);
            float height = 40f + 3 * 34f + (goals.Count + stats.Count + party.Count) * 26f;
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float vy = 0f;
            vy = Section(viewRect, vy, "This level", goals);
            vy = Section(viewRect, vy, "The run so far", stats);
            vy = Section(viewRect, vy, "The party", party);
            Widgets.EndScrollView();
        }

        private static float Section(Rect view, float y, string title, List<KeyValuePair<string, string>> lines)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, y, view.width, 28f), title.ToUpper());
            Widgets.DrawLineHorizontal(0f, y + 26f, view.width);
            y += 32f;
            foreach (KeyValuePair<string, string> line in lines)
            {
                Widgets.Label(new Rect(0f, y, view.width * 0.5f, 26f), line.Key);
                Widgets.Label(new Rect(view.width * 0.5f, y, view.width * 0.5f, 26f), line.Value);
                y += 26f;
            }

            return y + 8f;
        }

        private static List<KeyValuePair<string, string>> Goals(Map map)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (map == null)
            {
                return list;
            }

            Building_FacilityTerminal terminal = map.listerThings.AllThings.OfType<Building_FacilityTerminal>().FirstOrDefault(t => t.kind == TerminalKind.Extraction);
            Building_ExtractionGate gate = map.listerThings.ThingsOfDef(Site44DefOf.Site44_ExtractionGate).OfType<Building_ExtractionGate>().FirstOrDefault();
            list.Add(new KeyValuePair<string, string>("Extraction terminal", terminal == null ? "none on this level" : terminal.used ? "hacked" : "not yet hacked"));
            list.Add(new KeyValuePair<string, string>("Extraction gate", gate == null ? "none on this level" : gate.unlocked ? "open" : "sealed"));
            MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
            if (facility != null)
            {
                list.Add(new KeyValuePair<string, string>("Clearance", DirectorVoice.TierName(facility.Tier, map)));
                string hazards = facility.HazardSummary;
                if (hazards != null)
                {
                    list.Add(new KeyValuePair<string, string>("Hazards", hazards));
                }
            }

            return list;
        }

        private static List<KeyValuePair<string, string>> Stats(GameComponent_Delve run, Map map)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (run == null)
            {
                return list;
            }

            int tier = DelveTiers.TierOf(run.depth);
            Site44Settings settings = Site44Settings.Current;
            float pressure = DelveTiers.Pressure(run.depth, settings.tierStrengthX);
            int elapsed = Find.TickManager.TicksGame - run.startTick;
            list.Add(new KeyValuePair<string, string>("Depth", run.depth.ToString()));
            list.Add(new KeyValuePair<string, string>("Tier", DelveTiers.TierName(tier) + (tier > 0 ? " (danger x" + pressure.ToString("0.00") + ")" : "")));
            if (DelveTiers.IsGuardianDepth(run.depth))
            {
                list.Add(new KeyValuePair<string, string>("This level", "ends the tier: a guardian holds it"));
            }

            list.Add(new KeyValuePair<string, string>("Best depth", Site44Settings.Global.bestDelveDepth.ToString()));
            list.Add(new KeyValuePair<string, string>("Time in the ruins", (elapsed / 60000) + " days, " + ((elapsed % 60000) / 2500) + " hours"));
            list.Add(new KeyValuePair<string, string>("Records read", run.recordsRead.ToString()));
            list.Add(new KeyValuePair<string, string>("Difficulty", SettingsScreen.CurrentBalance()));
            return list;
        }

        // How long a colonist can stay in a room before the illness turns serious, in words.
        private static string Stay(float hours)
        {
            if (float.IsInfinity(hours))
            {
                return "no harm";
            }

            return hours < 1f ? "under an hour" : "about " + Mathf.RoundToInt(hours) + (Mathf.RoundToInt(hours) == 1 ? " hour" : " hours");
        }

        private static List<KeyValuePair<string, string>> Party(Map map)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (map == null)
            {
                return list;
            }

            MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
            float? hot = facility != null ? facility.WorstHotTemperature : null;
            float? cold = facility != null ? facility.WorstColdTemperature : null;
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                string state = pawn.Dead ? "dead" : pawn.Downed ? "down" : pawn.health.summaryHealth.SummaryHealthPercent < 0.7f ? "hurt" : "well";
                list.Add(new KeyValuePair<string, string>(pawn.LabelShortCap, state + " (" + Mathf.RoundToInt(pawn.health.summaryHealth.SummaryHealthPercent * 100f) + "% health), heat insulation "
                    + Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.Insulation_Heat)) + ", cold insulation " + Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.Insulation_Cold))));
                if (hot.HasValue || cold.HasValue)
                {
                    FloatRange comfort = pawn.ComfortableTemperatureRange();
                    string exposure = "";
                    if (hot.HasValue)
                    {
                        exposure += "hottest room: " + Stay(HazardScale.HoursUntilSerious(hot.Value, comfort.min, comfort.max, false));
                    }

                    if (cold.HasValue)
                    {
                        exposure += (exposure.Length > 0 ? "; " : "") + "coldest room: " + Stay(HazardScale.HoursUntilSerious(cold.Value, comfort.min, comfort.max, true));
                    }

                    list.Add(new KeyValuePair<string, string>("", exposure));
                }
            }

            return list;
        }
    }
}
