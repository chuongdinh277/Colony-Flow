using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ColonyFlow;
using TMPro; // Added for TextMeshPro

public class UICanvasGameMenu : UICanvas
{
    [Header("Top Bar")]
    [SerializeField] private Button btnAvatar;
    [SerializeField] private TextMeshProUGUI txtCoins;
    [SerializeField] private TextMeshProUGUI txtLives;
    [SerializeField] private Button btnSettings;

    [Header("Bottom Menu Buttons")]
    [SerializeField] private Button btnRanking;
    [SerializeField] private Button btnTasks;
    [SerializeField] private Button btnStore;
    [SerializeField] private Button btnAwards;

    [Header("Main")]
    [SerializeField] private Button btnPlay;

    public override void Setup()
    {
        base.Setup();

        if (btnPlay != null) btnPlay.onClick.AddListener(OnPlayClicked);
        if (btnSettings != null) btnSettings.onClick.AddListener(OnSettingsClicked);
        
        // Bind Bottom Menu Buttons
        if (btnRanking != null) btnRanking.onClick.AddListener(() => Debug.Log("Ranking clicked - Coming Soon!"));
        if (btnTasks != null) btnTasks.onClick.AddListener(() => Debug.Log("Tasks clicked - Coming Soon!"));
        if (btnStore != null) btnStore.onClick.AddListener(OnStoreClicked);
        if (btnAwards != null) btnAwards.onClick.AddListener(() => Debug.Log("Awards clicked - Coming Soon!"));

        // Init UI values
        UpdateUI();
    }

    private void UpdateUI()
    {
        // Example logic for updating text. You can replace with real data from PlayerPrefs/GameManager
        if (txtCoins != null) txtCoins.text = "4";
        if (txtLives != null) txtLives.text = "Full";
    }

    private void OnPlayClicked()
    {
        if (GameManager.Ins != null)
        {
            GameManager.ChangeState(GameState.Playing);
        }
        
        // Hide Menu
        CloseDirectly();
        
        // Show Gameplay UI
        UICanvasGameplay.Show();
        
        // Start the level in the current scene
        if (LevelManager.Ins != null)
        {
            if (LevelManager.Ins.Controller != null) LevelManager.Ins.Controller.gameObject.SetActive(true);
            LevelManager.Ins.StartSavedLevel();
        }
    }

    private void OnSettingsClicked()
    {
        UICanvasGameSetting.Show(false);
    }

    private void OnStoreClicked()
    {
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);
        UICanvasStore.Show();
    }

    public static UICanvasGameMenu Show()
    {
        return UIManager.Ins.OpenUI<UICanvasGameMenu>();
    }

    public static void Hide()
    {
        UIManager.Ins.CloseUI<UICanvasGameMenu>();
    }
}
