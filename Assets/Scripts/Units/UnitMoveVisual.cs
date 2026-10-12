using System.Collections.Generic;
using UnityEngine;

using FuturatsTest.Grid;
using FuturatsTest.Utils;

namespace FuturatsTest.Units
{
    [RequireComponent(typeof(GridManager))]
    public class UnitMoveVisual : MonoBehaviour
    {

        private const float HEIGHT_ABOVE_GROUND = 0.02f;

        [SerializeField] private UnitSelectionController selectionController;
        [SerializeField] private Material material;
        [SerializeField, Range(0.5f, 1f)] private float tileFill = 0.9f;

        private GridManager grid;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private BaseUnit shownUnit;

        private void Start()
        {
            grid = GetComponent<GridManager>();

            if (selectionController == null)
            {
                Debug.LogError(name + ": UnitMoveVisual needs a Selection Controller", this);
                enabled = false;
                return;
            }

            CreateRangeObject();
        }

        private void Update()
        {
            BaseUnit selectedUnit = selectionController.SelectedUnit;
            if (selectedUnit == shownUnit) return;

            shownUnit = selectedUnit;
            if (shownUnit != null)
            {
                Show(shownUnit);
            }
            else
            {
                Hide();
            }
        }

        private void OnDestroy()
        {
            DestroyMesh();
        }

        private void CreateRangeObject()
        {
            // Child of the grid, so the squares line up with it
            GameObject rangeObject = new GameObject("MoveRange");
            rangeObject.transform.SetParent(transform, false);

            meshFilter = rangeObject.AddComponent<MeshFilter>();
            meshRenderer = rangeObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
        }

        private void Show(BaseUnit unit)
        {
            DestroyMesh();
            meshFilter.sharedMesh = BuildRangeMesh(GetReachableTiles(unit));
            meshRenderer.enabled = true;
        }

        private void Hide()
        {
            meshRenderer.enabled = false;
            DestroyMesh();
        }

        private HashSet<Vector2Int> GetReachableTiles(BaseUnit unit)
        {
            HashSet<Vector2Int> reachableTiles = new HashSet<Vector2Int>();
            foreach (Vector2Int pivot in unit.FindReachablePositions().Keys)
            {
                foreach (Vector2Int offset in unit.UnitTiles)
                {
                    reachableTiles.Add(pivot + offset);
                }
            }
            return reachableTiles;
        }

        private Mesh BuildRangeMesh(HashSet<Vector2Int> tiles)
        {
            // Positions are local to the grid: 0,0 is the grid's corner, like in GridVisual
            float cellSize = grid.cellSize;
            float squareSize = cellSize * tileFill;
            Vector3 halfSquare = new Vector3(squareSize * 0.5f, 0f, squareSize * 0.5f);

            QuadMesh quadMesh = new QuadMesh();
            foreach (Vector2Int tile in tiles)
            {
                Vector3 tileCenter = new Vector3((tile.x + 0.5f) * cellSize, HEIGHT_ABOVE_GROUND, (tile.y + 0.5f) * cellSize);
                Vector3 corner = tileCenter - halfSquare;
                quadMesh.AddSquare(corner, squareSize, Vector2.zero, Vector2.one);
            }
            return quadMesh.ToMesh("Move Range");
        }

        private void DestroyMesh()
        {
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
                meshFilter.sharedMesh = null;
            }
        }

    }
}