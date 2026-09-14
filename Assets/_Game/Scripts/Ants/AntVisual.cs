using UnityEngine;

namespace ColonyFlow
{
    public sealed class AntVisual : MonoBehaviour
    {
        [SerializeField] private Renderer[] coloredRenderers;
        [SerializeField] private Animator animator;
        [SerializeField, HideInInspector] private int visualVersion = 7;

        private AntMaterialPalette palette;
        private static readonly int IdleTrigger = Animator.StringToHash("Idle");
        private static readonly int WalkTrigger = Animator.StringToHash("Walk");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");

        private void Awake() => palette = Resources.Load<AntMaterialPalette>("Ant/AntMaterialPalette");

        public void SetColor(Color color)
        {
            if (coloredRenderers == null) return;
            Material material = palette != null ? palette.GetClosestMaterial(color) : null;
            var block = material == null ? new MaterialPropertyBlock() : null;
            if (block != null)
            {
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
            }
            foreach (Renderer cachedRenderer in coloredRenderers)
            {
                if (cachedRenderer == null) continue;
                if (material != null) cachedRenderer.sharedMaterial = material;
                else cachedRenderer.SetPropertyBlock(block);
            }
        }

        public void PlayIdle() => SetTrigger(IdleTrigger);
        public void PlayWalk() => SetTrigger(WalkTrigger);
        public void PlayAttack() => SetTrigger(AttackTrigger);

        public void SetVisible(bool visible)
        {
            if (coloredRenderers == null) return;
            foreach (Renderer cachedRenderer in coloredRenderers)
                if (cachedRenderer != null) cachedRenderer.enabled = visible;
        }

        private void SetTrigger(int trigger)
        {
            if (animator == null) return;
            animator.ResetTrigger(IdleTrigger);
            animator.ResetTrigger(WalkTrigger);
            animator.ResetTrigger(AttackTrigger);
            animator.SetTrigger(trigger);
        }

        public void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) return;
            Vector3 planar = new(direction.x, direction.y, 0f);
            planar.Normalize();
            float angle = Mathf.Atan2(planar.y, planar.x) * Mathf.Rad2Deg - 90f;
            // Keep feet on the XY play surface. The tilted gameplay camera supplies
            // the side view and natural far-leg occlusion for every travel direction.
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
