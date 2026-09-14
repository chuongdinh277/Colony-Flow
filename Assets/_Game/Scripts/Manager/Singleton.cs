using UnityEngine;

namespace ColonyFlow
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Ins { get; private set; }

        protected virtual void Awake()
        {
            if (Ins != null && Ins != this)
            {
                Destroy(gameObject);
                return;
            }
            Ins = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Ins == this) Ins = null;
        }
    }
}
