using System.Collections.Generic;
using UnityEngine;
using FuturatsTest.Units;

namespace FuturatsTest.Grid
{
    public class GridOccupancy
    {

        private GridManager grid;
        private BaseUnit[,] occupantMap = new BaseUnit[0, 0]; // which unit is on which tile right now
        private List<BaseUnit> units = new List<BaseUnit>();

        public GridOccupancy(GridManager grid)
        {
            this.grid = grid;
        }

        public void ResetOccupancy(int width, int height)
        {
            occupantMap = new BaseUnit[width, height];
        }

        public List<BaseUnit> Units { get => units; }

        public BaseUnit GetOccupant(Vector2Int cell)
        {
            if (IsInsideMap(cell))
            {
                return occupantMap[cell.x, cell.y];
            }
            else
            {
                return null;
            }
        }

        private bool IsInsideMap(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < occupantMap.GetLength(0) && cell.y < occupantMap.GetLength(1);
        }


        public void RegisterUnit(BaseUnit unit)
        {
            if (!units.Contains(unit)) units.Add(unit);
        }

        public void UnRegisterUnit(BaseUnit unit)
        {
            units.Remove(unit);
            for (int x = 0; x < occupantMap.GetLength(0); x++)
            {
                for (int y = 0; y < occupantMap.GetLength(1); y++)
                {
                    if (occupantMap[x, y] == unit) occupantMap[x, y] = null;
                }
            }
        }

        public void SetOccupancy(BaseUnit unit, Vector2Int pivot, bool occupy)
        {
            foreach (Vector2Int offset in unit.UnitTiles)
            {
                Vector2Int cell = pivot + offset;
                if (!IsInsideMap(cell)) continue;

                if (occupy)
                {
                    occupantMap[cell.x, cell.y] = unit;
                }
                else if (occupantMap[cell.x, cell.y] == unit)
                {
                    occupantMap[cell.x, cell.y] = null;
                }
            }
        }

        public bool IsFreeForUnit(BaseUnit unit, Vector2Int pivot)
        {
            foreach (Vector2Int offset in unit.UnitTiles)
            {
                // Walkable
                Vector2Int cell = pivot + offset;
                if (!grid.IsWalkable(cell)) return false;

                // No occupants
                BaseUnit otherUnit = GetOccupant(cell);
                if (otherUnit != null && otherUnit != unit) return false;
            }
            return true;
        }


        /// <summary> Check walkable, occupant, unit going there. Unit passing through dont count /// </summary>
        public bool IsValidDestination(BaseUnit unit, Vector2Int pivot)
        {
            if (!grid.IsWalkableForUnit(unit, pivot)) return false;

            // Tiles the unit would cover
            HashSet<Vector2Int> myTiles = new HashSet<Vector2Int>();
            foreach (Vector2Int offset in unit.UnitTiles)
            {
                myTiles.Add(pivot + offset);
            }

            // Compare with where every other unit is going to end up
            foreach (BaseUnit otherUnit in units)
            {
                if (otherUnit == unit) continue;

                foreach (Vector2Int offset in otherUnit.UnitTiles)
                {
                    if (myTiles.Contains(otherUnit.Destination + offset)) return false;
                }
            }
            return true;
        }

        public bool TryGetDestination(BaseUnit unit, Vector2Int clickedCell, out Vector2Int pivot)
        {
            pivot = Vector2Int.zero;
            if (!grid.IsWalkable(clickedCell)) return false;

            // Tries to center unit, if there is no center, use bottom left of center
            Vector2Int centeredPivot = clickedCell - unit.CenterOffset;
            if (IsValidDestination(unit, centeredPivot))
            {
                pivot = centeredPivot;
                return true;
            }

            // Try every placement where some part of the unit covers the clicked tile and keep the one closest to the centered placement
            bool found = false;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int offset in unit.UnitTiles)
            {
                Vector2Int candidate = clickedCell - offset;
                int distance = (candidate - centeredPivot).sqrMagnitude;
                if (distance < bestDistance && IsValidDestination(unit, candidate))
                {
                    bestDistance = distance;
                    pivot = candidate;
                    found = true;
                }
            }
            return found;
        }

        public bool[,] BuildBlockedMap(BaseUnit self, bool includeCurrentOccupancy)
        {
            bool[,] blockedMap = new bool[grid.Width, grid.Height];

            // Not walkable or current occupants
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BaseUnit occupant = GetOccupant(cell);
                    bool isOccupiedByOther = occupant != null && occupant != self;
                    blockedMap[x, y] = !grid.IsWalkable(cell) || (includeCurrentOccupancy && isOccupiedByOther);
                }
            }

            // Destination
            foreach (BaseUnit otherUnit in units)
            {
                if (otherUnit == self) continue;

                foreach (Vector2Int offset in otherUnit.UnitTiles)
                {
                    Vector2Int cell = otherUnit.Destination + offset;
                    if (grid.InBounds(cell)) blockedMap[cell.x, cell.y] = true;
                }
            }
            return blockedMap;
        }


        public bool TryReserveStep(BaseUnit unit, Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> positions = GetStepPositions(from, to);

            foreach (Vector2Int position in positions)
            {
                if (!IsFreeForUnit(unit, position)) return false;
            }

            foreach (Vector2Int position in positions)
            {
                SetOccupancy(unit, position, true);
            }
            return true;
        }

        public void CompleteStep(BaseUnit unit, Vector2Int from, Vector2Int to)
        {
            foreach (Vector2Int position in GetStepPositions(from, to))
            {
                SetOccupancy(unit, position, false); // Release tiles during movement
            }
            SetOccupancy(unit, to, true);
        }

        private List<Vector2Int> GetStepPositions(Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> positions = new List<Vector2Int> { from, to };

            Vector2Int step = to - from;
            bool isDiagonal = step.x != 0 && step.y != 0;
            // Add neighbours to avoid passing through units
            if (isDiagonal)
            {
                positions.Add(new Vector2Int(from.x + step.x, from.y));
                positions.Add(new Vector2Int(from.x, from.y + step.y));
            }
            return positions;
        }
    }
}
