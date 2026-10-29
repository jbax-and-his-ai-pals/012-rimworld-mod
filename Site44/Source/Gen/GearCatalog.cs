using System;
using System.Collections.Generic;
using System.Linq;

namespace Site44.Gen
{
    public class GearPiece
    {
        public string Name;
        public float Value;     // insulation points
        public string Slot;     // shell, torso, legs or head: pieces in one slot do not stack
        public int MinDepth;    // the first depth it can turn up as loot
    }

    // The temperature gear the mod places, kept as numbers so that the hardest room can be checked against the best set and the
    // gear left at the entrance can be sized to the rooms of the level.
    public static class GearCatalog
    {
        public static readonly string[] Slots = { "shell", "torso", "legs", "head" };

        public static readonly GearPiece[] Cold =
        {
            new GearPiece { Name = "Site44_ThermalWrap", Value = 22, Slot = "shell", MinDepth = 1 },
            new GearPiece { Name = "Site44_PolarShell", Value = 52, Slot = "shell", MinDepth = 3 },
            new GearPiece { Name = "Site44_CryoShell", Value = 62, Slot = "shell", MinDepth = 8 },
            new GearPiece { Name = "Site44_ThermalVest", Value = 22, Slot = "torso", MinDepth = 1 },
            new GearPiece { Name = "Site44_ThermalLeggings", Value = 15, Slot = "legs", MinDepth = 1 },
            new GearPiece { Name = "Site44_FrostLeggings", Value = 22, Slot = "legs", MinDepth = 8 },
            new GearPiece { Name = "Site44_ThermalHood", Value = 10, Slot = "head", MinDepth = 1 },
            new GearPiece { Name = "Site44_FrostHood", Value = 16, Slot = "head", MinDepth = 8 }
        };

        public static readonly GearPiece[] Heat =
        {
            new GearPiece { Name = "Site44_HeatShell", Value = 45, Slot = "shell", MinDepth = 3 },
            new GearPiece { Name = "Site44_FurnaceShell", Value = 62, Slot = "shell", MinDepth = 8 },
            new GearPiece { Name = "Site44_CoolingVest", Value = 22, Slot = "torso", MinDepth = 1 },
            new GearPiece { Name = "Site44_CoolingLeggings", Value = 15, Slot = "legs", MinDepth = 1 },
            new GearPiece { Name = "Site44_EmberLeggings", Value = 22, Slot = "legs", MinDepth = 8 },
            new GearPiece { Name = "Site44_CoolingHood", Value = 10, Slot = "head", MinDepth = 1 },
            new GearPiece { Name = "Site44_VentHood", Value = 16, Slot = "head", MinDepth = 8 }
        };

        public static GearPiece[] For(bool hot)
        {
            return hot ? Heat : Cold;
        }

        // Insulation of the best piece in every slot: what a fully dedicated character wears.
        public static float BestSet(bool hot, int depth = int.MaxValue)
        {
            return Slots.Sum(s => For(hot).Where(p => p.Slot == s && p.MinDepth <= depth).Select(p => p.Value).DefaultIfEmpty(0f).Max());
        }

        // Pieces to leave near the entrance for a hazard that needs "needed" insulation: the smallest single piece that reaches the
        // share of it, or else the biggest pieces from distinct slots until they do. Only pieces that can exist at this depth.
        public static List<GearPiece> Entry(bool hot, int depth, float needed, float share)
        {
            float target = Math.Max(1f, needed * share);
            List<GearPiece> available = For(hot).Where(p => p.MinDepth <= depth).ToList();
            GearPiece single = available.Where(p => p.Value >= target).OrderBy(p => p.Value).FirstOrDefault();
            if (single != null)
            {
                return new List<GearPiece> { single };
            }

            var chosen = new List<GearPiece>();
            float sum = 0f;
            foreach (GearPiece piece in available.OrderByDescending(p => p.Value))
            {
                if (chosen.Any(c => c.Slot == piece.Slot))
                {
                    continue;
                }

                chosen.Add(piece);
                sum += piece.Value;
                if (sum >= target)
                {
                    break;
                }
            }

            return chosen;
        }
    }
}
