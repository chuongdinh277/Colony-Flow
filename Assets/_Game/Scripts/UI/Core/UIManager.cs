using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using ColonyFlow;

public class UIManager : Singleton<UIManager>
{
    [Header("UI Root")]
    [SerializeField] private Transform canvasParentTF;

    private readonly Dictionary<System.Type, UICanvas> _uiPrefabs = new Dictionary<System.Type, UICanvas>();
    private readonly Dictionary<System.Type, UICanvas> _instantiatedUIs = new Dictionary<System.Type, UICanvas>();
    private UICanvas[] _uiResources;

    #region Canvas Management

    public T OpenUI<T>() where T : UICanvas
    {
        T canvas = GetUI<T>();
        if (canvas == null) return null;

        canvas.Setup();
        canvas.Open();
        return canvas;
    }

    public void CloseUI<T>() where T : UICanvas
    {
        if (IsOpened<T>())
        {
            GetUI<T>().CloseDirectly();
        }
    }

    public void CloseUI<T>(float delayTime) where T : UICanvas
    {
        if (IsOpened<T>())
        {
            GetUI<T>().Close(delayTime);
        }
    }

    public bool IsOpened<T>() where T : UICanvas
    {
        return IsLoaded<T>() && _instantiatedUIs[typeof(T)].gameObject.activeInHierarchy;
    }

    public bool IsLoaded<T>() where T : UICanvas
    {
        System.Type type = typeof(T);
        return _instantiatedUIs.ContainsKey(type) && _instantiatedUIs[type] != null;
    }

    public T GetUI<T>() where T : UICanvas
    {
        if (!IsLoaded<T>())
        {
            T prefab = GetUIPrefab<T>();
            if (prefab == null)
            {
                Debug.LogError($"UI prefab not found for {typeof(T).Name} in Resources/UI.");
                return null;
            }

            if (canvasParentTF == null)
            {
                GameObject root = new GameObject("UICanvasRoot");
                Object.DontDestroyOnLoad(root);
                canvasParentTF = root.transform;
            }

            UICanvas canvas = Instantiate(prefab, canvasParentTF);
            _instantiatedUIs[typeof(T)] = canvas;
        }
        return _instantiatedUIs[typeof(T)] as T;
    }

    public void CloseAll()
    {
        List<UICanvas> allCanvas = new List<UICanvas>(_instantiatedUIs.Values);
        foreach (var canvas in allCanvas)
        {
            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                canvas.CloseDirectly();
            }
        }
    }

    private T GetUIPrefab<T>() where T : UICanvas
    {
        System.Type type = typeof(T);
        if (!_uiPrefabs.ContainsKey(type))
        {
            if (_uiResources == null)
            {
                _uiResources = Resources.LoadAll<UICanvas>("UI/");
            }

            foreach (var ui in _uiResources)
            {
                if (ui is T)
                {
                    _uiPrefabs[type] = ui;
                    break;
                }
            }
        }

        return _uiPrefabs.TryGetValue(type, out UICanvas prefab)
            ? prefab as T
            : null;
    }

    #endregion

    #region Back Button Handling

    private Dictionary<UICanvas, UnityAction> _backActionEvents = new Dictionary<UICanvas, UnityAction>();
    private List<UICanvas> _backCanvasHistory = new List<UICanvas>();

    private UICanvas BackTopUI 
    {
        get
        {
            if (_backCanvasHistory.Count > 0)
            {
                return _backCanvasHistory[_backCanvasHistory.Count - 1];
            }
            return null;
        }
    }

    private void LateUpdate()
    {
        bool escapePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePressed = true;
        }
#else
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            escapePressed = true;
        }
#endif

        if (escapePressed && BackTopUI != null)
        {
            if (_backActionEvents.TryGetValue(BackTopUI, out UnityAction action))
            {
                action?.Invoke();
            }
        }
    }

    public void PushBackAction(UICanvas canvas, UnityAction action)
    {
        if (!_backActionEvents.ContainsKey(canvas))
        {
            _backActionEvents.Add(canvas, action);
        }
    }

    public void AddBackUI(UICanvas canvas)
    {
        if (!_backCanvasHistory.Contains(canvas))
        {
            _backCanvasHistory.Add(canvas);
        }
    }

    public void RemoveBackUI(UICanvas canvas)
    {
        _backCanvasHistory.Remove(canvas);
        _backActionEvents.Remove(canvas);
    }

    public void ClearBackKey()
    {
        _backCanvasHistory.Clear();
    }

    #endregion
}
