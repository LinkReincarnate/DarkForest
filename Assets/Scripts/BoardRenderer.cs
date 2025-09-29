using System.Collections.Generic;
using UnityEngine;

namespace DarkForest
{
    public class BoardRenderer : MonoBehaviour
    {
        public GameState game;
        public int layerToShow = 0;
        public float cellSize = 0.5f;
        public bool revealOccupants = false;
        public Color unknownColor = Color.gray;
        public Color missColor = Color.white;
        public Color nearColor = Color.yellow;
        public Color hitColor = Color.red;
        public Color occupantColor = new Color(0.2f, 0.6f, 1f, 1f);
        public Color hoverColor = new Color(0.2f, 0.9f, 1f, 0.6f);
        public Color placementPreviewColor = new Color(0.2f, 1f, 0.2f, 0.6f);
        public Color placementBlockedColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        public Color gridLineColor = new Color(1f, 1f, 1f, 0.18f);
        public float gridLineWidth = 0.01f;
        public Material gridLineMaterial;

        private MeshRenderer[,] cellRenderers;
        private Color[,] baseColors;
        private Cell[,] cachedCells;
        private bool[,] cellBlocked;
        private Board currentBoard;
        private int currentLayer;
        private bool hasHover;
        private int hoverX;
        private int hoverY;

        private readonly List<GameObject> quads = new List<GameObject>();
        private readonly List<LineRenderer> gridLines = new List<LineRenderer>();
        private readonly HashSet<(int x, int y)> placementPreview = new HashSet<(int x, int y)>();

        public void RenderCentral(PlayerState attacker, PlayerState defender, bool interactable = false)
        {
            if (defender == null || defender.hiddenBoard == null)
            {
                Clear();
                return;
            }
            RenderBoard(defender.hiddenBoard, false, interactable);
        }

        public void RenderHidden(PlayerState owner, bool interactable = false)
        {
            if (owner == null || owner.hiddenBoard == null)
            {
                Clear();
                return;
            }
            RenderBoard(owner.hiddenBoard, true, interactable);
        }

        private void RenderBoard(Board board, bool revealUnprobed, bool enableColliders)
        {
            Clear();

            currentBoard = board;
            currentLayer = Mathf.Clamp(layerToShow, 0, board.L - 1);
            cellRenderers = new MeshRenderer[board.W, board.H];
            baseColors = new Color[board.W, board.H];
            cachedCells = new Cell[board.W, board.H];
            cellBlocked = new bool[board.W, board.H];

            for (int y = 0; y < board.H; y++)
            {
                for (int x = 0; x < board.W; x++)
                {
                    var cell = board.Get(x, y, currentLayer);
                    if (cell == null) continue;

                    cachedCells[x, y] = cell;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.SetParent(transform, false);
                    quad.transform.localScale = new Vector3(cellSize, cellSize, 1f);
                    quad.transform.localPosition = new Vector3(x * cellSize, y * cellSize, 0f);

                    var renderer = quad.GetComponent<MeshRenderer>();
                    renderer.material = new Material(Shader.Find("Unlit/Color"));
                    Color baseColor = PickColor(cell, revealUnprobed);
                    renderer.material.color = baseColor;

                    var collider = quad.GetComponent<Collider>();
                    if (collider) collider.enabled = enableColliders;

                    quads.Add(quad);
                    cellRenderers[x, y] = renderer;
                    baseColors[x, y] = baseColor;
                    cellBlocked[x, y] = cell.occupant != null || cell.probed;
                }
            }

            CreateGrid(board);
            SetHover(null, null);
        }

