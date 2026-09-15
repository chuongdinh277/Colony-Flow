using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class AntManager : MonoBehaviour
    {
        private readonly HashSet<AntAgent> activeAnts = new();
        private AntAgent prefab;
        private Transform activeRoot;

        public int ActiveCount => activeAnts.Count;

        public void Initialize(AntAgent antPrefab, Transform root)
        {
            CancelAll();
            prefab = antPrefab;
            activeRoot = root;
        }

        public bool TryLaunch(PixelBoard board, PixelCell target, IReadOnlyList<Vector3> route, int returnWaypointIndex,
            IReadOnlyList<Vector3> returnRoute, Color color, object reservationOwner,
            Action<AntAgent, PixelCell, bool> completed, out AntAgent ant)
        {
            ant = null;
            if (prefab == null || board == null || target == null || route == null || route.Count == 0) return false;
            AntAgent spawned = SimplePool.Spawn(prefab, route[0], Quaternion.identity, activeRoot);
            bool reserved = spawned != null && (reservationOwner != null
                ? target.TryTransferReservation(reservationOwner, spawned)
                : target.TryReserve(spawned));
            if (spawned == null || !reserved)
            {
                if (spawned != null) SimplePool.Despawn(spawned);
                return false;
            }

            ant = spawned;
            activeAnts.Add(spawned);
            spawned.Launch(board, target, route, returnWaypointIndex, returnRoute, color, (agent, pixel, success) =>
            {
                activeAnts.Remove(agent);
                completed?.Invoke(agent, pixel, success);
                SimplePool.Despawn(agent);
            });
            return true;
        }

        public void CancelAll()
        {
            foreach (AntAgent ant in new List<AntAgent>(activeAnts))
                if (ant != null) SimplePool.Despawn(ant);
            activeAnts.Clear();
        }

        private void OnDisable() => CancelAll();
    }
}
