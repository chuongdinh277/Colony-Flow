using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    public static class ColonyFlowMenu
    {
        [MenuItem("GameObject/Colony Flow/Create Gameplay Root", false, 10)]
        public static void CreateGameplayRoot()
        {
            var root = new GameObject("ColonyFlow_Gameplay");
            Undo.RegisterCreatedObjectUndo(root, "Create Colony Flow Gameplay");
            root.AddComponent<GameFlowController>();
            root.AddComponent<GameplayBootstrap>();
            Selection.activeGameObject = root;
        }

        [MenuItem("Assets/Create/Colony Flow/Default Palette")]
        public static void CreateDefaultPalette()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/PixelPalette.asset");
            var palette = ScriptableObject.CreateInstance<PixelPalette>();
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = palette;
        }
    }
}
