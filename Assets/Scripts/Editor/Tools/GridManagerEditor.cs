using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using FuturatsTest.Grid;

namespace FuturatsTest.Tools.Grid
{
    [CustomEditor(typeof(GridManager))]
    public class GridManagerEditor : Editor
    {
        static bool s_editing;
        static readonly HashSet<Vector2Int> s_selected = new HashSet<Vector2Int>();
        static bool s_dragAdds;

        public override void OnInspectorGUI()
        {
            var gm = (GridManager)target;
            DrawDefaultInspector();

            if (GUILayout.Button("Rebuild"))
            {
                gm.Build();
            }

            EditorGUILayout.Space(10);

            // Edit Tiles button
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = s_editing ? new Color(0.5f, 1f, 0.6f) : oldBg;
            if (GUILayout.Button(s_editing ? "Editing Tiles: ON" : "Edit Tiles", GUILayout.Height(28)))
            {
                s_editing = !s_editing;
                if (!s_editing) s_selected.Clear();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = oldBg;

            if (!s_editing) return;

            EditorGUILayout.HelpBox(
                "Scene view:\n" +
                "- Click a tile to select it\n" +
                "- Shift + click / drag to add or remove tiles\n" +
                "- Esc to clear the selection", MessageType.None);

            // Remove tiles that are outside the map after resizing
            s_selected.RemoveWhere(c => !gm.InBounds(c));

            if (s_selected.Count == 0)
            {
                EditorGUILayout.LabelField("No tile selected.");
                return;
            }

            // Select tile
            EditorGUILayout.Space(4);
            string title = s_selected.Count == 1 ? $"Tile {First()}" : $"{s_selected.Count} tiles selected";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            // Current values (mixed if they differ)
            bool firstWalkable = true, mixedWalkable = false, firstSet = false;
            int firstCost = 1;
            bool mixedCost = false;
            GameObject firstPrefab = null;
            bool mixedPrefab = false;
            Vector3 firstOffset = Vector3.zero;
            bool mixedOffset = false;
            Material firstMaterial = null;
            bool mixedMaterial = false;
            foreach (var c in s_selected)
            {
                var t = gm.GetTileData(c);
                bool walkable = t == null || t.walkable;
                int cost = t != null ? t.moveCost : 1;
                GameObject prefab = t != null ? t.prefab : null;
                Vector3 offset = t != null ? t.prefabOffset : Vector3.zero;
                Material material = t != null ? t.material : null;
                if (!firstSet)
                {
                    firstWalkable = walkable;
                    firstCost = cost;
                    firstPrefab = prefab;
                    firstOffset = offset;
                    firstMaterial = material;
                    firstSet = true;
                }
                else
                {
                    if (walkable != firstWalkable) mixedWalkable = true;
                    if (cost != firstCost) mixedCost = true;
                    if (prefab != firstPrefab) mixedPrefab = true;
                    if (offset != firstOffset) mixedOffset = true;
                    if (material != firstMaterial) mixedMaterial = true;
                }
            }

            // Walkable
            EditorGUI.showMixedValue = mixedWalkable;
            EditorGUI.BeginChangeCheck();
            bool newWalkable = EditorGUILayout.Toggle("Walkable", firstWalkable);
            if (EditorGUI.EndChangeCheck()) SetWalkable(gm, newWalkable);

            // Move cost
            EditorGUI.showMixedValue = mixedCost;
            EditorGUI.BeginChangeCheck();
            int newCost = Mathf.Max(1, EditorGUILayout.IntField("Move Cost", firstCost));
            if (EditorGUI.EndChangeCheck()) SetMoveCost(gm, newCost);

            // Prefab
            EditorGUI.showMixedValue = mixedPrefab;
            EditorGUI.BeginChangeCheck();
            var newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", firstPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) SetPrefab(gm, newPrefab);

            // prefab offset (can be set before or after choosing the prefab)
            EditorGUI.showMixedValue = mixedOffset;
            EditorGUI.BeginChangeCheck();
            Vector3 newOffset = EditorGUILayout.Vector3Field("Prefab Offset", firstOffset);
            if (EditorGUI.EndChangeCheck()) SetPrefabOffset(gm, newOffset);

            // material
            EditorGUI.showMixedValue = mixedMaterial;
            EditorGUI.BeginChangeCheck();
            var newMaterial = (Material)EditorGUILayout.ObjectField("Material", firstMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck()) SetMaterial(gm, newMaterial);
            EditorGUI.showMixedValue = false;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Block + Remove Prefab")) { SetWalkable(gm, false); SetPrefab(gm, null); }
                if (GUILayout.Button("Reset Tile(s)"))
                {
                    SetWalkable(gm, true);
                    SetMoveCost(gm, 1);
                    SetPrefab(gm, null);
                    SetPrefabOffset(gm, Vector3.zero);
                    SetMaterial(gm, null);
                }
            }
            if (GUILayout.Button("Clear Selection"))
            {
                s_selected.Clear();
                SceneView.RepaintAll();
            }
        }

