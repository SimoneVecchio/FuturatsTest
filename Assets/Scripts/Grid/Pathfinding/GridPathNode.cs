using UnityEngine;


namespace FuturatsTest.Grid.Pathfinding
{
    public class GridPathNode
    {

        public int x;
        public int y;

        public int gCost;   // what it cost to get here from the start
        public int hCost;   // guess of what it still costs to reach the end
        public int fCost;   // gCost + hCost: the total, used to pick the next node

        public GridPathNode previousNode;   // the node we came from (used to build the path at the end)

        public GridPathNode(int x, int y)
        {
            this.x = x;
            this.y = y;
            gCost = int.MaxValue;
            previousNode = null;
        }

        public void CalculateFCost()
        {
            fCost = gCost + hCost;
        }

        public Vector2Int GetPosition()
        {
            return new Vector2Int(x, y);
        }

        public override string ToString()
        {
            return x + "," + y;
        }

    }
}
