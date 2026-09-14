using UnityEngine;

namespace ColonyFlow
{
    public class GameManager : Singleton<GameManager>
    {
        private static GameState gameState;

        public static void ChangeState(GameState state)
        {
            gameState = state;
        }

        public static bool IsState(GameState state) => gameState == state;

        protected override void Awake()
        {
            base.Awake();
            if (Ins != this) return;
            
            Input.multiTouchEnabled = false;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            int maxScreenHeight = 1280;
            float ratio = (float)Screen.currentResolution.width / (float)Screen.currentResolution.height;

            if (Screen.currentResolution.height > maxScreenHeight)
            {
                Screen.SetResolution(Mathf.RoundToInt(ratio * maxScreenHeight), maxScreenHeight, true);
            }
        }

        private void Start()
        {
            SetInitialGameState();
            
            // Initialize other managers
            if (BoosterManager.Ins != null) BoosterManager.Ins.OnInit();

            // Only show Loading -> Menu if we are NOT in the Gameplay scene
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay")
            {
                UICanvasLoading.Show();
            }
        }

        private void SetInitialGameState()
        {
            ChangeState(GameState.MainMenu);
        }

        public void ShowMainMenuAfterLoading()
        {
            // Called by UICanvasLoading when it hits 100%
            UICanvasGameMenu.Show();
            
            if (SoundManager.Ins != null)
            {
                // SoundManager.Ins.PlaySound(SoundID.BGM_Menu);
            }
        }

        public void Pause(bool paused)
        {
            if (paused && IsState(GameState.Playing))
            {
                Time.timeScale = 0f;
                ChangeState(GameState.Paused);
            }
            else if (!paused && IsState(GameState.Paused))
            {
                Time.timeScale = 1f;
                ChangeState(GameState.Playing);
            }
        }

        public void ResetMatch(bool nextLevel)
        {
            Time.timeScale = 1f;
            SimplePool.CollectAll();
            
            if (LevelManager.Ins != null)
            {
                if (nextLevel) LevelManager.Ins.OnNextLevel();
                else LevelManager.Ins.ReloadLevel();
            }
        }

        public void SetTargetFrameRate(int fps)
        {
            Application.targetFrameRate = Mathf.Clamp(fps, 30, 240);
        }

        public void SetFpsVisible(bool visible)
        {
            FpsCounter counter = FindFirstObjectByType<FpsCounter>(FindObjectsInactive.Include);
            if (counter != null) counter.gameObject.SetActive(visible);
        }
    }
}
