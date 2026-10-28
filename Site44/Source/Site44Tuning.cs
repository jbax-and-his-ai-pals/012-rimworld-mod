using RimWorld;
using UnityEngine;
using Verse;

namespace Site44
{
    // Every balance and look knob in one place. Player-facing ones read from Site44Settings;
    // the rest are internal constants.
    public static class Site44Tuning
    {
        private static Site44Settings S => Site44Settings.Current;

        // Flavour multipliers of the type being generated (1 when none).
        private static SiteTypeDef T => SiteTypes.Current;

        // Open layout: every door is a plain door and the whole complex can be explored freely. A share of the
        // gates stays locked behind terminals as optional vaults. Off = the gated progression layout.
        public static bool OpenLayout => S.openLayout;
        public static float OpenLayoutLockedShare => Mathf.Clamp01(0.25f * S.lockedVaultX);

        // Turrets, patrol mechs and terminal alarms.
        public static bool SecurityEnabled => S.securityEnabled;

        // Overheated sectors in the gated layout.
        public static bool HotZonesEnabled => S.hotZonesEnabled;

        // Share of ordinary side rooms that survived intact: clean sterile floor, undamaged furniture, better loot.
        public static float PreservedRoomChance => Mathf.Clamp01(0.07f * S.preservedRoomX);

        // Multiplier on every loot stack placed in the complex.
        // Delve levels up to this depth avoid extreme climates when the biome pool allows.
        public const int MildLevels = 3;

        // Chance that a terminal-locked gate also has a keycard on the near side, and that the card is guarded by a nest.
        public static float KeycardChance => Mathf.Clamp01(0.5f * S.keycardX);

        // Share of medicine and meal picks kept at x1 supply, and the bait item chances at x1.
        public static float SupplyShare => Mathf.Clamp01(0.25f * S.supplyX);
        public static float DrugFindChance => Mathf.Clamp01(0.15f * S.drugFindX);
        public static float DelverGearChance => Mathf.Clamp01(0.12f * S.delverGearX);
        public static float RestrictedWingChance => Mathf.Clamp01(0.75f * S.restrictedWingX);
        public const float KeycardNestChance = 0.7f;

        // Chance that a diagnostic or hatch node carries a bonus action.
        public const float NodeExtraChance = 0.2f;

        // Chance that an ordinary room is given a beat (crate, body, casket or nest) when it is not forced to.
        public const float BeatChance = 0.6f;

        // Medicine and meals are scarce: most picks of them are dropped, and the stacks that remain are small.
        public static bool IsConsumable(ThingDef def)
        {
            return def != null && (def.IsMedicine || (def.ingestible != null && (def.ingestible.foodType & FoodTypeFlags.Meal) != 0));
        }

        public static bool KeepSupply(ThingDef def)
        {
            return !IsConsumable(def) || Rand.Chance(SupplyShare);
        }

        public static int ScarceCount(ThingDef def, int count)
        {
            return IsConsumable(def) ? Mathf.Min(count, 1 + Mathf.RoundToInt(8f * SupplyShare)) : count;
        }

        public static float LootAmount => S.lootAmount * (GameComponent_Delve.Level?.LootMultiplier ?? 1f);

        // Share of floor cells carrying dust or debris in ruined rooms and in corridors.
        public static float DustChanceRoom => Mathf.Clamp01(0.30f * S.dustAmount * (T != null ? T.dustFactor : 1f));
        public static float DustChanceCorridor => Mathf.Clamp01(0.14f * S.dustAmount * (T != null ? T.dustFactor : 1f));

        // Loot crates: chance per room, higher in storerooms, archives, server rooms and labs.
        public static float CrateChanceStock => Mathf.Clamp01(0.30f * S.crateDensity * (T != null ? T.crateFactor : 1f));
        public static float CrateChanceOther => Mathf.Clamp01(0.05f * S.crateDensity * (T != null ? T.crateFactor : 1f));

        // Cryosleep caskets: about one per this many rooms (0 = none); survivors are capped, hostiles are a share.
        public static float CasketDensity => S.casketDensity * (T != null ? T.casketFactor : 1f);
        public const int RoomsPerCasket = 35;
        public static int SurvivorCaskets => S.survivorCaskets;
        public static float HostileCasketShare => S.hostileCasketX <= 0f ? 0f : Mathf.Clamp01(0.25f * S.hostileCasketX + (GameComponent_Delve.Level?.HostileCasketBonus ?? 0f));

        // Share of rooms holding a dried-out corpse (with loot half the time).
        public static float StrayCorpseChance => Mathf.Clamp01(0.04f * S.strayCorpseX * (T != null ? T.strayCorpseFactor : 1f));

        // Special rooms: rec rooms are uncommon; secure rooms are locked dead ends with one surprise each.
        public static float RecRoomFrequency => S.recRoomFrequency;
        public static float SecureRoomDensity => S.secureRoomDensity * (T != null ? T.secureRoomFactor : 1f) * (GameComponent_Delve.Level?.SecureRoomFactor ?? 1f);
        public static float DroidGuardChance => GameComponent_Delve.Level?.DroidGuardChance ?? 0.35f;
        public static bool DroidGuards => S.droidGuards;
        public static float PropDensity => S.propDensity;
        public static float ClutterFactor => T != null ? T.clutterFactor : 1f;
        public static float RoomVariety => S.roomVariety;

        // When more than this share of a building's floor has lost its roof, the open floor is weathered.
        public const float OpenRoofShare = 0.3f;

        // Furniture hit points as a fraction of maximum in ruined rooms: ruin 0 is pristine, 1 is wrecked.
        public static float RuinedFurnitureMinHealth => Mathf.Lerp(0.8f, 0.05f, Mathf.Clamp01(0.75f * S.ruinX));
        public static float RuinedFurnitureMaxHealth => Mathf.Lerp(1.0f, 0.45f, Mathf.Clamp01(0.75f * S.ruinX));
    }
}
