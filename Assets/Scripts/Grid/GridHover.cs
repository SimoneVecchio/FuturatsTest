using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridHover
{

    private GridManager grid;

    private bool isGameHoverValid;
    private bool isSceneHoverValid;
    private Vector2Int gameHoverCell;
    private Vector2Int sceneHoverCell;

    public GridHover(GridManager grid)
    {
        this.grid = grid;
    }

    /// <summary>Game view: follow the mouse while playing.</summary>
    public void UpdateGameHover()
    {
        if (!Application.isPlaying || !grid.showHover) return;

        Camera cam = Camera.main;
        isGameHoverValid = cam != null && grid.TryGetCellFromRay(cam.ScreenPointToRay(GetMousePosition()), out gameHoverCell);
    }

#if UNITY_EDITOR
    public void OnSceneGUI(SceneView view)
    {
        if (!grid.showHover) return;
        if (!view.wantsMouseMove) view.wantsMouseMove = true; // ask the Scene view to send mouse-move events

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseLeaveWindow)
        {
            isSceneHoverValid = false;
            view.Repaint();
            return;
        }
        if (currentEvent.type != EventType.MouseMove && currentEvent.type != EventType.MouseDrag) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        bool isValid = grid.TryGetCellFromRay(ray, out Vector2Int cell);

        // Only repaint when the hovered tile actually changes
        if (isValid != isSceneHoverValid || cell != sceneHoverCell)
        {
            isSceneHoverValid = isValid;
            sceneHoverCell = cell;
            view.Repaint();
        }
    }
#endif

    public void DrawGizmos(Vector3 tileSize)
    {
        Gizmos.color = grid.hoverColor;
        if (isSceneHoverValid)
        {
            Gizmos.DrawWireCube(grid.CellCenter(sceneHoverCell, grid.GroundHeight + 0.02f), tileSize);
        }
        if (isGameHoverValid && Application.isPlaying)
        {
            Gizmos.DrawWireCube(grid.CellCenter(gameHoverCell, grid.GroundHeight + 0.02f), tileSize);
        }
    }

    private static Vector2 GetMousePosition()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }
}