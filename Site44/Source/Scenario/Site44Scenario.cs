using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Site44
{
    // Site-44 scenarios never use the world beyond the climate and biome of the start tile, so the world and
    // start-site pages are replaced by an automatic step: a default world and a start tile of the chosen biome.
    // The storyteller, ideoligion and colonist pages stay as they are. Off (in the mod settings) or with the
    // Odyssey expansion's planet layers, the ordinary pages are used.
    public class Site44Scenario : Scenario
    {
        public override Page GetFirstConfigPage()
        {
            if (!Site44Settings.Current.skipWorldSetup || ModsConfig.OdysseyActive)
            {
                return base.GetFirstConfigPage();
            }

            List<Page> pages = new List<Page> { new Page_SelectStoryteller(), new Page_Site44Biomes(), new Page_Site44World(), new Page_Site44StartTile() };
            if (ModsConfig.IdeologyActive)
            {
                pages.Add(new Page_ChooseIdeoPreset());
            }

            foreach (Page page in AllParts.SelectMany(p => p.GetConfigPages()))
            {
                pages.Add(page);
            }

            Page first = PageUtility.StitchedPages(pages);
            if (first != null)
            {
                Page last = first;
                while (last.next != null)
                {
                    last = last.next;
                }

                last.nextAct = PageUtility.InitGameStart;
            }

            return first;
        }
    }

    // The one page we add: the starting biome, and the biomes later complexes of the run may come from.
    // Everything is preselected, so Next is a fine answer.
    public class Page_Site44Biomes : Page
    {
        private Vector2 scrollStart;
        private Vector2 scrollPool;

        public override string PageTitle => "Where do you start?";

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            bool delve = Find.Scenario != null && Find.Scenario.AllParts.Any(p => p is ScenPart_Site44Delve);
            float gap = 16f;
            float colWidth = delve ? (main.width - gap) / 2f : main.width;

            Rect left = new Rect(main.x, main.y, colWidth, main.height);
            Widgets.Label(new Rect(left.x, left.y, left.width, 44f), "Starting biome. Only the climate and biome of your start matter.");
            BiomeChoice.DrawStartList(new Rect(left.x, left.y + 48f, left.width, left.height - 48f), ref scrollStart);

            if (delve)
            {
                Rect right = new Rect(main.x + colWidth + gap, main.y, colWidth, main.height);
                Widgets.Label(new Rect(right.x, right.y, right.width, 44f), "Biomes for later complexes in this run. At least one must stay ticked.");
                BiomeChoice.DrawList(new Rect(right.x, right.y + 48f, right.width, right.height - 48f), ref scrollPool);
            }

            DoBottomButtons(inRect, "Next");
        }
    }

    // Generates a default world the moment it opens, then moves on.
    public class Page_Site44World : Page_CreateWorldParams
    {
        private bool started;

        public override void DoWindowContents(Rect inRect)
        {
            if (!started)
            {
                started = true;
                Reset();
                CanDoNext();
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, inRect.height / 2f - 20f, inRect.width, 40f), "Preparing the ruins...");
            Text.Font = GameFont.Small;
        }
    }

    // Picks the start tile (of the chosen biome when possible) and moves on.
    public class Page_Site44StartTile : Page
    {
        private bool started;

        public override void DoWindowContents(Rect inRect)
        {
            if (!started)
            {
                started = true;
                PlanetTile tile = ChooseTile();
                Find.WorldInterface.SelectedTile = tile;
                Find.GameInitData.startingTile = tile;
                DoNext();
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, inRect.height / 2f - 20f, inRect.width, 40f), "Choosing a site...");
            Text.Font = GameFont.Small;
        }

        private static PlanetTile ChooseTile()
        {
            Site44Settings settings = Site44Settings.Current;
            PlanetTile fallback = TileFinder.RandomStartingTile();
            bool explicitStart = !string.IsNullOrEmpty(settings.startBiome);
            if (!explicitStart && settings.biomePool.Count == 0)
            {
                return fallback;
            }

            for (int i = 0; i < 600; i++)
            {
                PlanetTile candidate = TileFinder.RandomStartingTile();
                BiomeDef biome = Find.WorldGrid[candidate].PrimaryBiome;
                if (biome == null)
                {
                    continue;
                }

                if (explicitStart ? biome.defName == settings.startBiome : settings.biomePool.Contains(biome.defName))
                {
                    return candidate;
                }
            }

            Log.Message("[Site-44] no start tile found in the chosen biome(s); using a random one.");
            return fallback;
        }
    }
}
