#if UNITY_EDITOR
using UnityEditor;

internal sealed class AntSpriteImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith("Assets/_Game/Resources/Ant/ant_chibi_topdown.png")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.filterMode = UnityEngine.FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
    }
}
#endif
