using UnityEngine;

namespace ColonyFlow
{
    public sealed class ColonyTileView : GameUnit
    {
        [SerializeField] private MeshRenderer faceRenderer;
        [SerializeField] private MeshRenderer depthRenderer;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh countLabel;
        [SerializeField] private BoxCollider2D cachedCollider;
        private SpriteRenderer layeredFace;
        private SpriteRenderer layeredDepth;
        private SpriteRenderer layeredGloss;
        [SerializeField, Min(.1f)] private float shiftSpeed = 7f;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"), ColorId=Shader.PropertyToID("_Color");
        private MaterialPropertyBlock faceBlock, depthBlock;
        private ColonyTile tile; private ColonyTileBoard owner; private Color displayColor; private Vector3 targetPosition; private bool boardVisible=true;
        public BoxCollider2D CachedCollider=>cachedCollider;

        public void Configure(MeshRenderer face,MeshRenderer depth,TextMesh label,BoxCollider2D collider)
        {faceRenderer=face;depthRenderer=depth;countLabel=label;cachedCollider=collider;spriteRenderer=null;}
        public void Configure(SpriteRenderer renderer,BoxCollider2D collider)
        {spriteRenderer=renderer;cachedCollider=collider;if(spriteRenderer.sprite==null)spriteRenderer.sprite=RuntimeSprite.Square;spriteRenderer.sortingOrder=30;}
        public void Bind(ColonyTile model,ColonyTileBoard board,Color color)
        {tile=model;owner=board;displayColor=color;targetPosition=TF.position;model.View=this;EnsureLayeredSpriteVisual();EnsureLabel();Refresh();}
        public void Refresh()
        {
            if (tile == null) return;
            gameObject.SetActive(boardVisible && tile.State != TileState.Completed);
            Color color = displayColor;
            SetColor(faceRenderer, color, ref faceBlock);
            SetColor(depthRenderer, new Color(color.r * .58f, color.g * .58f, color.b * .58f, 1f), ref depthBlock);
            if (spriteRenderer != null) spriteRenderer.color = color;
            if (layeredFace != null) { layeredFace.sprite = RuntimeSprite.ColonyBox(color); layeredFace.color = Color.white; layeredFace.sharedMaterial = RuntimeSprite.MaterialFor(layeredFace.sprite); }
            if (layeredDepth != null) { layeredDepth.sprite = RuntimeSprite.ColonyBox(color); layeredDepth.color = new Color(0.58f, 0.58f, 0.58f, 1f); layeredDepth.sharedMaterial = RuntimeSprite.MaterialFor(layeredDepth.sprite); }
            if (layeredGloss != null) layeredGloss.enabled = false;
            if (countLabel != null) 
            {
                countLabel.text = tile.Hidden && tile.State == TileState.Locked ? "?" : tile.Count.ToString();
                countLabel.color = color.grayscale > .68f ? new Color32(49, 42, 58, 255) : Color.white;
                countLabel.gameObject.SetActive(tile.State != TileState.InTray);
            }
        }
        public bool TryClick()=>tile!=null&&tile.State==TileState.Available&&owner.TrySelect(tile.Id);
        private void OnMouseDown()=>TryClick();
        public void MoveTo(Vector3 worldPosition) => targetPosition = worldPosition;
        public Vector3 TargetPosition => targetPosition;
        public void SetBoardVisible(bool visible) { boardVisible = visible; Refresh(); }
        public void SetEmphasis(bool emphasized)
        {
            if(faceRenderer!=null)
            {
                Transform visual=faceRenderer.transform;
                Vector3 scale=visual.localScale;
                scale.z=emphasized?.72f:.50f;
                visual.localScale=scale;
            }
            if(countLabel!=null)
            {
                Vector3 position=countLabel.transform.localPosition;
                position.z=emphasized?-.39f:-.28f;
                countLabel.transform.localPosition=position;
            }
        }
        private void Update()=>TF.position=Vector3.MoveTowards(TF.position,targetPosition,shiftSpeed*Time.deltaTime);
        private static void SetColor(Renderer renderer,Color color,ref MaterialPropertyBlock block)
        {if(renderer==null)return;block??=new MaterialPropertyBlock();block.Clear();block.SetColor(BaseColor,color);block.SetColor(ColorId,color);renderer.SetPropertyBlock(block);}
        private void EnsureLabel()
        {
            if(countLabel!=null)
            {
                MeshRenderer existingRenderer=countLabel.GetComponent<MeshRenderer>();
                if(existingRenderer!=null)existingRenderer.sortingOrder=32;
                countLabel.transform.localPosition=new Vector3(0,0,-.16f);
                return;
            }
            GameObject labelObject=new("Count");labelObject.transform.SetParent(transform,false);labelObject.transform.localPosition=new Vector3(0,0,-.56f);
            countLabel=labelObject.AddComponent<TextMesh>();countLabel.anchor=TextAnchor.MiddleCenter;countLabel.alignment=TextAlignment.Center;countLabel.fontSize=64;countLabel.characterSize=.065f;countLabel.fontStyle=FontStyle.Bold;
            countLabel.GetComponent<MeshRenderer>().sortingOrder=32;
        }

        private void EnsureLayeredSpriteVisual()
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            if (depthRenderer != null) depthRenderer.enabled = false;
            
            // Clean up any rogue 3D meshes injected by previous generation
            MeshRenderer[] allMeshes = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var m in allMeshes)
            {
                if (m != faceRenderer && m != depthRenderer && (countLabel == null || m.gameObject != countLabel.gameObject))
                {
                    m.enabled = false;
                }
            }

            if (layeredFace != null) return;
            layeredDepth = CreateLayer("TileDepth", new Vector3(.055f, -.11f, .04f), new Vector3(1f, .96f, 1f), 29);
            layeredFace = CreateLayer("TileFace", new Vector3(0f, 0f, -.06f), Vector3.one, 30);
            layeredGloss = CreateLayer("TileGloss", new Vector3(-.16f, .27f, -.08f), new Vector3(.58f, .12f, 1f), 31);
        }
        private SpriteRenderer CreateLayer(string layerName,Vector3 position,Vector3 scale,int order)
        {
            GameObject child=new(layerName);child.transform.SetParent(transform,false);
            child.transform.localPosition=position;child.transform.localScale=scale;
            SpriteRenderer renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=RuntimeSprite.RoundedSquare;renderer.sortingOrder=order;
            return renderer;
        }
    }
}
