using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ColonyFlow;

public class UICanvasGameSetting : UICanvas
{
    [Header("Buttons")]
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnHome;

    [Header("Toggles")]
    [SerializeField] private Button btnSound;
    [SerializeField] private Button btnMusic;
    [SerializeField] private Button btnVibration;
    [SerializeField] private Button btnTheme;

    [Header("Toggles (Background ON - Green)")]
    [SerializeField] private Image imgSoundBgOn;
    [SerializeField] private Image imgMusicBgOn;
    [SerializeField] private Image imgVibrationBgOn;
    [SerializeField] private Image imgThemeBgOn;

    [Header("Toggles (Background OFF - Purple)")]
    [SerializeField] private Image imgSoundBgOff;
    [SerializeField] private Image imgMusicBgOff;
    [SerializeField] private Image imgVibrationBgOff;
    [SerializeField] private Image imgThemeBgOff;

    [Header("Toggles (Knob RectTransform)")]
    [SerializeField] private RectTransform knobSound;
    [SerializeField] private RectTransform knobMusic;
    [SerializeField] private RectTransform knobVibration;
    [SerializeField] private RectTransform knobTheme;

    private bool gameplay;
    private bool sound = true;
    private bool music = true;
    private bool vibration = true;
    private bool theme = true;

    [Header("Toggle Settings")]
    [SerializeField] private float knobOnX = 40f; 
    [SerializeField] private float knobOffX = -40f;
    [SerializeField] private float animationDuration = 0.15f;

    public override void Setup()
    {
        base.Setup();

        if (btnClose != null) btnClose.onClick.AddListener(OnCloseClicked);
        if (btnRetry != null) btnRetry.onClick.AddListener(OnRetryClicked);
        if (btnHome != null) btnHome.onClick.AddListener(OnHomeClicked);

        // Bind root row buttons
        if (btnSound != null) btnSound.onClick.AddListener(OnSoundClicked);
        if (btnMusic != null) btnMusic.onClick.AddListener(OnMusicClicked);
        if (btnVibration != null) btnVibration.onClick.AddListener(OnVibrationClicked);
        if (btnTheme != null) btnTheme.onClick.AddListener(OnThemeClicked);

        Refresh(false);
    }

    private void OnSoundClicked() { sound = !sound; Refresh(true); }
    private void OnMusicClicked() { music = !music; SoundManager.Ins?.SetMusicEnabled(music); Refresh(true); }
    private void OnVibrationClicked() { vibration = !vibration; Refresh(true); }
    private void OnThemeClicked() { theme = !theme; Refresh(true); }

    private void Refresh(bool animate = false)
    {
        SetToggleState(imgSoundBgOn, imgSoundBgOff, knobSound, sound, animate);
        SetToggleState(imgMusicBgOn, imgMusicBgOff, knobMusic, music, animate);
        SetToggleState(imgVibrationBgOn, imgVibrationBgOff, knobVibration, vibration, animate);
        SetToggleState(imgThemeBgOn, imgThemeBgOff, knobTheme, theme, animate);
    }

    private void SetToggleState(Image bgOn, Image bgOff, RectTransform knob, bool isOn, bool animate)
    {
        // Ensure both images are active so we can crossfade them
        if (bgOn != null && !bgOn.gameObject.activeSelf) bgOn.gameObject.SetActive(true);
        if (bgOff != null && !bgOff.gameObject.activeSelf) bgOff.gameObject.SetActive(true);

        float targetAlphaOn = isOn ? 1f : 0f;
        float targetAlphaOff = isOn ? 0f : 1f;
        float targetX = isOn ? knobOnX : knobOffX;

        if (!animate)
        {
            if (bgOn != null) bgOn.color = new Color(1, 1, 1, targetAlphaOn);
            if (bgOff != null) bgOff.color = new Color(1, 1, 1, targetAlphaOff);
            if (knob != null) knob.anchoredPosition = new Vector2(targetX, knob.anchoredPosition.y);
        }
        else
        {
            StartCoroutine(AnimateToggle(bgOn, bgOff, knob, targetAlphaOn, targetAlphaOff, targetX));
        }
    }

    private System.Collections.IEnumerator AnimateToggle(Image bgOn, Image bgOff, RectTransform knob, float targetAlphaOn, float targetAlphaOff, float targetX)
    {
        float elapsed = 0f;
        float startAlphaOn = bgOn != null ? bgOn.color.a : targetAlphaOn;
        float startAlphaOff = bgOff != null ? bgOff.color.a : targetAlphaOff;
        float startX = knob != null ? knob.anchoredPosition.x : targetX;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animationDuration;
            t = 1f - Mathf.Pow(1f - t, 3f); // Ease out

            if (bgOn != null) bgOn.color = new Color(1, 1, 1, Mathf.Lerp(startAlphaOn, targetAlphaOn, t));
            if (bgOff != null) bgOff.color = new Color(1, 1, 1, Mathf.Lerp(startAlphaOff, targetAlphaOff, t));
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t), knob.anchoredPosition.y);
            
            yield return null;
        }

        if (bgOn != null) bgOn.color = new Color(1, 1, 1, targetAlphaOn);
        if (bgOff != null) bgOff.color = new Color(1, 1, 1, targetAlphaOff);
        if (knob != null) knob.anchoredPosition = new Vector2(targetX, knob.anchoredPosition.y);
    }

    private void OnCloseClicked()
    {
        if (gameplay && GameManager.Ins != null)
        {
            GameManager.Ins.Pause(false);
        }
        CloseDirectly();
    }

    private void OnRetryClicked()
    {
        if (GameManager.Ins != null)
        {
            GameManager.Ins.ResetMatch(false);
        }
        else
        {
            Time.timeScale = 1f;
            if (LevelManager.Ins != null)
            {
                LevelManager.Ins.ReloadLevel();
            }
        }
        CloseDirectly();
    }

    private void OnHomeClicked()
    {
        if (GameManager.Ins != null)
        {
            GameManager.Ins.GameSpeedScale = 1f;
            GameManager.ChangeState(GameState.MainMenu);
        }
        Time.timeScale = 1f;
        
        // Hide Gameplay UI if open
        if (UIManager.Ins != null) UIManager.Ins.CloseUI<UICanvasGameplay>();
        
        // Clear board/level visually
        if (LevelManager.Ins != null && LevelManager.Ins.Controller != null) 
            LevelManager.Ins.Controller.gameObject.SetActive(false);
        
        UICanvasGameMenu.Show();
        CloseDirectly();
    }

    public static void Show(bool fromGameplay)
    {
        UICanvasGameSetting setting = UIManager.Ins.OpenUI<UICanvasGameSetting>();
        if (setting != null)
        {
            setting.gameplay = fromGameplay;
            if (fromGameplay && GameManager.Ins != null)
            {
                GameManager.Ins.Pause(true);
            }
        }
    }

    public static void Hide()
    {
        UIManager.Ins.CloseUI<UICanvasGameSetting>();
    }
}
