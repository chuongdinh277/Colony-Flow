using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public static class GridPathfinder
    {
        public static bool TryFindPath(Vector2Int start, Vector2Int goal, PixelBoard board, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start);
            cameFrom[start] = start;

            int minX = -2, minY = -2, maxX = board.Width + 1, maxY = board.Height + 1;
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == goal)
                {
                    Reconstruct(start, goal, cameFrom, path);
                    return true;
                }

                foreach (Vector2Int direction in GridDirections.Four)
                {
                    Vector2Int next = current + direction;
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                    if (cameFrom.ContainsKey(next)) continue;
                    if (next != goal && !board.IsWalkable(next)) continue;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private static void Reconstruct(Vector2Int start, Vector2Int goal, Dictionary<Vector2Int, Vector2Int> cameFrom, List<Vector2Int> path)
        {
            Vector2Int current = goal;
            path.Add(current);
            while (current != start)
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
        }
    }
}
