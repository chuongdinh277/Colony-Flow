using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class BorderPath : MonoBehaviour
    {
        // The ant lane belongs to the UI frame, not to the artwork silhouette.
        // Keep extra vertical breathing room between the picture and that lane.
        [SerializeField, Min(1)] private int horizontalMarginCells = 1;
        [SerializeField, Min(1)] private int verticalMarginCells = 2;
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

        public IReadOnlyList<Vector2Int> Points => points;
        public int HorizontalMarginCells => horizontalMarginCells;
        public int VerticalMarginCells => verticalMarginCells;
        public int SpawnIndex { get; private set; }
        public Vector3 EntranceWorldPosition => points.Count == 0
            ? transform.position
            : ToWorld(points[SpawnIndex]) + Vector3.down * board.CellSize * entranceOffsetCells;

        public void Build(PixelBoard pixelBoard)
        {
            board = pixelBoard;
            points.Clear();
            indexByPoint.Clear();
            routeCache.Clear();
            
            minX = -horizontalMarginCells;
            minY = -verticalMarginCells;
            maxX = board.Width - 1 + horizontalMarginCells;
            maxY = board.Height - 1 + verticalMarginCells;

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
            float ty = Mathf.InverseLerp(minY, maxY, gridPosition.y);
            Vector3 boardWorld = board.GridToWorld(gridPosition);
            if (gridPosition.y == minY || gridPosition.y == maxY)
                return new Vector3(Mathf.Lerp(worldMin.x, worldMax.x, tx),
                    gridPosition.y == minY ? worldMin.y : worldMax.y, boardWorld.z);
            if (gridPosition.x == minX || gridPosition.x == maxX)
                return new Vector3(gridPosition.x == minX ? worldMin.x : worldMax.x,
                    Mathf.Lerp(worldMin.y, worldMax.y, ty), boardWorld.z);
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
        }
    }
}
