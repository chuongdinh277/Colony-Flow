using UnityEngine;

namespace ColonyFlow
{
    public sealed class PixelCellView : GameUnit
    {
        [Header("3D box")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform boxVisual;
        [SerializeField, Range(0.95f, 1f)] private float fill = 1f;
        [Header("Legacy fallback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        private SpriteRenderer depthSprite;
        private SpriteRenderer glossSprite;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock colorBlock;
        private Transform carrier;
        private Vector3 carryLocalOffset;
        private Vector3 carryStartPosition;
        private float carryBlend;
        public PixelCell Cell { get; private set; }

        public void Configure(MeshRenderer renderer, Transform visual)
        {
            meshRenderer = renderer;
            boxVisual = visual;
            spriteRenderer = null;
        }

        public void Configure(SpriteRenderer renderer, bool unused = true)
        {
            spriteRenderer = renderer;
            if (spriteRenderer.sprite == null) spriteRenderer.sprite = RuntimeSprite.RoundedSquare;
        }

        public void Bind(PixelCell cell, Color color, float size)
        {
            carrier = null;
            Cell = cell;
            TF.localScale = Vector3.one * size;
            gameObject.name = $"Pixel_{cell.Position.x}_{cell.Position.y}";

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
                colorBlock ??= new MaterialPropertyBlock();
                colorBlock.Clear();
                colorBlock.SetColor(BaseColor, color);
                colorBlock.SetColor(ColorId, color);
                meshRenderer.SetPropertyBlock(colorBlock);
                if (boxVisual != null)
                {
                    boxVisual.localRotation = Quaternion.identity;
                    boxVisual.localScale = Vector3.one * fill;
                }
                
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (depthSprite != null) depthSprite.enabled = false;
                if (glossSprite != null) glossSprite.enabled = false;
            }
            else
            {
                EnsureLayeredSpriteVisual();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = RuntimeSprite.BeveledPixel(color);
                    spriteRenderer.sharedMaterial = RuntimeSprite.MaterialFor(spriteRenderer.sprite);
                    spriteRenderer.color = Color.white;
                    spriteRenderer.size = Vector2.one;
                    spriteRenderer.enabled = true;
                }
                if (depthSprite != null)
                {
                    depthSprite.sprite = RuntimeSprite.RoundedSquare;
                    depthSprite.sharedMaterial = RuntimeSprite.MaterialFor(depthSprite.sprite);
                    depthSprite.color = new Color(color.r * 0.58f, color.g * 0.58f, color.b * 0.58f, 1f);
                    depthSprite.size = Vector2.one;
                    depthSprite.enabled = true;
                }
                if (glossSprite != null)
                {
                    glossSprite.enabled = false;
                }
            }
        }

        private void EnsureLayeredSpriteVisual()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (depthSprite == null)
            {
                depthSprite = CreateLayer("PixelDepth", new Vector3(0.045f, -0.09f, 0.04f), new Vector3(1f, 0.96f, 1f), 9);
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = CreateLayer("PixelFace", new Vector3(0f, 0f, -0.06f), Vector3.one, 10);
            }
            if (glossSprite == null)
            {
                glossSprite = CreateLayer("PixelGloss", new Vector3(-0.16f, 0.27f, -0.08f), new Vector3(0.58f, 0.12f, 1f), 11);
            }
        }

        private SpriteRenderer CreateLayer(string layerName, Vector3 position, Vector3 scale, int order)
        {
            GameObject child = new GameObject(layerName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.RoundedSquare;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void LateUpdate()
        {
            if (carrier == null) return;
            carryBlend = Mathf.MoveTowards(carryBlend, 1f, Time.deltaTime / 0.14f);
            float eased = 1f - Mathf.Pow(1f - carryBlend, 3f);
            TF.position = Vector3.Lerp(carryStartPosition, carrier.TransformPoint(carryLocalOffset), eased);
        }

        public void BeginCarry(Transform antTransform, float cellSize)
        {
            Cell = null;
            carrier = antTransform;
            carryStartPosition = TF.position;
            carryBlend = 0f;
            // Local +Y is the ant's forward direction. TransformPoint therefore keeps
            // the cube in front of its head after it turns to return to the nest.
            // The ant root is already uniformly scaled from CellSize, so this offset
            // is expressed in ant-local model units (multiplying again would shrink it twice).
            carryLocalOffset = new Vector3(0f, 1.08f, -.56f);
            TF.SetParent(antTransform.parent, true);
            if (boxVisual != null)
            {
                boxVisual.localRotation = Quaternion.Euler(-18f, 0f, 12f);
                boxVisual.localScale *= 1.08f;
            }
            if (spriteRenderer != null) spriteRenderer.sortingOrder = 31;
        }

        public void ReturnToPool()
        {
            carrier = null;
            carryBlend = 0f;
            Cell = null;
            if (boxVisual != null)
            {
                boxVisual.localRotation = Quaternion.identity;
                boxVisual.localScale = Vector3.one * fill;
            }
            if (spriteRenderer != null) spriteRenderer.sortingOrder = 10;
            SimplePool.Despawn(this);
        }

        public void SetDestroyed() => ReturnToPool();

        private void OnDisable()
        {
            carrier = null;
            carryBlend = 0f;
            Cell = null;
            if (boxVisual != null)
            {
                boxVisual.localRotation = Quaternion.identity;
                boxVisual.localScale = Vector3.one * fill;
            }
            if (spriteRenderer != null) spriteRenderer.sortingOrder = 10;
        }
    }
}
