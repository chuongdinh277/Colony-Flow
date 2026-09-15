using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public static class SimplePool
    {
        private sealed class Pool
        {
            private readonly Queue<GameUnit> inactive = new();
            private readonly HashSet<GameUnit> inactiveSet = new();
            private readonly HashSet<GameUnit> active = new();
            private readonly GameUnit prefab;
            private readonly Transform root;
            private readonly bool collect;
            private readonly int key;

            public Pool(int key, GameUnit prefab, Transform root, bool collect)
            {
                this.key = key;
                this.prefab = prefab;
                this.root = root;
                this.collect = collect;
            }

            public GameUnit Spawn(Vector3 position, Quaternion rotation, Transform parent)
            {
                GameUnit unit = null;
                while (inactive.Count > 0 && unit == null)
                {
                    unit = inactive.Dequeue();
                    if (unit != null) inactiveSet.Remove(unit);
                }
                if (unit == null)
                {
                    unit = Object.Instantiate(prefab, root);
                }
                else
                {
                    inactiveSet.Remove(unit);
                }

                unit.PoolKey = key;
                unit.gameObject.SetActive(false);
                unit.TF.SetParent(parent != null ? parent : root, false);
                unit.TF.SetPositionAndRotation(position, rotation);
                unit.TF.localScale = Vector3.one;
                unit.gameObject.SetActive(true);
                if (collect) active.Add(unit);
                return unit;
            }

            public void Despawn(GameUnit unit)
            {
                if (unit == null) return;
                active.Remove(unit);
                if (!inactiveSet.Add(unit)) return; // Already inactive in pool, ignore duplicate despawn
                unit.gameObject.SetActive(false);
                unit.TF.SetParent(root, false);
                inactive.Enqueue(unit);
            }

            public void Collect()
            {
                if (!collect) return;
                foreach (GameUnit unit in new List<GameUnit>(active)) Despawn(unit);
            }
        }

        public const int DefaultPoolSize = 8;
        private static readonly Dictionary<int, Pool> Pools = new();
        private static Transform root;
        public static Transform Root => root != null ? root : root = new GameObject("Pool").transform;

        public static void Preload(GameUnit prefab, int amount = DefaultPoolSize, Transform parent = null, bool collect = true)
        {
            if (prefab == null) return;
            Pool pool = GetOrCreate(prefab, parent, collect);
            var units = new List<GameUnit>(Mathf.Max(0, amount));
            for (int i = 0; i < amount; i++) units.Add(pool.Spawn(Vector3.zero, Quaternion.identity, null));
            foreach (GameUnit unit in units) pool.Despawn(unit);
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : GameUnit
        {
            if (prefab == null) return null;
            return GetOrCreate(prefab, null, true).Spawn(position, rotation, parent) as T;
        }

        public static void Despawn(GameUnit unit)
        {
            if (unit == null) return;
            if (Pools.TryGetValue(unit.PoolKey, out Pool pool)) pool.Despawn(unit);
            else Object.Destroy(unit.gameObject);
        }

        public static void CollectAll()
        {
            foreach (Pool pool in Pools.Values) pool.Collect();
        }

        private static Pool GetOrCreate(GameUnit prefab, Transform parent, bool collect)
        {
            int key = prefab.GetInstanceID();
            if (Pools.TryGetValue(key, out Pool pool)) return pool;
            Transform poolRoot = parent;
            if (poolRoot == null)
            {
                poolRoot = new GameObject($"{prefab.poolType}_{prefab.name}").transform;
                poolRoot.SetParent(Root, false);
            }
            pool = new Pool(key, prefab, poolRoot, collect);
            Pools.Add(key, pool);
            prefab.PoolKey = key;
            return pool;
        }
    }
}
