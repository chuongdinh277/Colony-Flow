using UnityEngine;
using ColonyFlow.UI;

namespace ColonyFlow
{
    public sealed class GameplayBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelData level;

        private void Start()
        {
#if UNITY_EDITOR
            string playtestPath = UnityEditor.EditorPrefs.GetString("ColonyFlow.PlaytestLevelPath", string.Empty);
            if (!string.IsNullOrEmpty(playtestPath))
            {
                LevelData selected = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(playtestPath);
                if (selected != null) level = selected;
                UnityEditor.EditorPrefs.DeleteKey("ColonyFlow.PlaytestLevelPath");
            }
#endif
            if (GameManager.Ins == null)
                new GameObject("Managers").AddComponent<GameManager>();

            if (FindFirstObjectByType<UICanvasGameplay>() == null)
            {
                UICanvasGameplay.Show();
            }

            // The old 12x10 DemoLevel is only a striped placeholder. Use the
            // detailed 32x32 test map for gameplay visual testing instead.
            if (level == null || (level.width <= 12 && level.height <= 10))
            {
                LevelData detailedTestLevel = Resources.Load<LevelData>("Levels/Level_testmap");
                if (detailedTestLevel != null) level = detailedTestLevel;
            }

            if (level != null) LevelManager.Ins.StartLevel(level);
            else LevelManager.Ins.StartSavedLevel();
        }
    }
}