        private void CreateGrid(Board board)
        {
            foreach (var line in gridLines)
            {
                if (line)
                {
                    Destroy(line.gameObject);
                }
            }
            gridLines.Clear();

            if (gridLineMaterial == null)
            {
                gridLineMaterial = new Material(Shader.Find("Unlit/Color"));
                gridLineMaterial.color = gridLineColor;
            }

            float minX = -cellSize * 0.5f;
            float maxX = (board.W - 1) * cellSize + cellSize * 0.5f;
            float minY = -cellSize * 0.5f;
            float maxY = (board.H - 1) * cellSize + cellSize * 0.5f;

            for (int x = 0; x <= board.W; x++)
            {
                float xPos = minX + x * cellSize;
                var line = CreateLine();
                line.SetPosition(0, new Vector3(xPos, minY, -0.01f));
                line.SetPosition(1, new Vector3(xPos, maxY, -0.01f));
            }

            for (int y = 0; y <= board.H; y++)
            {
                float yPos = minY + y * cellSize;
                var line = CreateLine();
                line.SetPosition(0, new Vector3(minX, yPos, -0.01f));
                line.SetPosition(1, new Vector3(maxX, yPos, -0.01f));
            }
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("GridLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = gridLineMaterial;
            lr.startColor = gridLineColor;
            lr.endColor = gridLineColor;
            lr.positionCount = 2;
            lr.widthMultiplier = gridLineWidth;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            gridLines.Add(lr);
            return lr;
        }

        private Color PickColor(Cell cell, bool revealUnprobed)
        {
            if (cell.probed)
            {
                if (cell.lastReport == CellReport.Miss) return missColor;
                if (cell.lastReport == CellReport.NearMiss) return nearColor;
                if (cell.lastReport == CellReport.Hit) return hitColor;
            }
            else if (revealUnprobed && cell.occupant != null)
            {
                return occupantColor;
            }
            return unknownColor;
        }

        public void SetHover(int? x, int? y)
        {
            if (cellRenderers == null || baseColors == null)
            {
                return;
            }

            if (hasHover)
            {
                RestoreCellColor(hoverX, hoverY);
                hasHover = false;
            }

            if (!x.HasValue || !y.HasValue)
            {
                return;
            }

            int cx = x.Value;
            int cy = y.Value;
            if (!IsWithinBoard(cx, cy))
            {
                return;
            }

            var renderer = cellRenderers[cx, cy];
            if (renderer == null)
            {
                return;
            }

            renderer.material.color = Color.Lerp(baseColors[cx, cy], hoverColor, 0.6f);
            hoverX = cx;
            hoverY = cy;
            hasHover = true;
        }

        private bool IsWithinBoard(int x, int y)
        {
            return cellRenderers != null && x >= 0 && y >= 0 && x < cellRenderers.GetLength(0) && y < cellRenderers.GetLength(1);
        }

        // rotation: 0..3 clockwise 90-degree steps
        public void ShowPlacementPreview(BodyDefinition definition, int ox, int oy, int rotation = 0)
        {
            ClearPlacementPreview();
            if (definition == null || definition.shape == null || cachedCells == null)
            {
                return;
            }

            bool fits = true;
            foreach (var offset in definition.shape.points)
            {
                int rx = offset.x;
                int ry = offset.y;
                switch (rotation & 3)
                {
                    case 1: rx = -offset.y; ry = offset.x; break;
                    case 2: rx = -offset.x; ry = -offset.y; break;
                    case 3: rx = offset.y; ry = -offset.x; break;
                }
                int cx = ox + rx;
                int cy = oy + ry;
                if (!IsWithinBoard(cx, cy) || cellBlocked[cx, cy])
                {
                    fits = false;
                    break;
                }
            }

            Color previewColor = fits ? placementPreviewColor : placementBlockedColor;

            foreach (var offset in definition.shape.points)
            {
                int rx = offset.x;
                int ry = offset.y;
                switch (rotation & 3)
                {
                    case 1: rx = -offset.y; ry = offset.x; break;
                    case 2: rx = -offset.x; ry = -offset.y; break;
                    case 3: rx = offset.y; ry = -offset.x; break;
                }
                int cx = ox + rx;
                int cy = oy + ry;
                HighlightPreviewCell(cx, cy, previewColor);
            }
        }

        public void ShowRowPreview(int rowY)
        {
            ClearPlacementPreview();
            if (cellRenderers == null || !IsWithinBoard(0, rowY)) return;
            for (int x = 0; x < cellRenderers.GetLength(0); x++)
            {
                HighlightPreviewCell(x, rowY, placementPreviewColor);
            }
        }

        public void ShowColumnPreview(int columnX)
        {
            ClearPlacementPreview();
            if (cellRenderers == null || !IsWithinBoard(columnX, 0)) return;
            for (int y = 0; y < cellRenderers.GetLength(1); y++)
            {
                HighlightPreviewCell(columnX, y, placementPreviewColor);
            }
        }

        public void ShowHalfColumnPreview(int columnX, int startY, int endY)
        {
            ClearPlacementPreview();
            if (cellRenderers == null || baseColors == null) return;
            startY = Mathf.Max(0, startY);
            endY = Mathf.Min(cellRenderers.GetLength(1) - 1, endY);
            for (int y = startY; y <= endY; y++)
            {
                HighlightPreviewCell(columnX, y, placementPreviewColor);
            }
        }

        private void HighlightPreviewCell(int x, int y, Color color)
        {
            if (!IsWithinBoard(x, y)) return;
            var renderer = cellRenderers[x, y];
            if (renderer == null) return;
            renderer.material.color = Color.Lerp(baseColors[x, y], color, 0.65f);
            placementPreview.Add((x, y));
        }

        public void ClearPlacementPreview()
        {
            if (placementPreview.Count == 0) return;
            foreach (var (px, py) in placementPreview)
            {
                RestoreCellColor(px, py);
            }
            placementPreview.Clear();
        }

        private void RestoreCellColor(int x, int y)
        {
            if (cellRenderers == null || baseColors == null) return;
            if (!IsWithinBoard(x, y)) return;

            var renderer = cellRenderers[x, y];
            if (renderer == null) return;

            renderer.material.color = baseColors[x, y];
        }

        public void Clear()
        {
            foreach (var quad in quads)
            {
                if (quad) Destroy(quad);
            }
            quads.Clear();

            foreach (var line in gridLines)
            {
                if (line) Destroy(line.gameObject);
            }
            gridLines.Clear();

            placementPreview.Clear();

            cellRenderers = null;
            baseColors = null;
            cachedCells = null;
            cellBlocked = null;
            currentBoard = null;
            hasHover = false;
        }
    }
}
