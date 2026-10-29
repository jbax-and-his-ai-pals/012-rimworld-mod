using System;
using System.Collections.Generic;
using System.Linq;

namespace Site44.Gen
{
    public enum ThreatKind { Nest, Security }

    // A place something dangerous may go: a room for a creature nest, or a security sector for a turret and mechanoid group.
    public sealed class ThreatSite
    {
        public int Id;                                   // room id for nests, zone id for security
        public int ZoneId;
        public ThreatKind Kind;
        public List<int> Neighbours = new List<int>();   // adjacent site ids of the same kind (nests are never placed side by side)
    }

    public sealed class ThreatPick
    {
        public int SiteId;
        public ThreatKind Kind;
        public int Tier;                                 // 1 and up
        public float Cost;
    }

    public sealed class ThreatSettings
    {
        public int Depth = 1;
        public float Budget;
        public float NestWeight = 1f;                    // relative share of the budget spent on nests
        public float SecurityWeight = 1f;                // relative share spent on security
        public int MaxNestsPerZone = 2;
        public int MaxNestTier;                          // 0: no extra limit beyond the depth
        public int MaxSecurityTier;                      // 0: no extra limit
    }

    /// <summary>
    /// One budget of danger per level, spent on nests and security. The depth and the Danger setting decide how big the budget
    /// is; the weights decide the mix; whatever cannot be spent (no suitable place, nothing affordable) is simply left unspent.
    /// Pure and deterministic from the seed.
    /// </summary>
    public static class ThreatBudget
    {
        public static readonly float[] NestCosts = { 2.5f, 4f, 6f };
        public static readonly float[] SecurityCosts = { 3.5f, 6f, 9f, 13f };

        // Default split between nests and security before the weights apply.
        private const float NestShare = 0.4f;
        private const float SecurityShare = 0.6f;

        public const float FlatPerDepth = 1f;

        public static float PointsPerRoom(int depth)
        {
            return Math.Min(0.55f, 0.2f + 0.03f * (Math.Max(1, depth) - 1));
        }

        public static float BudgetFor(int depth, int rooms, float danger)
        {
            // A per-room share that grows with depth, plus a flat step per depth so small complexes still get harder.
            return (Math.Max(0, rooms) * PointsPerRoom(depth) + FlatPerDepth * (Math.Max(1, depth) - 1)) * Math.Max(0f, danger);
        }

        public static int NestTierCap(int depth)
        {
            return depth <= 1 ? 1 : depth <= 3 ? 2 : 3;
        }

        public static int SecurityTierCap(int depth)
        {
            return depth <= 1 ? 1 : depth <= 3 ? 2 : depth <= 6 ? 3 : 4;
        }

        public static List<ThreatPick> Plan(int seed, IList<ThreatSite> sites, ThreatSettings settings)
        {
            var picks = new List<ThreatPick>();
            float shareN = NestShare * Math.Max(0f, settings.NestWeight);
            float shareS = SecurityShare * Math.Max(0f, settings.SecurityWeight);
            if (settings.Budget <= 0f || shareN + shareS <= 0f)
            {
                return picks;
            }

            var rng = new FacilityRng(unchecked(seed * 7919 + 13));
            float budgetS = settings.Budget * shareS / (shareN + shareS);
            float budgetN = settings.Budget - budgetS;

            // Security first: a handful of fixed sectors, each funded at roughly its fair share.
            List<ThreatSite> security = Shuffled(sites.Where(s => s.Kind == ThreatKind.Security).ToList(), rng);
            if (shareS > 0f && security.Count > 0)
            {
                int cap = SecurityTierCap(settings.Depth);
                if (settings.MaxSecurityTier > 0)
                {
                    cap = Math.Min(cap, settings.MaxSecurityTier);
                }

                float fairShare = budgetS / security.Count;
                float remaining = budgetS;
                foreach (ThreatSite site in security)
                {
                    int tier = 0;
                    for (int t = 0; t < Math.Min(cap, SecurityCosts.Length); t++)
                    {
                        if (SecurityCosts[t] <= remaining && (t == 0 || SecurityCosts[t] <= fairShare + 0.001f))
                        {
                            tier = t + 1;
                        }
                    }

                    if (tier == 0)
                    {
                        continue;
                    }

                    picks.Add(new ThreatPick { SiteId = site.Id, Kind = ThreatKind.Security, Tier = tier, Cost = SecurityCosts[tier - 1] });
                    remaining -= SecurityCosts[tier - 1];
                }

                budgetN += remaining;   // unspent security goes to the nests
            }
            else
            {
                budgetN += budgetS;
            }

            // Nests: one pass over shuffled rooms, never side by side, never too many in one zone.
            if (shareN > 0f || budgetN > 0f)
            {
                int nestCap = Math.Min(NestTierCap(settings.Depth), NestCosts.Length);
                if (settings.MaxNestTier > 0)
                {
                    nestCap = Math.Min(nestCap, settings.MaxNestTier);
                }

                var chosen = new HashSet<int>();
                var perZone = new Dictionary<int, int>();
                foreach (ThreatSite site in Shuffled(sites.Where(s => s.Kind == ThreatKind.Nest).ToList(), rng))
                {
                    if (budgetN < NestCosts[0])
                    {
                        break;
                    }

                    int inZone;
                    perZone.TryGetValue(site.ZoneId, out inZone);
                    if (shareN <= 0f || inZone >= settings.MaxNestsPerZone || site.Neighbours.Any(chosen.Contains))
                    {
                        continue;
                    }

                    int tier = 1 + rng.Next(nestCap);
                    while (tier > 1 && NestCosts[tier - 1] > budgetN)
                    {
                        tier--;
                    }

                    budgetN -= NestCosts[tier - 1];
                    picks.Add(new ThreatPick { SiteId = site.Id, Kind = ThreatKind.Nest, Tier = tier, Cost = NestCosts[tier - 1] });
                    chosen.Add(site.Id);
                    perZone[site.ZoneId] = inZone + 1;
                }
            }

            // A level that can afford a threat at all gets at least one: a nest if nests are wanted, otherwise a security sector.
            if (picks.Count == 0)
            {
                List<ThreatSite> nestSites = Shuffled(sites.Where(s => s.Kind == ThreatKind.Nest).ToList(), rng);
                List<ThreatSite> securitySites = Shuffled(sites.Where(s => s.Kind == ThreatKind.Security).ToList(), rng);
                if (shareN > 0f && nestSites.Count > 0 && settings.Budget >= NestCosts[0])
                {
                    picks.Add(new ThreatPick { SiteId = nestSites[0].Id, Kind = ThreatKind.Nest, Tier = 1, Cost = NestCosts[0] });
                }
                else if (shareS > 0f && securitySites.Count > 0 && settings.Budget >= SecurityCosts[0])
                {
                    picks.Add(new ThreatPick { SiteId = securitySites[0].Id, Kind = ThreatKind.Security, Tier = 1, Cost = SecurityCosts[0] });
                }
            }

            return picks.OrderBy(p => p.Kind).ThenBy(p => p.SiteId).ToList();
        }

        private static List<ThreatSite> Shuffled(List<ThreatSite> list, FacilityRng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                ThreatSite tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }

            return list;
        }
    }
}
