using UnityEngine;

namespace ColonyFlow
{
    public sealed class FpsCounter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
        [SerializeField] private Vector2 size = new(110f, 36f);
        // Góc hiển thị: TopLeft=0, TopRight=1, BottomLeft=2, BottomRight=3
        [SerializeField] private int corner = 1;
        [SerializeField] private Vector2 margin = new(12f, 12f);

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

            float x, y;
            switch (corner)
            {
                case 0: // Top-Left
                    x = margin.x;
                    y = margin.y;
                    break;
                case 1: // Top-Right
                    x = Screen.width - size.x - margin.x;
                    y = margin.y;
                    break;
                case 2: // Bottom-Left
                    x = margin.x;
                    y = Screen.height - size.y - margin.y;
                    break;
                default: // Bottom-Right
                    x = Screen.width - size.x - margin.x;
                    y = Screen.height - size.y - margin.y;
                    break;
            }

            GUI.Label(new Rect(x, y, size.x, size.y), label, style);
        }

        private void EnsureStyle()
        {
            if (style != null) return;
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.45f)) }
            };
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        // Tự tạo FpsCounter nếu chưa có trong scene
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<FpsCounter>() != null) return;
            var go = new GameObject("FpsCounter");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsCounter>();
        }
    }
}
