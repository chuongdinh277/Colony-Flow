using UnityEngine;

namespace ColonyFlow
{
    public sealed class BoardFrameView : MonoBehaviour
    {
        [Header("Custom Sprites")]
        [SerializeField] private Sprite customBorderSprite;
        [SerializeField] private Sprite customPanelSprite;

        private SpriteRenderer shadow;
        private SpriteRenderer border;
        private SpriteRenderer panel;
        private SpriteRenderer entranceRim;
        private SpriteRenderer entrance;

        public void SetCustomSprites(Sprite frame, Sprite panel)
        {
            customBorderSprite = frame;
            customPanelSprite = panel;
        }

        public void Build(PixelBoard board, BorderPath path)
        {
            EnsureVisuals();

            float pictureWidth = board.Width * board.CellSize;
            float pictureHeight = board.Height * board.CellSize;
            float squareSide = Mathf.Max(pictureWidth, pictureHeight) + board.CellSize * 4.5f;
            Vector2 frameSize = Vector2.one * squareSide;
            transform.position = board.transform.position;

            SetLayer(shadow, frameSize + new Vector2(0.18f, 0.18f), customBorderSprite != null ? new Color32(0, 0, 0, 115) : new Color32(170, 117, 72, 115), -23);
            shadow.transform.localPosition = new Vector3(0.10f, -0.14f, 0f);
            SetLayer(border, frameSize, customBorderSprite != null ? Color.white : new Color32(211, 158, 104, 255), -22);
            
            if (customBorderSprite != null && customPanelSprite == null)
            {
                panel.gameObject.SetActive(false);
            }
            else
            {
                panel.gameObject.SetActive(true);
                SetLayer(panel, frameSize - new Vector2(0.22f, 0.22f), customPanelSprite != null ? Color.white : new Color32(248, 237, 222, 255), -21);
            }

            Vector3 spawnWorld = path.EntranceWorldPosition;
            Vector3 localSpawn = transform.InverseTransformPoint(spawnWorld);
            entranceRim.transform.localPosition = localSpawn;
            entrance.transform.localPosition = localSpawn + new Vector3(0f, 0.025f, 0f);
            SetLayer(entranceRim, new Vector2(board.CellSize * 4.8f, board.CellSize * 1.45f), new Color32(220, 137, 77, 255), 18);
            SetLayer(entrance, new Vector2(board.CellSize * 4.15f, board.CellSize * 1.0f), new Color32(74, 35, 31, 255), 19);
        }

        private void EnsureVisuals()
        {
            if (shadow == null) shadow = CreateRenderer("FrameShadow", customBorderSprite);
            if (border == null) border = CreateRenderer("FrameBorder", customBorderSprite);
            if (panel == null) panel = CreateRenderer("FramePanel", customPanelSprite);
            if (entranceRim == null) entranceRim = CreateRenderer("AntEntranceRim", null);
            if (entrance == null) entrance = CreateRenderer("AntEntrance", null);
        }

        private SpriteRenderer CreateRenderer(string childName, Sprite customSprite)
        {
            Transform child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = customSprite != null ? customSprite : RuntimeSprite.RoundedSquare;
            renderer.drawMode = SpriteDrawMode.Sliced;
            return renderer;
        }

        private static void SetLayer(SpriteRenderer renderer, Vector2 size, Color color, int order)
        {
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = order;
        }
    }
}
