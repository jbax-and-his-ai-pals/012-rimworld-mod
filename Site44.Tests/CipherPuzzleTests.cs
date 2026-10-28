using System;
using System.Collections.Generic;
using System.Linq;
using Site44.Gen;
using Xunit;

namespace Site44.Tests
{
    public class CipherPuzzleTests
    {
        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static readonly int[] Skills = { 0, 10, 20 };

        [Fact]
        public void WordListsAreValid()
        {
            foreach (int len in new[] { 5, 6, 7 })
            {
                var words = CipherPuzzle.WordsFor(len);
                Assert.True(words.Count >= 60, "length " + len + " has " + words.Count);
                Assert.Equal(words.Count, words.Distinct().Count());
                foreach (string w in words)
                {
                    Assert.Equal(len, w.Length);
                    Assert.True(w.All(c => c >= 'A' && c <= 'Z'));
                }
            }
        }

        [Fact]
        public void MatchFunctionHandcrafted()
        {
            Assert.Equal(5, CipherPuzzle.CountMatches("ROTOR", "ROTOR"));
            Assert.Equal(0, CipherPuzzle.CountMatches("ROTOR", "VALVE"));
            Assert.Equal(3, CipherPuzzle.CountMatches("STEAM", "STEEL"));
            Assert.Equal(1, CipherPuzzle.CountMatches("ABCDE", "AXXXX"));
            Assert.Equal(0, CipherPuzzle.CountMatches("ABCDE", "EABCD")); // shifted, not positional
        }

        [Fact]
        public void IsDeterministic()
        {
            for (int seed = 0; seed < 50; seed++)
                for (int tier = 0; tier <= 3; tier++)
                {
                    var a = CipherPuzzle.Create(seed, 7, tier);
                    var b = CipherPuzzle.Create(seed, 7, tier);
                    Assert.Equal(a.Answer, b.Answer);
                    Assert.Equal(a.Candidates.ToArray(), b.Candidates.ToArray());
                    string ra, rb;
                    Assert.Equal(a.TryPurge(out ra), b.TryPurge(out rb));
                    Assert.Equal(ra, rb);
                }
        }

        [Fact]
        public void SizesAndLimitsFollowRules()
        {
            for (int skill = -3; skill <= 25; skill++)
                for (int tier = -1; tier <= 4; tier++)
                {
                    var p = CipherPuzzle.Create(11, skill, tier);
                    int s = Clamp(skill, 0, 20), t = Clamp(tier, 0, 3);
                    Assert.Equal(t <= 1 ? 5 : (t == 2 ? 6 : 7), p.CodeLength);
                    Assert.Equal(Clamp(16 - s / 2 + t, 6, 20), p.Candidates.Count);
                    Assert.Equal(Clamp(4 + s / 5 - (t >= 2 ? 1 : 0), 3, 8), p.AttemptsLeft);
                    Assert.Equal(Clamp(s / 4, 0, 4), p.PurgesLeft);
                    Assert.Equal(CipherOutcome.Continue, p.State);
                    Assert.Empty(p.History);
                    Assert.All(p.Candidates, c => Assert.Equal(p.CodeLength, c.Length));
                }
        }

        [Fact]
        public void NoDuplicatesAndAnswerPresent()
        {
            for (int seed = 0; seed < 100; seed++)
                foreach (int skill in Skills)
                    for (int tier = 0; tier <= 3; tier++)
                    {
                        var p = CipherPuzzle.Create(seed, skill, tier);
                        Assert.Equal(p.Candidates.Count, p.Candidates.Distinct().Count());
                        Assert.Contains(p.Answer, p.Candidates);
                    }
        }

        [Fact]
        public void GuessingAnswerSolves()
        {
            var p = CipherPuzzle.Create(5, 10, 1);
            string answer = p.Answer;
            int before = p.Candidates.Count;
            var r = p.Guess(answer);
            Assert.Equal(CipherOutcome.Solved, r.Outcome);
            Assert.Equal(p.CodeLength, r.Matches);
            Assert.Equal(CipherOutcome.Solved, p.State);
            Assert.Equal(before - 1, p.Candidates.Count);
            Assert.Single(p.History);
            Assert.Throws<InvalidOperationException>(() => p.Guess(p.Candidates[0]));
        }

