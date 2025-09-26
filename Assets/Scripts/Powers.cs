using UnityEngine;

namespace DarkForest
{
    public abstract class Power : ScriptableObject
    {
        public string powerName;
        public PowerTiming timing;
        [TextArea] public string text;

        public virtual bool RequiresTargetCell => false;
        public abstract bool TryPlay(PowerContext ctx);
    }

    public class PowerContext
    {
        public GameState game;
        public PlayerState self;
        public PlayerState target;
        public BodyInstance sourceBody;
        public Vector3Int probe;
        public System.Random rng;
    }

    [CreateAssetMenu(menuName = "DarkForest/Powers/FalseReport")]
    public class FalseReportPower : Power
    {
        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.self == null) return false;
            ctx.self.falseReportTokens++;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "DarkForest/Powers/ScanRow")]
    public class ScanRowPower : Power
    {
        public override bool RequiresTargetCell => true;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.game == null || ctx.self == null || ctx.target == null) return false;
            ctx.game.RevealRow(ctx.self, ctx.target, ctx.probe.y, ctx.probe.z);
            return true;
        }
    }

    [CreateAssetMenu(menuName = "DarkForest/Powers/ScanColumn")]
    public class ScanColumnPower : Power
    {
        public bool halfRange = false;

        public override bool RequiresTargetCell => true;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.game == null || ctx.self == null || ctx.target == null) return false;
            ctx.game.RevealColumn(ctx.self, ctx.target, ctx.probe.x, ctx.probe.y, ctx.probe.z, halfRange);
            return true;
        }
    }

        [CreateAssetMenu(menuName = "DarkForest/Powers/RevealNeighbors")]
    public class RevealNeighborsPower : Power
    {
        public bool includeDiagonals = true;
        public bool includeVertical = true;

        public override bool RequiresTargetCell => true;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.target == null) return false;
            var board = ctx.target.hiddenBoard;
            if (board == null) return false;

            RevealCell(board, ctx.probe.x, ctx.probe.y, ctx.probe.z);

            int minZ = includeVertical ? -1 : 0;
            int maxZ = includeVertical ? 1 : 0;

            for (int dz = minZ; dz <= maxZ; dz++)
            {
                int nz = ctx.probe.z + dz;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        if (!includeDiagonals && Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                        if (!includeVertical && dz != 0) continue;

                        RevealCell(board, ctx.probe.x + dx, ctx.probe.y + dy, nz);
                    }
                }
            }

            if (ctx.game != null && ctx.self != null && ctx.sourceBody != null && ctx.sourceBody.Definition != null && ctx.sourceBody.Definition.type == BodyType.SpaceStation)
            {
                ctx.game.ActivateSpaceStationLogging(ctx.self, ctx.sourceBody);
            }

            return true;
        }

        void RevealCell(Board board, int x, int y, int z)
        {
            var cell = board.Get(x, y, z);
            if (cell == null) return;

            cell.probed = true;

            if (cell.occupant != null)
            {
                cell.lastReport = CellReport.Hit;
                return;
            }

            bool near = false;
            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };

            for (int i = 0; i < dx.Length; i++)
            {
                var neighbor = board.Get(x + dx[i], y + dy[i], z + dz[i]);
                if (neighbor?.occupant != null && neighbor.occupant.Definition != null && neighbor.occupant.Definition.ReportsNearMissAt(neighbor, x, y, z))
                {
                    near = true;
                    break;
                }
            }

            cell.lastReport = near ? CellReport.NearMiss : CellReport.Miss;
        }
    }

[CreateAssetMenu(menuName = "DarkForest/Powers/GainCurrency")]
    public class GainCurrencyPower : Power
    {
        public int amount = 1;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.self == null) return false;
            ctx.self.currency += amount;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "DarkForest/Powers/ScrambleProbe")]
    public class ScrambleProbePower : Power
    {
        public override bool RequiresTargetCell => true;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.target == null) return false;
            var board = ctx.target.hiddenBoard;
            if (board == null) return false;

            var cell = board.Get(ctx.probe.x, ctx.probe.y, ctx.probe.z);
            if (cell == null || !cell.probed) return false;

            cell.probed = false;
            cell.lastReport = CellReport.Unknown;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "DarkForest/Powers/EraseProbe")]
    public class EraseProbePower : Power
    {
        public override bool RequiresTargetCell => true;

        public override bool TryPlay(PowerContext ctx)
        {
            if (ctx.self == null) return false;
            var board = ctx.self.hiddenBoard;
            if (board == null) return false;

            var cell = board.Get(ctx.probe.x, ctx.probe.y, ctx.probe.z);
            if (cell == null || !cell.probed) return false;

            cell.probed = false;
            cell.lastReport = CellReport.Unknown;
            return true;
        }
    }
}