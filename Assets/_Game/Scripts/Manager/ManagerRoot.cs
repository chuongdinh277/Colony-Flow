using UnityEngine;

namespace ColonyFlow
{
    public class ManagerRoot : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
