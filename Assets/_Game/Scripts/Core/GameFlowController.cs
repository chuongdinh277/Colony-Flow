using System;
using UnityEngine;

namespace ColonyFlow
{
    /// <summary>Compatibility facade for scenes/scripts created before the Manager architecture.</summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void StartLevel(LevelData data)
        {
            EnsureManagers();
            LevelManager.Ins.StartLevel(data);
        }

        public void Pause(bool paused)
        {
            EnsureManagers();
            GameManager.Ins.Pause(paused);
        }

        public void SetSpeed(float speed) => Time.timeScale = Mathf.Clamp(speed, 0.25f, 2f);

        public void StopLevel()
        {
            Time.timeScale = 1f;
            if (LevelManager.Ins != null && LevelManager.Ins.Controller != null)
                LevelManager.Ins.Controller.Shutdown();
            GameManager.ChangeState(GameState.None);
        }

        public void SetTargetFrameRate(int fps)
        {
            EnsureManagers();
            GameManager.Ins.SetTargetFrameRate(fps);
        }

        public void SetFpsVisible(bool visible)
        {
            EnsureManagers();
            GameManager.Ins.SetFpsVisible(visible);
        }

        private static void EnsureManagers()
        {
            if (GameManager.Ins == null)
                new GameObject("Managers").AddComponent<GameManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
