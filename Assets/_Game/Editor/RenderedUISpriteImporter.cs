#if UNITY_EDITOR
using UnityEditor;

namespace ColonyFlow.Editor
{
    public sealed class RenderedUISpriteImporter : AssetPostprocessor
    {
        private const string RenderedFolder = "Assets/_Game/Texture/UI/Rendered/";
        private const string ManualKitFolder = "Assets/_Game/Texture/UI/ManualKit/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(RenderedFolder, System.StringComparison.Ordinal) &&
                !assetPath.StartsWith(ManualKitFolder, System.StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
        }

        [MenuItem("Colony Flow/UI/Reimport Rendered UI Textures")]
        private static void ReimportAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                RenderedFolder.TrimEnd('/'), ManualKitFolder.TrimEnd('/')
            });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
