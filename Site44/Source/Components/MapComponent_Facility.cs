using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Site44.Gen;
using Verse;
using Verse.AI.Group;

namespace Site44
{
    public class MapComponent_Facility : MapComponent
    {
        private const int HeatCadenceTicks = 150;
        private const float HotZoneTemperature = 90f;
        private const float SafeTemperature = 21f;

        public int clearance;
        public string siteTypeDefName;   // which kind of complex this is; names the insider clearance rung
        public IntVec3 startCell = IntVec3.Invalid;
        public bool securityAnnounced;
        private Dictionary<string, int> keyColours = new Dictionary<string, int>();
        public bool coolantLoopDiverted;
        public bool mainReactorOnline;
        public float facilityAiIntegrity = 1f;

        private int announcedTier;
        private int gridNx;
        private int gridNy;
        private int gridSeed;
        private int graphSeed;
        private int targetZones;

        private List<string> unlockedSectors = new List<string>();
        private List<int> hotAnchors = new List<int>();
        public List<SecurityGroup> securityGroups = new List<SecurityGroup>();

        public MapComponent_Facility(Map map) : base(map)
        {
        }

        public bool IsFacilityMap => gridNx > 0;
        public int Tier => clearance;
        public int GridNx => gridNx;
        public int GridNy => gridNy;
        public int GridSeed => gridSeed;
        public int GraphSeed => graphSeed;
        public int TargetZones => targetZones;

        // gridNx/gridNy hold the plan width and height; gridSeed the plan seed.
        public void Initialize(int width, int height, int planSeed, int graphSeedValue, int targetZoneCount, List<IntVec3> hotRoomAnchors)
        {
            gridNx = width;
            gridNy = height;
            gridSeed = planSeed;
            graphSeed = graphSeedValue;
            targetZones = targetZoneCount;
            hotAnchors.Clear();
            foreach (IntVec3 anchor in hotRoomAnchors)
            {
                hotAnchors.Add(anchor.x);
                hotAnchors.Add(anchor.z);
            }
        }


        public bool IsSectorUnlocked(string sectorId) => unlockedSectors.Contains(sectorId);

        // Clearance only ever rises: a badge or a granting node sets it to at least the given rung.
        public void GrantClearance(int tier)
        {
            clearance = System.Math.Max(clearance, System.Math.Min(tier, Progression.MaxTier));
            while (announcedTier < Tier)
            {
                announcedTier++;
                OnTierReached(announcedTier);
            }
        }

        private void OnTierReached(int tier)
        {
            FacilityFaction.ApplyTier(tier);
            Find.LetterStack.ReceiveLetter(DirectorVoice.TierLabel(tier, map), DirectorVoice.TierText(tier, map), LetterDefOf.PositiveEvent);
        }

        // Replaces each sealed hatch of the sector with a plain door and lifts the fog behind it.
        public int UnsealSector(string sectorId)
        {
            if (!unlockedSectors.Contains(sectorId))
            {
                unlockedSectors.Add(sectorId);
            }

            List<Building_SealedHatch> hatches = map.listerThings.ThingsOfDef(Site44DefOf.Site44_SealedHatch)
                .OfType<Building_SealedHatch>()
                .Where(h => h.sectorId == sectorId)
                .ToList();

            foreach (Building_SealedHatch hatch in hatches)
            {
                IntVec3 cell = hatch.Position;
                hatch.Destroy();

                Thing door = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Door"), ThingDefOf.Steel);
                GenSpawn.Spawn(door, cell, map);

                map.fogGrid.Unfog(cell);
                foreach (IntVec3 adjacent in GenAdj.CellsAdjacentCardinal(cell, Rot4.North, IntVec2.One))
                {
                    if (adjacent.InBounds(map) && adjacent.Walkable(map))
                    {
                        FloodFillerFog.FloodUnfog(adjacent, map);
                    }
                }
            }

            return hatches.Count;
        }

        public void DivertCoolant()
        {
            if (coolantLoopDiverted)
            {
                return;
            }

            coolantLoopDiverted = true;
            ApplyHeat(SafeTemperature);
            Find.LetterStack.ReceiveLetter(DirectorVoice.CoolantLabel, DirectorVoice.CoolantText, LetterDefOf.PositiveEvent);
        }

