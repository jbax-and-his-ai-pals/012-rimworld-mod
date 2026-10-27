using System.Linq;
using LudeonTK;
using RimWorld;
using Site44.Gen;
using Verse;

namespace Site44
{
    public static class Site44DebugActions
    {
        private static MapComponent_Facility Facility()
        {
            MapComponent_Facility facility = Find.CurrentMap?.GetComponent<MapComponent_Facility>();
            if (facility == null || !facility.IsFacilityMap)
            {
                Messages.Message("[Site-44] this map has no facility.", MessageTypeDefOf.RejectInput, false);
                return null;
            }

            return facility;
        }

        [DebugAction("Site-44", "Enclave: add 100 standing", allowedGameStates = AllowedGameStates.Playing)]
        private static void EnclaveStanding()
        {
            MapComponent_Enclave enclave = Find.CurrentMap?.GetComponent<MapComponent_Enclave>();
            if (enclave == null || !enclave.active)
            {
                Messages.Message("[Site-44] this map is not an enclave.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            enclave.AddStanding(100);
            Messages.Message("[Site-44] standing now " + enclave.standing + ".", MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Site-44", "Generate ruin quest (choose type)", allowedGameStates = AllowedGameStates.Playing)]
        private static void GenerateRuinQuest()
        {
            ChooseType(type => Site44Quests.GenerateQuest(type));
        }

        [DebugAction("Site-44", "Generate ruin quest and open its map (choose type)", allowedGameStates = AllowedGameStates.Playing)]
        private static void OpenRuinMap()
        {
            ChooseType(type =>
            {
                var site = Site44Quests.FindRuinSite(type) ?? Site44Quests.GenerateQuest(type);
                if (site != null)
                {
                    Map map = Site44Quests.OpenMap(site);
                    Site44Quests.WarpColoniesTo(map);
                }
            });
        }

        [DebugAction("Site-44", "Generate ruin map (choose type and size)", allowedGameStates = AllowedGameStates.Playing)]
        private static void OpenRuinMapSized()
        {
            ChooseType(type =>
            {
                System.Collections.Generic.List<FloatMenuOption> sizes = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (string size in new[] { "Extra small", "Small", "Medium", "Large", "XL" })
                {
                    string captured = size;
                    sizes.Add(new FloatMenuOption(size, () =>
                    {
                        Site44Quests.SizeOverride = captured;
                        try
                        {
                            var site = Site44Quests.GenerateQuest(type);
                            if (site != null)
                            {
                                Map map = Site44Quests.OpenMap(site);
                                Site44Quests.WarpColoniesTo(map);
                            }
                        }
                        finally
                        {
                            Site44Quests.SizeOverride = null;
                        }
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(sizes));
            });
        }

        private static void ChooseType(System.Action<SiteTypeDef> action)
        {
            System.Collections.Generic.List<FloatMenuOption> options = new System.Collections.Generic.List<FloatMenuOption>();
            foreach (SiteTypeDef type in SiteTypes.All)
            {
                SiteTypeDef captured = type;
                options.Add(new FloatMenuOption(type.LabelCap, () => action(captured)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
        [DebugAction("Site-44", "Toggle hero mode on selected colonists (F7)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleHeroMode()
        {
            HeroMode.ToggleSelected();
        }

        [DebugAction("Site-44", "Toggle fog drawing (F9)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleFog()
        {
            GameComponent_Site44Hotkeys.ToggleFogDrawing();
        }

        [DebugAction("Site-44", "Unseal hatch at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UnsealHatchAtCursor()
        {
            MapComponent_Facility facility = Facility();
            if (facility == null)
            {
                return;
            }

            IntVec3 cursor = UI.MouseCell();
            Building_SealedHatch hatch = Find.CurrentMap.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch)
                .OfType<Building_SealedHatch>()
                .OrderBy(h => h.Position.DistanceToSquared(cursor))
                .FirstOrDefault(h => h.Position.DistanceTo(cursor) <= 3f);
            if (hatch == null)
            {
                Messages.Message("[Site-44] no sealed hatch within 3 cells of the cursor.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            facility.UnsealSector(hatch.sectorId);
        }

        [DebugAction("Site-44", "Unseal all sectors (F8)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void UnsealAllSectors()
        {
            MapComponent_Facility facility = Facility();
            if (facility == null)
            {
                return;
            }

            var sectorIds = Find.CurrentMap.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch)
                .OfType<Building_SealedHatch>()
                .Select(h => h.sectorId)
                .Distinct()
                .ToList();
            foreach (string sectorId in sectorIds)
            {
                facility.UnsealSector(sectorId);
            }

            Messages.Message("[Site-44] unsealed " + sectorIds.Count + " sector(s).", MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Site-44", "Add 1 clearance point", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddPoint()
        {
            Facility()?.AddPoints(1);
        }

        [DebugAction("Site-44", "Set clearance to OVERSEER", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MaxClearance()
        {
            MapComponent_Facility facility = Facility();
            if (facility != null && facility.points < 9)
            {
                facility.AddPoints(9 - facility.points);
            }
        }

        [DebugAction("Site-44", "Trigger alarm at cursor", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AlarmAtCursor()
        {
            Facility()?.TriggerAlarm(UI.MouseCell(), 6);
        }

        [DebugAction("Site-44", "Divert coolant", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DivertCoolant()
        {
            Facility()?.DivertCoolant();
        }

        [DebugAction("Site-44", "Reactor online", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReactorOnline()
        {
            Facility()?.BringReactorOnline();
        }

        [DebugAction("Site-44", "Log sector graph", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogGraph()
        {
            MapComponent_Facility facility = Facility();
            if (facility == null)
            {
                return;
            }

            SiteTypeDef type = SiteTypes.Resolve(Find.CurrentMap);
            BuildingPlan plan = type != null
                ? BuildingPlanner.Generate(facility.GridNx, facility.GridNy, facility.GridSeed, type.ToProfile())
                : BuildingPlanner.Generate(facility.GridNx, facility.GridNy, facility.GridSeed);
            FacilityGraph graph = FacilityGraph.Build(plan, facility.TargetZones, facility.GraphSeed);
            string lines = string.Join("\n", graph.Zones
                .Select(z => "#" + z.Id + " depth " + z.Depth + " " + z.Kind + " rooms=" + z.RoomIds.Count + " cells=" + z.CellCount
                             + (z.ParentId >= 0 ? " <- " + z.ParentId + " " + z.Gate + (z.Gate == GateKind.Terminal ? " tier " + z.RequiredTier : "") : "")
                             + (z.EvaCache ? " [EVA]" : ""))
                .ToArray());
            Log.Message("[Site-44] zone graph\n" + lines + "\n" + graph.Simulate().Report + "\n" + plan.ToAscii());
        }
    }
}

