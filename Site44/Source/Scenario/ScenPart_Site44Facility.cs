using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Site44
{
    // Marks the scenario for GenStep_Facility, and kits out each starting pawn like a scavenger:
    // one cheap weapon matched to their stronger combat skill, and now and then a better piece.
    public class ScenPart_Site44Facility : ScenPart
    {
        private static readonly KeyValuePair<string, float>[] RangedWeapons =
        {
            new KeyValuePair<string, float>("Gun_Revolver", 40f),
            new KeyValuePair<string, float>("Gun_Autopistol", 24f),
            new KeyValuePair<string, float>("Bow_Short", 16f),
            new KeyValuePair<string, float>("Gun_PumpShotgun", 12f),
            new KeyValuePair<string, float>("Gun_BoltActionRifle", 8f)
        };

        private static readonly KeyValuePair<string, float>[] MeleeWeapons =
        {
            new KeyValuePair<string, float>("MeleeWeapon_Knife", 34f),
            new KeyValuePair<string, float>("MeleeWeapon_Club", 30f),
            new KeyValuePair<string, float>("MeleeWeapon_Spear", 16f),
            new KeyValuePair<string, float>("MeleeWeapon_Mace", 14f),
            new KeyValuePair<string, float>("MeleeWeapon_Gladius", 6f)
        };

        // Mostly poor to normal; good gear is rare.
        private static readonly KeyValuePair<QualityCategory, float>[] Qualities =
        {
            new KeyValuePair<QualityCategory, float>(QualityCategory.Awful, 10f),
            new KeyValuePair<QualityCategory, float>(QualityCategory.Poor, 30f),
            new KeyValuePair<QualityCategory, float>(QualityCategory.Normal, 42f),
            new KeyValuePair<QualityCategory, float>(QualityCategory.Good, 14f),
            new KeyValuePair<QualityCategory, float>(QualityCategory.Excellent, 4f)
        };

        public override string Summary(Scenario scen)
        {
            return "Scavengers: each of you starts with a modest weapon suited to your skills.";
        }

        public override void Notify_PawnGenerated(Pawn pawn, PawnGenerationContext context, bool redressed)
        {
            if (context == PawnGenerationContext.PlayerStarter)
            {
                EquipWeapon(pawn, false);
                EquipRareArmor(pawn);
            }
        }

        // Safety net: if anything stripped the generated weapon before the game began, hand it out again.
        public override void PostGameStart()
        {
            EquipUnarmedColonists();
        }

        public static void EquipUnarmedColonists()
        {
            int equipped = 0;
            List<string> report = new List<string>();
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
            {
                if (pawn.equipment != null && pawn.equipment.Primary == null && EquipWeapon(pawn, true))
                {
                    equipped++;
                }

                report.Add(pawn.LabelShort + ": " + (pawn.equipment?.Primary?.LabelCap.ToString() ?? "unarmed"));
            }

            Log.Message("[Site-44] starting gear: " + equipped + " re-equipped at game start; " + string.Join(", ", report.ToArray()));
        }

        // Picks the stronger of Shooting and Melee (random on a tie); pawns incapable of violence stay unarmed.
        private static bool EquipWeapon(Pawn pawn, bool onlyIfUnarmed)
        {
            if (pawn.equipment == null || pawn.skills == null || !pawn.RaceProps.Humanlike || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }

            if (onlyIfUnarmed && pawn.equipment.Primary != null)
            {
                return false;
            }

            SkillRecord shooting = pawn.skills.GetSkill(SkillDefOf.Shooting);
            SkillRecord melee = pawn.skills.GetSkill(SkillDefOf.Melee);
            bool canShoot = !shooting.TotallyDisabled;
            bool canMelee = !melee.TotallyDisabled;
            if (!canShoot && !canMelee)
            {
                return false;
            }

            bool ranged;
            if (canShoot && canMelee)
            {
                ranged = shooting.Level == melee.Level ? Rand.Bool : shooting.Level > melee.Level;
            }
            else
            {
                ranged = canShoot;
            }

            ThingDef weaponDef = Pick(ranged ? RangedWeapons : MeleeWeapons);
            if (weaponDef == null)
            {
                Log.Warning("[Site-44] no starting weapon definition found.");
                return false;
            }

            pawn.equipment.DestroyAllEquipment();
            ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(weaponDef, weaponDef.MadeFromStuff ? GenStuff.RandomStuffByCommonalityFor(weaponDef) : null);
            CompQuality quality = weapon.TryGetComp<CompQuality>();
            if (quality != null)
            {
                quality.SetQuality(PickQuality(), ArtGenerationContext.Outsider);
            }

            pawn.equipment.AddEquipment(weapon);
            return true;
        }

        // Once in a while someone has a scrap of real protection.
        private static void EquipRareArmor(Pawn pawn)
        {
            if (pawn.apparel == null || !Rand.Chance(0.10f)
                || pawn.apparel.WornApparel.Any(a => a.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)))
            {
                return;
            }

            ThingDef vest = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_FlakVest");
            if (vest != null)
            {
                Apparel apparel = (Apparel)ThingMaker.MakeThing(vest, vest.MadeFromStuff ? GenStuff.RandomStuffByCommonalityFor(vest) : null);
                pawn.apparel.Wear(apparel, false);
            }
        }
        private static ThingDef Pick(KeyValuePair<string, float>[] table)
        {
            KeyValuePair<string, float> choice;
            if (!table.Where(p => DefDatabase<ThingDef>.GetNamedSilentFail(p.Key) != null).TryRandomElementByWeight(p => p.Value, out choice))
            {
                return null;
            }

            return DefDatabase<ThingDef>.GetNamedSilentFail(choice.Key);
        }

        private static QualityCategory PickQuality()
        {
            return Qualities.RandomElementByWeight(p => p.Value).Key;
        }
    }
}


