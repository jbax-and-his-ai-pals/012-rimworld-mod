using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // The list of biomes a new game may start in. At least one must stay selected; an empty stored list means all.
    public static class BiomeChoice
    {
        // Climates that kill an unprepared party. They start unticked and ticking one warns.
        private static readonly string[] Extreme = { "IceSheet", "SeaIce", "ExtremeDesert" };
        private static readonly string[] Cold = { "IceSheet", "SeaIce", "Tundra", "BorealForest", "ColdBog" };

        public static bool IsExtreme(BiomeDef biome)
        {
            return biome != null && System.Array.IndexOf(Extreme, biome.defName) >= 0;
        }

        public static bool IsCold(BiomeDef biome)
        {
            return biome != null && System.Array.IndexOf(Cold, biome.defName) >= 0;
        }

        // The first time through, the pool is every biome except the extremes.
        public static void EnsureDefault(Site44Settings s)
        {
            if (s.biomePoolV2)
            {
                return;
            }

            s.biomePoolV2 = true;
            List<BiomeDef> all = Candidates();
            List<string> mild = all.Where(b => !IsExtreme(b)).Select(b => b.defName).ToList();
            s.biomePool = mild.Count >= all.Count ? new List<string>() : mild;
            s.Write();
        }

        public static bool PoolHasMild(Site44Settings s)
        {
            return Candidates().Any(b => IsAllowed(s, b) && !IsExtreme(b));
        }

        public static bool PoolHasCold(Site44Settings s)
        {
            return Candidates().Any(b => IsAllowed(s, b) && IsCold(b));
        }

        public static string ExtremeTicked(Site44Settings s)
        {
            return string.Join(", ", Candidates().Where(b => IsAllowed(s, b) && IsExtreme(b)).Select(b => b.LabelCap.ToString()).ToArray());
        }

        public static List<BiomeDef> Candidates()
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.canBuildBase && b.implemented).OrderBy(b => b.label).ToList();
        }

        public static bool IsAllowed(Site44Settings s, BiomeDef biome)
        {
            return s.biomePool.Count == 0 || s.biomePool.Contains(biome.defName);
        }

        public static void Toggle(Site44Settings s, List<BiomeDef> all, BiomeDef biome, bool on)
        {
            List<string> selected = s.biomePool.Count == 0 ? all.Select(b => b.defName).ToList() : new List<string>(s.biomePool);
            if (on)
            {
                if (!selected.Contains(biome.defName))
                {
                    selected.Add(biome.defName);
                }
            }
            else
            {
                if (selected.Count <= 1)
                {
                    Messages.Message("At least one biome must stay selected.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                selected.Remove(biome.defName);
            }

            s.biomePool = selected.Count >= all.Count ? new List<string>() : selected;
            s.Write();
            if (on && IsExtreme(biome))
            {
                Messages.Message(biome.LabelCap + " is an extreme climate. An unprepared party can die of exposure there.", MessageTypeDefOf.NegativeEvent, false);
            }
        }

        public static bool IsInPool(string biomeDefName)
        {
            Site44Settings s = Site44Settings.Current;
            return s.biomePool.Count == 0 || s.biomePool.Contains(biomeDefName);
        }

        // Banded rows with the tick box at the left next to the name; the whole row toggles, so a wide screen is no obstacle.
        public static void DrawList(Rect outer, ref Vector2 scroll)
        {
            Site44Settings s = Site44Settings.Current;
            List<BiomeDef> biomes = Candidates();
            const float rowHeight = 30f;
            Rect view = new Rect(0f, 0f, outer.width - 16f, biomes.Count * rowHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < biomes.Count; i++)
            {
                Rect row = new Rect(0f, i * rowHeight, view.width, rowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }

                if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }

                bool on = IsAllowed(s, biomes[i]);
                bool before = on;
                Widgets.Checkbox(new Vector2(row.x + 6f, row.y + 3f), ref on, 24f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 40f, row.y, row.width - 40f, row.height), biomes[i].LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(row))
                {
                    on = !on;
                }

                if (on != before)
                {
                    Toggle(s, biomes, biomes[i], on);
                }
            }

            Widgets.EndScrollView();
        }
    }
}
