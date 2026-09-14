using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class AntRouteService : MonoBehaviour
    {
        private readonly struct Node : IComparable<Node>
        {
            public readonly Vector2Int Position;
            public readonly int G;
            public readonly int F;
            private readonly int sequence;

            public Node(Vector2Int position, int g, int f, int sequence)
            {
                Position = position;
                G = g;
                F = f;
                this.sequence = sequence;
            }

            public int CompareTo(Node other)
            {
                int result = F.CompareTo(other.F);
                if (result == 0) result = G.CompareTo(other.G);
                if (result == 0) result = sequence.CompareTo(other.sequence);
                return result;
            }
        }

        private sealed class CachedRoute
        {
            public int revision;
            public List<Vector3> route;
        }

        [SerializeField] private PixelBoard board;
        [SerializeField] private BorderPath border;
        private readonly SortedSet<Node> open = new();
        private readonly Dictionary<Vector2Int, Vector2Int> parent = new();
        private readonly Dictionary<Vector2Int, int> scores = new();
        private readonly Dictionary<long, CachedRoute> cache = new();
        private readonly List<Vector2Int> reversePath = new();
        private readonly Dictionary<Vector2Int, int> bfsDistance = new();
        private readonly Dictionary<Vector2Int, int> bfsBorderIndex = new();
        private readonly Queue<Vector2Int> bfsQueue = new();
        private int bfsRevision = -1;
        private int budgetFrame = -1;
        private bool searchedThisFrame;
        private int sequence;
        private int minX, minY, maxX, maxY;

        public void Initialize(PixelBoard pixelBoard, BorderPath borderPath)
        {
            board = pixelBoard;
            border = borderPath;
            cache.Clear();
            bfsDistance.Clear();
            bfsBorderIndex.Clear();
            bfsRevision = -1;
            budgetFrame = -1;
            if (border.Points.Count == 0) return;
            minX = maxX = border.Points[0].x;
            minY = maxY = border.Points[0].y;
            foreach (Vector2Int point in border.Points)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
        }

        public Vector3 EntranceWorldPosition => border != null ? border.EntranceWorldPosition : transform.position;

        public bool TryBuildBestRoute(int colorIndex, Vector3 colonySpawnWorld,
            out PixelCell bestTarget, out List<Vector3> bestRoute)
        {
            bestTarget = null;
            bestRoute = null;
            if (board == null || border == null) return false;

            BuildBfsDistanceField();
            List<PixelCell> candidates = board.GetAvailableTargets(colorIndex);
            candidates.Sort(CompareBottomLeftFirst);
            int entryIndex = border.FindNearestWorldIndex(colonySpawnWorld);
            int bestExitIndex = -1;
            foreach (PixelCell candidate in candidates)
            {
                int candidateDistance = int.MaxValue;
                int candidateBorderDistance = int.MaxValue;
                int candidateExitIndex = -1;
                foreach (Vector2Int direction in GridDirections.Four)
                {
                    Vector2Int neighbor = candidate.Position + direction;
                    if (!bfsDistance.TryGetValue(neighbor, out int innerDistance)) continue;
                    int borderIndex = bfsBorderIndex[neighbor];
                    int borderDistance = border.GetDistance(entryIndex, borderIndex);
                    if (innerDistance > candidateDistance ||
                        (innerDistance == candidateDistance && borderDistance >= candidateBorderDistance)) continue;
                    candidateDistance = innerDistance;
                    candidateBorderDistance = borderDistance;
                    candidateExitIndex = borderIndex;
                }

                // Target priority is gameplay-defined: bottom-to-top, then
                // left-to-right. Path length only chooses this target's exit.
                if (candidateExitIndex < 0) continue;
                bestTarget = candidate;
                bestExitIndex = candidateExitIndex;
                break;
            }

            if (bestTarget == null) return false;
            return TryBuildRoute(bestTarget, colonySpawnWorld, bestExitIndex, out bestRoute);
        }

        public bool TryBuildRoute(PixelCell target, Vector3 colonySpawnWorld, out List<Vector3> worldRoute)
        {
            if (target == null || board == null || border == null)
            {
                worldRoute = null;
                return false;
            }
            BuildBfsDistanceField();
            int exitIndex = -1;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int direction in GridDirections.Four)
            {
                Vector2Int neighbor = target.Position + direction;
                if (!bfsDistance.TryGetValue(neighbor, out int distance) || distance >= bestDistance) continue;
                bestDistance = distance;
                exitIndex = bfsBorderIndex[neighbor];
            }
            return TryBuildRoute(target, colonySpawnWorld, exitIndex, out worldRoute);
        }

        private bool TryBuildRoute(PixelCell target, Vector3 colonySpawnWorld, int exitIndex,
            out List<Vector3> worldRoute)
        {
            worldRoute = null;
            if (target == null || target.IsDestroyed || board == null || border == null || exitIndex < 0) return false;
            int entryIndex = border.FindNearestWorldIndex(colonySpawnWorld);
            long cacheKey = ((long)entryIndex << 32) | ((long)(ushort)target.Position.x << 16) | (ushort)target.Position.y;
            if (cache.TryGetValue(cacheKey, out CachedRoute saved) && saved.revision == board.Revision)
            {
                if (saved.route == null) return false;
                worldRoute = new List<Vector3>(saved.route);
                return true;
            }

            RefreshBudget();
            if (searchedThisFrame) return false;
            searchedThisFrame = true;
            bool found = RunAStar(target, entryIndex, exitIndex, out List<Vector3> route);
            cache[cacheKey] = new CachedRoute { revision = board.Revision, route = route };
            if (!found) return false;
            worldRoute = new List<Vector3>(route);
            return true;
        }

        public bool MayHaveReachableTarget(int colorIndex)
        {
            List<PixelCell> candidates = board.GetAvailableTargets(colorIndex);
            return candidates.Count > 0;
        }

        public List<Vector3> BuildReturnToEntranceRoute(Vector3 colonySpawnWorld)
        {
            if (board == null || border == null) return null;
            int entryIndex = border.FindNearestWorldIndex(colonySpawnWorld);
            List<Vector3> result = border.BuildShortestWorldRoute(entryIndex, border.SpawnIndex);
            result.Add(board.ProjectToGameplayPlane(border.EntranceWorldPosition));
            return result;
        }

        private void BuildBfsDistanceField()
        {
            if (bfsRevision == board.Revision) return;
            bfsRevision = board.Revision;
            bfsDistance.Clear();
            bfsBorderIndex.Clear();
            bfsQueue.Clear();
            for (int i = 0; i < border.Points.Count; i++)
            {
                Vector2Int point = border.GetPoint(i);
                if (!board.IsWalkable(point) || bfsDistance.ContainsKey(point)) continue;
                bfsDistance[point] = 0;
                bfsBorderIndex[point] = i;
                bfsQueue.Enqueue(point);
            }

            while (bfsQueue.Count > 0)
            {
                Vector2Int current = bfsQueue.Dequeue();
                int nextDistance = bfsDistance[current] + 1;
                foreach (Vector2Int direction in GridDirections.Four)
                {
                    Vector2Int next = current + direction;
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                    if (!board.IsWalkable(next) || bfsDistance.ContainsKey(next)) continue;
                    bfsDistance[next] = nextDistance;
                    bfsBorderIndex[next] = bfsBorderIndex[current];
                    bfsQueue.Enqueue(next);
                }
            }
        }

        private bool RunAStar(PixelCell target, int entryIndex, int exitIndex, out List<Vector3> route)
        {
            route = null;
            open.Clear();
            sequence = 0;
            parent.Clear();
            scores.Clear();
            foreach (Vector2Int direction in GridDirections.Four)
            {
                Vector2Int start = target.Position + direction;
                if (!board.IsWalkable(start) || scores.ContainsKey(start)) continue;
                scores[start] = 0;
                parent[start] = start;
                open.Add(new Node(start, 0, Manhattan(start, border.GetPoint(exitIndex)), sequence++));
            }

            Vector2Int goal = default;
            int borderIndex = -1;
            int bestInnerDistance = int.MaxValue;
            int bestTotalDistance = int.MaxValue;
            while (open.Count > 0)
            {
                Node current = open.Min;
                open.Remove(current);
                if (!scores.TryGetValue(current.Position, out int known) || known != current.G) continue;
                if (current.F > bestTotalDistance) break;
                if (current.Position == border.GetPoint(exitIndex))
                {
                    int borderDistance = border.GetDistance(entryIndex, exitIndex);
                    int totalDistance = current.G + borderDistance;
                    if (totalDistance < bestTotalDistance || (totalDistance == bestTotalDistance && current.G < bestInnerDistance))
                    {
                        goal = current.Position;
                        borderIndex = exitIndex;
                        bestInnerDistance = current.G;
                        bestTotalDistance = totalDistance;
                    }
                    continue;
                }

                foreach (Vector2Int direction in GridDirections.Four)
                {
                    Vector2Int next = current.Position + direction;
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                    if (!board.IsWalkable(next)) continue;
                    int nextScore = current.G + 1;
                    if (scores.TryGetValue(next, out int oldScore) && nextScore >= oldScore) continue;
                    scores[next] = nextScore;
                    parent[next] = current.Position;
                    open.Add(new Node(next, nextScore,
                        nextScore + Manhattan(next, border.GetPoint(exitIndex)), sequence++));
                }
            }
            if (borderIndex < 0) return false;

            reversePath.Clear();
            Vector2Int step = goal;
            reversePath.Add(step);
            while (parent[step] != step)
            {
                step = parent[step];
                reversePath.Add(step);
            }

            route = border.BuildShortestWorldRoute(entryIndex, borderIndex);
            if (reversePath.Count > 1)
            {
                Vector3 borderWorld = route[route.Count - 1];
                Vector3 firstInnerWorld = board.GridToWorld(reversePath[1]);
                Vector2Int exit = border.GetPoint(borderIndex);
                Vector3 corner = exit.x == minX || exit.x == maxX
                    ? new Vector3(borderWorld.x, firstInnerWorld.y, borderWorld.z)
                    : new Vector3(firstInnerWorld.x, borderWorld.y, borderWorld.z);
                if ((corner - borderWorld).sqrMagnitude > 0.000001f &&
                    (corner - firstInnerWorld).sqrMagnitude > 0.000001f) route.Add(corner);
            }
            for (int i = 1; i < reversePath.Count; i++) route.Add(board.GridToWorld(reversePath[i]));
            route.Add(board.GridToWorld(target.Position));
            return true;
        }

        private int Heuristic(Vector2Int point)
        {
            int dx = Mathf.Min(Mathf.Abs(point.x - minX), Mathf.Abs(maxX - point.x));
            int dy = Mathf.Min(Mathf.Abs(point.y - minY), Mathf.Abs(maxY - point.y));
            return Mathf.Min(dx, dy);
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static int CompareBottomLeftFirst(PixelCell a, PixelCell b)
        {
            int row = a.Position.y.CompareTo(b.Position.y);
            return row != 0 ? row : a.Position.x.CompareTo(b.Position.x);
        }

        private void RefreshBudget()
        {
            if (budgetFrame == Time.frameCount) return;
            budgetFrame = Time.frameCount;
            searchedThisFrame = false;
        }
    }
}
