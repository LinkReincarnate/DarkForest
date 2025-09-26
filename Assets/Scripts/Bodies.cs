using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkForest
{
    [System.Serializable]
    public struct V3
    {
        public int x, y, z;
        public V3(int X, int Y, int Z) { x = X; y = Y; z = Z; }
    }

    [System.Serializable]
    public class BodyShape
    {
        public List<V3> points = new List<V3>();

        public BodyShape() {}
        public BodyShape(params V3[] pts) { points.AddRange(pts); }
        public BodyShape(IEnumerable<V3> pts) { points.AddRange(pts); }

        public BodyShape Clone()
        {
            return new BodyShape(points.ToArray());
        }
    }

    public abstract class BodyDefinition : ScriptableObject
    {
        public BodyType type;
        public string displayName;
        public int populationCapacity;
        public int price;
        public BodyShape shape;
        [TextArea] public string rulesSummary;

        public Power[] placementPowers;
        public Power[] colonizationPowers;

        public enum NearPattern { None, AdjacentNoDiag, AdjacentAllDirs, AdjacentAllLayers }
        public NearPattern nearPattern = NearPattern.None;

        public virtual bool ReportsNearMissAt(Cell neighborCell, int probeX, int probeY, int probeZ)
        {
            switch (nearPattern)
            {
                case NearPattern.None: return false;
                case NearPattern.AdjacentNoDiag: return true;
                case NearPattern.AdjacentAllDirs:
                    return Mathf.Abs(neighborCell.x - probeX) <= 1 && Mathf.Abs(neighborCell.y - probeY) <= 1 && neighborCell.z == probeZ;
                case NearPattern.AdjacentAllLayers:
                    return Mathf.Abs(neighborCell.x - probeX) <= 1 && Mathf.Abs(neighborCell.y - probeY) <= 1 && Mathf.Abs(neighborCell.z - probeZ) <= 1;
            }
            return false;
        }
    }

    public class BodyInstance
    {
        public BodyDefinition Definition { get; private set; }
        public Team Owner { get; private set; }
        public int HitCount { get; set; }
        public List<Cell> OccupiedCells { get; private set; } = new List<Cell>();
        public bool IsFullyColonized => HitCount >= OccupiedCells.Count;
        public bool PlacementPowersGranted { get; set; }
        public bool ColonizationPowersGranted { get; set; }

        public BodyInstance(BodyDefinition def, Team owner)
        {
            Definition = def;
            Owner = owner;
            PlacementPowersGranted = false;
            ColonizationPowersGranted = false;
        }
    }
}