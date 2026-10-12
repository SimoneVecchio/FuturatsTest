using UnityEditor;
using UnityEngine;

using FuturatsTest.Units;

[CustomEditor(typeof(BaseUnit))]
public class UnitEditor : Editor
{

    private const int SUB_UNIT_BUTTON_SIZE = 40;

    private static readonly Color SELECTED_COLOR = new Color(1f, 0.85f, 0.3f);
    private static readonly Color OWN_PREFAB_COLOR = new Color(0.45f, 0.75f, 1f);
    private static readonly Color DEFAULT_PREFAB_COLOR = new Color(0.75f, 0.75f, 0.75f);

    private Vector2Int selectedSubUnit;


    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BaseUnit unit = (BaseUnit)target;
        if (targets.Length > 1) return; // sub unit editing works on one unit at a time

        if (PrefabUtility.IsPartOfPrefabAsset(unit))
        {
            EditorGUILayout.HelpBox("Open the prefab to edit its sub units.", MessageType.Info);
            return;
        }

        int width = Mathf.Max(1, unit.Width);
        int depth = Mathf.Max(1, unit.Depth);
        if (selectedSubUnit.x >= width || selectedSubUnit.y >= depth)
        {
            selectedSubUnit = Vector2Int.zero;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sub Units", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("P = pivot", MessageType.None);

        DrawSubUnitGrid(unit, width, depth);
        DrawSelectedSubUnitPrefab(unit);
        DrawButtons(unit);
    }

    private void DrawSubUnitGrid(BaseUnit unit, int width, int depth)
    {
        Color oldColor = GUI.backgroundColor;

        for (int z = depth - 1; z >= 0; z--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < width; x++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                bool hasOwnPrefab = unit.SubUnitHasOwnPrefab(cell);

                if (cell == selectedSubUnit)
                {
                    GUI.backgroundColor = SELECTED_COLOR;
                }
                else if (hasOwnPrefab)
                {
                    GUI.backgroundColor = OWN_PREFAB_COLOR;
                }
                else
                {
                    GUI.backgroundColor = DEFAULT_PREFAB_COLOR;
                }

                GameObject prefab = unit.GetSubUnitPrefab(cell);
                string prefabName = prefab != null ? prefab.name : "Cube";
                string label = (cell == Vector2Int.zero ? "P\n" : "\n") + ShortName(prefabName);
                string tooltip = "(" + x + "," + z + ") " + prefabName + (hasOwnPrefab ? "" : " (default)");

                if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Width(SUB_UNIT_BUTTON_SIZE), GUILayout.Height(SUB_UNIT_BUTTON_SIZE)))
                {
                    selectedSubUnit = cell;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        GUI.backgroundColor = oldColor;
    }

    private void DrawSelectedSubUnitPrefab(BaseUnit unit)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sub Unit (" + selectedSubUnit.x + ", " + selectedSubUnit.y + ")", EditorStyles.miniBoldLabel);

        GameObject currentPrefab = unit.SubUnitHasOwnPrefab(selectedSubUnit) ? unit.GetSubUnitPrefab(selectedSubUnit) : null;

        EditorGUI.BeginChangeCheck();
        GameObject pickedPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Prefab", "Empty = use the Default Model."), currentPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(unit, "Change Sub Unit Prefab");
            unit.SetSubUnitPrefab(selectedSubUnit, pickedPrefab);
            EditorUtility.SetDirty(unit);
        }
    }

    private void DrawButtons(BaseUnit unit)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(!unit.SubUnitHasOwnPrefab(selectedSubUnit));
        if (GUILayout.Button("Use Default For This Sub Unit"))
        {
            Undo.RecordObject(unit, "Reset Sub Unit Prefab");
            unit.SetSubUnitPrefab(selectedSubUnit, null);
            EditorUtility.SetDirty(unit);
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Reset All Sub Units"))
        {
            Undo.RecordObject(unit, "Reset All Sub Unit Prefabs");
            unit.ClearSubUnitPrefabs();
            EditorUtility.SetDirty(unit);
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button(new GUIContent("Rebuild Models", "Respawns all models")))
        {
            unit.BuildModels();
        }
    }

    private static string ShortName(string prefabName)
    {
        if (prefabName.Length <= 5)
        {
            return prefabName;
        }
        else
        {
            return prefabName.Substring(0, 5);
        }
    }
}