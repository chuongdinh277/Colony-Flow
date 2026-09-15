using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro; // Added TMPro
using ColonyFlow;

public class UICanvasGameplay : UICanvas
{
    [Header("Gameplay Rendering")]
    [SerializeField] private RectTransform pictureFrame;
    [SerializeField, Range(0f, 0.15f)] private float pictureInnerPadding = 0.045f;

    private const int GameplayRenderLayer = 31;
    private Camera gameplayRenderCamera;
    private RenderTexture gameplayRenderTexture;
    private RawImage gameplayRenderImage;
    private Camera sourceCamera;
    private int sourceCameraMask;

    [Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI txtLevel;
    [SerializeField] private Button btnPause;
    [SerializeField] private Button btnSpeed;
    [SerializeField] private TextMeshProUGUI txtSpeed;

    [Header("Boosters & Trays")]
    [SerializeField] private Button[] btnBoosters = new Button[4];
    [Header("Tray")]
    [SerializeField] private List<Image> imgTraySlots = new List<Image>();
    [SerializeField] private Image traySlotPrefab; // The prefab to clone when adding a new slot
    [SerializeField] private Transform traySlotsContainer; // The layout group container

    private readonly float[] speeds = { 1f, 2f };
    private int speedIndex;
    private Image speedButtonImage;
    private GameObject speedBadgeObject;
    private TextMeshProUGUI speedBadgeText;
    private Image speedBadgeImage;
    private Coroutine speedBounceCoroutine;
    private readonly Dictionary<Image, Image> occupiedTrayVisuals = new();
    private readonly Dictionary<Image, TextMeshProUGUI> centeredTrayLabels = new();

    public event Action<int> BoosterClicked;

    public override void Setup()
    {
        base.Setup();

        UIManager.EnsureEventSystem();
        ConfigureGameplayRendering();
        EnsureGameplayRenderSurface();

        if (btnPause != null)
        {
            btnPause.onClick.RemoveAllListeners();
            btnPause.onClick.AddListener(OnPauseClicked);
        }

        if (btnSpeed == null)
        {
            Transform found = FindDeepChild(transform, "Btn_Speed");
            if (found != null) btnSpeed = found.GetComponent<Button>();
        }

        if (btnSpeed != null)
        {
            speedButtonImage = btnSpeed.GetComponent<Image>();
            if (txtSpeed == null)
            {
                txtSpeed = btnSpeed.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            EnsureSpeedStatusBadge();

            btnSpeed.onClick.RemoveAllListeners();
            btnSpeed.onClick.AddListener(OnSpeedClicked);
        }

        float currentSpeed = GameManager.Ins != null ? GameManager.Ins.GameSpeedScale : Time.timeScale;
        speedIndex = currentSpeed >= 1.5f ? 1 : 0;
        ApplySpeed(speeds[speedIndex], false);

        for (int i = 0; i < btnBoosters.Length; i++)
        {
            if (btnBoosters[i] != null)
            {
                int index = i;
                btnBoosters[i].onClick.RemoveAllListeners();
                btnBoosters[i].onClick.AddListener(() => BoosterClicked?.Invoke(index));
            }
        }
        
        if (LevelManager.Ins != null)
        {
            LevelManager.Ins.LevelLoaded -= OnLevelLoaded;
            LevelManager.Ins.LevelLoaded += OnLevelLoaded;
            if (LevelManager.Ins.CurrentLevel != null)
            {
                OnLevelLoaded(LevelManager.Ins.CurrentLevelIndex, LevelManager.Ins.CurrentLevel);
            }
        }
    }

    /// <summary>
    /// Returns the authored picture area as a normalized screen rectangle. The
    /// gameplay board can then follow the UI frame without changing its layout.
    /// </summary>
    public bool TryGetPictureViewport(out Rect viewport)
    {
        viewport = default;
        if (pictureFrame == null)
        {
            Transform found = FindDeepChild(transform, "Attack");
            if (found != null) pictureFrame = found as RectTransform;
        }
        if (pictureFrame == null) return false;

        Canvas.ForceUpdateCanvases();
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null || canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f) return false;

        // Both objects belong to the same Canvas. Measuring in Canvas-local space
        // avoids feeding UI world coordinates through the tilted gameplay camera.
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, pictureFrame);
        Rect rootRect = canvasRect.rect;
        viewport = Rect.MinMaxRect(
            (bounds.min.x - rootRect.xMin) / rootRect.width,
            (bounds.min.y - rootRect.yMin) / rootRect.height,
            (bounds.max.x - rootRect.xMin) / rootRect.width,
            (bounds.max.y - rootRect.yMin) / rootRect.height);
        viewport.xMin = Mathf.Clamp01(viewport.xMin);
        viewport.xMax = Mathf.Clamp01(viewport.xMax);
        viewport.yMin = Mathf.Clamp01(viewport.yMin);
        viewport.yMax = Mathf.Clamp01(viewport.yMax);
        float padX = viewport.width * pictureInnerPadding;
        float padY = viewport.height * pictureInnerPadding;
        viewport.xMin += padX;
        viewport.xMax -= padX;
        viewport.yMin += padY;
        viewport.yMax -= padY;
        return viewport.width > 0f && viewport.height > 0f;
    }

