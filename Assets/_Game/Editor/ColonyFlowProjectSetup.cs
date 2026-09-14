using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColonyFlow.Editor
{
    public static class ColonyFlowProjectSetup
    {
        private const string DataFolder = "Assets/_Game/Data";
        private const string SceneFolder = "Assets/_Game/Scenes";
        private const string PalettePath = DataFolder + "/DefaultPalette.asset";
        private const string LevelPath = DataFolder + "/DemoLevel.asset";
        private const string ScenePath = SceneFolder + "/Gameplay.unity";

        [InitializeOnLoadMethod]
        private static void CreateInitialDemoWhenMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateDemoContent();
            };
        }

        [MenuItem("Colony Flow/Create Demo Content")]
        public static void CreateDemoContent()
        {
            EnsureFolder("Assets/_Game", "Data");
            EnsureFolder("Assets/_Game", "Scenes");

            PixelPalette palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<PixelPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }
            FillDemoLevel(level, palette);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            CreateGameplayScene(level);
            Debug.Log($"Colony Flow demo created: {ScenePath}", level);
            Selection.activeObject = level;
        }

        [MenuItem("Colony Flow/Play Demo %#g")]
        public static void PlayDemo()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateDemoContent();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void FillDemoLevel(LevelData level, PixelPalette palette)
        {
            level.width = 12;
            level.height = 10;
            level.cellSize = 0.34f;
            level.trayCapacity = 5;
            level.palette = palette;
            level.pixels = new List<PixelData>();

            for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
            {
                bool body = y >= 1 && y <= 8 && x >= Mathf.Abs(y - 5) / 2 && x < level.width - Mathf.Abs(y - 5) / 2;
                if (!body) continue;
                int color = y < 3 ? 1 : y < 5 ? 2 : y < 7 ? 3 : 4;
                level.pixels.Add(new PixelData(new Vector2Int(x, y), color));
            }

            var counts = new Dictionary<int, int>();
            foreach (PixelData pixel in level.pixels)
                counts[pixel.colorIndex] = counts.TryGetValue(pixel.colorIndex, out int count) ? count + 1 : 1;

            level.colonyTiles = new List<ColonyTileData>();
            int id = 0;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                int remaining = pair.Value;
                while (remaining > 0)
                {
                    int amount = Mathf.Min(20, remaining);
                    level.colonyTiles.Add(new ColonyTileData { id = id++, colorIndex = pair.Key, count = amount });
                    remaining -= amount;
                }
            }
            const int columns = 4;
            for (int i = 0; i < level.colonyTiles.Count; i++)
                level.colonyTiles[i].boardPosition = new Vector2Int(i % columns, i / columns);
        }

        private static void CreateGameplayScene(LevelData level)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(247, 220, 179, 255);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var root = new GameObject("ColonyFlow_Gameplay");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<GameFlowController>();
            GameplayBootstrap bootstrap = root.AddComponent<GameplayBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("level").objectReferenceValue = level;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
