using System.Collections.Generic;
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
        Reactor
    }

    public class Building_FacilityTerminal : Building
    {
        public TerminalKind kind;
        public string sectorId;
        public int requiredTier;
        private bool used;

        public bool Hacked => used;

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
            Scribe_Values.Look(ref requiredTier, "requiredTier");
            Scribe_Values.Look(ref used, "used");
        }

        private string KindLabel()
        {
            switch (kind)
            {
                case TerminalKind.Diagnostic: return "diagnostic";
                case TerminalKind.Valve: return "coolant valve";
                case TerminalKind.Reactor: return "reactor";
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
                status = "Locked: " + KindLabel() + ". Needs clearance " + DirectorVoice.TierName(requiredTier) + ".";
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
            else if (selPawn.skills == null || selPawn.skills.GetSkill(PrimarySkill).TotallyDisabled)
            {
                yield return new FloatMenuOption(label + " (incapable of " + PrimarySkill.label + ")", null);
            }
            else if (kind == TerminalKind.Gate && facility.Tier < requiredTier)
            {
                yield return new FloatMenuOption(label + " (needs clearance " + DirectorVoice.TierName(requiredTier) + ")", null);
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

        public void Hack(Pawn hacker)
        {
            if (used)
            {
                return;
            }

            MapComponent_Facility facility = Map.GetComponent<MapComponent_Facility>();
            if (kind == TerminalKind.Gate && facility.Tier < requiredTier)
            {
                Messages.Message("Access denied: clearance " + DirectorVoice.TierName(requiredTier) + " required.", this, MessageTypeDefOf.RejectInput, false);
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
            }
        }
    }
}
