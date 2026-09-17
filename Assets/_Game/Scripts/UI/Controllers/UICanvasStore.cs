using ColonyFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UICanvasStore : UICanvas
{
    private const int BoosterPrice = 50;

    [Header("Buttons")]
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnBuySlotBox;
    [SerializeField] private Button btnBuyBomb;
    [SerializeField] private Button btnBuyMagnet;
    [SerializeField] private Button btnBuySwap;

    [Header("Owned Count")]
    [SerializeField] private TextMeshProUGUI txtSlotBoxCount;
    [SerializeField] private TextMeshProUGUI txtBombCount;
    [SerializeField] private TextMeshProUGUI txtMagnetCount;
    [SerializeField] private TextMeshProUGUI txtSwapCount;

    public override void Setup()
    {
        base.Setup();
        UIManager.EnsureEventSystem();
        BindButtons();
        RefreshStore();
    }

    private void OnEnable()
    {
        RefreshStore();
    }

    private void OnDisable()
    {
        // Counts are refreshed whenever the popup opens.
    }

    private void BindButtons()
    {
        Bind(btnClose, OnCloseClicked);
        Bind(btnBuySlotBox, () => Buy(BoosterType.Shovel));
        Bind(btnBuyBomb, () => Buy(BoosterType.Bomb));
        Bind(btnBuyMagnet, () => Buy(BoosterType.Magnet));
        Bind(btnBuySwap, () => Buy(BoosterType.Shuffle));
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void Buy(BoosterType type)
    {
        if (DataManager.Ins == null || BoosterManager.Ins == null) return;
        if (DataManager.Ins.Coins < BoosterPrice) return;
        DataManager.Ins.Coins -= BoosterPrice;
        BoosterManager.Ins.AddBooster(type, 1);
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);
        RefreshStore();
    }

    private void RefreshStore()
    {
        if (DataManager.Ins == null) return;

        SetCount(txtSlotBoxCount, BoosterType.Shovel);
        SetCount(txtBombCount, BoosterType.Bomb);
        SetCount(txtMagnetCount, BoosterType.Magnet);
        SetCount(txtSwapCount, BoosterType.Shuffle);

        bool canBuy = DataManager.Ins.Coins >= BoosterPrice;
        if (btnBuySlotBox != null) btnBuySlotBox.interactable = canBuy;
        if (btnBuyBomb != null) btnBuyBomb.interactable = canBuy;
        if (btnBuyMagnet != null) btnBuyMagnet.interactable = canBuy;
        if (btnBuySwap != null) btnBuySwap.interactable = canBuy;
    }

    private static void SetCount(TextMeshProUGUI label, BoosterType type)
    {
        if (label != null)
            label.text = $"Hiện có: <color=#18A13A>{(BoosterManager.Ins != null ? BoosterManager.Ins.GetBoosterCount(type) : 0)}</color>";
    }

    private void OnCloseClicked()
    {
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);
        CloseDirectly();
    }

    public static UICanvasStore Show() => UIManager.Ins.OpenUI<UICanvasStore>();
    public static void Hide() => UIManager.Ins.CloseUI<UICanvasStore>();
}
