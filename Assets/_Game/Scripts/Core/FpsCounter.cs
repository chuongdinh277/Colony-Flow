using UnityEngine;

namespace ColonyFlow
{
    public sealed class FpsCounter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
        [SerializeField] private Vector2 margin = new(14f, 12f);
        [SerializeField] private Vector2 size = new(116f, 42f);

        private GUIStyle style;
        private float elapsed;
        private float accumulatedTime;
        private int accumulatedFrames;
        private string label = "FPS: --";

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            elapsed += delta;
            accumulatedTime += delta;
            accumulatedFrames++;
            if (elapsed < refreshInterval) return;

            float fps = accumulatedTime > 0f ? accumulatedFrames / accumulatedTime : 0f;
            label = $"FPS: {Mathf.RoundToInt(fps)}";
            elapsed = 0f;
            accumulatedTime = 0f;
            accumulatedFrames = 0;
        }

        private void OnGUI()
        {
            EnsureStyle();
            Rect safe = Screen.safeArea;
            var rect = new Rect(
                safe.xMax - size.x - margin.x,
                Screen.height - safe.yMax + margin.y,
                size.x,
                size.y);
            GUI.Label(rect, label, style);
        }

        private void EnsureStyle()
        {
            if (style != null) return;
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
    }
}
