using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    public enum CipherOutcome { Continue, Solved, Failed }

    public sealed class CipherGuessResult
    {
        public string Code;
        public int Matches;               // characters equal to the answer's at the same position
        public CipherOutcome Outcome;     // state after this guess
    }

    /// <summary>
    /// Pure-logic "cipher lock" puzzle: pick the access code out of a list of look-alike candidates.
    /// Every wrong guess reports how many characters sit in the correct position.
    /// </summary>
    public sealed class CipherPuzzle
    {
        // ---------------------------------------------------------------- PRNG
        private sealed class Rng
        {
            private uint s;
            public Rng(int seed)
            {
                uint z = unchecked((uint)seed + 0x9E3779B9u);
                z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu);
                z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
                z ^= z >> 16;
                s = z == 0 ? 0x1234567u : z;
                for (int i = 0; i < 4; i++) NextU();
            }
            public uint NextU()
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                return s;
            }
            public int Next(int n) { return n <= 1 ? 0 : (int)(NextU() % (uint)n); }
            public void Shuffle<T>(IList<T> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = Next(i + 1);
                    T t = list[i]; list[i] = list[j]; list[j] = t;
                }
            }
        }

        // ---------------------------------------------------------------- word lists
        private const string Words5 =
            "ROTOR VALVE RELAY BOLTS GEARS CABLE WIRES PANEL LEVER PUMPS FUSES DIODE TIMER METER BATCH CACHE " +
            "CLOCK CRANK DRIVE FLASH FLUID FRAME GAUGE GRILL HINGE INPUT LASER LOGIC MOTOR NODES PIXEL PLATE " +
            "PLUGS POWER PRESS PROBE QUEUE RADAR RIVET ROUTE SHAFT SHIFT SLOTS SOLAR SPOOL STACK STEAM STEEL " +
            "STORE SWEEP TABLE TRACE TUBES UNITS VAULT VENTS VOLTS WATTS WHEEL TAPES DRAIN BLADE BRAKE CHAIN " +
            "CHIPS CODEC DRILL FORMS FILES DESKS PAPER STAMP INDEX LABEL LINKS LOCKS MACRO PORTS SCALE SCREW " +
            "SEALS SPARK TOOLS WELDS TABLE BLOCK CLOAK SALVE METES WIRED FUSED STEAL STEEP STEER PLANE SLATE " +
            "PLACE BLAZE SHAFT SHAFTS LEVEL LEVEE PANES PANIC TIMES TIMED BOLTED CRATE GRATE GRADE TRADE " +
            "BLEND BLOWN FLOOR FLOOD STAGE STAKE STALK STAMP SHELF SHELL SHIFT SWIPE SWITCH";

        private const string Words6 =
            "PISTON SENSOR FILTER CLUTCH TOGGLE WRENCH BINDER ENGINE GASKET HOPPER NOZZLE SOCKET SWITCH BUFFER " +
            "COOLER DAMPER DRIVER FLANGE FUSION HEATER KEYPAD LADDER MODULE OUTLET PACKET PULLEY RELAYS ROUTER " +
            "SERVER SIGNAL SPINDLE SPRING STAPLE STATOR SYSTEM THREAD TOWERS TUNNEL VOLUME WIDGET WINDOW WIRING " +
            "BRIDGE CAMERA CARTON COUPLE CURSOR DEVICE DOCKET ENCODE FOLDER GROUND HEADER LAYOUT LOADER MEMORY " +
            "METERS NEEDLE OFFICE PRINTS REPORT SCREEN TICKET TIMERS STITCH POCKET JACKET BUCKET ROCKET PICKET " +
            "BUFFET FILLER HEALER SERVES DRIVEN LEADER READER HOLDER PULLED MOTORS SENSED SIGNED LOADED LOADEN " +
            "BOLTED GEARED CABLED PANELS VALVES FUSING PLUGIN DIALER SPOOLS";

        private const string Words7 =
            "BEARING TURBINE CIRCUIT FIXTURE BRACKET CABINET PLUNGER SILICON PRINTER STORAGE ADAPTER ANTENNA " +
            "BATTERY BOILERS BUFFERS CHASSIS CONDUIT CONSOLE CONTROL COUNTER COUPLER DISPLAY DRAWERS ENCODER " +
            "EXHAUST FILTERS FURNACE GRINDER HANDLER HARNESS INSTALL LOCKERS MACHINE MAGNETS MONITOR NETWORK " +
            "OUTPUTS PACKAGE PROCESS PROJECT RECEIPT RECORDS RELEASE REPAIRS RESERVE SCANNER SENSORS SERVICE " +
            "SHUTTER SPINNER STARTER STATION THERMAL TRACKER TRANSIT TRIGGER VENTING VOLTAGE WARNING WELDING " +
            "WRAPPER ARCHIVE BALANCE BOOSTER BREAKER CHANNEL CLOSURE COMMAND COOLANT DIAGRAM FEEDERS ADAPTOR " +
            "COUNTED COUPLED WRAPPED REPAIRED STARTED TRACKED CONDUCT CONSULT ROOSTER GRINNER HANDLES FILTERED " +
            "PACKAGE BEAMING SEALANT PRESSES";

        private static readonly string[][] pools = BuildPools();

        private static string[][] BuildPools()
        {
            var result = new string[8][];
            result[5] = Parse(Words5, 5);
            result[6] = Parse(Words6, 6);
            result[7] = Parse(Words7, 7);
            return result;
        }

        private static string[] Parse(string text, int length)
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
            foreach (string w in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (w.Length != length) continue;
                if (seen.Add(w)) list.Add(w);
            }
            return list.ToArray();
        }

        /// <summary>Distinct, validated word list for a code length (5..7). Exposed for tests.</summary>
        internal static IReadOnlyList<string> WordsFor(int length)
        {
            return pools[length];
        }

        internal static int CountMatches(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length), m = 0;
            for (int i = 0; i < n; i++) if (a[i] == b[i]) m++;
            return m;
        }

        // ---------------------------------------------------------------- state
        private readonly List<string> candidates = new List<string>();
        private readonly List<CipherGuessResult> history = new List<CipherGuessResult>();
        private readonly Rng rng;
        private readonly string answer;
        private int attemptsLeft;
        private int purgesLeft;
        private CipherOutcome state = CipherOutcome.Continue;

        public int CodeLength { get; private set; }
        public IReadOnlyList<string> Candidates { get { return candidates; } }
        public int AttemptsLeft { get { return attemptsLeft; } }
        public int PurgesLeft { get { return purgesLeft; } }
        public CipherOutcome State { get { return state; } }
        public IReadOnlyList<CipherGuessResult> History { get { return history; } }
        public string Answer { get { return answer; } }

        private CipherPuzzle(string answer, Rng rng) { this.answer = answer; this.rng = rng; }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static CipherPuzzle Create(int seed, int hackerSkill, int tier)
        {
            int skill = Clamp(hackerSkill, 0, 20);
            tier = Clamp(tier, 0, 3);
            int length = tier <= 1 ? 5 : (tier == 2 ? 6 : 7);
            int count = Clamp(16 - skill / 2 + tier, 6, 20);

            var rng = new Rng(unchecked(seed + skill * 7919 + tier * 104729));
            string[] pool = pools[length];

            // Choose an answer that has a near-miss partner in the list when the list allows it.
            int[] best = new int[pool.Length];
            int globalBest = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                int b = 0;
                for (int j = 0; j < pool.Length; j++)
                {
                    if (i == j) continue;
                    int m = CountMatches(pool[i], pool[j]);
                    if (m < length && m > b) b = m;
                }
                best[i] = b;
                if (b > globalBest) globalBest = b;
            }
            int threshold = Math.Min(length - 2, globalBest);
            var answerIdx = new List<int>();
            for (int i = 0; i < pool.Length; i++) if (best[i] >= threshold) answerIdx.Add(i);
            string answer = pool[answerIdx[rng.Next(answerIdx.Count)]];

            var puzzle = new CipherPuzzle(answer, rng);
            puzzle.CodeLength = length;
            puzzle.attemptsLeft = Clamp(3 + skill / 5 - (tier >= 2 ? 1 : 0), 2, 7);
            puzzle.purgesLeft = Clamp(skill / 4, 0, 4);

            // Bucket decoys by positional matches.
            var zero = new List<string>();
            var low = new List<string>();
            var mid = new List<string>();
            var near = new List<string>();
            foreach (string w in pool)
            {
                if (w == answer) continue;
                int m = CountMatches(w, answer);
                if (m >= length) continue;
                if (m == 0) zero.Add(w);
                else if (m >= length - 2) near.Add(w);
                else if (m <= 2) low.Add(w);
                else mid.Add(w);
            }
            rng.Shuffle(zero); rng.Shuffle(low); rng.Shuffle(mid);
            // Prefer the closest near-misses by shuffling then stable-sorting nothing: any is fine, but keep strongest first-ish.
            rng.Shuffle(near);

            int need = count - 1;
            var picked = new List<string>();
            var used = new HashSet<string>();
            Action<List<string>, int> take = (bucket, n) =>
            {
                for (int i = 0; i < bucket.Count && n > 0 && picked.Count < need; i++)
                {
                    if (used.Add(bucket[i])) { picked.Add(bucket[i]); n--; }
                }
            };

            int nNear = count >= 12 ? 2 : 1;
            int nZero = Math.Max(1, (need + 3) / 4);
            int nLow = Math.Max(1, (need * 35 + 99) / 100);
            take(near, nNear);
            take(zero, nZero);
            take(low, nLow);
            take(mid, need);        // fill with mid-range
            take(low, need);        // then leftovers, closest-to-answer last
            take(zero, need);
            take(near, need);

            var all = new List<string>(picked);
            all.Add(answer);
            rng.Shuffle(all);
            puzzle.candidates.AddRange(all);
            return puzzle;
        }

        public CipherGuessResult Guess(string code)
        {
            if (state != CipherOutcome.Continue)
                throw new InvalidOperationException("The puzzle is already finished.");
            int idx = code == null ? -1 : candidates.IndexOf(code);
            if (idx < 0) throw new ArgumentException("Code is not a selectable candidate.", "code");

            candidates.RemoveAt(idx);
            var result = new CipherGuessResult { Code = code, Matches = CountMatches(code, answer) };
            if (code == answer)
            {
                state = CipherOutcome.Solved;
            }
            else
            {
                attemptsLeft--;
                if (attemptsLeft <= 0) { attemptsLeft = 0; state = CipherOutcome.Failed; }
            }
            result.Outcome = state;
            history.Add(result);
            return result;
        }

        public bool TryPurge(out string removed)
        {
            removed = null;
            if (state != CipherOutcome.Continue || purgesLeft <= 0) return false;

            var wrong = new List<string>();
            foreach (string c in candidates) if (c != answer) wrong.Add(c);
            if (wrong.Count == 0) return false;

            // Stable sort by ascending match count; choose among the lower half.
            var order = new List<KeyValuePair<int, string>>();
            foreach (string w in wrong) order.Add(new KeyValuePair<int, string>(CountMatches(w, answer), w));
            for (int i = 1; i < order.Count; i++)
            {
                var item = order[i];
                int j = i - 1;
                while (j >= 0 && order[j].Key > item.Key) { order[j + 1] = order[j]; j--; }
                order[j + 1] = item;
            }
            int pool = (order.Count + 1) / 2;
            removed = order[rng.Next(pool)].Value;
            candidates.Remove(removed);
            purgesLeft--;
            return true;
        }
    }
}
