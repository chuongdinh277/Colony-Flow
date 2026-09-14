#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    public static class AntMaterialGenerator
    {
        private const string PalettePath = "Assets/_Game/Data/DefaultPalette.asset";
        private const string SourceMaterialPath = "Assets/_Game/Art/Ant/Material.mat";
        private const string MaterialFolder = "Assets/_Game/Art/Ant/Materials";
        private const string LibraryFolder = "Assets/_Game/Resources/Ant";
        private const string LibraryPath = LibraryFolder + "/AntMaterialPalette.asset";

        static AntMaterialGenerator()
        {
            EditorApplication.delayCall += GenerateIfRequired;
        }

        [MenuItem("Colony Flow/Ant/Regenerate Palette Materials")]
        public static void Generate()
        {
            PixelPalette palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(PalettePath);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (palette == null || source == null)
            {
                Debug.LogError("Ant materials require DefaultPalette.asset and Art/Ant/Material.mat.");
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder(LibraryFolder);
            var generated = new List<Material>(palette.Count);
            for (int i = 0; i < palette.Count; i++)
            {
                string id = Sanitize(palette.GetId(i));
                string path = $"{MaterialFolder}/Ant_{i:00}_{id}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(source);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    material.shader = source.shader;
                    material.CopyPropertiesFromMaterial(source);
                }

                Color color = palette.GetColor(i);
                material.name = $"Ant_{i:00}_{id}";
                material.enableInstancing = true;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                EditorUtility.SetDirty(material);
                generated.Add(material);
            }

            AntMaterialPalette library = AssetDatabase.LoadAssetAtPath<AntMaterialPalette>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AntMaterialPalette>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject serialized = new(library);
            serialized.FindProperty("sourcePalette").objectReferenceValue = palette;
            SerializedProperty materials = serialized.FindProperty("materials");
            materials.arraySize = generated.Count;
            for (int i = 0; i < generated.Count; i++)
                materials.GetArrayElementAtIndex(i).objectReferenceValue = generated[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated {generated.Count} ant materials from {palette.name}.", library);
        }

        private static void GenerateIfRequired()
        {
            PixelPalette palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(PalettePath);
            AntMaterialPalette library = AssetDatabase.LoadAssetAtPath<AntMaterialPalette>(LibraryPath);
            if (palette != null && (library == null || library.Materials.Count != palette.Count)) Generate();
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            value = value.Replace('#', '_').Replace(' ', '_');
            return string.IsNullOrWhiteSpace(value) ? "Color" : value;
        }
    }
}
#endif
