using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class ColonyController
    {
        private sealed class PreparedAnt
        {
            public PixelCell target;
            public List<Vector3> outboundRoute;
            public int returnWaypointIndex;
            public List<Vector3> returnRoute;
        }

        private readonly PixelBoard board;
        private readonly AntRouteService routeService;
        private readonly AntManager antManager;
        private readonly FPSManager fpsManager;
        private readonly PixelPalette palette;
        private readonly HashSet<AntAgent> activeAnts = new();
        private readonly Queue<PreparedAnt> preparedAnts = new();
        private readonly int maxConcurrentAnts;
        private Vector3 antSpawnPosition;
        private readonly Action<ColonyController> completed;
        private readonly float spawnInterval;
        private int routePendingCount;
        private float nextSpawnTime;

        public int ColorIndex { get; }
        public int InitialCount { get; }
        public int RemainingCount { get; private set; }
        public ColonyState State { get; private set; }
        public int ActiveAntCount => activeAnts.Count;
        public Color DisplayColor => palette.GetColor(ColorIndex);
        public bool IsReady { get; set; } = false;
        public event Action<int> RemainingCountChanged;

        public void UpdateAntSpawnPosition(Vector3 newSpawnPosition)
        {
            antSpawnPosition = board.ProjectToGameplayPlane(newSpawnPosition);
        }

        public ColonyController(int colorIndex, int count, PixelBoard board, AntRouteService routeService,
            AntManager antManager, FPSManager fpsManager, PixelPalette palette, int maxConcurrentAnts, float antSpawnInterval, Vector3 spawnPosition,
            Action<ColonyController> onCompleted)
        {
            ColorIndex = colorIndex;
            InitialCount = count;
            RemainingCount = count;
            this.board = board;
            this.routeService = routeService;
            this.antManager = antManager;
            this.fpsManager = fpsManager;
            this.palette = palette;
            this.maxConcurrentAnts = Mathf.Max(1, maxConcurrentAnts);
            spawnInterval = Mathf.Max(0.05f, antSpawnInterval);
            antSpawnPosition = board.ProjectToGameplayPlane(spawnPosition);
            completed = onCompleted;
            State = ColonyState.Active;
        }

        public void Tick()
        {
            if (!IsReady || State == ColonyState.Completed || RemainingCount <= 0) return;

            if (preparedAnts.Count > 0 && Time.time >= nextSpawnTime)
            {
                PreparedAnt prepared = preparedAnts.Dequeue();
                if (antManager.TryLaunch(board, prepared.target, prepared.outboundRoute,
                        prepared.returnWaypointIndex, prepared.returnRoute, palette.GetColor(ColorIndex), this,
                        OnAntCompleted, out AntAgent ant))
                {
                    activeAnts.Add(ant);
                    // Update remaining count immediately when the ant emerges from the box/cave
                    RemainingCount--;
                    RemainingCountChanged?.Invoke(RemainingCount);
                    if (RemainingCount <= 0)
                    {
                        State = ColonyState.Completed;
                        fpsManager?.Cancel(this);
                        completed?.Invoke(this);
                        return;
                    }
                }
                else
                {
                    prepared.target?.Release(this);
                }
                nextSpawnTime = Time.time + spawnInterval;
            }

            if (State == ColonyState.Completed || RemainingCount <= 0) return;

            // Maintain a small pipeline buffer (1 pending ant) so multiple colonies of the
            // same color can draw and spawn ants concurrently without one monopolizing all targets,
            // while respecting maxConcurrentAnts limit to prevent infinite active 3D ants accumulation.
            int currentPending = preparedAnts.Count + routePendingCount;
            int availableAntSlots = maxConcurrentAnts - (activeAnts.Count + currentPending);
            if (availableAntSlots <= 0) return;

            int bufferCapacity = Mathf.Min(1, availableAntSlots);
            int needed = Mathf.Min(RemainingCount - currentPending, bufferCapacity - currentPending);
            int availableTargets = board.GetAvailableTargets(ColorIndex).Count;
            int requestCount = Mathf.Clamp(needed, 0, availableTargets - routePendingCount);

            int scheduled = 0;
            for (int i = 0; i < requestCount; i++)
            {
                if (fpsManager == null || !fpsManager.RequestRoute(this, ColorIndex, antSpawnPosition, OnRouteReady)) break;
                routePendingCount++;
                scheduled++;
            }
            State = activeAnts.Count == 0 && preparedAnts.Count == 0 && scheduled == 0 && routePendingCount == 0
                ? ColonyState.Blocked
                : ColonyState.Active;
        }

        public bool HasAvailableTarget() => routeService.MayHaveReachableTarget(ColorIndex);

        private void OnRouteReady(PixelCell bestTarget, List<Vector3> bestRoute)
        {
            routePendingCount = Mathf.Max(0, routePendingCount - 1);
            if (State == ColonyState.Completed || bestTarget == null || bestRoute == null) return;
            if (!bestTarget.TryReserve(this)) return;
            for (int i = 0; i < bestRoute.Count; i++)
                bestRoute[i] = board.ProjectToGameplayPlane(bestRoute[i]);
            if (!routeService.TryFindExitWaypoint(bestRoute,
                    out int exitWaypointIndex, out int borderExitIndex))
            {
                bestTarget.Release(this);
                return;
            }
            if (bestRoute.Count > 0)
            {
                Vector3 borderStart = bestRoute[0];
                List<Vector3> slotToBorder = routeService.BuildSlotToBorderRoute(antSpawnPosition, borderStart);
                if (slotToBorder != null && slotToBorder.Count > 0)
                {
                    bestRoute.InsertRange(0, slotToBorder);
                    exitWaypointIndex += slotToBorder.Count;
                }
                else
                {
                    bestRoute.Insert(0, antSpawnPosition);
                    exitWaypointIndex++;
                }
            }
            else
            {
                bestRoute.Insert(0, antSpawnPosition);
                exitWaypointIndex++;
            }
            List<Vector3> returnRoute = routeService.BuildReturnToEntranceRoute(borderExitIndex);
            preparedAnts.Enqueue(new PreparedAnt
            {
                target = bestTarget,
                outboundRoute = bestRoute,
                returnWaypointIndex = exitWaypointIndex,
                returnRoute = returnRoute
            });
        }

        private void OnAntCompleted(AntAgent ant, PixelCell pixel, bool success)
        {
            activeAnts.Remove(ant);
        }
    }
}
