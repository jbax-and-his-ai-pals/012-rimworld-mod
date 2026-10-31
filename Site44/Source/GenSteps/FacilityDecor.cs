using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    // Furnishes rooms by function with vanilla furniture, ruins them with damage and dust,
    // and leaves a few rooms pristine with better loot.
    public class FacilityDecor
    {
        private readonly Map map;
        private readonly BuildingPlan plan;
        private readonly IntVec3 origin;
        private readonly Func<IntVec3, bool> isFree;
        private PlanRoom room;
        private bool preserved;

        public FacilityDecor(Map map, BuildingPlan plan, IntVec3 origin, Func<IntVec3, bool> isFree)
        {
            this.map = map;
            this.plan = plan;
            this.origin = origin;
            this.isFree = isFree;
        }

        private static ThingDef Def(string name)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }

        public void Decorate(PlanRoom room, bool preserved)
        {
            this.room = room;
            this.preserved = preserved;

            // Functions are matched by name so new plan functions never break the build.
            switch (room.Function.ToString())
            {
                case "PrivateOffice":
                    Desk(1);
                    Furnish("ShelfSmall", 0, 1, true);
                    Furnish("EndTable", 0, 1, true);
                    break;
                case "OpenOffice":
                    Desk(Mathf.Clamp(room.Cells.Count / 14, 2, 10));
                    Furnish("ShelfSmall", 1, 2, true);
                    break;
                case "Conference":
                    ConferenceTable();
                    break;
                case "Reception":
                case "Lobby":
                    Furnish("Table2x2c", 1, 1, false);
                    Furnish("Couch", 1, 2, true);
                    Furnish("Armchair", 1, 3, true);
                    Furnish("PlantPot", 1, 2, true);
                    break;
                case "Atrium":
                    Furnish("PlantPot", 3, 8, true);
                    Furnish("Couch", 1, 3, true);
                    Furnish("Armchair", 2, 5, true);
                    break;
                case "BreakRoom":
                    FurnishWithChairs("Table2x2c", 4, false);
                    Furnish("Couch", 0, 1, true);
                    Furnish("Shelf", 0, 1, true);
                    if (Rand.Chance(0.35f))
                    {
                        Furnish("Bed", 1, 1, true);
                    }
                    break;
                case "Ward":
                    Furnish("HospitalBed", Mathf.Clamp(room.Cells.Count / 12, 2, 6), Mathf.Clamp(room.Cells.Count / 8, 3, 8), true);
                    Furnish("VitalsMonitor", 1, 2, true);
                    Furnish("Site44_MedBayConsole", 0, 1, true);
                    Furnish("Stool", 1, 2, false);
                    ScatterItems("MedicineIndustrial", 0, 1, 2, 5);
                    break;
                case "OperatingRoom":
                    Furnish("AncientOperatingTable", 1, 2, false);
                    Furnish("VitalsMonitor", 1, 2, true);
                    Furnish("ShelfSmall", 1, 2, true);
                    Furnish("HospitalBed", 0, 1, true);
                    break;
                case "Pharmacy":
                    Furnish("Shelf", 3, 6, true);
                    Furnish("ShelfSmall", 1, 3, true);
                    Furnish("Table1x2c", 1, 1, false);
                    Furnish("Stool", 1, 2, false);
                    ScatterItems("MedicineIndustrial", 2, 3, 3, 8);
                    ScatterItems("MedicineHerbal", 1, 2, 4, 10);
                    break;
                case "Morgue":
                    Furnish("AncientRefrigerator", 3, 6, true);
                    Furnish("AncientOperatingTable", 0, 1, false);
                    FacilityContainers remains = new FacilityContainers(map, plan, origin, isFree);
                    int bodies = Rand.RangeInclusive(1, 3);
                    for (int i = 0; i < bodies; i++)
                    {
                        remains.TrySpawnStrayCorpse(room);
                    }
                    break;
                case "Bunkroom":
                    Furnish("AncientBed", Mathf.Clamp(room.Cells.Count / 9, 5, 12), Mathf.Clamp(room.Cells.Count / 6, 6, 14), true);
                    Furnish("Bed", 0, 2, true);
                    Furnish("AncientLockerBank", 1, 3, true);
                    Furnish("EndTable", 1, 3, true);
                    break;
                case "Apartment":
                    if (Rand.Chance(0.3f))
                    {
                        Furnish("DoubleBed", 1, 1, true);
                    }
                    else
                    {
                        Furnish("Bed", 1, 1, true);
                    }

                    Furnish("Dresser", 1, 1, true);
                    Furnish("EndTable", 1, 1, true);
                    FurnishWithChairs("Table1x2c", 2, false);
                    Furnish("AncientKitchenSink", 0, 1, true);
                    Furnish("PlantPot", 0, 1, true);
                    break;
                case "Cell":
                    Furnish("AncientBed", 1, 2, true);
                    Furnish("EndTable", 0, 1, true);
                    break;
                case "Containment":
                    Furnish("AncientStorageCylinder", 1, 3, true);
                    Furnish("AncientMachine", 0, 2, true);
                    break;
                case "Kitchen":
                    Furnish("AncientStove", 1, 2, true);
                    Furnish("AncientOven", 0, 1, true);
                    Furnish("AncientKitchenSink", 1, 2, true);
                    Furnish("AncientRefrigerator", 1, 2, true);
                    Furnish("AncientMicrowave", 0, 1, true);
                    Furnish("Table1x2c", 1, 2, false);
                    Furnish("Stool", 1, 3, false);
                    break;
                case "Dormitory":
                    Furnish("AncientBed", Mathf.Clamp(room.Cells.Count / 14, 3, 8), Mathf.Clamp(room.Cells.Count / 9, 4, 10), true);
                    Furnish("Bed", 1, 2, true);
                    Furnish("Dresser", 2, 4, true);
                    Furnish("EndTable", 2, 4, true);
                    break;
                case "Infirmary":
                    Furnish("HospitalBed", 2, 4, true);
                    Furnish("Site44_MedBayConsole", 1, 1, true);
                    Furnish("VitalsMonitor", 1, 2, true);
                    Furnish("AncientOperatingTable", 0, 1, false);
                    Furnish("ShelfSmall", 1, 2, true);
                    Furnish("Stool", 1, 3, false);
                    ScatterItems("MedicineIndustrial", 1, 2, 3, 8);
                    break;
                case "LockerRoom":
                    Furnish("AncientLockerBank", 2, 5, true);
                    Furnish("Stool", 2, 5, false);
                    Furnish("Table1x2c", 0, 1, false);
                    break;
                case "Library":
                    Furnish("Bookcase", 4, 8, true);
                    Furnish("BookcaseSmall", 1, 3, true);
                    FurnishWithChairs("Table2x2c", 3, false);
                    Furnish("Armchair", 2, 4, true);
                    Furnish("PlantPot", 0, 1, true);
                    break;
                case "Workshop":
                    Furnish("TableMachining", 1, 2, false);
                    Furnish("FabricationBench", 0, 1, false);
                    Furnish("AncientMachine", 1, 2, true);
                    Furnish("Shelf", 1, 3, true);
                    Furnish("AncientBarrel", 1, 3, true);
                    break;
                case "Armory":
                    Furnish("AncientMilitaryCrate", 1, 2, true);
                    Furnish("AncientEquipmentBlocks", 1, 2, true);
                    Furnish("AncientLockerBank", 1, 2, true);
                    ScatterWeapons(1, 3);
                    break;
                case "CommsRoom":
                    Furnish("CommsConsole", 1, 1, true);
                    Furnish("AncientDisplayBank", 1, 2, true);
                    Furnish("AncientSystemRack", 1, 2, true);
                    Furnish("DiningChair", 1, 2, false);
                    break;
                case "Classroom":
                    for (int row = 0; row < Mathf.Clamp(room.Cells.Count / 12, 3, 8); row++)
                    {
                        FurnishWithChairs("Table1x2c", 1, false);
                    }

                    Furnish("Bookcase", 1, 1, true);
                    break;
                case "RecRoom":
                    Furnish("BilliardsTable", 0, 1, false);
                    Furnish("PokerTable", 0, 1, false);
                    Furnish("ChessTable", 0, 1, false);
                    Furnish("TubeTelevision", 0, 1, true);
                    Furnish("FlatscreenTelevision", 0, 1, true);
                    Furnish("Couch", 2, 3, true);
                    Furnish("Armchair", 2, 4, true);
                    Furnish("EndTable", 1, 2, true);
                    Furnish("PlantPot", 1, 2, true);
                    break;
                case "Auditorium":
                    Furnish("DiningChair", 14, 40, false);
                    Furnish("Table2x4c", 0, 1, false);
                    break;
                case "Vault":
                    Furnish("AncientLockerBank", 2, 4, true);
                    Furnish("AncientCrate", 2, 4, false);
                    break;
                case "Cafeteria":
                    Furnish("Table2x4c", 1, 4, false);
                    Furnish("Table2x2c", 1, 3, false);
                    Furnish("DiningChair", 6, 14, false);
                    break;
                case "Storeroom":
                case "Warehouse":
                    Furnish("Shelf", Mathf.Clamp(room.Cells.Count / 14, 2, 10), Mathf.Clamp(room.Cells.Count / 8, 3, 14), true);
                    Furnish("ShelfSmall", 1, 3, true);
                    Furnish("AncientCrate", Mathf.Clamp(room.Cells.Count / 40, 0, 2), Mathf.Clamp(room.Cells.Count / 18, 1, 6), false);
                    ScatterTypeLoot(1, Mathf.Clamp(room.Cells.Count / 40, 1, 4));
                    break;
                case "Archive":
                    Furnish("Bookcase", 3, 8, true);
                    Furnish("BookcaseSmall", 1, 3, true);
                    break;
                case "Mailroom":
                    Furnish("Shelf", 1, 3, true);
                    Furnish("Table1x2c", 1, 1, true);
                    break;
                case "ServerRoom":
                    Furnish("AncientSystemRack", 2, 6, true);
                    break;
                case "Lab":
                    Furnish("Table2x2c", 1, 3, false);
                    Furnish("Table1x2c", 1, 3, true);
                    Furnish("ShelfSmall", 1, 3, true);
                    Furnish("AncientMachine", 0, 2, true);
                    break;
                case "MachineHall":
                    Furnish("AncientGenerator", Mathf.Clamp(room.Cells.Count / 80, 1, 4), Mathf.Clamp(room.Cells.Count / 50, 2, 5), false);
                    Furnish("AncientMachine", Mathf.Clamp(room.Cells.Count / 40, 2, 7), Mathf.Clamp(room.Cells.Count / 25, 3, 10), false);
                    Furnish("AncientBarrel", Mathf.Clamp(room.Cells.Count / 40, 2, 6), Mathf.Clamp(room.Cells.Count / 22, 3, 9), true);
                    Furnish("AncientStorageCylinder", 0, Mathf.Clamp(room.Cells.Count / 90, 0, 3), true);
                    ScatterTypeLoot(0, Mathf.Clamp(room.Cells.Count / 80, 0, 2));
                    break;
                case "Mechanical":
                case "Boiler":
                    Furnish("AncientMachine", 1, 2, true);
                    Furnish("AncientBarrel", 1, 3, true);
                    break;
                case "SecurityPost":
                    FurnishWithChairs("Table1x2c", 2, true);
                    Furnish("AncientLockerBank", 0, 1, true);
                    break;
                case "Closet":
                    Furnish("ShelfSmall", 0, 2, true);
                    break;
            }

            if (preserved)
            {
                PreservedLoot();
            }
            else
            {
                Dust();
            }
        }

        // The entrance reception: kept up, spotless, and visibly better than the rest of the ruin.
        public void DecorateReception(PlanRoom room)
        {
            this.room = room;
            this.preserved = true;
            switch (room.Function.ToString())
            {
                case "Reception":
                    FurnishWithChairs("Table2x2c", 2, false);
                    FurnishWithChairs("Table1x2c", 1, true);
                    Furnish("Bookcase", 1, 2, true);
                    Furnish("PlantPot", 1, 3, true);
                    break;
                case "Atrium":
                    Furnish("PlantPot", 6, 12, true);
                    Furnish("Couch", 2, 4, true);
                    Furnish("Armchair", 3, 6, true);
                    break;
                default:
                    Furnish("Couch", 2, 4, true);
                    Furnish("Armchair", 2, 5, true);
                    Furnish("EndTable", 2, 4, true);
                    Furnish("PlantPot", 4, 8, true);
                    FurnishWithChairs("Table2x2c", 3, false);
                    break;
            }
        }

        // The executive office: carpeted, well furnished and a little less ruined than most.
        public void DecorateExecutive(PlanRoom room)
        {
            this.room = room;
            this.preserved = false;
            FurnishWithChairs("Table2x2c", 3, false);
            Furnish("Couch", 1, 1, true);
            Furnish("Bookcase", 2, 3, true);
            Furnish("ShelfSmall", 1, 2, true);
            Furnish("PlantPot", 1, 2, true);
            Dust();
        }

        // ----- furniture -----

        private void Furnish(string defName, int min, int max, bool againstWall)
        {
            ThingDef def = Def(defName);
            if (def == null)
            {
                return;
            }

            int count = Rand.RangeInclusive(min, max);
            for (int i = 0; i < count; i++)
            {
                TrySpawn(def, againstWall, true);
            }
        }

        private void Desk(int count)
        {
            ThingDef desk = Def("Table1x2c");
            ThingDef chair = Def("DiningChair");
            if (desk == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Thing table = TrySpawn(desk, true, true);
                if (table != null && chair != null)
                {
                    SpawnChairsAround(table, chair, 1);
                }
            }
        }

        private void ConferenceTable()
        {
            ThingDef def = Def(room.Cells.Count >= 40 ? "Table2x4c" : "Table2x2c");
            ThingDef chair = Def("DiningChair");
            if (def == null)
            {
                return;
            }

            Thing table = TrySpawn(def, false, true);
            if (table != null && chair != null)
            {
                SpawnChairsAround(table, chair, 6);
            }
        }

        private void FurnishWithChairs(string tableDefName, int chairs, bool againstWall)
        {
            ThingDef table = Def(tableDefName);
            ThingDef chair = Def("DiningChair");
            if (table == null)
            {
                return;
            }

            Thing placed = TrySpawn(table, againstWall, false);
            if (placed != null && chair != null)
            {
                SpawnChairsAround(placed, chair, chairs);
            }
        }

        private void SpawnChairsAround(Thing table, ThingDef chairDef, int count)
        {
            CellRect occupied = table.OccupiedRect();
            List<IntVec3> spots = GenAdj.CellsAdjacentCardinal(table).ToList();
            spots.Shuffle();
            foreach (IntVec3 spot in spots)
            {
                if (count <= 0)
                {
                    break;
                }

                if (!InRoom(spot) || !isFree(spot))
                {
                    continue;
                }

                IntVec3 near = occupied.ClosestCellTo(spot);
                GenSpawn.Spawn(Make(chairDef), spot, map, Rot4.FromAngleFlat((near - spot).AngleFlat));
                count--;
            }
        }

        private Thing TrySpawn(ThingDef def, bool againstWall, bool randomRotation)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                IntVec3 cell = ToWorld(room.Cells.RandomElement());
                Rot4 rot = randomRotation && def.size.x == 1 && def.size.z == 1 ? Rot4.Random : Rot4.North;
                if (def.size.x != def.size.z && randomRotation && Rand.Bool)
                {
                    rot = Rot4.East;
                }

                CellRect occupied = GenAdj.OccupiedRect(cell, rot, def.size);
                if (!occupied.Cells.All(c => InRoom(c) && isFree(c)))
                {
                    continue;
                }

                if (againstWall && !occupied.AdjacentCells.Any(IsWallLike))
                {
                    continue;
                }

                Thing thing = Make(def);
                GenSpawn.Spawn(thing, cell, map, rot);
                return thing;
            }

            return null;
        }

        private Thing Make(ThingDef def)
        {
            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            if (!preserved && thing.def.useHitPoints && thing.MaxHitPoints > 1)
            {
                thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * Rand.Range(Site44Tuning.RuinedFurnitureMinHealth, Site44Tuning.RuinedFurnitureMaxHealth)));
            }

            return thing;
        }

        // ----- dust and loot -----

        private static readonly string[] OfficeFilth = { "Filth_Dirt", "Filth_Dirt", "Filth_ScatteredDocuments", "Filth_Trash", "Filth_RubbleBuilding" };
        private static readonly string[] PlantFilth = { "Filth_Dirt", "Filth_OilSmear", "Filth_MachineBits", "Filth_RubbleBuilding" };

        private void Dust()
        {
            float chance = room.IsCorridor ? Site44Tuning.DustChanceCorridor : Site44Tuning.DustChanceRoom;
            string function = room.Function.ToString();
            string[] table = function == "Mechanical" || function == "MachineHall" || function == "Boiler" || function == "Workshop" || function == "Armory" ? PlantFilth : OfficeFilth;
            foreach (LayoutCell cell in room.Cells)
            {
                if (!Rand.Chance(chance))
                {
                    continue;
                }

                IntVec3 world = ToWorld(cell);
                ThingDef filth = Def(table.RandomElement());
                if (filth != null && world.GetEdifice(map) == null)
                {
                    FilthMaker.TryMakeFilth(world, map, filth, 1);
                }
            }
        }

        private static readonly string[] PreservedLootTable = { "ComponentSpacer", "MedicineUltratech", "Plasteel", "Gold", "Silver", "MedicineIndustrial" };

        private void PreservedLoot()
        {
            int stacks = Mathf.Max(1, Mathf.RoundToInt(Rand.RangeInclusive(3, 5) * Site44Tuning.LootAmount));
            for (int i = 0; i < stacks; i++)
            {
                ThingDef def = Def(PreservedLootTable.RandomElement());
                if (def == null)
                {
                    continue;
                }

                IntVec3 cell = ToWorld(room.Cells.RandomElement());
                if (!isFree(cell))
                {
                    continue;
                }

                if (!Site44Tuning.KeepSupply(def))
                {
                    continue;
                }

                Thing loot = ThingMaker.MakeThing(def);
                loot.stackCount = Mathf.Clamp(Site44Tuning.ScarceCount(def, Mathf.RoundToInt(Rand.RangeInclusive(def.stackLimit / 5, def.stackLimit / 2) * Site44Tuning.LootAmount)), 1, def.stackLimit);
                GenPlace.TryPlaceThing(loot, cell, map, ThingPlaceMode.Near);
            }
        }

        // Loose goods of the kind this complex holds: steel and parts in a plant, medicine in a hospital, and so on.
        private void ScatterTypeLoot(int minStacks, int maxStacks)
        {
            FacilityContainers goods = new FacilityContainers(map, plan, origin, isFree);
            int stacks = Mathf.RoundToInt(Rand.RangeInclusive(minStacks, maxStacks) * Site44Tuning.ClutterFactor);
            for (int i = 0; i < stacks; i++)
            {
                IntVec3 cell = ToWorld(room.Cells.RandomElement());
                Thing loot = isFree(cell) ? goods.RandomLoot() : null;
                if (loot != null)
                {
                    GenPlace.TryPlaceThing(loot, cell, map, ThingPlaceMode.Near);
                }
            }
        }

        // Loose items on the floor of a room (medical supplies in a clinic, for example).
        private void ScatterItems(string defName, int minStacks, int maxStacks, int minCount, int maxCount)
        {
            ThingDef def = Def(defName);
            if (def == null)
            {
                return;
            }

            int stacks = Rand.RangeInclusive(minStacks, maxStacks);
            for (int i = 0; i < stacks; i++)
            {
                IntVec3 cell = ToWorld(room.Cells.RandomElement());
                if (!isFree(cell))
                {
                    continue;
                }

                if (!Site44Tuning.KeepSupply(def))
                {
                    continue;
                }

                Thing item = ThingMaker.MakeThing(def);
                item.stackCount = Mathf.Clamp(Site44Tuning.ScarceCount(def, Mathf.RoundToInt(Rand.RangeInclusive(minCount, maxCount) * Site44Tuning.LootAmount)), 1, def.stackLimit);
                GenPlace.TryPlaceThing(item, cell, map, ThingPlaceMode.Near);
            }
        }

        private static readonly string[] ArmoryWeapons = { "Gun_Revolver", "Gun_Autopistol", "MeleeWeapon_Knife", "MeleeWeapon_Club", "Bow_Short", "MeleeWeapon_Spear", "Gun_PumpShotgun" };

        // Worn, low-grade weapons left in an armory.
        private void ScatterWeapons(int min, int max)
        {
            int count = Rand.RangeInclusive(min, max);
            for (int i = 0; i < count; i++)
            {
                ThingDef def = Def(ArmoryWeapons.RandomElement());
                IntVec3 cell = ToWorld(room.Cells.RandomElement());
                if (def == null || !isFree(cell))
                {
                    continue;
                }

                Thing weapon = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                CompQuality quality = weapon.TryGetComp<CompQuality>();
                if (quality != null)
                {
                    quality.SetQuality(Rand.Chance(0.6f) ? QualityCategory.Poor : QualityCategory.Normal, ArtGenerationContext.Outsider);
                }

                if (weapon.def.useHitPoints)
                {
                    weapon.HitPoints = Mathf.Max(1, Mathf.RoundToInt(weapon.MaxHitPoints * Rand.Range(0.3f, 0.8f)));
                }

                GenPlace.TryPlaceThing(weapon, cell, map, ThingPlaceMode.Near);
            }
        }

        // ----- helpers -----

        private IntVec3 ToWorld(LayoutCell cell)
        {
            return new IntVec3(origin.x + cell.X, 0, origin.z + cell.Y);
        }

        private bool InRoom(IntVec3 c)
        {
            return plan.RoomAt(c.x - origin.x, c.z - origin.z) == room.Id;
        }

        private bool IsWallLike(IntVec3 c)
        {
            PlanTile tile = plan.At(c.x - origin.x, c.z - origin.z);
            return tile == PlanTile.Wall || tile == PlanTile.Window;
        }
    }
}

