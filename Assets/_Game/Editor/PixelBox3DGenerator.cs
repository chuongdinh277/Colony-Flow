#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    public static class PixelBox3DGenerator
    {
        private const string Folder = "Assets/_Game/Art/PixelBox";
        private const string MeshPath = Folder + "/RoundedPixelBox.asset";
        private const string MaterialPath = Folder + "/PixelBoxBase.mat";
        private const string PrefabPath = "Assets/_Game/Resources/Prefabs/PixelBox.prefab";
        private const int Version = 7;

        static PixelBox3DGenerator() => EditorApplication.delayCall += GenerateIfOutdated;

        [MenuItem("Colony Flow/Pixel Box/Regenerate 3D Pixel Box")]
        public static void Generate()
        {
            EnsureFolder(Folder);
            AssetDatabase.DeleteAsset(MeshPath);
            Mesh mesh = BuildRoundedCube(10, .12f);
            mesh.name = "RoundedPixelBox";
            AssetDatabase.CreateAsset(mesh, MeshPath);

            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Ant/Material.mat");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.enableInstancing = true;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .48f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            var root = new GameObject("PixelBox");
            Transform visual = new GameObject("RoundedCubeVisual").transform;
            visual.SetParent(root.transform, false);
            MeshFilter filter = visual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            PixelCellView view = root.AddComponent<PixelCellView>();
            view.Configure(renderer, visual);
            view.poolType = PoolType.Pixel;
            SerializedObject data = new(view);
            data.FindProperty("fill").floatValue = 1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt("ColonyFlow.PixelBoxVersion", Version);
            Debug.Log("Generated rounded 3D PixelBox prefab.");
        }

        private static void GenerateIfOutdated()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponentInChildren<MeshRenderer>() == null ||
                EditorPrefs.GetInt("ColonyFlow.PixelBoxVersion", 0) < Version) Generate();
        }

        private static Mesh BuildRoundedCube(int segments, float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            AddFace(vertices, normals, triangles, segments, Vector3.right, Vector3.up, Vector3.forward, radius);
            AddFace(vertices, normals, triangles, segments, Vector3.left, Vector3.up, Vector3.back, radius);
            AddFace(vertices, normals, triangles, segments, Vector3.up, Vector3.forward, Vector3.right, radius);
            AddFace(vertices, normals, triangles, segments, Vector3.down, Vector3.forward, Vector3.left, radius);
            AddFace(vertices, normals, triangles, segments, Vector3.forward, Vector3.up, Vector3.left, radius);
            AddFace(vertices, normals, triangles, segments, Vector3.back, Vector3.up, Vector3.right, radius);
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(List<Vector3> vertices,List<Vector3> normals,List<int> triangles,int segments,Vector3 normal,Vector3 up,Vector3 right,float radius)
        {
            int start=vertices.Count;
            for(int y=0;y<=segments;y++) for(int x=0;x<=segments;x++)
            {
                float u=x/(float)segments-.5f, v=y/(float)segments-.5f;
                Vector3 p=normal*.5f+right*u+up*v;
                Vector3 inner=new(Mathf.Clamp(p.x,-.5f+radius,.5f-radius),Mathf.Clamp(p.y,-.5f+radius,.5f-radius),Mathf.Clamp(p.z,-.5f+radius,.5f-radius));
                Vector3 n=(p-inner).normalized; vertices.Add(inner+n*radius); normals.Add(n);
            }
            bool flip=Vector3.Dot(Vector3.Cross(right,up),normal)<0f;
            for(int y=0;y<segments;y++) for(int x=0;x<segments;x++)
            {
                int a=start+y*(segments+1)+x,b=a+1,c=a+segments+1,d=c+1;
                if(!flip){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
                else{triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            }
        }

        private static void EnsureFolder(string path)
        {
            string current="Assets"; string[] parts=path.Split('/');
            for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
    }
}
#endif
