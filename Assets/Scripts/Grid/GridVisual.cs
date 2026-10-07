using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridVisual
{

    private const string VISUALS_NAME = "Visuals";

    // Rebuilt from gridmanager configs no need to save, only the prafbs are saved
    private const HideFlags GENERATED_FLAGS = HideFlags.DontSave | HideFlags.NotEditable;

    private GridManager grid;
    private List<Mesh> generatedMeshes = new List<Mesh>();

    public GridVisual(GridManager grid)
    {
        this.grid = grid;
    }

    public void Refresh()
    {
        Clear();

        GameObject visualsRoot = new GameObject(VISUALS_NAME);
        visualsRoot.transform.SetParent(grid.transform, false);

        QuadMesh groundSquares = new QuadMesh();
        Dictionary<Material, List<Vector2Int>> tilesByMaterial = new Dictionary<Material, List<Vector2Int>>();
        float cellSize = grid.cellSize;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GridManager.TileData tileData = grid.GetTileData(cell);
                Material ownMaterial = tileData != null ? tileData.material : null;

                if (ownMaterial == null)
                {
                    // Show this tile's piece of the ground texture. 0,0 is bottom left and 1,1 is top right
                    Vector3 corner = new Vector3(x * cellSize, 0f, y * cellSize);
                    Vector2 uvMin = new Vector2((float)x / grid.Width, (float)y / grid.Height);
                    Vector2 uvMax = new Vector2((float)(x + 1) / grid.Width, (float)(y + 1) / grid.Height);
                    groundSquares.AddSquare(corner, cellSize, uvMin, uvMax);
                }
                else
                {
                    // Tile with its own material
                    if (!tilesByMaterial.ContainsKey(ownMaterial))
                    {
                        tilesByMaterial[ownMaterial] = new List<Vector2Int>();
                    }
                    tilesByMaterial[ownMaterial].Add(cell);
                }
            }
        }

        // Create the game objects
        CreateVisualObject("Ground", groundSquares, grid.groundMaterial, visualsRoot.transform);
        foreach (KeyValuePair<Material, List<Vector2Int>> pair in tilesByMaterial)
        {
            QuadMesh squares = new QuadMesh();
            foreach (List<Vector2Int> group in FindTouchingGroups(pair.Value))
            {
                AddGroupSquares(squares, group, GetTextureMode(pair.Key));
            }
            CreateVisualObject(pair.Key.name, squares, pair.Key, visualsRoot.transform);
        }

        // Lock them so nobody edits them by mistake
        visualsRoot.hideFlags = GENERATED_FLAGS;

#if UNITY_EDITOR
        SceneVisibilityManager.instance.DisablePicking(visualsRoot, true);
