using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class AntRouteService : MonoBehaviour
    {
        private sealed class CachedRoute
        {
            public int revision;
            public List<Vector3> route;
        }

        private struct InnerPathResult
        {
            public int revision;
            public int exitIndex;
            public Vector2Int edgeCell;
            public List<Vector2Int> pathFromEdge;
        }

        [SerializeField] private PixelBoard board;
        [SerializeField] private BorderPath border;
        private readonly Dictionary<long, CachedRoute> cache = new();
        private readonly Dictionary<int, CachedRoute> returnToEntranceCache = new();
        private readonly Dictionary<long, InnerPathResult> innerPathCache = new();

        // Reusable BFS data structures to avoid GC allocation per request
        private readonly Queue<Vector2Int> bfsQueue = new();
        private readonly Dictionary<Vector2Int, Vector2Int> bfsParentMap = new();
        private readonly Dictionary<Vector2Int, int> bfsDepthMap = new();

        private int MinX => border != null ? -border.HorizontalMarginCells : 0;
        private int MaxX => border != null && board != null ? board.Width - 1 + border.HorizontalMarginCells : 0;
        private int MinY => border != null ? -border.VerticalMarginCells : 0;
        private int MaxY => border != null && board != null ? board.Height - 1 + border.VerticalMarginCells : 0;

        public void Initialize(PixelBoard pixelBoard, BorderPath borderPath)
        {
            board = pixelBoard;
            border = borderPath;
            cache.Clear();
            returnToEntranceCache.Clear();
            innerPathCache.Clear();
        }

        public Vector3 EntranceWorldPosition => border != null ? border.EntranceWorldPosition : transform.position;

        public bool TryBuildBestRoute(int colorIndex, Vector3 colonySpawnWorld,
            out PixelCell bestTarget, out List<Vector3> bestRoute)
        {
            bestTarget = null;
            bestRoute = null;
            if (board == null || border == null) return false;

            List<PixelCell> candidates = board.GetAvailableTargets(colorIndex);
            candidates.Sort(CompareBottomLeftFirst);

            foreach (PixelCell candidate in candidates)
            {
                if (TryBuildRoute(candidate, colonySpawnWorld, out bestRoute))
                {
                    bestTarget = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryBuildRoute(PixelCell target, Vector3 colonySpawnWorld, out List<Vector3> worldRoute)
        {
            worldRoute = null;
            if (target == null || target.IsDestroyed || board == null || border == null) return false;

            int entryIndex = border.FindNearestWorldIndex(colonySpawnWorld);

            int exitIndex;
            Vector2Int edgeCell;
            List<Vector2Int> pathFromEdge;

            long innerKey = ((long)entryIndex << 32) | ((long)(ushort)target.Position.x << 16) | (ushort)target.Position.y;
            if (innerPathCache.TryGetValue(innerKey, out InnerPathResult cachedInner) && cachedInner.revision == board.Revision)
            {
                exitIndex = cachedInner.exitIndex;
                edgeCell = cachedInner.edgeCell;
                pathFromEdge = cachedInner.pathFromEdge;
            }
            else
            {
                if (!TryFindPathToPictureEdge(target, entryIndex,
                        out exitIndex, out edgeCell, out pathFromEdge))
                {
                    return false;
                }
                innerPathCache[innerKey] = new InnerPathResult
                {
                    revision = board.Revision,
                    exitIndex = exitIndex,
                    edgeCell = edgeCell,
                    pathFromEdge = pathFromEdge
                };
            }

            int routeEntry = CaveAdjustedEntryIndex(colonySpawnWorld, exitIndex, entryIndex);

            long cacheKey = ((long)routeEntry << 32) | ((long)(ushort)target.Position.x << 16) | (ushort)target.Position.y;
            if (cache.TryGetValue(cacheKey, out CachedRoute saved) && saved.revision == board.Revision)
            {
                if (saved.route == null) return false;
                worldRoute = new List<Vector3>(saved.route);
                return true;
            }

            // 1. Walk along the outer frame border from routeEntry to exitIndex
            worldRoute = border.BuildShortestWorldRoute(routeEntry, exitIndex);

            // 2. Walk straight and perpendicularly from the border point into the picture edge cell
            Vector3 edgeWorld = board.GridToWorld(edgeCell);
            worldRoute.Add(edgeWorld);

            // 3. Walk through the walkable cells inside the picture to the target pixel
            for (int i = 1; i < pathFromEdge.Count; i++)
            {
                worldRoute.Add(board.GridToWorld(pathFromEdge[i]));
            }

            cache[cacheKey] = new CachedRoute { revision = board.Revision, route = worldRoute };
            return true;
        }

        public bool MayHaveReachableTarget(int colorIndex)
        {
            List<PixelCell> candidates = board.GetAvailableTargets(colorIndex);
            return candidates.Count > 0;
        }

        public bool TryFindExitWaypoint(IReadOnlyList<Vector3> route,
            out int routeWaypointIndex, out int borderIndex)
        {
            routeWaypointIndex = -1;
            borderIndex = -1;
            if (route == null || border == null) return false;
            for (int i = 0; i < route.Count; i++)
            {
                if (!border.TryGetWorldIndex(route[i], out int currentBorderIndex)) break;
                routeWaypointIndex = i;
                borderIndex = currentBorderIndex;
            }
            return routeWaypointIndex >= 0 && borderIndex >= 0;
        }

        public List<Vector3> BuildReturnToEntranceRoute(int exitIndex)
        {
            if (returnToEntranceCache.TryGetValue(exitIndex, out CachedRoute saved) &&
                saved.revision == border.Revision) return new List<Vector3>(saved.route);
            
            // Reaches the first contact point on the bottom border, then cuts diagonally straight into the cave!
            List<Vector3> result = border.BuildShortestWorldRouteToBottomContact(exitIndex, out int bottomIndex);
            
            // From the first bottom border contact point, ant heads diagonally straight to the cave entrance!
            Vector3 caveEntrance = board.ProjectToGameplayPlane(border.EntranceWorldPosition);
            if (result.Count == 0 || (result[result.Count - 1] - caveEntrance).sqrMagnitude > 0.0001f)
            {
                result.Add(caveEntrance);
            }
            returnToEntranceCache[exitIndex] = new CachedRoute { revision = border.Revision, route = result };
            return new List<Vector3>(result);
        }

        /// <summary>
        /// Builds the path from a colony box/slot up to the border entry point (bottom border).
        /// If the straight path intersects the cave, veers around the side of the cave (S/Z curve).
        /// </summary>
        public List<Vector3> BuildSlotToBorderRoute(Vector3 spawnWorld, Vector3 borderTarget)
        {
            var waypoints = new List<Vector3> { spawnWorld };
            if (border == null || board == null) return waypoints;

            Vector3 entrance = border.EntranceWorldPosition;
            // The cave art on the backdrop spans roughly from x = -1.55f to +1.65f (covering
            // the stone rim, left leaves, and the tilted wooden lid on the right) and vertically
            // about 1.0f in each direction from the entrance center.
            float caveLeft   = entrance.x - 1.55f;
            float caveRight  = entrance.x + 1.65f;
            float caveBottom = entrance.y - 0.95f;
            float caveTop    = entrance.y + 1.05f;

            float borderY = borderTarget.y;
            bool intersectsCaveHorizontally = spawnWorld.x >= caveLeft && spawnWorld.x <= caveRight;
            bool spawnIsBelowCave  = spawnWorld.y < caveBottom;
            bool borderIsAboveCave = borderY > caveTop;

            if (intersectsCaveHorizontally && spawnIsBelowCave && borderIsAboveCave)
            {
                // Veer toward the side closer to borderTarget (which is now the cave-adjusted
                // entry, so borderTarget.x ≈ avoidX — no horizontal snap-back at frame bottom).
                float avoidX = borderTarget.x <= entrance.x ? caveLeft : caveRight;
                waypoints.Add(new Vector3(spawnWorld.x, caveBottom, spawnWorld.z));
                waypoints.Add(new Vector3(avoidX,       caveBottom, spawnWorld.z));
                waypoints.Add(new Vector3(avoidX,       caveTop,    spawnWorld.z));
                waypoints.Add(new Vector3(avoidX,       borderY,    spawnWorld.z));
                // No snap-back: border walk entry is already at avoidX side.
            }
            else
            {
                // Straight up to bottom border level, then horizontal to entry point.
                Vector3 corner = new(spawnWorld.x, borderY, spawnWorld.z);
                if ((corner - spawnWorld).sqrMagnitude   > 0.0001f) waypoints.Add(corner);
                if ((corner - borderTarget).sqrMagnitude > 0.0001f) waypoints.Add(new Vector3(borderTarget.x, borderY, spawnWorld.z));
            }

            return waypoints;
        }

        /// <summary>
        /// Finds the shortest walkable BFS path from the target pixel to the boundary of the picture.
        /// From the picture edge cell, projects straight out perpendicularly to the 4 outer border sides,
        /// picks the closest outer border, and returns the exit border index and the inner path.
        /// </summary>
        private bool TryFindPathToPictureEdge(PixelCell target, int entryIndex,
            out int exitIndex, out Vector2Int edgeCell, out List<Vector2Int> pathFromEdgeToTarget)
        {
            exitIndex = -1;
            edgeCell = default;
            pathFromEdgeToTarget = null;
            if (target == null || target.IsDestroyed || board == null || border == null) return false;

            int boardW = board.Width;
            int boardH = board.Height;

            bool IsOutsidePicture(Vector2Int pos) =>
                pos.x < 0 || pos.x >= boardW || pos.y < 0 || pos.y >= boardH;

            // Checks all outward-facing directions from a picture edge cell and projects
            // straight out to the corresponding outer frame border. Picks the closest border.
            bool TryGetBorderProjection(Vector2Int cell, out int bestBorderIdx, out int minProjDist, out int minBorderDist)
            {
                bestBorderIdx = -1;
                minProjDist = int.MaxValue;
                minBorderDist = int.MaxValue;
                int bestBorderDistance = int.MaxValue;

                foreach (Vector2Int dir in GridDirections.Four)
                {
                    Vector2Int outside = cell + dir;
                    if (!IsOutsidePicture(outside)) continue;

                    Vector2Int borderPt;
                    int projDist;
                    if (dir == Vector2Int.left)
                    {
                        borderPt = new Vector2Int(MinX, cell.y);
                        projDist = cell.x - MinX;
                    }
                    else if (dir == Vector2Int.right)
                    {
                        borderPt = new Vector2Int(MaxX, cell.y);
                        projDist = MaxX - cell.x;
                    }
                    else if (dir == Vector2Int.down)
                    {
                        borderPt = new Vector2Int(cell.x, MinY);
                        projDist = cell.y - MinY;
                    }
                    else // up
                    {
                        borderPt = new Vector2Int(cell.x, MaxY);
                        projDist = MaxY - cell.y;
                    }

                    if (!border.TryGetIndex(borderPt, out int bIdx)) continue;
                    int bDist = border.GetDistance(entryIndex, bIdx);

                    if (projDist < minProjDist || (projDist == minProjDist && bDist < bestBorderDistance))
                    {
                        minProjDist = projDist;
                        bestBorderDistance = bDist;
                        bestBorderIdx = bIdx;
                    }
                }

                minBorderDist = bestBorderDistance;
                return bestBorderIdx >= 0;
            }

            // Case 1: Target pixel is already at the boundary of the picture
            if (TryGetBorderProjection(target.Position, out int directBorderIdx, out _, out _))
            {
                edgeCell = target.Position;
                exitIndex = directBorderIdx;
                pathFromEdgeToTarget = new List<Vector2Int> { target.Position };
                return true;
            }

            // Case 2: Target is inside the picture; run BFS through walkable cells inside the picture
            // to find the shortest path to an edge cell that opens to the outside.
            bfsQueue.Clear();
            bfsParentMap.Clear();
            bfsDepthMap.Clear();

            foreach (Vector2Int dir in GridDirections.Four)
            {
                Vector2Int start = target.Position + dir;
                if (IsOutsidePicture(start))
                {
                    if (TryGetBorderProjection(target.Position, out int bIdx, out _, out _))
                    {
                        edgeCell = target.Position;
                        exitIndex = bIdx;
                        pathFromEdgeToTarget = new List<Vector2Int> { target.Position };
                        return true;
                    }
                }
                else if (board.IsWalkable(start))
                {
                    bfsQueue.Enqueue(start);
                    bfsParentMap[start] = target.Position;
                    bfsDepthMap[start] = 1;
                }
            }

            Vector2Int foundEdgeCell = default;
            int foundExitIndex = -1;
            int bestTotalCost = int.MaxValue;

            while (bfsQueue.Count > 0)
            {
                Vector2Int curr = bfsQueue.Dequeue();
                int currentDepth = bfsDepthMap[curr];

                // If current depth is already worse than bestTotalCost, any further search is pointless
                if (currentDepth > bestTotalCost) break;

                if (TryGetBorderProjection(curr, out int bIdx, out int pDist, out int bDist))
                {
                    // Prioritize minimizing the distance walked OFF the border (currentDepth + pDist).
                    // Multiply by a large factor so that walking along the border (bDist) is heavily preferred
                    // and only acts as a tie-breaker, matching the player's visual expectation.
                    int totalCost = (currentDepth + pDist) * 1000 + bDist;
                    if (totalCost < bestTotalCost)
                    {
                        bestTotalCost = totalCost;
                        foundEdgeCell = curr;
                        foundExitIndex = bIdx;
                    }
                }

                foreach (Vector2Int dir in GridDirections.Four)
                {
                    Vector2Int next = curr + dir;
                    if (IsOutsidePicture(next)) continue;
                    if (!board.IsWalkable(next) || bfsDepthMap.ContainsKey(next)) continue;

                    bfsDepthMap[next] = currentDepth + 1;
                    bfsParentMap[next] = curr;
                    bfsQueue.Enqueue(next);
                }
            }

            if (foundExitIndex < 0) return false;

            edgeCell = foundEdgeCell;
            exitIndex = foundExitIndex;

            // Trace path from edgeCell back to target.Position
            pathFromEdgeToTarget = new List<Vector2Int>();
            Vector2Int step = edgeCell;
            while (step != target.Position)
            {
                pathFromEdgeToTarget.Add(step);
                step = bfsParentMap[step];
            }
            pathFromEdgeToTarget.Add(target.Position);

            return true;
        }

        private static int CompareBottomLeftFirst(PixelCell a, PixelCell b)
        {
            int row = a.Position.y.CompareTo(b.Position.y);
            return row != 0 ? row : a.Position.x.CompareTo(b.Position.x);
        }

        /// <summary>
        /// When the spawn position is directly above/below the cave opening, the ant must veer
        /// sideways to avoid it.  Returns the border entry index on the veer side so the border
        /// walk starts exactly there — eliminating the horizontal snap-back that would otherwise
        /// occur at the frame-bottom level.
        /// Returns <paramref name="defaultEntry"/> unchanged if no adjustment is needed.
        /// </summary>
        private int CaveAdjustedEntryIndex(Vector3 spawnWorld, int exitIndex, int defaultEntry)
        {
            if (border == null || board == null) return defaultEntry;
            Vector3 entrance = border.EntranceWorldPosition;
            if (spawnWorld.y >= entrance.y) return defaultEntry; // spawn is not below the frame

            float caveLeft  = entrance.x - 1.55f;
            float caveRight = entrance.x + 1.65f;

            if (spawnWorld.x < caveLeft || spawnWorld.x > caveRight) return defaultEntry; // outside cave zone

            // Determine veer direction: same rule as BuildSlotToBorderRoute —
            // toward the side that's closer to the exit (avoids backtracking).
            Vector3 exitWorld = border.ToWorld(border.GetPoint(exitIndex));
            float avoidX = exitWorld.x <= entrance.x ? caveLeft : caveRight;

            // Return the bottom-border point nearest to avoidX.
            return border.FindNearestWorldIndex(new Vector3(avoidX, entrance.y, spawnWorld.z));
        }

    }
}
