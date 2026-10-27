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
        }

        public static bool IsInPool(string biomeDefName)
        {
            Site44Settings s = Site44Settings.Current;
            return s.biomePool.Count == 0 || s.biomePool.Contains(biomeDefName);
        }

        // The starting biome: radio buttons, with "random from the pool" first.
        public static void DrawStartList(Rect outer, ref Vector2 scroll, bool onlyPool)
        {
            Site44Settings s = Site44Settings.Current;
            List<BiomeDef> biomes = Candidates().Where(b => !onlyPool || IsAllowed(s, b)).ToList();
            if (onlyPool && !string.IsNullOrEmpty(s.startBiome) && !biomes.Any(b => b.defName == s.startBiome))
            {
                s.startBiome = "";
            }

            Rect view = new Rect(0f, 0f, outer.width - 16f, (biomes.Count + 1) * 28f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            if (Widgets.RadioButtonLabeled(new Rect(0f, 0f, view.width, 26f), onlyPool ? "Random from the pool" : "Random", string.IsNullOrEmpty(s.startBiome)))
            {
                s.startBiome = "";
                s.Write();
            }

            for (int i = 0; i < biomes.Count; i++)
            {
                if (Widgets.RadioButtonLabeled(new Rect(0f, (i + 1) * 28f, view.width, 26f), biomes[i].LabelCap, s.startBiome == biomes[i].defName))
                {
                    s.startBiome = biomes[i].defName;
                    s.Write();
                }
            }

            Widgets.EndScrollView();
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