#endif
    }


    /// <summary> Splits the tiles that use one material into groups of tiles that touch each other. Should be used for merging texturs like masses of water etc </summary>
    private static List<List<Vector2Int>> FindTouchingGroups(List<Vector2Int> cells)
    {
        HashSet<Vector2Int> notGroupedYet = new HashSet<Vector2Int>(cells);
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        foreach (Vector2Int start in cells)
        {
            if (!notGroupedYet.Remove(start)) continue; // already in a group

            List<Vector2Int> group = new List<Vector2Int>();
            Stack<Vector2Int> toCheck = new Stack<Vector2Int>();
            toCheck.Push(start);

            while (toCheck.Count > 0)
            {
                Vector2Int cell = toCheck.Pop();
                group.Add(cell);

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int neighbour = cell + direction;
                    if (notGroupedYet.Remove(neighbour))
                    {
                        toCheck.Push(neighbour);
                    }
                }
            }

            groups.Add(group);
        }
        return groups;
    }

    private GridManager.TextureMode GetTextureMode(Material material)
    {
        foreach (GridManager.MaterialMode entry in grid.materialModes)
        {
            if (entry != null && entry.material == material) return entry.mode;
        }
        return GridManager.TextureMode.StretchOverGroup;
    }

    private void AddGroupSquares(QuadMesh squares, List<Vector2Int> group, GridManager.TextureMode mode)
    {
        float cellSize = grid.cellSize;

        Vector2Int min = group[0];
        Vector2Int max = group[0];
        foreach (Vector2Int cell in group)
        {
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);
        }
        int groupWidth = max.x - min.x + 1;
        int groupHeight = max.y - min.y + 1;
        HashSet<Vector2Int> groupCells = mode == GridManager.TextureMode.Border ? new HashSet<Vector2Int>(group) : null;

        foreach (Vector2Int cell in group)
        {
            Vector3 corner = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
            int x = cell.x - min.x;   // position inside the group
            int y = cell.y - min.y;

            if (mode == GridManager.TextureMode.Border)
            {
                AddBorderSquare(squares, corner, cell, groupCells);
            }
            else if (mode == GridManager.TextureMode.StretchOverGroup)
            {
                Vector2 uvMin = new Vector2((float)x / groupWidth, (float)y / groupHeight);
                Vector2 uvMax = new Vector2((float)(x + 1) / groupWidth, (float)(y + 1) / groupHeight);
                squares.AddSquare(corner, cellSize, uvMin, uvMax);
            }
            else if (groupWidth >= groupHeight)
            {
                // Horizontal strip
                float t = groupHeight;
                squares.AddSquare(corner, cellSize,
                    new Vector2(x / t, y / t), new Vector2((x + 1) / t, y / t),
                    new Vector2(x / t, (y + 1) / t), new Vector2((x + 1) / t, (y + 1) / t));
            }
            else
            {
                // Vertical strip, rotated by 90 degrees
                float t = groupWidth;
                squares.AddSquare(corner, cellSize,
                    new Vector2(y / t, 1 - x / t), new Vector2(y / t, 1 - (x + 1) / t),
                    new Vector2((y + 1) / t, 1 - x / t), new Vector2((y + 1) / t, 1 - (x + 1) / t));
            }
        }
    }

    /// <summary> 9 slice: picks the piece of the 3x3 texture based on which neighbours are in the group </summary>
    private void AddBorderSquare(QuadMesh squares, Vector3 corner, Vector2Int cell, HashSet<Vector2Int> groupCells)
    {
        bool hasLeft = groupCells.Contains(cell + Vector2Int.left);
        bool hasRight = groupCells.Contains(cell + Vector2Int.right);
        bool hasDown = groupCells.Contains(cell + Vector2Int.down);
        bool hasUp = groupCells.Contains(cell + Vector2Int.up);

        // 0 = left/bottom, 1 = middle, 2 = right/top
        int column = !hasLeft ? 0 : !hasRight ? 2 : 1;
        int row = !hasDown ? 0 : !hasUp ? 2 : 1;

        Vector2 uvMin = new Vector2(column / 3f, row / 3f);
        Vector2 uvMax = new Vector2((column + 1) / 3f, (row + 1) / 3f);
        squares.AddSquare(corner, grid.cellSize, uvMin, uvMax);
    }

    private void CreateVisualObject(string objectName, QuadMesh squares, Material material, Transform parent)
    {
        if (squares.IsEmpty) return;

        Mesh mesh = squares.ToMesh(objectName);
        generatedMeshes.Add(mesh); // destroyed on the next rebuild

        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(parent, false);
        visualObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        visualObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        visualObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        visualObject.hideFlags = GENERATED_FLAGS;
    }

    private void Clear()
    {
        Transform oldVisuals = grid.transform.Find(VISUALS_NAME);
        if (oldVisuals != null) DestroySafe(oldVisuals.gameObject);

        foreach (Mesh mesh in generatedMeshes)
        {
            DestroySafe(mesh);
        }
        generatedMeshes.Clear();
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;

        if (Application.isPlaying)
        {
            Object.Destroy(obj);
        }
        else
        {
            Object.DestroyImmediate(obj);
        }
    }
}