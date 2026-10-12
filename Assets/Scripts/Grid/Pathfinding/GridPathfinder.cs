using FuturatsTest.Grid;
using System.Collections.Generic;
using UnityEngine;

using FuturatsTest.Units;

namespace FuturatsTest.Grid.Pathfinding
{
    public static class GridPathfinder
    {

        private static readonly Vector2Int[] NEIGHBOUR_DIRECTIONS = {
        new Vector2Int( 1,  0),  // Right
        new Vector2Int(-1,  0),  // Left
        new Vector2Int( 0,  1),  // Up
        new Vector2Int( 0, -1),  // Down
        new Vector2Int( 1,  1),  // Right Up
        new Vector2Int( 1, -1),  // Right Down
        new Vector2Int(-1,  1),  // Left Up
        new Vector2Int(-1, -1),  // Left Down
    };

        public static List<Vector2Int> FindPath(GridManager grid, BaseUnit unit, Vector2Int start, Vector2Int end, bool avoidCurrentOccupancy, bool allowDiagonal = true, int maxCost = int.MaxValue)
        {
            if (!grid.InBounds(start))
            {
                // Invalid Path
                return null;
            }

            bool[,] blockedMap = grid.GetOccupancy().BuildBlockedMap(unit, avoidCurrentOccupancy); // Tiles the unit can't go on (not walkable/blocked/units moving towards it)

            if (!CanUnitFit(grid, unit, blockedMap, end)) return null;

            GridPathNode[,] nodes = CreateNodes(grid.Width, grid.Height);
            GridPathNode startNode = nodes[start.x, start.y];
            GridPathNode endNode = nodes[end.x, end.y];

            List<GridPathNode> openList = new List<GridPathNode> { startNode };   // nodes still to check
            HashSet<GridPathNode> closedList = new HashSet<GridPathNode>();       // nodes already checked

            startNode.gCost = 0;
            startNode.hCost = CalculateDistanceCost(startNode, endNode);
            startNode.CalculateFCost();

            while (openList.Count > 0)
            {
                GridPathNode currentNode = GetLowestFCostNode(openList);
                if (currentNode == endNode)
                {
                    // Reached final node
                    return CalculatePath(endNode);
                }

                openList.Remove(currentNode);
                closedList.Add(currentNode);

                foreach (GridPathNode neighbourNode in GetNeighbourList(nodes, currentNode, allowDiagonal))
                {
                    if (closedList.Contains(neighbourNode)) continue;

                    if (!CanUnitFit(grid, unit, blockedMap, neighbourNode.GetPosition()))
                    {
                        // The unit can never stand here
                        closedList.Add(neighbourNode);
                        continue;
                    }

                    if (IsCuttingCorner(grid, unit, blockedMap, currentNode, neighbourNode)) continue;

                    int tentativeGCost = currentNode.gCost + CalculateStepCost(grid, unit, neighbourNode);

                    if (tentativeGCost > maxCost)
                    {
                        // Not enough movement points to get here this way
                        continue;
                    }

                    if (tentativeGCost < neighbourNode.gCost)
                    {
                        neighbourNode.previousNode = currentNode;
                        neighbourNode.gCost = tentativeGCost;
                        neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endNode);
                        neighbourNode.CalculateFCost();

                        if (!openList.Contains(neighbourNode))
                        {
                            openList.Add(neighbourNode);
                        }
                    }
                }
            }

            // Out of nodes on the openList
            return null;
        }

        private static GridPathNode[,] CreateNodes(int width, int height)
        {
            GridPathNode[,] nodes = new GridPathNode[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    nodes[x, y] = new GridPathNode(x, y);
                }
            }
            return nodes;
        }

        private static List<GridPathNode> GetNeighbourList(GridPathNode[,] nodes, GridPathNode currentNode, bool allowDiagonal)
        {
            List<GridPathNode> neighbourList = new List<GridPathNode>();
            int width = nodes.GetLength(0);
            int height = nodes.GetLength(1);

            foreach (Vector2Int direction in NEIGHBOUR_DIRECTIONS)
            {
                bool isDiagonal = direction.x != 0 && direction.y != 0;
                if (isDiagonal && !allowDiagonal) continue;

                int x = currentNode.x + direction.x;
                int y = currentNode.y + direction.y;
                if (x >= 0 && y >= 0 && x < width && y < height)
                {
                    neighbourList.Add(nodes[x, y]);
                }
            }
            return neighbourList;
        }

        private static bool CanUnitFit(GridManager grid, BaseUnit unit, bool[,] blockedMap, Vector2Int position)
        {
            foreach (Vector2Int offset in unit.UnitTiles)
            {
                Vector2Int tile = position + offset;
                if (!grid.InBounds(tile) || blockedMap[tile.x, tile.y])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// A diagonal step is only allowed if the unit also fits on both straight neighbours,
        /// otherwise it would squeeze through the corner between two blocked tiles.
        /// </summary>
        private static bool IsCuttingCorner(GridManager grid, BaseUnit unit, bool[,] blockedMap, GridPathNode fromNode, GridPathNode toNode)
        {
            bool isDiagonal = fromNode.x != toNode.x && fromNode.y != toNode.y;
            if (!isDiagonal) return false;

            Vector2Int sideA = new Vector2Int(toNode.x, fromNode.y);
            Vector2Int sideB = new Vector2Int(fromNode.x, toNode.y);
            return !CanUnitFit(grid, unit, blockedMap, sideA) || !CanUnitFit(grid, unit, blockedMap, sideB);
        }

        private static int CalculateStepCost(GridManager grid, BaseUnit unit, GridPathNode toNode)
        {
            return grid.GetCostForUnit(unit, toNode.GetPosition());
        }

        private static int CalculateDistanceCost(GridPathNode a, GridPathNode b)
        {
            int xDistance = Mathf.Abs(a.x - b.x);
            int yDistance = Mathf.Abs(a.y - b.y);
            return Mathf.Max(xDistance, yDistance);
        }

        private static GridPathNode GetLowestFCostNode(List<GridPathNode> pathNodeList)
        {
            GridPathNode lowestFCostNode = pathNodeList[0];
            for (int i = 1; i < pathNodeList.Count; i++)
            {
                GridPathNode node = pathNodeList[i];
                bool isLower = node.fCost < lowestFCostNode.fCost;
                bool isTieButCloser = node.fCost == lowestFCostNode.fCost && node.hCost < lowestFCostNode.hCost;

                if (isLower || isTieButCloser)
                {
                    lowestFCostNode = node;
                }
            }
            return lowestFCostNode;
        }

        private static List<Vector2Int> CalculatePath(GridPathNode endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            path.Add(endNode.GetPosition());

            GridPathNode currentNode = endNode;
            while (currentNode.previousNode != null)
            {
                path.Add(currentNode.previousNode.GetPosition());
                currentNode = currentNode.previousNode;
            }

            path.Reverse();
            return path;
        }
    }
}
