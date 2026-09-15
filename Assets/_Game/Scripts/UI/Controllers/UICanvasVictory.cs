using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ColonyFlow;

public class UICanvasVictory : UICanvas
{
    [Header("UI Elements")]
    [SerializeField] private Button btnGold;
    [SerializeField] private TextMeshProUGUI txtLevel;

    public override void Setup()
    {
        base.Setup();

        UIManager.EnsureEventSystem();

        if (btnGold == null)
        {
            Transform t = transform.Find("CanvasVictory/Panel/Btn_Gold") ?? transform.Find("Btn_Gold");
            if (t == null)
            {
                foreach (var b in GetComponentsInChildren<Button>(true))
                {
                    if (b.name.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.name.IndexOf("Next", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        btnGold = b;
                        break;
                    }
                }
            }
            else
            {
                btnGold = t.GetComponent<Button>();
            }
        }

        if (txtLevel == null)
        {
            foreach (var txt in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (txt.transform.parent != null && txt.transform.parent.name.IndexOf("Btn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // Skip button labels
                if (txt.text.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.name.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtLevel = txt;
                    break;
                }
            }
        }

        if (btnGold != null)
        {
            btnGold.onClick.RemoveAllListeners();
            btnGold.onClick.AddListener(OnGoldClicked);
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

    private void OnGoldClicked()
    {
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);

        // Advance to next level
        if (GameManager.Ins != null)
        {
            GameManager.Ins.ResetMatch(true);
            GameManager.ChangeState(GameState.Playing);
        }
        else if (LevelManager.Ins != null)
        {
            LevelManager.Ins.OnNextLevel();
        }

        CloseDirectly();
        UICanvasGameplay.Show();
    }

    public static UICanvasVictory Show()
    {
        return UIManager.Ins.OpenUI<UICanvasVictory>();
    }

    public static void Hide()
    {
        UIManager.Ins.CloseUI<UICanvasVictory>();
    }
}
