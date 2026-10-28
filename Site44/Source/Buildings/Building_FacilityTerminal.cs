using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Site44
{
    public enum TerminalKind
    {
        Gate,
        Diagnostic,
        Valve,
        Reactor,
        Security,
        Extraction
    }

    // What a node may also do once its puzzle is solved. Rare: most nodes only do their one job.
    public enum NodeExtra
    {
        None,
        UnsealNearby,   // opens the nearest sealed hatches besides its own
        SecurityDown,   // shuts down the nearest hostile security network
        Reveal          // marks where the extraction gate is
    }

    public class Building_FacilityTerminal : Building
    {
        public TerminalKind kind;
        public string sectorId;
        public int groupId = -1;
        public int requiredTier;
        public NodeExtra extra;
        private bool used;
        private int lockedUntil;

        public bool Hacked => used;

        public bool UsesCipherLock => Site44Settings.Current.hackMinigame && (kind == TerminalKind.Gate || kind == TerminalKind.Diagnostic || kind == TerminalKind.Security || kind == TerminalKind.Extraction);

        public bool LockedOut => Find.TickManager.TicksGame < lockedUntil;

        public int PuzzleTier => kind == TerminalKind.Gate ? Mathf.Clamp(requiredTier, 0, 3) : (kind == TerminalKind.Security ? 2 : (kind == TerminalKind.Extraction ? 3 : 1));

        public void LockOut(Pawn hacker)
        {
            // A failed attempt costs no waiting: the terminal accepts another try straight away.
            Messages.Message(hacker.LabelShort + " fails to crack the terminal. Please try again.", this, MessageTypeDefOf.NegativeEvent, false);
            MapComponent_Facility facility = Map.GetComponent<MapComponent_Facility>();
            if (facility.Tier < FacilityFaction.AllyTier)
            {
                facility.MaybeTriggerAlarm(Position);
            }
        }

        public SkillDef PrimarySkill => kind == TerminalKind.Valve ? SkillDefOf.Construction : SkillDefOf.Intellectual;

        public int HackTicks(Pawn pawn)
        {
            float baseTicks;
            switch (kind)
            {
                case TerminalKind.Diagnostic: baseTicks = 900f; break;
                case TerminalKind.Valve: baseTicks = 1200f; break;
                case TerminalKind.Reactor: baseTicks = 1500f; break;
                default: baseTicks = 600f; break;
            }

            float speed = kind == TerminalKind.Valve
                ? pawn.GetStatValue(StatDefOf.ConstructionSpeed)
                : pawn.GetStatValue(StatDefOf.ResearchSpeed);
            return Mathf.Max(60, Mathf.RoundToInt(baseTicks / Mathf.Max(0.25f, speed)));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref sectorId, "sectorId");
            Scribe_Values.Look(ref groupId, "groupId", -1);
            Scribe_Values.Look(ref requiredTier, "requiredTier");
            Scribe_Values.Look(ref extra, "extra");
            Scribe_Values.Look(ref used, "used");
            Scribe_Values.Look(ref lockedUntil, "lockedUntil");
        }

        private string KindLabel()
        {
            switch (kind)
            {
                case TerminalKind.Diagnostic: return "diagnostic";
                case TerminalKind.Valve: return "coolant valve";
                case TerminalKind.Reactor: return "reactor";
                case TerminalKind.Security: return "security console";
                case TerminalKind.Extraction: return "extraction terminal";
                default: return sectorId != null && sectorId.StartsWith("vault") ? "secure room" : "sector gate " + sectorId;
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string status;
            if (used)
            {
                status = "Override accepted (" + KindLabel() + ").";
            }
            else if (kind == TerminalKind.Gate)
            {
                status = "Locked: " + KindLabel() + ". Needs clearance " + DirectorVoice.TierName(requiredTier, Map) + ".";
            }
            else
            {
                status = "Idle: " + KindLabel() + ".";
            }

            return text.NullOrEmpty() ? status : text + "\n" + status;
        }

        private string ActionLabel()
        {
            switch (kind)
            {
                case TerminalKind.Diagnostic: return "Run diagnostic";
                case TerminalKind.Valve: return "Divert coolant";
                case TerminalKind.Reactor: return "Restart reactor";
                case TerminalKind.Security: return "Access security console";
                case TerminalKind.Extraction: return "Unlock extraction gate";
                default: return "Hack terminal";
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            string label = ActionLabel();
            MapComponent_Facility facility = Map.GetComponent<MapComponent_Facility>();

            if (used)
            {
                yield return new FloatMenuOption(label + " (already done)", null);
            }
            else if (LockedOut)
            {
                yield return new FloatMenuOption(label + " (locked out)", null);
            }
            else if (selPawn.skills == null || selPawn.skills.GetSkill(PrimarySkill).TotallyDisabled)
            {
                yield return new FloatMenuOption(label + " (incapable of " + PrimarySkill.label + ")", null);
            }
            else if (kind == TerminalKind.Gate && facility.Tier < requiredTier)
            {
                yield return new FloatMenuOption(label + " (needs clearance " + DirectorVoice.TierName(requiredTier, Map) + ")", null);
            }
            else if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + " (cannot reach)", null);
            }
            else
            {
                yield return new FloatMenuOption(label, () =>
                {
                    Job job = JobMaker.MakeJob(Site44DefOf.Site44_HackTerminal, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }

        private void OpenSecurityMenu(Pawn hacker)
        {
            MapComponent_Facility facility = Map.GetComponent<MapComponent_Facility>();
            SecurityGroup group = facility.GroupById(groupId);
            if (group == null || group.mode != SecurityMode.Hostile)
            {
                used = true;
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Seize control (turrets and droids join you)", () => ApplySecurityMode(group, SecurityMode.Allied, hacker)),
                new FloatMenuOption("Shut down (destroy the group)", () => ApplySecurityMode(group, SecurityMode.Disabled, hacker)),
                new FloatMenuOption("Leave it running", null)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplySecurityMode(SecurityGroup group, SecurityMode mode, Pawn hacker)
        {
            used = true;
            group.SetMode(mode);
            Messages.Message(hacker.LabelShort + (mode == SecurityMode.Allied ? " seizes the security network." : " shuts the security network down."),
                new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
        }

        public void Hack(Pawn hacker)
        {
            if (used)
            {
                return;
            }

            MapComponent_Facility facility = Map.GetComponent<MapComponent_Facility>();
            if (kind == TerminalKind.Gate && facility.Tier < requiredTier)
            {
                Messages.Message("Access denied: clearance " + DirectorVoice.TierName(requiredTier, Map) + " required.", this, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (kind == TerminalKind.Security)
            {
                OpenSecurityMenu(hacker);
                return;
            }

            used = true;
            switch (kind)
            {
                case TerminalKind.Gate:
                    int tierBefore = facility.Tier;
                    int unsealed = facility.UnsealSector(sectorId);
                    facility.AddPoints(Progression.GatePoints);
                    Messages.Message(hacker.LabelShort + " overrides the terminal. " + unsealed + " hatch(es) unseal.", new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
                    if (tierBefore < FacilityFaction.AllyTier)
                    {
                        facility.MaybeTriggerAlarm(Position);
                    }
                    break;
                case TerminalKind.Diagnostic:
                    facility.AddPoints(Progression.DiagnosticPoints);
                    Messages.Message(hacker.LabelShort + " completes the diagnostic. Facility confidence in your credentials rises.", new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
                    break;
                case TerminalKind.Valve:
                    facility.DivertCoolant();
                    break;
                case TerminalKind.Reactor:
                    facility.BringReactorOnline();
                    break;
                case TerminalKind.Extraction:
                    foreach (Thing gateThing in Map.listerThings.ThingsOfDef(Site44DefOf.Site44_ExtractionGate))
                    {
                        ((Building_ExtractionGate)gateThing).unlocked = true;
                    }

                    Messages.Message(hacker.LabelShort + " unlocks the extraction gate.", new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
                    break;
            }

            if (extra != NodeExtra.None && (kind == TerminalKind.Gate || kind == TerminalKind.Diagnostic))
            {
                ApplyExtra(hacker, facility);
            }
        }

        // The bonus a rare node carries. It always does something: with nothing to unseal or shut down it marks the gate.
        private void ApplyExtra(Pawn hacker, MapComponent_Facility facility)
        {
            NodeExtra what = extra;
            if (what == NodeExtra.UnsealNearby)
            {
                List<Building_SealedHatch> hatches = Map.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch)
                    .OfType<Building_SealedHatch>()
                    .Where(h => h.sectorId != sectorId)
                    .OrderBy(h => h.Position.DistanceTo(Position))
                    .ToList();
                List<string> sectors = hatches.Select(h => h.sectorId).Distinct().Take(2).ToList();
                if (sectors.Count == 0)
                {
                    what = NodeExtra.Reveal;
                }
                else
                {
                    int opened = 0;
                    foreach (string sector in sectors)
                    {
                        opened += facility.UnsealSector(sector);
                    }

                    Messages.Message("The node also releases " + opened + " nearby hatch(es).", new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
                    return;
                }
            }

            if (what == NodeExtra.SecurityDown)
            {
                SecurityGroup group = facility.securityGroups
                    .Where(g => g.mode == SecurityMode.Hostile)
                    .OrderBy(g => g.turrets.Where(t => t != null && t.Spawned).Select(t => t.Position.DistanceTo(Position)).Concat(g.droids.Where(d => d != null && d.Spawned).Select(d => d.Position.DistanceTo(Position))).DefaultIfEmpty(9999f).Min())
                    .FirstOrDefault();
                if (group != null)
                {
                    group.SetMode(SecurityMode.Disabled);
                    Messages.Message("The node also shuts down a security network.", new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent);
                    return;
                }

                what = NodeExtra.Reveal;
            }

            Thing gate = Map.listerThings.ThingsOfDef(Site44DefOf.Site44_ExtractionGate).FirstOrDefault();
            if (gate != null)
            {
                Messages.Message("The node also pings the extraction gate.", new LookTargets(gate), MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
