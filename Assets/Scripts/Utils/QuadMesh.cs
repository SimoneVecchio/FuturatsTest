using System.Collections.Generic;
using UnityEngine;

public class QuadMesh
{

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector3> normals = new List<Vector3>();
    private List<Vector2> uv = new List<Vector2>();

    public bool IsEmpty { get => vertices.Count == 0; }

    /// <summary>Adds a square that shows the part of the texture from uvMin to uvMax</summary>
    /// <param name="corner">Bottom left corner</param>
    /// <param name="size">Length of each side</param>
    /// <param name="uvMin">Bottom left corner </param>
    /// <param name="uvMax">Top right corner</param>
    public void AddSquare(Vector3 corner, float size, Vector2 uvMin, Vector2 uvMax)
    {
        AddSquare(corner, size,
            new Vector2(uvMin.x, uvMin.y),
            new Vector2(uvMax.x, uvMin.y),
            new Vector2(uvMin.x, uvMax.y),
            new Vector2(uvMax.x, uvMax.y));
    }

    /// <summary>Adds a square with its own texture point for each corner, needed to rotate the texture</summary>
    public void AddSquare(Vector3 corner, float size, Vector2 uvBottomLeft, Vector2 uvBottomRight, Vector2 uvTopLeft, Vector2 uvTopRight)
    {
        // The new vertices are added after the ones already added, so the triangles points there
        int firstIndex = vertices.Count;

        // 2 - 3
        // |   |
        // 0 - 1
        Vector3[] squareVertices = new Vector3[4]
        {
            corner,                                  // 0 bottom left
            corner + new Vector3(size, 0, 0),        // 1 bottom right
            corner + new Vector3(0, 0, size),        // 2 top left
            corner + new Vector3(size, 0, size)      // 3 top right
        };
        vertices.AddRange(squareVertices);

        // Which side is the front depends on the order of the corners, going clockwise as seen by the camera it makes them the front (back isnt drawn)
        int[] squareTriangles = new int[6]
        {
            // lower left triangle
            firstIndex + 0, firstIndex + 2, firstIndex + 1,
            // upper right triangle
            firstIndex + 2, firstIndex + 3, firstIndex + 1
        };
        triangles.AddRange(squareTriangles);

        // Each vertex normal
        Vector3[] squareNormals = new Vector3[4]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };
        normals.AddRange(squareNormals);

        Vector2[] squareUv = new Vector2[4]
        {
            uvBottomLeft,
            uvBottomRight,
            uvTopLeft,
            uvTopRight
        };
        uv.AddRange(squareUv);
    }

    public Mesh ToMesh(string meshName)
    {
        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.hideFlags = HideFlags.DontSave;

        // More than 65,000 vertices (big maps) need 32-bit indices
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uv.ToArray();

        mesh.RecalculateBounds();
        return mesh;
    }
}