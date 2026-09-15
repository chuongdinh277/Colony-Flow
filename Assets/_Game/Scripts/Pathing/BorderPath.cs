using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class BorderPath : MonoBehaviour
    {
        // The ant lane belongs to the UI frame, not to the artwork silhouette.
        // Keep extra vertical breathing room between the picture and that lane.
        [SerializeField, Min(1)] private int horizontalMarginCells = 3;
        [SerializeField, Min(1)] private int verticalMarginCells = 3;
        [SerializeField, Min(0.5f)] private float entranceOffsetCells = 2.4f;
        private readonly List<Vector2Int> points = new();
        private readonly Dictionary<Vector2Int, int> indexByPoint = new();
        private int[] distanceFromSpawn = Array.Empty<int>();
        private readonly Dictionary<long, List<Vector3>> routeCache = new();
        private PixelBoard board;
        private int minX, minY, maxX, maxY;
        private bool hasWorldBounds;
        private Vector2 worldMin;
        private Vector2 worldMax;

        private int activeHorizontalMargin;
        private int activeVerticalMargin;

        public int Revision { get; private set; }
        public IReadOnlyList<Vector2Int> Points => points;
        public int HorizontalMarginCells => activeHorizontalMargin > 0 ? activeHorizontalMargin : horizontalMarginCells;
        public int VerticalMarginCells => activeVerticalMargin > 0 ? activeVerticalMargin : verticalMarginCells;
        
        public int BaseHorizontalMarginCells => horizontalMarginCells;
        public int BaseVerticalMarginCells => verticalMarginCells;

        public int SpawnIndex { get; private set; }
        
        public Vector3? CustomEntranceWorldPosition { get; set; }
        
        public Vector3 EntranceWorldPosition
        {
            get
            {
                if (CustomEntranceWorldPosition.HasValue) return CustomEntranceWorldPosition.Value;
                return points.Count == 0
                    ? transform.position
                    : ToWorld(points[SpawnIndex]) + Vector3.down * board.CellSize * entranceOffsetCells;
            }
        }

        public void SetDynamicMargins(int hMargin, int vMargin)
        {
            if (activeHorizontalMargin == hMargin && activeVerticalMargin == vMargin) return;
            activeHorizontalMargin = hMargin;
            activeVerticalMargin = vMargin;
            if (board != null) Build(board);
        }

        public void Build(PixelBoard pixelBoard)
        {
            board = pixelBoard;
            points.Clear();
            indexByPoint.Clear();
            routeCache.Clear();
            
            minX = -HorizontalMarginCells;
            minY = -VerticalMarginCells;
            maxX = board.Width - 1 + HorizontalMarginCells;
            maxY = board.Height - 1 + VerticalMarginCells;
            
            Revision++;

            for (int x = minX; x <= maxX; x++) points.Add(new Vector2Int(x, minY));
            for (int y = minY + 1; y <= maxY; y++) points.Add(new Vector2Int(maxX, y));
            for (int x = maxX - 1; x >= minX; x--) points.Add(new Vector2Int(x, maxY));
            for (int y = maxY - 1; y > minY; y--) points.Add(new Vector2Int(minX, y));

            Vector2Int bottomCenter = new((minX + maxX) / 2, minY);
            SpawnIndex = FindIndex(bottomCenter);
            distanceFromSpawn = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                indexByPoint[points[i]] = i;
                int clockwise = (i - SpawnIndex + points.Count) % points.Count;
                distanceFromSpawn[i] = Mathf.Min(clockwise, points.Count - clockwise);
            }
        }

        public void SetWorldBounds(Vector2 min, Vector2 max)
        {
            if (hasWorldBounds && (worldMin - min).sqrMagnitude < 0.000001f &&
                (worldMax - max).sqrMagnitude < 0.000001f) return;
            worldMin = min;
            worldMax = max;
            hasWorldBounds = max.x > min.x && max.y > min.y;
            Revision++;
            routeCache.Clear();
        }

        public bool TryGetIndex(Vector2Int point, out int index) => indexByPoint.TryGetValue(point, out index);

        public int GetDistanceFromSpawn(int index) =>
            index >= 0 && index < distanceFromSpawn.Length ? distanceFromSpawn[index] : int.MaxValue;

        public int GetDistance(int fromIndex, int toIndex)
        {
            if (points.Count == 0) return int.MaxValue;
            int clockwise = (toIndex - fromIndex + points.Count) % points.Count;
            return Mathf.Min(clockwise, points.Count - clockwise);
        }

        public bool TryGetWorldIndex(Vector3 worldPosition, out int index)
        {
            index = -1;
            if (points.Count == 0) return false;
            for (int i = 0; i < points.Count; i++)
            {
                if ((ToWorld(points[i]) - worldPosition).sqrMagnitude < 0.0001f)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the first point on the bottom border (y == minY) reached by the shortest route from startIndex.
        /// </summary>
        public int FindFirstBottomContactIndex(int startIndex)
        {
            if (points.Count == 0) return -1;
            if (points[startIndex].y == minY) return startIndex;

            int count = points.Count;
            // Scan forward
            int forwardSteps = 0;
            int forwardIndex = startIndex;
            while (forwardSteps < count && points[forwardIndex].y != minY)
            {
                forwardIndex = (forwardIndex + 1) % count;
                forwardSteps++;
            }

            // Scan backward
            int backwardSteps = 0;
            int backwardIndex = startIndex;
            while (backwardSteps < count && points[backwardIndex].y != minY)
            {
                backwardIndex = (backwardIndex - 1 + count) % count;
                backwardSteps++;
            }

            return forwardSteps <= backwardSteps ? forwardIndex : backwardIndex;
        }

        public List<Vector3> BuildShortestWorldRouteToBottomContact(int startIndex, out int bottomIndex)
        {
            bottomIndex = FindFirstBottomContactIndex(startIndex);
            if (bottomIndex < 0 || bottomIndex == startIndex)
            {
                return new List<Vector3> { ToWorld(points[startIndex]) };
            }
            return BuildShortestWorldRoute(startIndex, bottomIndex);
        }

        public int FindNearestBottomIndex(int startIndex) => FindFirstBottomContactIndex(startIndex);

        public int FindNearestIndex(Vector2Int gridPosition)
        {
            if (points.Count == 0) throw new InvalidOperationException("BorderPath has not been built.");
            int bestIndex = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                int distance = Manhattan(points[i], gridPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        public int FindNearestWorldIndex(Vector3 worldPosition)
        {
            if (points.Count == 0) throw new InvalidOperationException("BorderPath has not been built.");
            int bestIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 delta = ToWorld(points[i]) - worldPosition;
                float distance = delta.x * delta.x + delta.y * delta.y;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = i;
            }
            return bestIndex;
        }

        public List<Vector3> BuildShortestWorldRoute(int startIndex, int endIndex)
        {
            long cacheKey = ((long)startIndex << 32) | (uint)endIndex;
            if (routeCache.TryGetValue(cacheKey, out List<Vector3> cached)) return new List<Vector3>(cached);
            int count = points.Count;
            int clockwise = (endIndex - startIndex + count) % count;
            int counterClockwise = count - clockwise;
            int direction = clockwise <= counterClockwise ? 1 : -1;
            int steps = Mathf.Min(clockwise, counterClockwise);
            var route = new List<Vector3>(steps + 2);
            route.Add(ToWorld(points[startIndex]));
            int index = startIndex;
            for (int i = 0; i < steps; i++)
            {
                index = (index + direction + count) % count;
                route.Add(ToWorld(points[index]));
            }
            routeCache[cacheKey] = route;
            return new List<Vector3>(route);
        }

        public Vector3 ToWorld(Vector2Int gridPosition)
        {
            if (!hasWorldBounds) return board.GridToWorld(gridPosition);

            float tx = Mathf.InverseLerp(minX, maxX, gridPosition.x);
            Vector3 boardWorld = board.GridToWorld(gridPosition);
            if (gridPosition.y == minY || gridPosition.y == maxY)
                return new Vector3(Mathf.Lerp(worldMin.x, worldMax.x, tx),
                    gridPosition.y == minY ? worldMin.y : worldMax.y, boardWorld.z);
            if (gridPosition.x == minX || gridPosition.x == maxX)
                // Use board.GridToWorld Y so the Y exactly matches the inner margin cells
                // for the same row — prevents the vertical jolt when the border walk hands
                // off to the A* inner path.
                return new Vector3(gridPosition.x == minX ? worldMin.x : worldMax.x,
                    boardWorld.y, boardWorld.z);
            return boardWorld;
        }
        public Vector2Int GetPoint(int index) => points[index];

        private int FindIndex(Vector2Int point)
        {
            for (int i = 0; i < points.Count; i++) if (points[i] == point) return i;
            return 0;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private void OnDrawGizmosSelected()
        {
            if (board == null || points.Count < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Count; i++)
                Gizmos.DrawLine(ToWorld(points[i]), ToWorld(points[(i + 1) % points.Count]));

            // Draw Cave Avoidance Zone Gizmo (Cyan box) so developer can see the exact cave boundary
            Vector3 entrance = EntranceWorldPosition;
            Vector3 caveCenter = new(entrance.x + 0.05f, entrance.y + 0.05f, entrance.z);
            Vector3 caveSize = new(3.2f, 2.0f, 0.1f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(caveCenter, caveSize);
        }
    }
}
