using System;

namespace Site44.Gen
{
    // What a level of delve mode asks of the party: how big the complex is and how much it fights back or pays out.
    public sealed class DelveLevel
    {
        public int Depth;
        public string SizeName;            // XS, S, M, L or XL
        public float HostileCasketBonus;   // added to the hostile casket share
        public float LootMultiplier;       // on every loot stack
        public float SecureRoomFactor;     // on the secure room density
        public float DroidGuardChance;     // chance a secure room is guarded by a droid
    }

    public static class DelveDifficulty
    {
        private static readonly string[] Sizes = { "S", "S", "M", "M", "L", "L", "XL" };

        // Depth 1 is the first complex. Complexes grow, hostile cryosleepers become likelier, secure rooms multiply and
        // loot gets richer, up to a cap so a long run stays playable.
        public static DelveLevel For(int depth)
        {
            depth = Math.Max(1, depth);
            int steps = depth - 1;
            return new DelveLevel
            {
                Depth = depth,
                SizeName = Sizes[Math.Min(Sizes.Length - 1, steps)],
                HostileCasketBonus = Math.Min(0.35f, 0.04f * steps),
                LootMultiplier = Math.Min(1.8f, 1f + 0.08f * steps),
                SecureRoomFactor = Math.Min(1.8f, 1f + 0.1f * steps),
                DroidGuardChance = Math.Min(0.9f, 0.35f + 0.08f * steps)
            };
        }
    }
}
