using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // Pick which biomes a new game may start in. At least one must stay selected.
    public class Dialog_BiomeChoice : Window
    {
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 520f);

        public Dialog_BiomeChoice()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public static List<BiomeDef> Candidates()
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.canBuildBase && b.implemented).OrderBy(b => b.label).ToList();
        }

        // An empty list in the settings means every biome.
        public static bool IsAllowed(Site44Settings s, BiomeDef biome)
        {
            return s.startBiomes.Count == 0 || s.startBiomes.Contains(biome.defName);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Site44Settings s = Site44Settings.Current;
            List<BiomeDef> biomes = Candidates();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Start biomes");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 34f, inRect.width, 40f), "A new game starts on a random tile in one of the ticked biomes. At least one must stay ticked.");

            Rect outer = new Rect(0f, 80f, inRect.width, inRect.height - 126f);
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

            if (Widgets.ButtonText(new Rect(0f, inRect.height - 38f, 140f, 32f), "Select all"))
            {
                s.startBiomes.Clear();
            }

            if (Widgets.ButtonText(new Rect(inRect.width - 140f, inRect.height - 38f, 140f, 32f), "Close"))
            {
                Close();
            }
        }

        private static void Toggle(Site44Settings s, List<BiomeDef> all, BiomeDef biome, bool on)
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
        }
    }
}
