using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Site44.Gen;
using UnityEngine;
using Verse;

namespace Site44
{
    public enum CasketKind
    {
        Survivor,
        Hostile,
        Dead,
        Loot
    }

    // Loot crates and cryosleep caskets (with survivors, hostiles, corpses or just loot) and stray corpses.
    public class FacilityContainers
    {
        private readonly Map map;
        private readonly BuildingPlan plan;
        private readonly IntVec3 origin;
        private readonly Func<IntVec3, bool> isFree;

        public FacilityContainers(Map map, BuildingPlan plan, IntVec3 origin, Func<IntVec3, bool> isFree)
        {
            this.map = map;
            this.plan = plan;
            this.origin = origin;
            this.isFree = isFree;
        }

        private static readonly string[] LootTable =
        {
            "Steel", "ComponentIndustrial", "MedicineIndustrial", "Silver", "Silver", "Plasteel", "Gold", "Uranium", "MealSurvivalPack", "ComponentSpacer"
        };

        private static ThingDef Def(string name)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }

        // Loot comes from the current site type's table when it has one, else the built-in table.
        private static ThingDef PickLoot(bool rich)
        {
            // A medicine or meal pick that fails the scarcity roll is simply a stack fewer, not swapped for something else.
            ThingDef pick = PickLootOnce(rich);
            return Site44Tuning.KeepSupply(pick) ? pick : null;
        }

        private static ThingDef PickLootOnce(bool rich)
        {
            if (rich && Rand.Chance(Site44Settings.Current.delverGearChance))
            {
                return Rand.Bool ? Site44MoraleDefOf.Site44_DelverCap : Site44MoraleDefOf.Site44_DelverVest;
            }

            if (!rich && Rand.Chance(Site44Settings.Current.drugFindChance))
            {
                return Site44MoraleDefOf.Site44_FocusTab;
            }

            List<LootWeight> table = rich ? SiteTypes.Current?.richLoot : SiteTypes.Current?.loot;
            LootWeight pick;
            if (table != null && table.Count > 0 && table.TryRandomElementByWeight(l => l.weight, out pick) && pick.thingDef != null)
            {
                return pick.thingDef;
            }

            return Def((rich ? RichTable : LootTable).RandomElement());
        }

