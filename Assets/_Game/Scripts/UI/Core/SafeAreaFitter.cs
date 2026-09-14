using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect last;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (last != Screen.safeArea) Apply();
        }

        private void Apply()
        {
            last = Screen.safeArea;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = last.position / new Vector2(Screen.width, Screen.height);
            rect.anchorMax = (last.position + last.size) / new Vector2(Screen.width, Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
