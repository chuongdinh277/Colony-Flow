using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class LevelManager : Singleton<LevelManager>
    {
        private const string SavedLevelKey = "ColonyFlow.CurrentLevel";

        [SerializeField] private LevelController levelController;
        [SerializeField] private List<LevelData> levels = new();
        [SerializeField] private bool loadLevelsFromResources = true;
        [SerializeField] private string resourcesFolder = "Levels";
        [SerializeField, Min(1)] private int firstLevel = 1;

        [Header("3D Frame Customization")]
        public Sprite CustomFrameSprite;
        public Sprite CustomPanelSprite;

        public int CurrentLevelIndex { get; private set; } = 1;
        public LevelData CurrentLevel { get; private set; }
        public LevelController Controller => levelController;
        public event Action<int, LevelData> LevelLoaded;
        public event Action<int> LevelWon;
        public event Action<int> LevelFailed;

        protected override void Awake()
        {
            base.Awake();
            if (Ins != this) return;
            EnsureController();
            levelController.Victory -= OnVictory;
            levelController.Failed -= OnFailed;
            levelController.Victory += OnVictory;
            levelController.Failed += OnFailed;
            BuildLevelCatalog();
            CurrentLevelIndex = Mathf.Max(firstLevel, PlayerPrefs.GetInt(SavedLevelKey, firstLevel));
        }

        public void StartSavedLevel()
        {
#if UNITY_EDITOR
            string playtestPath = UnityEditor.EditorPrefs.GetString("ColonyFlow.PlaytestLevelPath", string.Empty);
            if (!string.IsNullOrEmpty(playtestPath))
            {
                LevelData selected = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(playtestPath);
                if (selected != null)
                {
                    UnityEditor.EditorPrefs.DeleteKey("ColonyFlow.PlaytestLevelPath");
                    StartLevel(selected);
                    return;
                }
            }
#endif
            OnLoadLevel(CurrentLevelIndex);
        }

        public void StartLevel(LevelData data)
        {
            if (data == null) return;
            int catalogIndex = levels.IndexOf(data);
            if (catalogIndex >= 0) CurrentLevelIndex = catalogIndex + 1;
            Load(data);
        }

        public void OnLoadLevel(int levelIndex)
        {
            BuildLevelCatalog();
            if (levels.Count == 0)
            {
                Debug.LogError("LevelManager: no LevelData assets are configured or available in Resources/Levels.");
                return;
            }

            CurrentLevelIndex = Mathf.Max(1, levelIndex);
            int mapIndex = (CurrentLevelIndex - 1) % levels.Count;
            Load(levels[mapIndex]);
        }

        public void ReloadLevel() => OnLoadLevel(CurrentLevelIndex);

        public void OnNextLevel()
        {
            CurrentLevelIndex++;
            PlayerPrefs.SetInt(SavedLevelKey, CurrentLevelIndex);
            PlayerPrefs.Save();
            OnLoadLevel(CurrentLevelIndex);
        }

        public int GetCurrentLevelIndex() => CurrentLevelIndex;

        private void Load(LevelData data)
        {
            EnsureController();
            GameManager.ChangeState(GameState.Loading);
            CurrentLevel = data;
            levelController.Initialize(data);
            GameManager.ChangeState(GameState.Playing);
            if (SoundManager.Ins != null) SoundManager.Ins.PlayMusic(SoundID.BGM_Gameplay);
            LevelLoaded?.Invoke(CurrentLevelIndex, data);
        }

        private void EnsureController()
        {
            if (levelController != null) return;
            levelController = new GameObject("LevelController").AddComponent<LevelController>();
            levelController.transform.SetParent(transform, false);
        }

        private void BuildLevelCatalog()
        {
            levels.RemoveAll(item => item == null);
            if (!loadLevelsFromResources || levels.Count > 0) return;
            levels.AddRange(Resources.LoadAll<LevelData>(resourcesFolder));
            levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void OnVictory()
        {
            GameManager.ChangeState(GameState.Victory);
            if (SoundManager.Ins != null) SoundManager.Ins.PlayGameFx(GameFxID.Win);
            LevelWon?.Invoke(CurrentLevelIndex);
            UICanvasVictory.Show();
        }

        private void OnFailed()
        {
            GameManager.ChangeState(GameState.Failed);
            if (SoundManager.Ins != null) SoundManager.Ins.PlayGameFx(GameFxID.Lose);
            LevelFailed?.Invoke(CurrentLevelIndex);
            UICanvasLose.Show();
        }

        protected override void OnDestroy()
        {
            if (levelController != null)
            {
                levelController.Victory -= OnVictory;
                levelController.Failed -= OnFailed;
                levelController.Shutdown();
            }
            base.OnDestroy();
        }
    }
}
