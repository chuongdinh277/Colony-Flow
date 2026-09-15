using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ColonyFlow;
using TMPro;

public class UICanvasLoading : UICanvas
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image imgFillBar; 
    [SerializeField] private TextMeshProUGUI txtProgress;

    [Header("Settings")]
    [SerializeField] private float displayTime = 2.0f;
    [SerializeField] private float fadeDuration = 0.5f;

    public override void Setup()
    {
        base.Setup();
        
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (imgFillBar != null) imgFillBar.fillAmount = 0f;
        if (txtProgress != null) txtProgress.text = "0%";
    }

    private bool isCustomAction = false;

    public override void Open()
    {
        base.Open();
        if (!isCustomAction)
        {
            StartCoroutine(Sequence());
        }
    }

    private IEnumerator Sequence()
    {
        float elapsed = 0f;
        while (elapsed < displayTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / displayTime);
            
            if (imgFillBar != null) imgFillBar.fillAmount = progress;
            if (txtProgress != null) txtProgress.text = $"{Mathf.FloorToInt(progress * 100)}%";
            
            yield return null;
        }
        
        if (imgFillBar != null) imgFillBar.fillAmount = 1f;
        if (txtProgress != null) txtProgress.text = "100%";
        
        // Typical loading screen transition
        transform.SetAsLastSibling();

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null) 
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        if (GameManager.Ins != null)
        {
            GameManager.Ins.ShowMainMenuAfterLoading();
        }

        CloseDirectly();
    }

    public static UICanvasLoading Show() 
    {
        UICanvasLoading loading = UIManager.Ins.GetUI<UICanvasLoading>();
        if (loading != null) loading.isCustomAction = false;
        return UIManager.Ins.OpenUI<UICanvasLoading>();
    }

    public static void ShowWithAction(float duration, System.Action onLoaded, System.Action onComplete = null)
    {
        UICanvasLoading loading = UIManager.Ins.GetUI<UICanvasLoading>();
        if (loading != null)
        {
            loading.isCustomAction = true;
            loading.StopAllCoroutines();
        }
        loading = UIManager.Ins.OpenUI<UICanvasLoading>();
        if (loading != null)
        {
            loading.StopAllCoroutines();
            loading.StartCoroutine(loading.CustomSequence(duration, onLoaded, onComplete));
        }
    }

    private IEnumerator CustomSequence(float duration, System.Action onLoaded, System.Action onComplete)
    {
        transform.SetAsLastSibling();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (imgFillBar != null) imgFillBar.fillAmount = 0f;
        if (txtProgress != null) txtProgress.text = "0%";

        float halfDuration = Mathf.Max(0.1f, duration * 0.5f);
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration) * 0.5f;
            if (imgFillBar != null) imgFillBar.fillAmount = progress;
            if (txtProgress != null) txtProgress.text = $"{Mathf.FloorToInt(progress * 100)}%";
            yield return null;
        }

        if (imgFillBar != null) imgFillBar.fillAmount = 0.5f;
        if (txtProgress != null) txtProgress.text = "50%";

        onLoaded?.Invoke();

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = 0.5f + Mathf.Clamp01(elapsed / halfDuration) * 0.5f;
            if (imgFillBar != null) imgFillBar.fillAmount = progress;
            if (txtProgress != null) txtProgress.text = $"{Mathf.FloorToInt(progress * 100)}%";
            yield return null;
        }

        if (imgFillBar != null) imgFillBar.fillAmount = 1f;
        if (txtProgress != null) txtProgress.text = "100%";

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        isCustomAction = false;
        CloseDirectly();
        onComplete?.Invoke();
    }

    public static void Hide() 
    {
        UIManager.Ins.CloseUI<UICanvasLoading>();
    }
}
