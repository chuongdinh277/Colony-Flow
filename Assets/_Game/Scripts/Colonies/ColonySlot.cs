using UnityEngine;

namespace ColonyFlow
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ColonySlot : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private TextMesh countLabel;
        // Ants emerge from the occupied box itself. The first route segment then
        // carries them from the box centre toward the picture-frame border.
        [SerializeField] private Vector3 antSpawnOffset = new(0f, 0.5f, 0f);
        public int Index { get; private set; }
        public ColonyController Colony { get; private set; }
        public bool IsEmpty => Colony == null;
        public Vector3 AntSpawnWorldPosition => transform.TransformPoint(antSpawnOffset);

        public void Configure(SpriteRenderer renderer) => background = renderer;

        public void Initialize(int index)
        {
            Index = index;
            if (background == null) return;
            background.sprite = RuntimeSprite.TrayBox;
            background.sortingOrder = 40;
            EnsureLabel();
            Clear();
        }

        public void Occupy(ColonyController colony)
        {
            Colony = colony;
            if (background != null) background.color = Color.clear;
        }

        public void Clear()
        {
            Colony = null;
            if (background != null) background.color = Color.clear;
        }

        private void LateUpdate()
        {
        }

        private void EnsureLabel()
        {
        }
    }
}
