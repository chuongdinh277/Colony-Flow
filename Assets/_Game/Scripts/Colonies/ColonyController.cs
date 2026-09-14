using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class ColonyController
    {
        private readonly PixelBoard board;
        private readonly AntRouteService routeService;
        private readonly AntManager antManager;
        private readonly FPSManager fpsManager;
        private readonly PixelPalette palette;
        private readonly HashSet<AntAgent> activeAnts = new();
        private readonly int maxConcurrentAnts;
        private readonly Vector3 antSpawnPosition;
        private readonly Action<ColonyController> completed;
        private readonly float spawnInterval;
        private float nextSpawnTime;
        private bool routePending;

        public int ColorIndex { get; }
        public int InitialCount { get; }
        public int RemainingCount { get; private set; }
        public ColonyState State { get; private set; }
        public int ActiveAntCount => activeAnts.Count;
        public Color DisplayColor => palette.GetColor(ColorIndex);
        public bool IsReady { get; set; } = false;

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
            bool scheduled = false;
            if (!routePending && Time.time >= nextSpawnTime && activeAnts.Count < maxConcurrentAnts && activeAnts.Count < RemainingCount)
            {
                scheduled = fpsManager != null && fpsManager.RequestRoute(this, ColorIndex, antSpawnPosition, OnRouteReady);
                routePending = scheduled;
            }
            State = activeAnts.Count == 0 && !scheduled && !routePending ? ColonyState.Blocked : ColonyState.Active;
        }

        public bool HasAvailableTarget() => routeService.MayHaveReachableTarget(ColorIndex);

        private void OnRouteReady(PixelCell bestTarget, List<Vector3> bestRoute)
        {
            routePending = false;
            nextSpawnTime = Time.time + spawnInterval;
            if (State == ColonyState.Completed || bestTarget == null || bestRoute == null) return;
            for (int i = 0; i < bestRoute.Count; i++)
                bestRoute[i] = board.ProjectToGameplayPlane(bestRoute[i]);
            int returnBorderEntryIndex = 1;
            if (bestRoute.Count > 0)
            {
                Vector3 borderStart = bestRoute[0];
                // Colony boxes sit below the picture. Move vertically at the
                // colony's X until touching the bottom border, then turn and
                // follow that border to its discrete path node.
                Vector3 corner = new(antSpawnPosition.x, borderStart.y, borderStart.z);
                if ((corner - antSpawnPosition).sqrMagnitude > 0.000001f &&
                    (corner - borderStart).sqrMagnitude > 0.000001f)
                {
                    bestRoute.Insert(0, corner);
                    returnBorderEntryIndex++;
                }
            }
            bestRoute.Insert(0, antSpawnPosition);
            List<Vector3> returnRoute = routeService.BuildReturnToEntranceRoute(antSpawnPosition);
            if (!antManager.TryLaunch(board, bestTarget, bestRoute, returnBorderEntryIndex, returnRoute,
                    palette.GetColor(ColorIndex), OnAntCompleted, out AntAgent ant)) return;
            activeAnts.Add(ant);
        }

        private void OnAntCompleted(AntAgent ant, PixelCell pixel, bool success)
        {
            activeAnts.Remove(ant);
            if (!success) return;
            RemainingCount--;
            if (RemainingCount > 0) return;
            State = ColonyState.Completed;
            fpsManager?.Cancel(this);
            completed?.Invoke(this);
        }
    }
}
