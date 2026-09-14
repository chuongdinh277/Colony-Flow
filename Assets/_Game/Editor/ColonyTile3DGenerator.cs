#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    public static class ColonyTile3DGenerator
    {
        private const string PrefabPath="Assets/_Game/Resources/Prefabs/ColonyTile3D.prefab";
        private const string SessionKey="ColonyFlow.ColonyTile3D.V1";
        static ColonyTile3DGenerator()
        {
            if(SessionState.GetBool(SessionKey,false))return;
            SessionState.SetBool(SessionKey,true);
            EditorApplication.delayCall+=GenerateIfMissing;
        }

        [MenuItem("Colony Flow/Colony Tile/Regenerate 3D Number Tile")]
        public static void Generate()
        {
            Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Art/PixelBox/RoundedPixelBox.asset");
            Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/PixelBox/PixelBoxBase.mat");
            if(mesh==null||material==null){Debug.LogError("Generate PixelBox 3D first.");return;}
            var root=new GameObject("ColonyTile3D");
            BoxCollider2D collider=root.AddComponent<BoxCollider2D>(); collider.size=Vector2.one;

            Transform visual=new GameObject("RoundedNumberBox").transform; visual.SetParent(root.transform,false);
            visual.localScale=new Vector3(1f,1f,.72f);
            MeshFilter filter=visual.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh=mesh;
            MeshRenderer renderer=visual.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On; renderer.receiveShadows=true;

            Transform labelTransform=new GameObject("Count").transform; labelTransform.SetParent(root.transform,false);
            labelTransform.localPosition=new Vector3(0f,0f,-.265f);
            TextMesh label=labelTransform.gameObject.AddComponent<TextMesh>();
            label.text="30"; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.fontSize=64; label.characterSize=.065f; label.fontStyle=FontStyle.Bold; label.color=Color.white;

            ColonyTileView view=root.AddComponent<ColonyTileView>(); view.Configure(renderer,null,label,collider); view.poolType=PoolType.ColonyTile;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
            Debug.Log("Generated pooled 3D ColonyTile prefab with count label.");
        }
        private static void GenerateIfMissing()
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null||prefab.GetComponentInChildren<MeshRenderer>()==null)Generate();
        }
    }
}
#endif