        [Fact]
        public void WrongGuessMatchesAreCorrectAndCountAttempts()
        {
            var p = CipherPuzzle.Create(9, 10, 0);
            string wrong = p.Candidates.First(c => c != p.Answer);
            int attempts = p.AttemptsLeft;
            var r = p.Guess(wrong);
            int expected = 0;
            for (int i = 0; i < wrong.Length; i++) if (wrong[i] == p.Answer[i]) expected++;
            Assert.Equal(expected, r.Matches);
            Assert.Equal(wrong, r.Code);
            Assert.Equal(CipherOutcome.Continue, r.Outcome);
            Assert.Equal(attempts - 1, p.AttemptsLeft);
            Assert.DoesNotContain(wrong, p.Candidates);
            Assert.Throws<ArgumentException>(() => p.Guess(wrong));
            Assert.Throws<ArgumentException>(() => p.Guess("NOTACODE"));
            Assert.Throws<ArgumentException>(() => p.Guess(null));
        }

        [Fact]
        public void RunningOutOfAttemptsFails()
        {
            var p = CipherPuzzle.Create(3, 0, 2); // 2 attempts
            int n = p.AttemptsLeft;
            CipherGuessResult last = null;
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(CipherOutcome.Continue, p.State);
                last = p.Guess(p.Candidates.First(c => c != p.Answer));
            }
            Assert.Equal(CipherOutcome.Failed, last.Outcome);
            Assert.Equal(CipherOutcome.Failed, p.State);
            Assert.Equal(0, p.AttemptsLeft);
            Assert.Throws<InvalidOperationException>(() => p.Guess(p.Candidates[0]));
            string removed;
            Assert.False(p.TryPurge(out removed));
        }

        [Fact]
        public void PurgeBehaves()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var p = CipherPuzzle.Create(seed, 20, 3);
                int purges = p.PurgesLeft;
                Assert.Equal(4, purges);
                for (int i = 0; i < purges; i++)
                {
                    int before = p.Candidates.Count;
                    string removed;
                    Assert.True(p.TryPurge(out removed));
                    Assert.NotEqual(p.Answer, removed);
                    Assert.DoesNotContain(removed, p.Candidates);
                    Assert.Equal(before - 1, p.Candidates.Count);
                    Assert.Equal(purges - i - 1, p.PurgesLeft);
                    Assert.Contains(p.Answer, p.Candidates);
                }
                string none;
                Assert.False(p.TryPurge(out none));
                Assert.Null(none);
                Assert.Equal(0, p.PurgesLeft);
            }
            // no purges at skill 0
            var q = CipherPuzzle.Create(1, 0, 0);
            string r;
            Assert.False(q.TryPurge(out r));
        }

        [Fact]
        public void PurgePrefersLowMatchCandidates()
        {
            // The first purge always comes from the lower half by match count.
            for (int seed = 0; seed < 100; seed++)
            {
                var p = CipherPuzzle.Create(seed, 8, 0);
                var wrongMatches = p.Candidates.Where(c => c != p.Answer)
                    .Select(c => CipherPuzzle.CountMatches(c, p.Answer)).OrderBy(x => x).ToList();
                int limit = wrongMatches[(wrongMatches.Count + 1) / 2 - 1];
                string removed;
                Assert.True(p.TryPurge(out removed));
                Assert.True(CipherPuzzle.CountMatches(removed, p.Answer) <= limit);
            }
        }

        [Fact]
        public void FeedbackHasSpread()
        {
            for (int seed = 0; seed < 200; seed++)
                foreach (int skill in Skills)
                    for (int tier = 0; tier <= 3; tier++)
                    {
                        var p = CipherPuzzle.Create(seed, skill, tier);
                        var ms = p.Candidates.Where(c => c != p.Answer)
                            .Select(c => CipherPuzzle.CountMatches(c, p.Answer)).ToList();
                        string ctx = "seed " + seed + " skill " + skill + " tier " + tier;
                        Assert.True(ms.Any(m => m <= 1), "no low-match decoy: " + ctx);
                        if (p.Candidates.Count >= 8)
                            Assert.True(ms.Any(m => m >= p.CodeLength - 2), "no near-miss: " + ctx);
                    }
        }

        [Fact]
        public void ConsistentSolverUsuallyWins()
        {
            foreach (int tier in new[] { 0, 1, 2, 3 })
                foreach (int skill in new[] { 10, 20 })
                {
                    int wins = 0;
                    for (int seed = 0; seed < 200; seed++)
                    {
                        var p = CipherPuzzle.Create(seed, skill, tier);
                        while (p.State == CipherOutcome.Continue)
                        {
                            string pick = p.Candidates.First(c => p.History.All(
                                h => CipherPuzzle.CountMatches(c, h.Code) == h.Matches));
                            p.Guess(pick);
                        }
                        if (p.State == CipherOutcome.Solved) wins++;
                    }
                    Assert.True(wins >= 120, "tier " + tier + " skill " + skill + " wins " + wins + "/200");
                }
        }
    }
}
