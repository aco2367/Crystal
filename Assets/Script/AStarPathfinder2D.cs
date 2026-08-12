using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder2D : MonoBehaviour
{
    public static AStarPathfinder2D Instance { get; private set; }

    [Header("Grid")]
    public Vector2 gridWorldSize = new Vector2(60f, 40f);
    public float cellSize = 1f;
    public bool allowDiagonalMovement = true;

    [Header("Collision")]
    public LayerMask blockedLayers;
    public float collisionCheckRadius = 0.45f;

    [Header("Scene View")]
    public bool drawGridBounds = true;
    public Color boundsColor = new Color(0f, 0.8f, 1f, 0.8f);
    public Color blockedColor = new Color(1f, 0.1f, 0.1f, 0.25f);
    public bool drawBlockedCellsWhenSelected;

    private int gridWidth;
    private int gridHeight;
    private Vector2 gridOrigin;

    private void Awake()
    {
        Instance = this;
        RebuildGridSize();
    }

    private void OnValidate()
    {
        RebuildGridSize();
    }

    public bool TryFindPath(Vector2 startWorldPosition, Vector2 targetWorldPosition, List<Vector2> path)
    {
        if (path == null)
            return false;

        path.Clear();
        RebuildGridSize();

        GridPosition start = WorldToGrid(startWorldPosition);
        GridPosition target = WorldToGrid(targetWorldPosition);

        if (!TryFindNearestWalkable(start, out start))
            return false;

        if (!TryFindNearestWalkable(target, out target))
            return false;

        List<PathNode> openNodes = new List<PathNode>();
        HashSet<GridPosition> closedPositions = new HashSet<GridPosition>();
        Dictionary<GridPosition, PathNode> knownNodes = new Dictionary<GridPosition, PathNode>();

        PathNode startNode = new PathNode(start, null, 0, GetHeuristic(start, target));
        openNodes.Add(startNode);
        knownNodes[start] = startNode;

        while (openNodes.Count > 0)
        {
            PathNode current = GetLowestCostNode(openNodes);

            if (current.Position.Equals(target))
            {
                BuildPath(current, path);
                return path.Count > 0;
            }

            openNodes.Remove(current);
            closedPositions.Add(current.Position);

            foreach (GridPosition neighborPosition in GetNeighbors(current.Position))
            {
                if (closedPositions.Contains(neighborPosition) || IsBlocked(neighborPosition))
                    continue;

                int moveCost = IsDiagonal(current.Position, neighborPosition) ? 14 : 10;
                int newCostFromStart = current.CostFromStart + moveCost;

                if (knownNodes.TryGetValue(neighborPosition, out PathNode knownNode))
                {
                    if (newCostFromStart >= knownNode.CostFromStart)
                        continue;

                    knownNode.Parent = current;
                    knownNode.CostFromStart = newCostFromStart;
                    knownNode.HeuristicCost = GetHeuristic(neighborPosition, target);
                    continue;
                }

                PathNode neighborNode = new PathNode(
                    neighborPosition,
                    current,
                    newCostFromStart,
                    GetHeuristic(neighborPosition, target)
                );

                knownNodes[neighborPosition] = neighborNode;
                openNodes.Add(neighborNode);
            }
        }

        return false;
    }

    private void RebuildGridSize()
    {
        float safeCellSize = Mathf.Max(0.05f, cellSize);
        gridWidth = Mathf.Max(1, Mathf.CeilToInt(gridWorldSize.x / safeCellSize));
        gridHeight = Mathf.Max(1, Mathf.CeilToInt(gridWorldSize.y / safeCellSize));
        gridOrigin = (Vector2)transform.position - new Vector2(gridWidth, gridHeight) * safeCellSize * 0.5f;
    }

    private GridPosition WorldToGrid(Vector2 worldPosition)
    {
        Vector2 localPosition = worldPosition - gridOrigin;
        int x = Mathf.Clamp(Mathf.FloorToInt(localPosition.x / cellSize), 0, gridWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(localPosition.y / cellSize), 0, gridHeight - 1);
        return new GridPosition(x, y);
    }

    private Vector2 GridToWorld(GridPosition position)
    {
        return gridOrigin + new Vector2((position.X + 0.5f) * cellSize, (position.Y + 0.5f) * cellSize);
    }

    private bool TryFindNearestWalkable(GridPosition origin, out GridPosition walkablePosition)
    {
        if (!IsBlocked(origin))
        {
            walkablePosition = origin;
            return true;
        }

        int maxRadius = Mathf.Max(gridWidth, gridHeight);

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
                {
                    if (x != origin.X - radius && x != origin.X + radius && y != origin.Y - radius && y != origin.Y + radius)
                        continue;

                    GridPosition candidate = new GridPosition(x, y);

                    if (!IsInsideGrid(candidate) || IsBlocked(candidate))
                        continue;

                    walkablePosition = candidate;
                    return true;
                }
            }
        }

        walkablePosition = origin;
        return false;
    }

    private IEnumerable<GridPosition> GetNeighbors(GridPosition position)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                if (!allowDiagonalMovement && x != 0 && y != 0)
                    continue;

                GridPosition neighbor = new GridPosition(position.X + x, position.Y + y);

                if (IsInsideGrid(neighbor))
                    yield return neighbor;
            }
        }
    }

    private bool IsInsideGrid(GridPosition position)
    {
        return position.X >= 0 && position.X < gridWidth && position.Y >= 0 && position.Y < gridHeight;
    }

    private bool IsBlocked(GridPosition position)
    {
        if (blockedLayers.value == 0)
            return false;

        return Physics2D.OverlapCircle(GridToWorld(position), collisionCheckRadius, blockedLayers) != null;
    }

    private int GetHeuristic(GridPosition from, GridPosition to)
    {
        int xDistance = Mathf.Abs(from.X - to.X);
        int yDistance = Mathf.Abs(from.Y - to.Y);

        if (!allowDiagonalMovement)
            return (xDistance + yDistance) * 10;

        int diagonal = Mathf.Min(xDistance, yDistance);
        int straight = Mathf.Abs(xDistance - yDistance);
        return diagonal * 14 + straight * 10;
    }

    private bool IsDiagonal(GridPosition from, GridPosition to)
    {
        return from.X != to.X && from.Y != to.Y;
    }

    private PathNode GetLowestCostNode(List<PathNode> nodes)
    {
        PathNode bestNode = nodes[0];

        for (int i = 1; i < nodes.Count; i++)
        {
            if (nodes[i].TotalCost < bestNode.TotalCost)
            {
                bestNode = nodes[i];
            }
        }

        return bestNode;
    }

    private void BuildPath(PathNode endNode, List<Vector2> path)
    {
        PathNode current = endNode;

        while (current != null)
        {
            path.Add(GridToWorld(current.Position));
            current = current.Parent;
        }

        path.Reverse();
    }

    private void OnDrawGizmos()
    {
        if (!drawGridBounds)
            return;

        RebuildGridSize();
        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWidth * cellSize, gridHeight * cellSize, 0f));
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBlockedCellsWhenSelected)
            return;

        RebuildGridSize();
        Gizmos.color = blockedColor;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridPosition position = new GridPosition(x, y);

                if (IsBlocked(position))
                    Gizmos.DrawCube(GridToWorld(position), Vector3.one * cellSize * 0.8f);
            }
        }
    }

    private struct GridPosition
    {
        public readonly int X;
        public readonly int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    private class PathNode
    {
        public GridPosition Position;
        public PathNode Parent;
        public int CostFromStart;
        public int HeuristicCost;
        public int TotalCost => CostFromStart + HeuristicCost;

        public PathNode(GridPosition position, PathNode parent, int costFromStart, int heuristicCost)
        {
            Position = position;
            Parent = parent;
            CostFromStart = costFromStart;
            HeuristicCost = heuristicCost;
        }
    }
}
