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
            bool delve = AllParts.Any(p => p is ScenPart_Site44Delve);
            if (!delve || !Site44Settings.Current.skipWorldSetup || ModsConfig.OdysseyActive)
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

    // The one page we add: the biomes the run's complexes are drawn from (the start included).
    // Everything is preselected, so Next is a fine answer.
    public class Page_Site44Biomes : Page
    {
        private Vector2 scrollPool;

        public override string PageTitle => "Biomes";

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Rect main = GetMainRect(inRect);
            // A narrow centred column: on an ultrawide screen a full-width row is too far from its tick box.
            float width = Mathf.Min(main.width, 520f);
            float x = main.x + (main.width - width) / 2f;
            Widgets.Label(new Rect(x, main.y, width, 44f), "Complex biomes are chosen at random from the ones you tick. At least one must stay ticked.");
            BiomeChoice.DrawList(new Rect(x, main.y + 48f, width, main.height - 48f), ref scrollPool);
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
            if (settings.biomePool.Count == 0)
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

                if (BiomeChoice.IsInPool(biome.defName))
                {
                    return candidate;
                }
            }

            Log.Message("[Site-44] no start tile found in the chosen biome(s); using a random one.");
            return fallback;
        }
    }
}
