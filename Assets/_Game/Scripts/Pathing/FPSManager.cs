using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class FPSManager : MonoBehaviour
    {
        private sealed class Request
        {
            public object owner;
            public int colorIndex;
            public Vector3 spawnWorld;
            public Action<PixelCell, List<Vector3>> completed;
        }

        private readonly Queue<Request> requests = new();
        private AntRouteService routeService;

        [SerializeField, Range(1f, 8f)] private float maxBudgetMilliseconds = 2f;
        [SerializeField, Range(1, 10)] private int maxRequestsPerFrame = 4;

        public int PendingCount => requests.Count;
        public int LastProcessedFrame { get; private set; } = -1;

        public void Initialize(AntRouteService service)
        {
            Clear();
            routeService = service;
        }

        public bool RequestRoute(object owner, int colorIndex, Vector3 spawnWorld,
            Action<PixelCell, List<Vector3>> completed)
        {
            if (owner == null || completed == null || routeService == null) return false;
            requests.Enqueue(new Request
            {
                owner = owner,
                colorIndex = colorIndex,
                spawnWorld = spawnWorld,
                completed = completed
            });
            return true;
        }

        private void Update()
        {
            if (requests.Count == 0) return;
            LastProcessedFrame = Time.frameCount;

            float startTime = Time.realtimeSinceStartup;
            float maxDuration = maxBudgetMilliseconds / 1000f;
            int processed = 0;

            while (requests.Count > 0 && processed < maxRequestsPerFrame)
            {
                Request request = requests.Dequeue();
                if (routeService.TryBuildBestRoute(request.colorIndex, request.spawnWorld,
                        out PixelCell target, out List<Vector3> route))
                    request.completed(target, route);
                else
                    request.completed(null, null);

                processed++;
                if (Time.realtimeSinceStartup - startTime >= maxDuration) break;
            }
        }

        public void Cancel(object owner)
        {
            if (owner == null) return;
            int count = requests.Count;
            for (int i = 0; i < count; i++)
            {
                Request request = requests.Dequeue();
                if (!ReferenceEquals(request.owner, owner)) requests.Enqueue(request);
            }
        }

        public void Clear()
        {
            requests.Clear();
            LastProcessedFrame = -1;
        }

        private void OnDisable() => Clear();
    }
}