    private void ConfigureGameplayRendering()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 100f;

        // Pixel boxes and colony boxes use renderer orders 11..31. Keeping the
        // gameplay canvas below them makes those objects visible over the authored
        // background/frame while the UI layout itself remains untouched.
        canvas.overrideSorting = true;
        canvas.sortingOrder = 0;
    }

    private void LateUpdate()
    {
        EnsureGameplayRenderSurface();
        LevelController controller = LevelManager.Ins != null ? LevelManager.Ins.Controller : null;
        if (controller == null || gameplayRenderCamera == null) return;

        // The UI Canvas can finish layout after the level has initialized (and it
        // can change on Game-view resize). Refit here using the final authored rect.
        if (TryGetPictureViewport(out Rect pictureViewport))
            controller.FitBoardToPictureViewport(pictureViewport);

        SetLayerRecursively(controller.Board != null ? controller.Board.transform : null, GameplayRenderLayer);
        // The five tray slots are authored UI Images. Do not render the legacy
        // world-space tray over them, otherwise it covers their TMP amounts.
        SetLayerRecursively(controller.Tray != null ? controller.Tray.transform : null, 0);
        Transform tileBoard = controller.transform.Find("ColonyTileBoard");
        Transform ants = controller.transform.Find("Ants");
        SetLayerRecursively(tileBoard, GameplayRenderLayer);
        SetLayerRecursively(ants, GameplayRenderLayer);

        if (sourceCamera != null)
        {
            gameplayRenderCamera.transform.SetPositionAndRotation(
                sourceCamera.transform.position, sourceCamera.transform.rotation);
            gameplayRenderCamera.orthographic = sourceCamera.orthographic;
            gameplayRenderCamera.orthographicSize = sourceCamera.orthographicSize;
            gameplayRenderCamera.fieldOfView = sourceCamera.fieldOfView;
        }
    }

    private void EnsureGameplayRenderSurface()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        if (sourceCamera != camera)
        {
            RestoreSourceCameraMask();
            sourceCamera = camera;
            sourceCameraMask = sourceCamera.cullingMask;
            sourceCamera.cullingMask &= ~(1 << GameplayRenderLayer);
        }

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        if (gameplayRenderTexture == null || gameplayRenderTexture.width != width || gameplayRenderTexture.height != height)
        {
            if (gameplayRenderTexture != null)
            {
                gameplayRenderTexture.Release();
                Destroy(gameplayRenderTexture);
            }
            gameplayRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "GameplayWorld_Transparent",
                filterMode = FilterMode.Bilinear
            };
            gameplayRenderTexture.Create();
            if (gameplayRenderImage != null) gameplayRenderImage.texture = gameplayRenderTexture;
        }

        if (gameplayRenderCamera == null)
        {
            GameObject cameraObject = new GameObject("GameplayWorldCamera");
            cameraObject.transform.SetParent(transform, false);
            gameplayRenderCamera = cameraObject.AddComponent<Camera>();
            gameplayRenderCamera.CopyFrom(sourceCamera);
            UniversalAdditionalCameraData sourceData = sourceCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData renderData = gameplayRenderCamera.GetUniversalAdditionalCameraData();
            renderData.renderPostProcessing = sourceData.renderPostProcessing;
            renderData.antialiasing = sourceData.antialiasing;
            renderData.antialiasingQuality = sourceData.antialiasingQuality;
            renderData.requiresDepthOption = CameraOverrideOption.On;
            renderData.requiresColorOption = CameraOverrideOption.On;
            gameplayRenderCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayRenderCamera.backgroundColor = Color.clear;
            gameplayRenderCamera.cullingMask = 1 << GameplayRenderLayer;
            gameplayRenderCamera.depth = sourceCamera.depth + 1f;
        }
        gameplayRenderCamera.targetTexture = gameplayRenderTexture;

        if (gameplayRenderImage == null)
        {
            Canvas hostCanvas = GetComponentInChildren<Canvas>(true);
            if (hostCanvas == null) return;
            GameObject surface = new GameObject("GameplayWorldSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = surface.GetComponent<RectTransform>();
            rect.SetParent(hostCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            gameplayRenderImage = surface.GetComponent<RawImage>();
            gameplayRenderImage.texture = gameplayRenderTexture;
            gameplayRenderImage.raycastTarget = false;
            // The booster panel and safe area controls occlude the world surface and receive clicks
            Transform boosterBar = FindDeepChild(transform, "BoosterBar");
            if (boosterBar != null)
            {
                Canvas boosterCanvas = boosterBar.GetComponent<Canvas>();
                if (boosterCanvas == null) boosterCanvas = boosterBar.gameObject.AddComponent<Canvas>();
                boosterCanvas.overrideSorting = true;
                boosterCanvas.sortingOrder = hostCanvas.sortingOrder + 10;
                if (boosterBar.GetComponent<GraphicRaycaster>() == null)
                    boosterBar.gameObject.AddComponent<GraphicRaycaster>();
            }

            Transform safeArea = FindDeepChild(transform, "SafeArea");
            if (safeArea != null)
            {
                Canvas existingCanvas = safeArea.GetComponent<Canvas>();
                if (existingCanvas != null) Destroy(existingCanvas);
                GraphicRaycaster existingRaycaster = safeArea.GetComponent<GraphicRaycaster>();
                if (existingRaycaster != null) Destroy(existingRaycaster);
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void RestoreSourceCameraMask()
    {
        if (sourceCamera != null) sourceCamera.cullingMask = sourceCameraMask;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName) return child;
            Transform nested = FindDeepChild(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }

    public void AddExtraUISlot()
    {
        if (traySlotPrefab == null || traySlotsContainer == null)
        {
            Debug.LogError("Vui lòng gán Prefab và Container cho Tray Slot trên Inspector!");
            return;
        }
        
        Image newSlot = Instantiate(traySlotPrefab, traySlotsContainer);
        imgTraySlots.Add(newSlot);
    }

    private void Update()
    {
        ColonyTray tray = LevelManager.Ins?.Controller?.Tray;
        for (int i = 0; i < imgTraySlots.Count; i++)
        {
            if (imgTraySlots[i] == null) continue;
            
            ColonyController colony = tray != null && i < tray.Slots.Count ? tray.Slots[i].Colony : null;
            Image slotImage = imgTraySlots[i];
            
            if (!centeredTrayLabels.TryGetValue(slotImage, out TextMeshProUGUI amount))
            {
                TextMeshProUGUI authored = slotImage.GetComponentInChildren<TextMeshProUGUI>(true);
                if (authored != null) authored.gameObject.SetActive(false);
                GameObject label = new GameObject("CenteredCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Canvas));
                Canvas labelCanvas = label.GetComponent<Canvas>();
                labelCanvas.overrideSorting = true;
                labelCanvas.sortingOrder = 5;
                amount = label.GetComponent<TextMeshProUGUI>();
                amount.rectTransform.SetParent(slotImage.transform, false);
                if (authored != null) amount.font = authored.font;
                amount.raycastTarget = false;
                centeredTrayLabels.Add(slotImage, amount);
            }
            if (amount != null)
            {
                amount.text = colony == null ? string.Empty : colony.RemainingCount.ToString();
                amount.color = colony != null && colony.DisplayColor.grayscale > .65f ? new Color(.15f,.12f,.18f,1f) : Color.white;
                amount.alignment = TextAlignmentOptions.Center;
                amount.fontStyle = FontStyles.Bold;
                amount.enableAutoSizing = false;
                amount.fontSize = 44f;
                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.outlineWidth = .18f;
                amount.outlineColor = colony != null && colony.DisplayColor.grayscale > .65f
                    ? new Color32(255, 255, 255, 150)
                    : new Color32(35, 25, 45, 210);
                RectTransform amountRect = amount.rectTransform;
                amountRect.anchorMin = new Vector2(.5f, .5f);
                amountRect.anchorMax = new Vector2(.5f, .5f);
                amountRect.pivot = new Vector2(.5f, .5f);
                Vector2 slotSize = slotImage.rectTransform.rect.size;
                amountRect.sizeDelta = new Vector2(Mathf.Max(70f, slotSize.x), Mathf.Max(70f, slotSize.y));
                amountRect.anchoredPosition = new Vector2(0f, 9f);
                amountRect.localPosition = new Vector3(amountRect.localPosition.x, amountRect.localPosition.y, -75f);
                amountRect.localScale = Vector3.one;
                amountRect.localRotation = Quaternion.identity;
                amountRect.SetAsLastSibling();
            }
            
            // Keep the UI slot visible so the 3D box overlaps it
            
            // Sync 3D tray slot position to UI tray slot position with a vertical shift to pop out and center perfectly
            if (tray != null && i < tray.Slots.Count)
            {
                Camera uiCam = Camera.main;
                Vector3 uiPos = imgTraySlots[i].transform.position;
                Vector3 upDir = Vector3.up;
                
                if (uiCam != null && uiCam.orthographic)
                {
                    Vector3 rayDir = uiCam.transform.forward;
                    if (Mathf.Abs(rayDir.z) > 0.001f)
                    {
                        float t = -uiPos.z / rayDir.z;
                        uiPos = uiPos + rayDir * t;
                    }
                    upDir = uiCam.transform.up;
                }
                
                Vector3 slotWorld = uiPos + upDir * 0.12f;
                LevelController levelController = LevelManager.Ins != null ? LevelManager.Ins.Controller : null;
                if (levelController != null && levelController.Board != null)
                    slotWorld = levelController.Board.ProjectToGameplayPlane(slotWorld);
                tray.Slots[i].transform.position = slotWorld;
            }
        }
    }

    private void OnLevelLoaded(int index, LevelData unused)
    {
        if (txtLevel != null)
        {
            txtLevel.text = "Level " + index;
        }
    }

    private void OnPauseClicked()
    {
        UICanvasGameSetting.Show(true);
    }

    private void EnsureSpeedStatusBadge()
    {
        if (btnSpeed == null || speedBadgeObject != null) return;

        Transform existingBadge = btnSpeed.transform.Find("SpeedStatusBadge");
        if (existingBadge != null)
        {
            speedBadgeObject = existingBadge.gameObject;
            speedBadgeImage = speedBadgeObject.GetComponent<Image>();
            speedBadgeText = speedBadgeObject.GetComponentInChildren<TextMeshProUGUI>(true);
            return;
        }

        speedBadgeObject = new GameObject("SpeedStatusBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        speedBadgeObject.transform.SetParent(btnSpeed.transform, false);

        RectTransform rt = speedBadgeObject.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(74f, 22f);

        speedBadgeImage = speedBadgeObject.GetComponent<Image>();
        speedBadgeImage.sprite = RuntimeSprite.RoundedSquare;
        speedBadgeImage.type = Image.Type.Sliced;
        speedBadgeImage.raycastTarget = false;

        GameObject textObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(speedBadgeObject.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        speedBadgeText = textObj.GetComponent<TextMeshProUGUI>();
        speedBadgeText.alignment = TextAlignmentOptions.Center;
        speedBadgeText.fontStyle = FontStyles.Bold;
        speedBadgeText.fontSize = 15f;
        speedBadgeText.enableAutoSizing = false;
        speedBadgeText.raycastTarget = false;
        if (txtLevel != null) speedBadgeText.font = txtLevel.font;
    }

    private void OnSpeedClicked()
    {
        speedIndex = (speedIndex + 1) % speeds.Length;
        ApplySpeed(speeds[speedIndex], true);
        SoundManager.Ins?.PlayUIFx(UIFxID.ButtonClick);
    }

    public void ApplySpeed(float speed, bool animate)
    {
        if (GameManager.Ins != null)
        {
            GameManager.Ins.GameSpeedScale = speed;
        }
        else
        {
            Time.timeScale = speed;
            Time.fixedDeltaTime = 0.02f * speed;
        }

        UpdateSpeedVisual(animate);
    }

    private void UpdateSpeedVisual(bool animate)
    {
        bool is2X = speedIndex == 1;

        if (speedButtonImage != null)
        {
            speedButtonImage.color = is2X ? Color.white : new Color(0.68f, 0.72f, 0.82f, 0.82f);
        }

        if (speedBadgeText != null && speedBadgeImage != null)
        {
            speedBadgeText.text = is2X ? "2X FAST" : "1X";
            speedBadgeText.color = is2X ? Color.white : new Color(0.8f, 0.85f, 0.9f, 0.95f);
            speedBadgeImage.color = is2X
                ? new Color(0.15f, 0.75f, 0.35f, 1f)
                : new Color(0.12f, 0.14f, 0.2f, 0.9f);
        }

        if (txtSpeed != null)
        {
            txtSpeed.text = is2X ? "2x" : "1x";
        }

        if (animate && btnSpeed != null && gameObject.activeInHierarchy)
        {
            if (speedBounceCoroutine != null) StopCoroutine(speedBounceCoroutine);
            speedBounceCoroutine = StartCoroutine(AnimateSpeedButton(is2X ? 1.06f : 1f));
        }
        else if (btnSpeed != null)
        {
            btnSpeed.transform.localScale = Vector3.one * (is2X ? 1.06f : 1f);
        }
    }

    private System.Collections.IEnumerator AnimateSpeedButton(float targetScale)
    {
        if (btnSpeed == null) yield break;
        Transform tr = btnSpeed.transform;
        Vector3 startScale = tr.localScale;
        Vector3 popScale = Vector3.one * (targetScale * 1.18f);
        Vector3 finalScale = Vector3.one * targetScale;

        float d1 = 0.08f;
        float elapsed = 0f;
        while (elapsed < d1)
        {
            elapsed += Time.unscaledDeltaTime;
            tr.localScale = Vector3.Lerp(startScale, popScale, elapsed / d1);
            yield return null;
        }

        float d2 = 0.12f;
        elapsed = 0f;
        while (elapsed < d2)
        {
            elapsed += Time.unscaledDeltaTime;
            tr.localScale = Vector3.Lerp(popScale, finalScale, elapsed / d2);
            yield return null;
        }
        tr.localScale = finalScale;
    }

    private void OnDestroy()
    {
        RestoreSourceCameraMask();
        if (gameplayRenderCamera != null) Destroy(gameplayRenderCamera.gameObject);
        if (gameplayRenderTexture != null)
        {
            gameplayRenderTexture.Release();
            Destroy(gameplayRenderTexture);
        }
        if (LevelManager.Ins != null)
        {
            LevelManager.Ins.LevelLoaded -= OnLevelLoaded;
        }
    }

    public static UICanvasGameplay Show()
    {
        return UIManager.Ins.OpenUI<UICanvasGameplay>();
    }

    public static void Hide()
    {
        UIManager.Ins.CloseUI<UICanvasGameplay>();
    }
}
