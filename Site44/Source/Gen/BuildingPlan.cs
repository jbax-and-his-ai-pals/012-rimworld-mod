using System.Collections.Generic;
using System.Text;

namespace Site44.Gen
{
    // Whole-site architectural plan: one to three buildings (office blocks, annexes) with real circulation.
    // Coordinates: local to the facility rectangle, (0,0) south-west, +x east, +y north.
    // Pure data; no RimWorld references.

    public enum PlanTile : byte
    {
        Outside,   // open sky: yards, courtyards, and the world beyond the building
        Wall,
        Floor,
        Door,      // a doorway between two rooms, or between a room and the outside
        Window     // see-through impassable panel; has Floor/Outside on two opposite sides
    }

    public sealed class PlanRoom
    {
        public int Id;                       // index in plan.Rooms
        public RoomFunction Function;
        public bool IsCorridor;              // hallways, lobbies' circulation spaces and atrium galleries that exist to connect
        public int BuildingId;               // index in plan.Buildings
        public List<LayoutCell> Cells = new List<LayoutCell>();   // every Floor cell, 4-connected
        public LayoutCell Anchor;            // a Floor cell of the room near its centroid
        public bool Concave;                 // bounding box area > cell count
    }

    // A door tile joining two rooms (or a room and the outside: RoomB == -1).
    public sealed class PlanLink
    {
        public int RoomA;
        public int RoomB;                    // -1 for an exterior door
        public LayoutCell Cell;              // the Door tile
        public bool Fire;                    // a corridor-subdividing fire door / bulkhead
    }

    public sealed class PlanBuilding
    {
        public int Id;
        public string Archetype;             // "Office", "Lab", "Warehouse", "Plant", ...
        public int MinX, MinY, MaxX, MaxY;   // bounding box of the building footprint
        public bool Isolated;                // no link corridor to the rest of the complex; entered from the yards
    }

    public sealed class BuildingPlan
    {
        public int Width;
        public int Height;
        public PlanTile[] Tiles;             // index = y * Width + x
        public int[] RoomIds;                // room id per Floor cell, -1 elsewhere
        public List<PlanRoom> Rooms = new List<PlanRoom>();
        public List<PlanLink> Links = new List<PlanLink>();
        public List<PlanBuilding> Buildings = new List<PlanBuilding>();
        public LayoutCell Start;             // an Outside cell in the yard in front of the main entrance (where the party starts)
        public LayoutCell MainEntrance;      // the Door tile of the main entrance (leads into the lobby of Buildings[0])
        public string Style = "Campus";               // layout style used: Campus, Scattered or Compound
        public int ParadeW, ParadeH;                   // Scattered: size of the open parade yard in front of the principal building
        public int GateCount;                          // Compound: gaps in the perimeter wall
        public List<LayoutCell> Gates = new List<LayoutCell>();   // Compound: the Outside cells of every gate gap
        public string ProfileName = "OfficeCampus";   // PlanProfile.Name used to generate this plan
        public int CourtyardsFilled, CourtyardsOpen;   // generator statistics: enclosed courtyards given a room vs left as yards

        public BuildingPlan(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new PlanTile[width * height];
            RoomIds = new int[width * height];
            for (int i = 0; i < RoomIds.Length; i++)
            {
                RoomIds[i] = -1;
            }
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height; }

        public PlanTile At(int x, int y) { return InBounds(x, y) ? Tiles[y * Width + x] : PlanTile.Outside; }

        public int RoomAt(int x, int y) { return InBounds(x, y) ? RoomIds[y * Width + x] : -1; }

        // North row first: ' ' outside, '#' wall, '.' floor, '+' door (fire doors '=' ), 'W' window, rooms' corridors ':'.
        public string ToAscii()
        {
            StringBuilder sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    char c;
                    switch (At(x, y))
                    {
                        case PlanTile.Outside: c = ' '; break;
                        case PlanTile.Wall: c = '#'; break;
                        case PlanTile.Window: c = 'W'; break;
                        case PlanTile.Door:
                            c = '+';
                            foreach (PlanLink link in Links)
                            {
                                if (link.Cell.X == x && link.Cell.Y == y && link.Fire) { c = '='; break; }
                            }
                            break;
                        default:
                        {
                            int id = RoomAt(x, y);
                            c = id >= 0 && Rooms[id].IsCorridor ? ':' : '.';
                            break;
                        }
                    }

                    sb.Append(c);
                }

                if (y > 0)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }
    }
}
