using System;
using System.Collections.Generic;
using System.Linq;

namespace Site44.Gen
{
    public class StoryCandidate
    {
        public string Name;
        public List<string> SiteTypes = new List<string>();   // empty = suits any complex
    }

    // Chooses which thread a complex tells. Threads written for the kind of complex are favoured, a thread is not repeated
    // within a run until every suitable one has been told, and then the run starts over.
    public static class StoryPicker
    {
        public const int SpecificWeight = 3;

        public static string Pick(IList<StoryCandidate> all, ICollection<string> used, string siteType, int seed, out bool reset)
        {
            reset = false;
            List<StoryCandidate> eligible = all.Where(c => c.SiteTypes.Count == 0 || (siteType != null && c.SiteTypes.Contains(siteType))).ToList();
            if (eligible.Count == 0)
            {
                return null;
            }

            List<StoryCandidate> fresh = eligible.Where(c => !used.Contains(c.Name)).ToList();
            if (fresh.Count == 0)
            {
                reset = true;
                fresh = eligible;
            }

            int total = fresh.Sum(c => c.SiteTypes.Count == 0 ? 1 : SpecificWeight);
            int roll = new Random(seed).Next(total);
            foreach (StoryCandidate c in fresh)
            {
                roll -= c.SiteTypes.Count == 0 ? 1 : SpecificWeight;
                if (roll < 0)
                {
                    return c.Name;
                }
            }

            return fresh[fresh.Count - 1].Name;
        }
    }
}