        public void BringReactorOnline()
        {
            if (mainReactorOnline)
            {
                return;
            }

            mainReactorOnline = true;
            List<Thing> lamps = map.listerThings.ThingsOfDef(Site44DefOf.Site44_LampEmergency).ToList();
            foreach (Thing lamp in lamps)
            {
                IntVec3 cell = lamp.Position;
                lamp.Destroy();
                GenSpawn.Spawn(ThingMaker.MakeThing(Site44DefOf.Site44_LampBright), cell, map);
            }

            Find.LetterStack.ReceiveLetter(DirectorVoice.ReactorLabel, DirectorVoice.ReactorText, LetterDefOf.PositiveEvent);
        }

        public SecurityGroup GroupById(int id)
        {
            foreach (SecurityGroup g in securityGroups)
            {
                if (g.id == id)
                {
                    return g;
                }
            }

            return null;
        }

        public void MaybeTriggerAlarm(IntVec3 near)
        {
            if (Site44Tuning.SecurityEnabled && Rand.Value < Site44Tuning.AlarmChance)
            {
                TriggerAlarm(near, 2 + Tier * 2);
            }
        }

        public void TriggerAlarm(IntVec3 near, int strength)
        {
            Faction faction = FacilityFaction.GetOrCreate();
            if (!faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            List<Pawn> squad = new List<Pawn>();
            foreach (string kindName in FacilityMechs.SquadFor(strength))
            {
                Pawn mech = FacilityMechs.Make(kindName, faction);
                if (mech == null)
                {
                    continue;
                }

                IntVec3 cell;
                if (!CellFinder.TryFindRandomCellNear(near, map, 14, c => c.Standable(map) && c.GetEdifice(map) == null && c.DistanceTo(near) >= 6f && c.GetRoom(map) == near.GetRoom(map), out cell))
                {
                    cell = near;
                }

                GenSpawn.Spawn(mech, cell, map);
                squad.Add(mech);
            }

            if (squad.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false), map, squad);
                Find.LetterStack.ReceiveLetter(DirectorVoice.AlarmLabel, DirectorVoice.AlarmText, LetterDefOf.ThreatSmall, new LookTargets(near, map));
            }
        }

        public override void MapComponentTick()
        {
            if (IsFacilityMap && Find.TickManager.TicksGame % 60 == 0)
            {
                ScanBadges();
                AnnounceSecurity();
                ApplyFocus();
                TeachNearby();
            }

            if (!IsFacilityMap || coolantLoopDiverted || !Site44Tuning.HotZonesEnabled)
            {
                return;
            }
            if (Find.TickManager.TicksGame % HeatCadenceTicks == 0)
            {
                ApplyHeat(HotZoneTemperature);
            }
        }

        // The colour of the lock for a sector: hatch and keycard ask for the same one.
        public int KeyColourIndex(string sectorId)
        {
            int index;
            if (!keyColours.TryGetValue(sectorId, out index))
            {
                index = keyColours.Count;
                keyColours[sectorId] = index;
            }

            return index;
        }

