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

    // The one page we add: which climates this run may start in. Everything is ticked by default, so Next is a fine answer.
    public class Page_Site44Biomes : Page
    {
        private Vector2 scroll;

        public override string PageTitle => "Where do you start?";

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            Widgets.Label(new Rect(main.x, main.y, main.width, 44f), "Only the climate and biome of your start matter. Tick the biomes this run may start in; leave everything ticked if you do not mind. At least one must stay ticked.");
            Rect list = new Rect(main.x, main.y + 52f, main.width, main.height - 52f);
            BiomeChoice.DrawList(list, ref scroll);
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
            PlanetTile tile = TileFinder.RandomStartingTile();
            if (settings.startBiomes.Count == 0)
            {
                return tile;
            }

            for (int i = 0; i < 600; i++)
            {
                PlanetTile candidate = TileFinder.RandomStartingTile();
                BiomeDef biome = Find.WorldGrid[candidate].PrimaryBiome;
                if (biome != null && settings.startBiomes.Contains(biome.defName))
                {
                    return candidate;
                }
            }

            Log.Message("[Site-44] no start tile found in the chosen biomes; using a random one.");
            return tile;
        }
    }
}
