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
        private const float AlarmChance = 0.35f;

        public int points;
        public string siteTypeDefName;   // which kind of complex this is; names the insider clearance rung
        public IntVec3 startCell = IntVec3.Invalid;
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
        public int Tier => Progression.TierForPoints(points);
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

        public void AddPoints(int amount)
        {
            points += amount;
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
            AddPoints(Progression.ValvePoints);
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
            AddPoints(Progression.ReactorPoints);
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
            if (Site44Tuning.SecurityEnabled && Rand.Value < AlarmChance)
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
            if (!IsFacilityMap || coolantLoopDiverted || !Site44Tuning.HotZonesEnabled)
            {
                return;
            }
            if (Find.TickManager.TicksGame % HeatCadenceTicks == 0)
            {
                ApplyHeat(HotZoneTemperature);
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
            Scribe_Values.Look(ref points, "points");
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

