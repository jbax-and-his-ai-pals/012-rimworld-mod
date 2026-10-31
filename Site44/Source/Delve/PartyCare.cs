using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Site44
{
    // The party's shared supplies. Medicine carried by any colonist is pooled here, and whoever tends draws what the patient needs
    // from the pool, so nobody has to carry their own. The pool is saved with the game and travels down every level.
    public class GameComponent_PartyStash : GameComponent
    {
        private const int PoolInterval = 600;

        private List<Thing> items = new List<Thing>();

        public GameComponent_PartyStash(Game game)
        {
        }

        public static GameComponent_PartyStash Instance => Verse.Current.Game?.GetComponent<GameComponent_PartyStash>();

        public static bool Enabled => GameComponent_Delve.IsDelve && Site44Settings.Current.sharedStash;

        public IEnumerable<Thing> Items => items;

        public int Count(ThingDef def)
        {
            return items.Where(t => t.def == def).Sum(t => t.stackCount);
        }

        public void Add(Thing thing)
        {
            if (thing == null || thing.stackCount <= 0)
            {
                return;
            }

            foreach (Thing existing in items)
            {
                if (existing.def == thing.def && existing.CanStackWith(thing))
                {
                    existing.TryAbsorbStack(thing, true);
                    if (thing.Destroyed || thing.stackCount <= 0)
                    {
                        return;
                    }
                }
            }

            items.Add(thing);
        }

        // Up to "count" of the best medicine the patient's care setting allows, or null when the pool has none.
        public Thing TakeMedicine(Pawn patient, int count)
        {
            if (count <= 0 || (patient.playerSettings != null && (int)patient.playerSettings.medCare <= 1))
            {
                return null;
            }

            Thing best = items
                .Where(t => t.def.IsMedicine && (patient.playerSettings == null || patient.playerSettings.medCare.AllowsMedicine(t.def)))
                .OrderByDescending(t => t.def.GetStatValueAbstract(StatDefOf.MedicalPotency))
                .FirstOrDefault();
            if (best == null)
            {
                return null;
            }

            Thing taken = best.SplitOff(Math.Min(count, best.stackCount));
            if (ReferenceEquals(taken, best) || best.stackCount <= 0)
            {
                items.Remove(best);
            }

            return taken;
        }

        // Moves medicine out of colonists' packs into the pool.
        public int PoolFromParty()
        {
            int moved = 0;
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
            {
                if (pawn.inventory == null)
                {
                    continue;
                }

                foreach (Thing medicine in pawn.inventory.innerContainer.Where(t => t.def.IsMedicine).ToList())
                {
                    int count = medicine.stackCount;
                    if (pawn.inventory.innerContainer.Remove(medicine))
                    {
                        Add(medicine);
                        moved += count;
                    }
                }
            }

            return moved;
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % PoolInterval == 0 && Enabled)
            {
                PoolFromParty();
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref items, "items", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                items = (items ?? new List<Thing>()).Where(t => t != null && !t.Destroyed).ToList();
            }
        }
    }

    // One-press care for a party that is drafted most of the time: each wounded colonist is sent a doctor, worst bleeding first,
    // using the same ordered "tend" job the base game gives a drafted doctor, but with medicine drawn from the shared pool (or
    // fetched from the ground) instead of only what the doctor happens to carry.
    public static class PartyCare
    {
        public static string LastReport;

        public static string TendEveryone(Map map)
        {
            if (map == null)
            {
                return null;
            }

            GameComponent_PartyStash stash = GameComponent_PartyStash.Instance;
            if (stash != null && GameComponent_PartyStash.Enabled)
            {
                stash.PoolFromParty();
            }

            List<Pawn> patients = map.mapPawns.FreeColonistsSpawned
                .Where(p => HealthAIUtility.ShouldBeTendedNowByPlayer(p))
                .OrderByDescending(p => p.health.hediffSet.BleedRateTotal)
                .ToList();
            if (patients.Count == 0)
            {
                return Report("Nobody needs tending.");
            }

            HashSet<Pawn> busy = new HashSet<Pawn>();
            int sent = 0;
            int withoutDoctor = 0;
            int withoutMedicine = 0;
            foreach (Pawn patient in patients)
            {
                Pawn doctor = ChooseDoctor(map, patient, busy);
                if (doctor == null)
                {
                    withoutDoctor++;
                    continue;
                }

                Thing medicine = null;
                if (stash != null)
                {
                    Thing taken = stash.TakeMedicine(patient, Medicine.GetMedicineCountToFullyHeal(patient));
                    if (taken != null)
                    {
                        int added = doctor.inventory.innerContainer.TryAdd(taken, taken.stackCount);
                        if (added < taken.stackCount && !taken.Destroyed)
                        {
                            stash.Add(taken);
                        }

                        medicine = doctor.inventory.innerContainer.FirstOrDefault(t => t.def == taken.def);
                    }
                }

                if (medicine == null)
                {
                    medicine = HealthAIUtility.FindBestMedicine(doctor, patient);
                }

                Job job = medicine != null ? JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine) : JobMaker.MakeJob(JobDefOf.TendPatient, patient);
                job.count = 1;
                job.draftedTend = true;
                doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                busy.Add(doctor);
                sent++;
                if (medicine == null)
                {
                    withoutMedicine++;
                }
            }

            string text = sent + " of " + patients.Count + " patient(s) sent a doctor";
            if (withoutMedicine > 0)
            {
                text += "; " + withoutMedicine + " with no medicine to use";
            }

            if (withoutDoctor > 0)
            {
                text += "; " + withoutDoctor + " with nobody free to tend them";
            }

            return Report(text + ".");
        }

        private static string Report(string text)
        {
            LastReport = text;
            Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
            return text;
        }

        // The best free doctor who can reach the patient: someone else if possible (self-tending is clumsier), by Medicine skill.
        private static Pawn ChooseDoctor(Map map, Pawn patient, HashSet<Pawn> busy)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Where(d => !busy.Contains(d)
                            && !d.Downed
                            && d.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                            && !d.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
                            && (d != patient || d.playerSettings == null || d.playerSettings.selfTend)
                            && d.CanReach(patient, PathEndMode.ClosestTouch, Danger.Deadly))
                .OrderBy(d => d == patient ? 1 : 0)
                .ThenByDescending(d => d.skills != null ? d.skills.GetSkill(SkillDefOf.Medicine).Level : 0)
                .FirstOrDefault();
        }

        // What the Run tab shows under "Supplies".
        public static List<KeyValuePair<string, string>> SupplyLines()
        {
            List<KeyValuePair<string, string>> lines = new List<KeyValuePair<string, string>>();
            GameComponent_PartyStash stash = GameComponent_PartyStash.Instance;
            if (stash == null || !GameComponent_PartyStash.Enabled)
            {
                lines.Add(new KeyValuePair<string, string>("Shared supplies", "off: each colonist carries their own"));
                return lines;
            }

            List<Thing> items = stash.Items.ToList();
            lines.Add(new KeyValuePair<string, string>("Shared medicine", items.Where(t => t.def.IsMedicine).Sum(t => t.stackCount) == 0
                ? "none"
                : string.Join(", ", items.Where(t => t.def.IsMedicine).Select(t => t.stackCount + " " + t.def.label).ToArray())));
            return lines;
        }
    }
}