        // One-time tips: the first time a colonist is near a gate, a terminal, a hatch, a keycard or a badge, the learning helper
        // gets a lesson about it. It does nothing when the player has turned the learning helper off, and never repeats.
        private void TeachNearby()
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0)
            {
                return;
            }

            Teach("Site44_ConceptGate", Site44DefOf.Site44_ExtractionGate, colonists);
            Teach("Site44_ConceptTerminal", Site44DefOf.Site44_Terminal, colonists);
            Teach("Site44_ConceptHatch", Site44DefOf.Site44_SealedHatch, colonists);
            Teach("Site44_ConceptKeycard", DefDatabase<ThingDef>.GetNamedSilentFail("Site44_Keycard"), colonists);
            Teach("Site44_ConceptBadge", DefDatabase<ThingDef>.GetNamedSilentFail("Site44_ClearanceBadge"), colonists);
        }

        private void Teach(string conceptName, ThingDef thingDef, List<Pawn> colonists)
        {
            ConceptDef concept = DefDatabase<ConceptDef>.GetNamedSilentFail(conceptName);
            if (concept == null || thingDef == null || PlayerKnowledgeDatabase.IsComplete(concept))
            {
                return;
            }

            foreach (Thing thing in map.listerThings.ThingsOfDef(thingDef))
            {
                if (colonists.Any(p => p.Position.DistanceTo(thing.Position) <= 14f))
                {
                    LessonAutoActivator.TeachOpportunity(concept, thing, OpportunityType.Important);
                    return;
                }
            }
        }

        // The explorer focus starts every level as recognised staff; the power focus gives the party its edge.
        private void ApplyFocus()
        {
            if (Site44Tuning.Explorer && clearance < FacilityFaction.AllyTier)
            {
                clearance = FacilityFaction.AllyTier;
                announcedTier = FacilityFaction.AllyTier;
                FacilityFaction.ApplyTier(clearance);
            }

            if (Site44Tuning.PowerTrip)
            {
                HediffDef edge = DefDatabase<HediffDef>.GetNamedSilentFail("Site44_DelversEdge");
                if (edge != null)
                {
                    foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (pawn.health != null && !pawn.health.hediffSet.HasHediff(edge))
                        {
                            pawn.health.AddHediff(edge);
                        }
                    }
                }
            }
        }

        // The first time a colonist comes near the facility's defences, say what they are and how to stand them down.
        private void AnnounceSecurity()
        {
            if (securityAnnounced || securityGroups.Count == 0 || Tier >= FacilityFaction.AllyTier)
            {
                return;
            }

            foreach (SecurityGroup group in securityGroups)
            {
                foreach (Thing turret in group.turrets)
                {
                    if (turret != null && turret.Spawned && map.mapPawns.FreeColonistsSpawned.Any(p => p.Position.DistanceTo(turret.Position) <= 30f))
                    {
                        securityAnnounced = true;
                        Find.LetterStack.ReceiveLetter("Defences active", "This complex's defences are awake. A security terminal can take them over or shut them down, and higher clearance makes the facility treat the party as staff.", LetterDefOf.NeutralEvent, new LookTargets(turret));
                        return;
                    }
                }
            }
        }

        // A colonist carrying a badge for this level is recognised at the badge's clearance; the badge is spent.
        private void ScanBadges()
        {
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                Thing_ClearanceBadge badge = pawn.inventory.innerContainer.OfType<Thing_ClearanceBadge>().FirstOrDefault(b => b.mapId == map.uniqueID)
                                             ?? pawn.carryTracker.CarriedThing as Thing_ClearanceBadge;
                if (badge == null || badge.mapId != map.uniqueID)
                {
                    continue;
                }

                int tier = badge.tier;
                string who = pawn.LabelShort;
                badge.Destroy();
                if (tier > clearance)
                {
                    Messages.Message(who + " is scanned by a clearance badge: " + DirectorVoice.TierName(tier, map) + ".", pawn, MessageTypeDefOf.PositiveEvent, false);
                    GrantClearance(tier);
                }
            }
        }

        private void ApplyHeat(float temperature)
        {
            HashSet<Room> done = new HashSet<Room>();
            for (int i = 0; i + 1 < hotAnchors.Count; i += 2)
            {
                Room room = new IntVec3(hotAnchors[i], 0, hotAnchors[i + 1]).GetRoom(map);
                if (room != null && !room.UsesOutdoorTemperature && done.Add(room))
                {
                    room.Temperature = temperature;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref clearance, "clearance");
            Scribe_Values.Look(ref securityAnnounced, "securityAnnounced");
            Scribe_Collections.Look(ref keyColours, "keyColours", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                keyColours = keyColours ?? new Dictionary<string, int>();
            }
            Scribe_Values.Look(ref siteTypeDefName, "siteTypeDefName");
            Scribe_Values.Look(ref startCell, "startCell", IntVec3.Invalid);
            Scribe_Values.Look(ref announcedTier, "announcedTier");
            Scribe_Values.Look(ref coolantLoopDiverted, "coolantLoopDiverted");
            Scribe_Values.Look(ref mainReactorOnline, "mainReactorOnline");
            Scribe_Values.Look(ref facilityAiIntegrity, "facilityAiIntegrity", 1f);
            Scribe_Values.Look(ref gridNx, "gridNx");
            Scribe_Values.Look(ref gridNy, "gridNy");
            Scribe_Values.Look(ref gridSeed, "gridSeed");

            Scribe_Values.Look(ref graphSeed, "graphSeed");
            Scribe_Values.Look(ref targetZones, "targetZones");
            Scribe_Collections.Look(ref unlockedSectors, "unlockedSectors", LookMode.Value);
            Scribe_Collections.Look(ref hotAnchors, "hotAnchors", LookMode.Value);
            Scribe_Collections.Look(ref securityGroups, "securityGroups", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                unlockedSectors = unlockedSectors ?? new List<string>();
                hotAnchors = hotAnchors ?? new List<int>();
                securityGroups = securityGroups ?? new List<SecurityGroup>();
            }
        }
    }
}

