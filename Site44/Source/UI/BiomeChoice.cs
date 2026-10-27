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
        public static List<BiomeDef> Candidates()
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.canBuildBase && b.implemented).OrderBy(b => b.label).ToList();
        }

        public static bool IsAllowed(Site44Settings s, BiomeDef biome)
        {
            return s.startBiomes.Count == 0 || s.startBiomes.Contains(biome.defName);
        }

        public static void Toggle(Site44Settings s, List<BiomeDef> all, BiomeDef biome, bool on)
        {
            List<string> selected = s.startBiomes.Count == 0 ? all.Select(b => b.defName).ToList() : new List<string>(s.startBiomes);
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

            s.startBiomes = selected.Count >= all.Count ? new List<string>() : selected;
            s.Write();
        }

        public static void DrawList(Rect outer, ref Vector2 scroll)
        {
            Site44Settings s = Site44Settings.Current;
            List<BiomeDef> biomes = Candidates();
            Rect view = new Rect(0f, 0f, outer.width - 16f, biomes.Count * 28f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < biomes.Count; i++)
            {
                bool on = IsAllowed(s, biomes[i]);
                bool before = on;
                Widgets.CheckboxLabeled(new Rect(0f, i * 28f, view.width, 26f), biomes[i].LabelCap, ref on);
                if (on != before)
                {
                    Toggle(s, biomes, biomes[i], on);
                }
            }

            Widgets.EndScrollView();
        }
    }
}
