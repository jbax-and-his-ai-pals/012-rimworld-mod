using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    // Test aid: a colonist in hero mode has full facility clearance, every locked sector open, power armor and a
    // charge rifle, maxed skills, and is healed and fed constantly. Toggle with F7 or the debug menu.
    public static class HeroMode
    {
        public static bool IsHero(Pawn pawn)
        {
            GameComponent_Site44Hotkeys host = Current.Game?.GetComponent<GameComponent_Site44Hotkeys>();
            return host != null && host.Heroes.Contains(pawn);
        }

        public static void ToggleSelected()
        {
            List<Pawn> pawns = Find.Selector.SelectedPawns.Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike).ToList();
            if (pawns.Count == 0)
            {
                Messages.Message("[Site-44] select a colonist first.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                Toggle(pawn);
            }
        }

        public static void Toggle(Pawn pawn)
        {
            GameComponent_Site44Hotkeys host = Current.Game.GetComponent<GameComponent_Site44Hotkeys>();
            if (host.Heroes.Remove(pawn))
            {
                Messages.Message("[Site-44] hero mode off for " + pawn.LabelShort + ".", MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            host.Heroes.Add(pawn);
            Empower(pawn);
            Messages.Message("[Site-44] hero mode ON for " + pawn.LabelShort + ": full clearance, everything unlocked, power armor, near-invincible.", MessageTypeDefOf.PositiveEvent, false);
        }

        private static void Empower(Pawn pawn)
        {
            // Clearance and locks, on every facility map.
            foreach (Map map in Find.Maps)
            {
                MapComponent_Facility facility = map.GetComponent<MapComponent_Facility>();
                if (facility == null || !facility.IsFacilityMap)
                {
                    continue;
                }

                facility.GrantClearance(3);

                foreach (string sectorId in map.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch)
                             .OfType<Building_SealedHatch>().Select(h => h.sectorId).Distinct().ToList())
                {
                    facility.UnsealSector(sectorId);
                }
            }

            // Skills.
            if (pawn.skills != null)
            {
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    if (!skill.TotallyDisabled)
                    {
                        skill.Level = 20;
                    }
                }
            }

            // Armor and weapon.
            if (pawn.apparel != null)
            {
                pawn.apparel.DestroyAll();
                Wear(pawn, "Apparel_PowerArmor");
                Wear(pawn, "Apparel_PowerArmorHelmet");
            }

            if (pawn.equipment != null)
            {
                pawn.equipment.DestroyAllEquipment();
                ThingDef gun = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_ChargeRifle") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
                if (gun != null)
                {
                    ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(gun, gun.MadeFromStuff ? GenStuff.DefaultStuffFor(gun) : null);
                    weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
                    pawn.equipment.AddEquipment(weapon);
                }
            }

            Restore(pawn);
        }

        private static void Wear(Pawn pawn, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            Apparel apparel = (Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
            pawn.apparel.Wear(apparel, false);
        }

        // Called often for every hero: wounds, illness and needs are wiped.
        public static void Restore(Pawn pawn)
        {
            if (pawn.Dead || pawn.health == null)
            {
                return;
            }

            if (pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury || h is Hediff_MissingPart || h.def.makesSickThought || h.def.tendable)
                || pawn.Downed)
            {
                pawn.health.Reset();
            }

            if (pawn.needs != null)
            {
                if (pawn.needs.food != null)
                {
                    pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
                }
                if (pawn.needs.rest != null)
                {
                    pawn.needs.rest.CurLevel = 1f;
                }

                if (pawn.needs.joy != null)
                {
                    pawn.needs.joy.CurLevel = 1f;
                }
            }

            if (pawn.InMentalState)
            {
                pawn.MentalState.RecoverFromState();
            }
        }
    }
}
