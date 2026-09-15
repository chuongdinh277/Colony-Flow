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

        [MenuItem("Colony Flow/Debug Board Grid 2D (Level 1 or Selected)", false, 20)]
        public static void DebugBoardGrid2D()
        {
            LevelData level = Selection.activeObject as LevelData;
            if (level == null)
            {
                // Try to find any level in Resources/Levels
                LevelData[] levels = Resources.LoadAll<LevelData>("Levels");
                if (levels != null && levels.Length > 0) level = levels[0];
            }
            if (level == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:LevelData");
                if (guids.Length > 0)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    level = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
                }
            }

            if (level == null)
            {
                Debug.LogWarning("[BoardGrid2D] Không tìm thấy LevelData nào để debug!");
                return;
            }

            Debug.Log($"[BoardGrid2D] Đang Debug Level: {level.name} (Width={level.width}, Height={level.height}, Tray={level.trayCapacity}, Tiles={level.colonyTiles.Count})");
            var grid = new BoardGrid2D();
            grid.Build(level);
            grid.LogDebug();
        }
    }
}
