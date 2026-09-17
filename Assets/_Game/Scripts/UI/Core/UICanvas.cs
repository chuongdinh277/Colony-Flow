using UnityEngine;
using ColonyFlow;

public class UICanvas : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private bool isDestroyOnClose;
    [SerializeField] private bool isHandlingRabbitEars;
    [SerializeField] private bool isWidescreenProcessing;

    protected RectTransform _rectTransform;
    protected Animator _animator;
    protected float _offsetY = 0;

    protected virtual void Start()
    {
        OnInit();
    }

    protected virtual void OnInit()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_animator == null) _animator = GetComponent<Animator>();
        
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 100f; // Ensure it's behind 3D elements which are at 0
        }

        // Automatically juice up all buttons in this canvas
        foreach (var btn in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (btn.GetComponent<UIButtonJuice>() == null)
            {
                // Exclude specific buttons that have their own custom juice
                if (btn.name.Contains("Speed") || btn.name.Contains("Booster")) continue;
                
                btn.gameObject.AddComponent<UIButtonJuice>();
            }
        }

        float ratio = (float)Screen.height / (float)Screen.width;
        
        if (isHandlingRabbitEars)
        {
            if (ratio > 2.1f)
            {
                Vector2 leftBottom = _rectTransform.offsetMin;
                Vector2 rightTop = _rectTransform.offsetMax;
                rightTop.y = -100f;
                _rectTransform.offsetMax = rightTop;
                leftBottom.y = 0f;
                _rectTransform.offsetMin = leftBottom;
                _offsetY = 100f;
            }
        }

        if (isWidescreenProcessing)
        {
            ratio = (float)Screen.width / (float)Screen.height;
            if (ratio < 2.1f)
            {
                float ratioDefault = 850 / 1920f;
                float ratioThis = ratio;
                float value = 1 - (ratioThis - ratioDefault);
                float width = _rectTransform.rect.width * value;
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
        }

        if (popups != null)
        {
            for (int i = 0; i < popups.Length; i++)
            {
                popups[i].ParentsPopup = this;
            }
        }
    }

    public virtual void Setup()
    {
        UIManager.Ins.AddBackUI(this);
        UIManager.Ins.PushBackAction(this, BackKey);
    }

    public virtual void BackKey()
    {
    }

    public virtual void Open()
    {
        CancelInvoke(nameof(CloseDirectly));
        gameObject.SetActive(true);
    }

    public virtual void CloseDirectly()
    {
        CancelInvoke(nameof(CloseDirectly));
        UIManager.Ins.RemoveBackUI(this);
        gameObject.SetActive(false);
        
        if (isDestroyOnClose)
        {
            Destroy(gameObject);
        }
    }

    public virtual void Close(float delayTime)
    {
        Invoke(nameof(CloseDirectly), delayTime);
    }

    #region Popup Management
    [Header("Popup Child")]
    [SerializeField] private UICanvas[] popups;
    public UICanvas ParentsPopup { get; private set; }

    public T GetPopup<T>() where T : UICanvas
    {
        T ui = null;
        if (popups != null)
        {
            for (int i = 0; i < popups.Length; i++)
            {
                if (popups[i] is T)
                {
                    ui = popups[i] as T;
                    break;
                }
            }
        }
        return ui;
    }

    public T OpenPopup<T>() where T : UICanvas
    {
        T ui = GetPopup<T>();
        if (ui != null)
        {
            ui.Setup();
            ui.Open();
        }
        return ui;
    }

    public bool IsOpenedPopup<T>() where T : UICanvas
    {
        UICanvas ui = GetPopup<T>();
        return ui != null && ui.gameObject.activeSelf;
    }

    public void ClosePopup<T>(float delayTime) where T : UICanvas
    {
        GetPopup<T>()?.Close(delayTime);
    }
    
    public void ClosePopupDirect<T>() where T : UICanvas
    {
        GetPopup<T>()?.CloseDirectly();
    }

    public void CloseAllPopup()
    {
        if (popups == null) return;
        for (int i = 0; i < popups.Length; i++)
        {
            popups[i].CloseDirectly();
        }
    }
    #endregion
}
