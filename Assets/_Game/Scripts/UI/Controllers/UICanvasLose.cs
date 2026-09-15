using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ColonyFlow;

public class UICanvasLose : UICanvas
{
    [Header("UI Elements")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnHome;
    [SerializeField] private TextMeshProUGUI txtLevel;

    public override void Setup()
    {
        base.Setup();

        UIManager.EnsureEventSystem();

        if (btnRetry == null)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                if (b.name.IndexOf("Retry", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    btnRetry = b;
                    break;
                }
            }
        }

        if (btnHome == null)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                if (b.name.IndexOf("Home", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    btnHome = b;
                    break;
                }
            }
        }

        if (txtLevel == null)
        {
            foreach (var txt in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (txt.transform.parent != null && txt.transform.parent.name.IndexOf("Btn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (txt.text.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.name.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtLevel = txt;
                    break;
                }
            }
        }

        if (btnRetry != null)
        {
            btnRetry.onClick.RemoveAllListeners();
            btnRetry.onClick.AddListener(OnRetryClicked);
        }

        if (btnHome != null)
        {
            btnHome.onClick.RemoveAllListeners();
            btnHome.onClick.AddListener(OnHomeClicked);
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        int levelIndex = LevelManager.Ins != null ? LevelManager.Ins.CurrentLevelIndex : 1;
        if (txtLevel != null)
        {
            txtLevel.text = "Level " + levelIndex;
        }
    }

    private void OnRetryClicked()
    {
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);

        if (GameManager.Ins != null)
        {
            GameManager.Ins.Pause(false);
        }
        Time.timeScale = 1f;
        CloseDirectly();

        UICanvasLoading.ShowWithAction(0.8f, () =>
        {
            if (GameManager.Ins != null)
            {
                GameManager.Ins.ResetMatch(false);
                GameManager.ChangeState(GameState.Playing);
            }
            else if (LevelManager.Ins != null)
            {
                LevelManager.Ins.ReloadLevel();
            }
        }, () =>
        {
            UICanvasGameplay.Show();
        });
    }

    private void OnHomeClicked()
    {
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);

        if (GameManager.Ins != null)
        {
            GameManager.Ins.GameSpeedScale = 1f;
            GameManager.ChangeState(GameState.MainMenu);
        }

        if (UIManager.Ins != null)
        {
            UIManager.Ins.CloseUI<UICanvasGameplay>();
        }

        if (LevelManager.Ins != null && LevelManager.Ins.Controller != null)
        {
            LevelManager.Ins.Controller.gameObject.SetActive(false);
        }

        UICanvasGameMenu.Show();
        CloseDirectly();
    }

    public static UICanvasLose Show()
    {
        return UIManager.Ins.OpenUI<UICanvasLose>();
    }

    public static void Hide()
    {
        UIManager.Ins.CloseUI<UICanvasLose>();
    }
}
