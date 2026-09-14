using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class PoolController : MonoBehaviour
    {
        [SerializeField] private List<PoolAmount> pools = new();

        private void Awake()
        {
            foreach (PoolAmount item in pools)
            {
                if (item.prefab != null) SimplePool.Preload(item.prefab, item.amount, item.root, item.collect);
            }
        }
    }

    [Serializable]
    public sealed class PoolAmount
    {
        public Transform root;
        public GameUnit prefab;
        [Min(0)] public int amount = 8;
        public bool collect = true;
    }
}
