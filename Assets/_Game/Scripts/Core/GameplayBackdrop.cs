using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class GameplayBackdrop : MonoBehaviour
    {
        private const string ResourcePath = "UI/Gameplay/gameplayBackdrop";
        private Camera targetCamera;
        private SpriteRenderer backdrop;

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            if (targetCamera == null) return;
            Transform existing = targetCamera.transform.Find("GameplayBackdrop");
            if (existing != null) backdrop = existing.GetComponent<SpriteRenderer>();
            if (backdrop == null)
            {
                GameObject go = new("GameplayBackdrop", typeof(SpriteRenderer));
                go.transform.SetParent(targetCamera.transform, false);
                backdrop = go.GetComponent<SpriteRenderer>();
            }
            backdrop.sprite = Resources.Load<Sprite>(ResourcePath);
            backdrop.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backdrop.receiveShadows = false;
            backdrop.sortingOrder = -1000;
            FitToCamera();
        }

        private void LateUpdate()
        {
            if (targetCamera != null && backdrop != null) FitToCamera();
        }

        private void FitToCamera()
        {
            if (backdrop.sprite == null) return;
            float depth = Mathf.Min(targetCamera.farClipPlane - 5f, 45f);
            Transform visual = backdrop.transform;
            visual.localPosition = new Vector3(0f, 0f, depth);
            visual.localRotation = Quaternion.identity;
            float viewHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(targetCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float viewWidth = viewHeight * targetCamera.aspect;
            Vector2 spriteSize = backdrop.sprite.bounds.size;
            float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * 1.015f;
            visual.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
