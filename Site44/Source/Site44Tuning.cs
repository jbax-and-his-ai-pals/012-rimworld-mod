using UnityEngine;

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
        public static float OpenLayoutLockedShare => S.lockedVaultShare;

        // Turrets, patrol mechs and terminal alarms.
        public static bool SecurityEnabled => S.securityEnabled;

        // Overheated sectors in the gated layout.
        public static bool HotZonesEnabled => S.hotZonesEnabled;

        // Share of ordinary side rooms that survived intact: clean sterile floor, undamaged furniture, better loot.
        public static float PreservedRoomChance => S.preservedRoomChance;

        // Multiplier on every loot stack placed in the complex.
        public static float LootAmount => S.lootAmount;

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
        public static float HostileCasketShare => S.hostileCasketShare;

        // Share of rooms holding a dried-out corpse (with loot half the time).
        public static float StrayCorpseChance => Mathf.Clamp01(S.strayCorpseChance * (T != null ? T.strayCorpseFactor : 1f));

        // Special rooms: rec rooms are uncommon; secure rooms are locked dead ends with one surprise each.
        public static float RecRoomFrequency => S.recRoomFrequency;
        public static float SecureRoomDensity => S.secureRoomDensity * (T != null ? T.secureRoomFactor : 1f);
        public static bool DroidGuards => S.droidGuards;
        public static float PropDensity => S.propDensity;
        public static float ClutterFactor => T != null ? T.clutterFactor : 1f;
        public static float RoomVariety => S.roomVariety;

        // Furniture hit points as a fraction of maximum in ruined rooms: ruin 0 is pristine, 1 is wrecked.
        public static float RuinedFurnitureMinHealth => Mathf.Lerp(0.8f, 0.05f, S.ruinLevel);
        public static float RuinedFurnitureMaxHealth => Mathf.Lerp(1.0f, 0.45f, S.ruinLevel);
    }
}
