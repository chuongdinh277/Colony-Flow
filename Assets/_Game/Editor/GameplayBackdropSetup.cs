#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class GameplayBackdropSetup
    {
        private const string Path = "Assets/_Game/Resources/UI/Gameplay/gameplayBackdrop.png";
        private const string Key = "ColonyFlow.GameplayBackdrop.V1";

        static GameplayBackdropSetup() => EditorApplication.delayCall += Configure;

        private static void Configure()
        {
            TextureImporter importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            if (importer == null) return;
            if (SessionState.GetBool(Key, false) && importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            SessionState.SetBool(Key, true);
        }
    }
}
#endif