        static Vector2Int First()
        {
            foreach (var c in s_selected) return c;
            return default;
        }

        // Actions

        static void SetMoveCost(GridManager gm, int cost)
        {
            Undo.RecordObject(gm, "Set Move Cost");
            foreach (var c in s_selected)
            {
                gm.GetOrCreateTileData(c).moveCost = cost;
                gm.CleanupTileData(c);
            }
            EditorUtility.SetDirty(gm);
        }

        static void SetWalkable(GridManager gm, bool walkable)
        {
            Undo.RecordObject(gm, walkable ? "Set Tiles Walkable" : "Set Tiles Not Walkable");
            foreach (var c in s_selected)
            {
                gm.GetOrCreateTileData(c).walkable = walkable;
                gm.CleanupTileData(c);
            }
            EditorUtility.SetDirty(gm);
            gm.RefreshWalkable();
            SceneView.RepaintAll();
        }

        static void SetMaterial(GridManager gm, Material material)
        {
            Undo.RecordObject(gm, material != null ? "Set Tile Material" : "Remove Tile Material");
            foreach (var c in s_selected)
            {
                gm.GetOrCreateTileData(c).material = material;
                gm.CleanupTileData(c);
            }
            EditorUtility.SetDirty(gm);
            gm.RefreshVisuals();
            SceneView.RepaintAll();
        }

        static void SetPrefabOffset(GridManager gm, Vector3 offset)
        {
            Undo.RecordObject(gm, "Set Prefab Offset");
            foreach (var c in s_selected)
            {
                var t = gm.GetOrCreateTileData(c);
                t.prefabOffset = offset;

                if (t.instance != null)
                {
                    Undo.RecordObject(t.instance.transform, "Set Prefab Offset");
                    t.instance.transform.position = gm.PrefabPosition(c);
                }

                gm.CleanupTileData(c);
            }
            EditorUtility.SetDirty(gm);
            SceneView.RepaintAll();
        }

        static void SetPrefab(GridManager gm, GameObject prefab)
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(prefab != null ? "Place Tile Prefab" : "Remove Tile Prefab");
            Undo.RecordObject(gm, "Tile Prefab");

            foreach (var c in s_selected)
            {
                var t = gm.GetOrCreateTileData(c);

                if (t.instance != null) Undo.DestroyObjectImmediate(t.instance);
                t.instance = null;
                t.prefab = prefab;

                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gm.GetPrefabsRoot(true));
                    go.transform.position = gm.PrefabPosition(c); // tile center + this tile's offset
                    go.name = $"{prefab.name} ({c.x},{c.y})";
                    Undo.RegisterCreatedObjectUndo(go, "Place Tile Prefab");
                    t.instance = go;
                }

                gm.CleanupTileData(c);
            }

            EditorUtility.SetDirty(gm);
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        // Scene view

        void OnSceneGUI()
        {
            var gm = (GridManager)target;
            if (!s_editing || Application.isPlaying) return;

            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id); // clicks select tiles, not objects

            // draw selection
            if (e.type == EventType.Repaint)
            {
                float cs = gm.cellSize, y = gm.GroundHeight + 0.03f;
                foreach (var c in s_selected)
                {
                    Vector3 p = gm.CellCenter(c, y);
                    float h = cs * 0.5f;
                    Handles.DrawSolidRectangleWithOutline(
                        new[] { p + new Vector3(-h, 0, -h), p + new Vector3(-h, 0, h), p + new Vector3(h, 0, h), p + new Vector3(h, 0, -h) },
                        new Color(1f, 0.9f, 0.2f, 0.25f), new Color(1f, 0.9f, 0.2f, 1f));
                }
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                s_selected.Clear();
                Repaint();
                e.Use();
                return;
            }

            if (e.button != 0 || e.alt) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (!gm.TryGetCellFromRay(HandleUtility.GUIPointToWorldRay(e.mousePosition), out Vector2Int cell)) return;

            bool additive = e.shift || e.control || e.command;

            if (e.type == EventType.MouseDown)
            {
                if (!additive)
                {
                    s_selected.Clear();
                    s_dragAdds = true;
                    s_selected.Add(cell);
                }
                else
                {
                    s_dragAdds = !s_selected.Contains(cell); // first tile decides: add or remove
                    if (s_dragAdds) s_selected.Add(cell); else s_selected.Remove(cell);
                }
            }
            else // drag
            {
                if (s_dragAdds) s_selected.Add(cell); else s_selected.Remove(cell);
            }

            e.Use();
            Repaint();             // refresh the inspector
            SceneView.RepaintAll();
        }
    }
}
