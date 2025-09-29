using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkForest
{
    [Serializable]
    public class Cell
    {
        public int x, y, z;
        public bool probed;
        public BodyInstance occupant;
        public CellReport lastReport = CellReport.Unknown;
    }

    public class Board
    {
        public int W, H, L;
        private Cell[,,] cells;

        public Board(int w, int h, int layers)
        {
            W = w;
            H = h;
            L = layers;
            cells = new Cell[w, h, layers];
            for (int z = 0; z < L; z++)
            {
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        cells[x, y, z] = new Cell { x = x, y = y, z = z };
                    }
                }
            }
        }

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < W && y < H && z < L;
        public Cell Get(int x, int y, int z) => InBounds(x, y, z) ? cells[x, y, z] : null;

        public IEnumerable<Cell> AllCells()
        {
            for (int z = 0; z < L; z++)
            {
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        yield return cells[x, y, z];
                    }
                }
            }
        }

        public bool CanPlace(BodyShape shape, int ox, int oy, int oz)
        {
            foreach (var p in shape.points)
            {
                int x = ox + p.x;
                int y = oy + p.y;
                int z = oz + p.z;
                var c = Get(x, y, z);
                if (c == null || c.occupant != null) return false;
                if (c.probed) return false;
            }
            return true;
        }

        // Rotation is a 90-degree step count (0..3). Rotates shape points clockwise by 90*rotation degrees.
        public bool CanPlace(BodyShape shape, int ox, int oy, int oz, int rotation)
        {
            foreach (var p in shape.points)
            {
                int rx = p.x;
                int ry = p.y;
                switch (rotation & 3)
                {
                    case 1: rx = -p.y; ry = p.x; break; // 90
                    case 2: rx = -p.x; ry = -p.y; break; // 180
                    case 3: rx = p.y; ry = -p.x; break; // 270
                }
                int x = ox + rx;
                int y = oy + ry;
                int z = oz + p.z;
                var c = Get(x, y, z);
                if (c == null || c.occupant != null) return false;
                if (c.probed) return false;
            }
            return true;
        }

        // rotation: 0..3 clockwise 90-degree steps
        public BodyInstance Place(BodyDefinition def, Team owner, int ox, int oy, int oz, int rotation = 0)
        {
            var inst = new BodyInstance(def, owner);
            foreach (var p in def.shape.points)
            {
                int rx = p.x;
                int ry = p.y;
                switch (rotation & 3)
                {
                    case 1: rx = -p.y; ry = p.x; break; // 90
                    case 2: rx = -p.x; ry = -p.y; break; // 180
                    case 3: rx = p.y; ry = -p.x; break; // 270
                }
                var c = Get(ox + rx, oy + ry, oz + p.z);
                if (c == null) throw new Exception("Out of bounds");
                c.occupant = inst;
                inst.OccupiedCells.Add(c);
            }
            return inst;
        }

        public ProbeOutcome Probe(int x, int y, int z)
        {
            var c = Get(x, y, z);
            if (c == null) return ProbeOutcome.OutOfBounds;

            c.probed = true;
            if (c.occupant != null)
            {
                c.lastReport = CellReport.Hit;
                c.occupant.HitCount++;
                return ProbeOutcome.Hit(c.occupant);
            }

            bool near = false;
            foreach (var n in Neighbors6AndVertical(x, y, z))
            {
                if (n.occupant != null && n.occupant.Definition.ReportsNearMissAt(n, x, y, z))
                {
                    near = true;
                    break;
                }
            }

            c.lastReport = near ? CellReport.NearMiss : CellReport.Miss;
            return near ? ProbeOutcome.NearMiss() : ProbeOutcome.Miss();
        }

        IEnumerable<Cell> Neighbors6AndVertical(int x, int y, int z)
        {
            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };
            for (int i = 0; i < 6; i++)
            {
                var c = Get(x + dx[i], y + dy[i], z + dz[i]);
                if (c != null) yield return c;
            }
        }
    }

    public struct ProbeOutcome
    {
        public bool isHit;
        public bool isMiss;
        public bool isNear;
        public bool wasFalseReport;
        public bool isPending;
        public BodyInstance hitBody;

        public static ProbeOutcome Hit(BodyInstance body)
        {
            return new ProbeOutcome { isHit = true, hitBody = body };
        }

        public static ProbeOutcome Miss()
        {
            return new ProbeOutcome { isMiss = true };
        }

        public static ProbeOutcome NearMiss()
        {
            return new ProbeOutcome { isNear = true };
        }

        public static ProbeOutcome FalseReport()
        {
            return new ProbeOutcome { isMiss = true, wasFalseReport = true };
        }

        public static ProbeOutcome Pending()
        {
            return new ProbeOutcome { isPending = true };
        }

        public static readonly ProbeOutcome OutOfBounds = new ProbeOutcome();
    }
}
