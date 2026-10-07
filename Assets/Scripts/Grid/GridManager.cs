using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GridManager : MonoBehaviour
{

    public enum TextureMode
    {
        StretchOverGroup,   // Texture stretched over the whole group
        RepeatAlongStrip,   // Texture repeated along the group
        Border              // 9 slice
    }

    [Serializable]
    public class MaterialMode
    {
        public Material material;
        public TextureMode mode = TextureMode.StretchOverGroup;
    }

    [Serializable]
    public class TileData
    {
        public Vector2Int cell;
        public bool walkable = true;
        [Min(1)] public int moveCost = 1;
        public Material material;

        public GameObject prefab;
        public GameObject instance;
        public Vector3 prefabOffset;
    }

    private const string PREFABS_NAME = "Tile Prefabs";   // prefabs placed on tiles (normal, saved objects)

    private static GridManager instance;
    public static GridManager Instance { get => instance; private set => instance = value; }

    [Header("Grid")]
    [Min(1)] public int width = 50;
    [Min(1)] public int height = 50;
    [Min(0.01f)] public float cellSize = 1f;

    [Header("Ground")]
    public Material groundMaterial;

    [Header("Tile Materials")]
    public List<MaterialMode> materialModes = new List<MaterialMode>();

    [Header("Gizmos")]
    public bool showHover = true;
    public Color hoverColor = Color.yellow;
    public bool showBlockedTiles = true;
    public Color blockedColor = new Color(1f, 0f, 0f, 0.4f);
    [Tooltip("Debug: green = walkable, red = blocked, yellow = unit.")]
    public bool drawAllCells = false;

    [SerializeField, HideInInspector] private List<TileData> tiles = new List<TileData>();

    private Dictionary<Vector2Int, TileData> tileLookup = new Dictionary<Vector2Int, TileData>();
    private bool[,] walkableMap = new bool[0, 0];


    private GridVisual visual;
    private GridHover hover;


    private int gridWidth;
    private int gridHeight;


    public int Width { get => gridWidth; private set => gridWidth = value; }
    public int Height { get => gridHeight; private set => gridHeight = value; }

    public float GroundHeight { get => transform.position.y; } // It's a quad

    public List<TileData> CustomTileData { get => tiles; }  // Tiles that have been edited


    private void Awake()
    {
        Instance = this;
        if (Application.isPlaying) Build();
    }

    private void OnEnable()
    {
        Instance = this;
        if (!Application.isPlaying) Build();
#if UNITY_EDITOR
        SceneView.duringSceneGui += OnSceneGUI;     // hover square in the Scene view
        Undo.undoRedoPerformed += RequestRebuild;   // Ctrl+Z needs to update the map as well
#endif
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= RequestRebuild;
#endif
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        RequestRebuild();
    }

    private void Update()
    {
        GetHover().UpdateGameHover();
    }

    private GridVisual GetVisual()
    {
        if (visual == null) visual = new GridVisual(this);
        return visual;
    }

    private GridHover GetHover()
    {
        if (hover == null) hover = new GridHover(this);
        return hover;
    }

    #region Map Building

    public void RequestRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        EditorApplication.delayCall -= DelayedBuild;
        EditorApplication.delayCall += DelayedBuild;
#endif
    }

    private void DelayedBuild()
    {
        if (this == null) return;
        if (!isActiveAndEnabled || Application.isPlaying) return;
        Build();
    }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);

        tileLookup.Clear();
        foreach (TileData tile in tiles)
        {
            if (tile != null) tileLookup[tile.cell] = tile;
        }

        RefreshWalkable();
        RefreshVisuals();
    }

    public void RefreshWalkable()
    {
        walkableMap = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                walkableMap[x, y] = true;
            }
        }

        // Refresh not walkables
        foreach (TileData tile in tiles)
        {
            if (tile != null && !tile.walkable && InBounds(tile.cell))
            {
                walkableMap[tile.cell.x, tile.cell.y] = false;
            }
        }
    }

    public void RefreshVisuals()
    {
        GetVisual().Refresh();
    }

    #endregion

    #region Tile Data
    public TileData GetTileData(Vector2Int cell)
    {
        if (tileLookup.TryGetValue(cell, out TileData tile)) return tile;
        return null;
    }

    public TileData GetOrCreateTileData(Vector2Int cell)
    {
        if (tileLookup.TryGetValue(cell, out TileData tile)) return tile;

        tile = new TileData { cell = cell };
        tiles.Add(tile);
        tileLookup[cell] = tile;
        return tile;
    }


    /// <summary> Remove not needed data in containers </summary>
    public void CleanupTileData(Vector2Int cell)
    {
        if (!tileLookup.TryGetValue(cell, out TileData tile)) return;

        bool isDefault = tile.walkable
                      && tile.prefab == null
                      && tile.instance == null
                      && tile.material == null
                      && tile.prefabOffset == Vector3.zero
                      && tile.moveCost == 1;
        if (isDefault)
        {
            tiles.Remove(tile);
            tileLookup.Remove(cell);
        }
    }

    public Vector3 PrefabPosition(Vector2Int cell)
    {
        TileData tile = GetTileData(cell);
        Vector3 offset = tile != null ? tile.prefabOffset : Vector3.zero;
        return CellCenter(cell, GroundHeight) + offset;
    }

    public Transform GetPrefabsRoot(bool create)
    {
        Transform root = transform.Find(PREFABS_NAME);
        if (root == null && create)
        {
            GameObject rootObject = new GameObject(PREFABS_NAME);
            rootObject.transform.SetParent(transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Tile Prefabs Root");
#endif
            root = rootObject.transform;
        }
        return root;
    }

    #endregion

    #region Utils

    /// <summary> Returns world position of tile </summary>
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - transform.position;
        int x = Mathf.FloorToInt(localPosition.x / cellSize);
        int y = Mathf.FloorToInt(localPosition.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 CellCenter(Vector2Int cell, float y)
    {
        Vector3 mapCorner = transform.position;
        return new Vector3(mapCorner.x + (cell.x + 0.5f) * cellSize, y, mapCorner.z + (cell.y + 0.5f) * cellSize);
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    public bool TryGetCellFromRay(Ray ray, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, GroundHeight, 0f));
        if (!groundPlane.Raycast(ray, out float distance)) return false;

        cell = WorldToCell(ray.GetPoint(distance));
        return InBounds(cell);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return InBounds(cell) && walkableMap[cell.x, cell.y];
    }

    public int GetCost(Vector2Int cell)
    {
        TileData tile = GetTileData(cell);
        return tile != null ? Mathf.Max(1, tile.moveCost) : 1;
    }

    #endregion

    #region Visuals

#if UNITY_EDITOR
    private void OnSceneGUI(SceneView view)
    {
        GetHover().OnSceneGUI(view);
    }
#endif

    private void OnDrawGizmos()
    {
        Vector3 tileSize = new Vector3(cellSize, 0.01f, cellSize);

        if (showBlockedTiles) DrawBlockedTilesGizmos(tileSize);
        if (drawAllCells && walkableMap.Length > 0) DrawAllCellsGizmos();
        if (showHover) GetHover().DrawGizmos(tileSize);
    }

    private void DrawBlockedTilesGizmos(Vector3 tileSize)
    {
        Gizmos.color = blockedColor;
        foreach (TileData tile in tiles)
        {
            if (tile == null || tile.walkable || tile.material != null || !InBounds(tile.cell)) continue;
            Gizmos.DrawCube(CellCenter(tile.cell, GroundHeight + 0.01f), tileSize);
        }
    }

    private void DrawAllCellsGizmos()
    {
        Vector3 smallTileSize = new Vector3(cellSize * 0.9f, 0.02f, cellSize * 0.9f);
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (walkableMap[x, y])
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                }
                else
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                }
                Gizmos.DrawCube(CellCenter(cell, GroundHeight + 0.02f), smallTileSize);
            }
        }
    }

    #endregion

}