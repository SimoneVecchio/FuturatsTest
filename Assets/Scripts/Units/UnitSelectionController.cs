using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using FuturatsTest.Grid;

namespace FuturatsTest.Units
{
    public class UnitSelectionController : MonoBehaviour
    {

        [Header("Input")]
        [SerializeField] private InputActionReference selectAction;
        [SerializeField] private InputActionReference deselectAction;
        [SerializeField] private InputActionReference pointAction;

        public Camera cam;
        public float maxRayDistance = 1000f;

        private BaseUnit selectedUnit;


        public BaseUnit SelectedUnit { get => selectedUnit; }


        private void OnEnable()
        {
            if (selectAction != null) selectAction.action.Enable();
            if (deselectAction != null) deselectAction.action.Enable();
            if (pointAction != null) pointAction.action.Enable();
        }

        private void OnDisable()
        {
            if (selectAction != null) selectAction.action.Disable();
            if (deselectAction != null) deselectAction.action.Disable();
            if (pointAction != null) pointAction.action.Disable();
        }

        private void Start()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            if (SelectPressed())
            {
                HandleLeftClick();
            }
            if (DeselectPressed())
            {
                Select(null);
            }
        }

        private void HandleLeftClick()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(PointerPosition());

            // Clicked a unit's model directly (not the selected unit) select it
            BaseUnit clickedUnit = GetClickedUnit(ray);
            if (clickedUnit != null && clickedUnit != selectedUnit)
            {
                TrySelect(clickedUnit);
                return;
            }

            if (!TryGetClickedTile(grid, ray, out Vector2Int clickedTile))
            {
                Debug.Log("Click missed the map");
                return;
            }

            // if the click landed on a tile a unit stands on (in the gap between its models) treat it as clicking that unit
            BaseUnit unitOnTile = grid.GetOccupancy().GetOccupant(clickedTile);
            if (unitOnTile != null)
            {
                if (unitOnTile != selectedUnit)
                {
                    TrySelect(unitOnTile);
                }
                return;
            }

            if (selectedUnit == null)
            {
                Debug.Log("No unit selected: click a unit first");
                return;
            }

            MoveSelectedUnit(grid, clickedTile);
        }

        private BaseUnit GetClickedUnit(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
            {
                return hit.collider.GetComponentInParent<BaseUnit>();
            }
            return null;
        }

        private bool TryGetClickedTile(GridManager grid, Ray ray, out Vector2Int tile)
        {
            tile = default(Vector2Int);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, grid.GroundHeight, 0f));
            Vector3 point;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance) && hit.collider.GetComponentInParent<BaseUnit>() == null)
            {
                point = hit.point - hit.normal * 0.01f;
            }
            else if (groundPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
            }
            else
            {
                return false;
            }

            tile = grid.WorldToCell(point);
            return true;
        }

        private void MoveSelectedUnit(GridManager grid, Vector2Int clickedTile)
        {
            if (!grid.GetOccupancy().TryGetDestination(selectedUnit, clickedTile, out Vector2Int destination))
            {
                Debug.Log("Tile " + clickedTile + ": not walkable or no room for " + selectedUnit.name + " (" + selectedUnit.UnitTiles.Length + " tiles)");
                return;
            }

            if (selectedUnit.MoveToDestination(destination))
            {
                // The unit deselected itself, so forget it
                selectedUnit = null;
            }
            else
            {
                Debug.Log("Tile " + clickedTile + " is not reachable for " + selectedUnit.name);
            }
        }


        private void TrySelect(BaseUnit unit)
        {
            if (unit.IsMoving)
            {
                Debug.Log(unit.name + " is moving: wait until it arrives to select it");
            }
            else
            {
                Select(unit);
            }
        }

        private void Select(BaseUnit unit)
        {
            if (selectedUnit != null) selectedUnit.SetSelected(false);
            selectedUnit = null;

            // SetSelected refuses units that are still moving
            if (unit != null && unit.SetSelected(true))
            {
                selectedUnit = unit;
            }
        }

        private bool SelectPressed()
        {
            return selectAction != null && selectAction.action.WasPressedThisFrame();
        }

        private bool DeselectPressed()
        {
            return deselectAction != null && deselectAction.action.WasPressedThisFrame();
        }

        private Vector2 PointerPosition()
        {
            return pointAction != null ? pointAction.action.ReadValue<Vector2>() : Vector2.zero;
        }
    }
}