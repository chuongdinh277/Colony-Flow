using UnityEngine;

namespace ColonyFlow
{
    public class GameUnit : MonoBehaviour
    {
        private Transform cachedTransform;
        public Transform TF => cachedTransform == null ? cachedTransform = transform : cachedTransform;
        public PoolType poolType;
        internal int PoolKey { get; set; }
    }
}