        // Makes a loot item; weapons and clothing come worn and of ordinary or poor quality.
        private static Thing MakeLoot(ThingDef def)
        {
            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            CompQuality quality = thing.TryGetComp<CompQuality>();
            if (quality != null)
            {
                quality.SetQuality(Rand.Chance(0.6f) ? QualityCategory.Poor : QualityCategory.Normal, ArtGenerationContext.Outsider);
            }

            if (def.useHitPoints && def.stackLimit == 1 && thing.MaxHitPoints > 1)
            {
                thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * Rand.Range(0.3f, 0.8f)));
            }

            return thing;
        }

        public Thing RandomLoot()
        {
            ThingDef def = PickLoot(false);
            if (def == null)
            {
                return null;
            }

            Thing thing = MakeLoot(def);
            if (def == Site44MoraleDefOf.Site44_FocusTab)
            {
                thing.stackCount = Rand.RangeInclusive(2, 6);
                return thing;
            }

            int max = def.stackLimit;
            // Rarer things come in small amounts.
            if (def.BaseMarketValue > 8f)
            {
                max = Mathf.Max(1, max / 4);
            }

            thing.stackCount = Mathf.Clamp(Site44Tuning.ScarceCount(def, Mathf.RoundToInt(Rand.RangeInclusive(Mathf.Max(1, max / 4), max) * Site44Tuning.LootAmount)), 1, def.stackLimit);
            return thing;
        }

        private static readonly string[] RichTable = { "ComponentSpacer", "Gold", "Plasteel", "MedicineUltratech", "Uranium", "Silver", "ComponentSpacer" };

        // The good stuff kept in secure rooms.
        public Thing RichLoot()
        {
            ThingDef def = PickLoot(true);
            if (def == null)
            {
                return RandomLoot();
            }

            Thing thing = MakeLoot(def);
            int max = Mathf.Max(2, def.stackLimit / 3);
            thing.stackCount = Mathf.Clamp(Site44Tuning.ScarceCount(def, Mathf.RoundToInt(Rand.RangeInclusive(Mathf.Max(1, max / 3), max) * Site44Tuning.LootAmount)), 1, def.stackLimit);
            return thing;
        }

        // ----- spots -----

        private bool InRoom(PlanRoom room, IntVec3 c)
        {
            return plan.RoomAt(c.x - origin.x, c.z - origin.z) == room.Id;
        }

        // A 1x2 container facing north with its interaction cell to the east kept free.
        private bool TryFindSpot(PlanRoom room, IntVec2 size, out IntVec3 center)
        {
            List<LayoutCell> cells = room.Cells.ToList();
            cells.Shuffle();
            foreach (LayoutCell cell in cells.Take(60))
            {
                IntVec3 candidate = new IntVec3(origin.x + cell.X, 0, origin.z + cell.Y);
                CellRect occupied = GenAdj.OccupiedRect(candidate, Rot4.North, size);
                IntVec3 interaction = candidate + IntVec3.East;
                if (occupied.Cells.All(c => InRoom(room, c) && isFree(c)) && InRoom(room, interaction) && isFree(interaction))
                {
                    center = candidate;
                    return true;
                }
            }

            center = IntVec3.Invalid;
            return false;
        }

        // ----- crates -----

        public bool TrySpawnCrate(PlanRoom room, bool rich = false)
        {
            ThingDef crateDef = Def("AncientHermeticCrate");
            IntVec3 spot;
            if (crateDef == null || !TryFindSpot(room, crateDef.size, out spot))
            {
                return false;
            }

            Building_Crate crate = ThingMaker.MakeThing(crateDef) as Building_Crate;
            if (crate == null)
            {
                return false;
            }

            int stacks = rich ? Rand.RangeInclusive(2, 4) : Rand.RangeInclusive(1, 3);
            for (int i = 0; i < stacks; i++)
            {
                Thing loot = rich ? RichLoot() : RandomLoot();
                if (loot != null && !crate.TryAcceptThing(loot, false))
                {
                    loot.Destroy();
                }
            }

            GenSpawn.Spawn(crate, spot, map, Rot4.North);
            return true;
        }

        // A welcome cache in the lobby: a little food and medicine so the first minutes are never desperate.
        public Thing TrySpawnSupplyCrate(PlanRoom room)
        {
            ThingDef crateDef = Def("Site44_SupplyCrate");
            IntVec3 spot;
            if (crateDef == null || !TryFindSpot(room, crateDef.size, out spot))
            {
                return null;
            }

            Building_Crate crate = ThingMaker.MakeThing(crateDef) as Building_Crate;
            if (crate == null)
            {
                return null;
            }

            AddSupply(crate, "MedicineIndustrial", 1);
            AddSupply(crate, "MealSurvivalPack", 3);
            AddSupply(crate, "Bedroll", 2);
            GenSpawn.Spawn(crate, spot, map, Rot4.North);
            return crate;
        }

        private static void AddSupply(Building_Crate crate, string defName, int count)
        {
            ThingDef def = Def(defName);
            if (def == null)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            thing.stackCount = Mathf.Clamp(count, 1, def.stackLimit);
            if (!crate.TryAcceptThing(thing, false))
            {
                thing.Destroy();
            }
        }

        // ----- people -----

        private static PawnKindDef Kind(params string[] names)
        {
            foreach (string name in names.OrderBy(n => Rand.Value))
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(name);
                if (kind != null)
                {
                    return kind;
                }
            }

            return DefDatabase<PawnKindDef>.GetNamedSilentFail("Drifter");
        }

        private static Pawn MakePawn(PawnKindDef kind, Faction faction, bool dead)
        {
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, forceDead: dead, worldPawnFactionDoesntMatter: true));
        }

        private static Corpse MakeCorpse()
        {
            Pawn pawn = MakePawn(Kind("Villager", "Scavenger", "Drifter", "Colonist"), null, true);
            Corpse corpse = pawn.Corpse;
            if (corpse != null)
            {
                corpse.Age = Rand.RangeInclusive(60000 * 20, 60000 * 400);
                pawn.relations.hidePawnRelations = true;
                CompRottable rot = corpse.GetComp<CompRottable>();
                if (rot != null)
                {
                    rot.RotProgress += corpse.Age;
                }
            }

            return corpse;
        }

        public bool TrySpawnCasket(PlanRoom room, CasketKind kind)
        {
            ThingDef casketDef = Def("Site44_Casket");
            IntVec3 spot;
            if (casketDef == null || !TryFindSpot(room, casketDef.size, out spot))
            {
                return false;
            }

            Building_Site44Casket casket = ThingMaker.MakeThing(casketDef) as Building_Site44Casket;
            if (casket == null)
            {
                return false;
            }

            switch (kind)
            {
                case CasketKind.Survivor:
                    if (Faction.OfAncients == null)
                    {
                        return false;
                    }

                    Accept(casket, MakePawn(Kind("Drifter", "SpaceRefugee_Clothed", "Colonist"), Faction.OfAncients, false));
                    break;
                case CasketKind.Hostile:
                    if (Faction.OfAncientsHostile == null)
                    {
                        return false;
                    }

                    Accept(casket, MakePawn(Kind("AncientSoldier", "Mercenary_Gunner", "Scavenger"), Faction.OfAncientsHostile, false));
                    break;
                case CasketKind.Dead:
                    Corpse corpse = MakeCorpse();
                    if (corpse != null)
                    {
                        Accept(casket, corpse);
                    }
                    break;
            }

            int items = kind == CasketKind.Loot ? Rand.RangeInclusive(2, 4) : Rand.RangeInclusive(0, 2);
            for (int i = 0; i < items; i++)
            {
                Thing loot = RandomLoot();
                if (loot != null && !casket.TryAcceptThing(loot, false))
                {
                    loot.Destroy();
                }
            }

            GenSpawn.Spawn(casket, spot, map, Rot4.North);
            return true;
        }

        private static void Accept(Building_Casket casket, Thing thing)
        {
            if (thing != null && !casket.TryAcceptThing(thing, false) && thing is Pawn pawn)
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
            }
        }

        // A dried-out corpse on the floor with whatever it carried.
        public bool TrySpawnStrayCorpse(PlanRoom room)
        {
            LayoutCell pick = room.Cells.RandomElement();
            IntVec3 cell = new IntVec3(origin.x + pick.X, 0, origin.z + pick.Y);
            if (!isFree(cell))
            {
                return false;
            }

            Corpse corpse = MakeCorpse();
            if (corpse == null)
            {
                return false;
            }

            GenSpawn.Spawn(corpse, cell, map);
            if (Rand.Chance(0.5f))
            {
                Thing loot = RandomLoot();
                if (loot != null)
                {
                    GenPlace.TryPlaceThing(loot, cell, map, ThingPlaceMode.Near);
                }
            }

            return true;
        }
    }
}

