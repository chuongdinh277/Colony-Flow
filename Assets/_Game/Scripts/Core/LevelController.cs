using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class LevelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelData level;
        [Header("Systems")]
        [SerializeField] private PixelBoard pixelBoard;
        [SerializeField] private BoardFrameView boardFrame;
        [SerializeField] private BorderPath borderPath;
        [SerializeField] private AntRouteService routeService;
        [SerializeField] private FPSManager fpsManager;
        [SerializeField] private ColonyTray tray;
        [SerializeField] private ColonyTileBoard tileBoard;
        [Header("Ants")]
        [SerializeField] private AntAgent antPrefab;
        [SerializeField] private Transform antRoot;
        [SerializeField] private AntManager antManager;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private GameplayCameraRig cameraRig;
        [SerializeField] private GameplayBackdrop backdrop;
        [SerializeField] private GameplayLightingRig lightingRig;
        [SerializeField, Min(1)] private int maxConcurrentAntsPerColony = 5;
        [SerializeField, Min(0.05f)] private float antSpawnInterval = 0.52f;
        [SerializeField, Min(0f)] private float deadlockDelay = 0.75f;

        private readonly Dictionary<ColonyController, ColonyTile> colonyTiles = new();
        private float deadlockTimer;

        public event Action Victory;
        public event Action Failed;
        public LevelData Level => level;
        public PixelBoard Board => pixelBoard;
        public ColonyTray Tray => tray;
        public bool IsRunning { get; private set; }
        public BoardGrid2D BoardGrid => boardGrid2D;
        private BoardGrid2D boardGrid2D;

        public void Initialize(LevelData data)
        {
            Shutdown();
            level = data;
            EnsureSystems();
            EnsureRuntimeAntPrefab();
            antManager.Initialize(antPrefab, antRoot);
            SimplePool.CollectAll();
            colonyTiles.Clear();
            pixelBoard.Build(level);
            
            if (boardFrame != null)
            {
                boardFrame.gameObject.SetActive(false);
            }
            
            borderPath.Build(pixelBoard);
            routeService.Initialize(pixelBoard, borderPath);
            fpsManager.Initialize(routeService);
            tray.Build(level.trayCapacity);
            
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (gameplayCamera != null)
            {
                if (cameraRig == null) cameraRig = gameplayCamera.GetComponent<GameplayCameraRig>();
                if (cameraRig == null) cameraRig = gameplayCamera.gameObject.AddComponent<GameplayCameraRig>();
                cameraRig.Apply(gameplayCamera);
                if (backdrop == null) backdrop = gameplayCamera.GetComponent<GameplayBackdrop>();
                if (backdrop == null) backdrop = gameplayCamera.gameObject.AddComponent<GameplayBackdrop>();
                backdrop.Initialize(gameplayCamera);

                Rect pictureViewport = new Rect(0.13f, 0.50f, 0.74f, 0.35f);
                UICanvasGameplay uiGameplay = FindFirstObjectByType<UICanvasGameplay>(FindObjectsInactive.Include);
                if (uiGameplay != null && uiGameplay.TryGetPictureViewport(out Rect vp)) pictureViewport = vp;
                FitBoardToPictureViewport(pictureViewport);
            }
            
            tileBoard.Build(level, gameplayCamera);
            tileBoard.TileSelected -= OnTileSelected;
            tileBoard.TileSelected += OnTileSelected;
            tray.ColonyCompleted -= OnColonyCompleted;
            tray.ColonyCompleted += OnColonyCompleted;
            deadlockTimer = 0f;
            IsRunning = true;

            // Generate 2D grid matrix
            boardGrid2D = new BoardGrid2D();
            boardGrid2D.Build(pixelBoard, borderPath, tray, tileBoard, level);
        }

        [ContextMenu("Debug Board Grid 2D")]
        public void DebugBoardGrid2D()
        {
            if (boardGrid2D == null)
            {
                boardGrid2D = new BoardGrid2D();
                boardGrid2D.Build(pixelBoard, borderPath, tray, tileBoard, level);
            }
            boardGrid2D.LogDebug();
        }

        public void Shutdown()
        {
            IsRunning = false;
            if (tileBoard != null)
            {
                tileBoard.TileSelected -= OnTileSelected;
                tileBoard.ClearBoard();
            }
            if (tray != null) tray.ColonyCompleted -= OnColonyCompleted;
            if (antManager != null) antManager.CancelAll();
            if (fpsManager != null) fpsManager.Clear();
            if (pixelBoard != null) pixelBoard.Clear();
            foreach (var kvp in colonyTiles)
            {
                if (kvp.Value?.View != null)
                {
                    kvp.Value.View.DetachColony();
                    SimplePool.Despawn(kvp.Value.View);
                    kvp.Value.View = null;
                }
            }
            colonyTiles.Clear();
            deadlockTimer = 0f;
        }

        private void Update()
        {
            if (!IsRunning) return;
            
            if (tray != null && tray.Slots != null)
            {
                for (int i = 0; i < tray.Slots.Count; i++)
                {
                    ColonySlot slot = tray.Slots[i];
                    if (slot != null && !slot.IsEmpty && slot.Colony != null)
                    {
                        if (colonyTiles.TryGetValue(slot.Colony, out ColonyTile tile) && tile != null && tile.View != null)
                        {
                            tile.View.MoveTo(slot.transform.position);
                        }
                        slot.Colony.UpdateAntSpawnPosition(slot.AntSpawnWorldPosition);
                    }
                }
            }

            foreach (var kvp in colonyTiles)
            {
                if (!kvp.Key.IsReady && kvp.Value.View != null)
                {
                    if (Vector3.Distance(kvp.Value.View.transform.position, kvp.Value.View.TargetPosition) < 0.01f)
                    {
                        kvp.Key.IsReady = true;
                    }
                }
            }
            
            tray.Tick();
            if (pixelBoard.RemainingPixels == 0 && antManager.ActiveCount == 0)
            {
                IsRunning = false;
                Victory?.Invoke();
                return;
            }

            if (!tray.IsFull || tray.HasProgressingColony())
            {
                deadlockTimer = 0f;
                return;
            }

            deadlockTimer += Time.deltaTime;
            if (deadlockTimer < deadlockDelay) return;
            IsRunning = false;
            Failed?.Invoke();
        }

        public ColonyTile GetColonyTile(ColonyController colony)
        {
            return colony != null && colonyTiles.TryGetValue(colony, out ColonyTile tile) ? tile : null;
        }

        private bool OnTileSelected(ColonyTile tile)
        {
            if (!IsRunning || tray.IsFull) return false;
            bool added = tray.TryAdd((spawnPosition, onCompleted) => new ColonyController(
                tile.ColorIndex,
                tile.Count,
                pixelBoard,
                routeService,
                antManager,
                fpsManager,
                level.palette,
                maxConcurrentAntsPerColony,
                antSpawnInterval,
                spawnPosition,
                onCompleted), out ColonyController colony, out ColonySlot slot);
            if (added)
            {
                colony.IsReady = tile.View == null;
                colonyTiles[colony] = tile;
                if (tile.View != null)
                {
                    tile.View.AttachColony(colony);
                    // Make the box visually DOJump into the tray slot instead of linear move
                    tile.View.JumpTo(slot.transform.position);
                }
            }
            return added;
        }

        private void OnColonyCompleted(ColonyController colony)
        {
            if (!colonyTiles.Remove(colony, out ColonyTile tile)) return;
            if (tile != null && tile.View != null)
            {
                tile.View.DetachColony();
                SimplePool.Despawn(tile.View);
                tile.View = null;
            }
            tileBoard.MarkCompleted(tile);
            SoundManager.Ins?.PlayGameFx(GameFxID.BoxCollected);
        }

        public void RefreshTraySlotPositions()
        {
            if (tray == null || tray.Slots == null) return;
            for (int i = 0; i < tray.Slots.Count; i++)
            {
                ColonySlot slot = tray.Slots[i];
                if (slot != null && !slot.IsEmpty && slot.Colony != null)
                {
                    if (colonyTiles.TryGetValue(slot.Colony, out ColonyTile tile) && tile != null && tile.View != null)
                    {
                        tile.View.MoveTo(slot.transform.position);
                    }
                    slot.Colony.UpdateAntSpawnPosition(slot.AntSpawnWorldPosition);
                }
            }
        }

        private void EnsureSystems()
        {
            pixelBoard = GetOrAdd(pixelBoard, "PixelBoard");
            borderPath = GetOrAdd(borderPath, "BorderPath");
            boardFrame = GetOrAdd(boardFrame, "BoardFrameView");
            if (LevelManager.Ins != null)
            {
                boardFrame.SetCustomSprites(LevelManager.Ins.CustomFrameSprite, LevelManager.Ins.CustomPanelSprite);
            }
            routeService = GetOrAdd(routeService, "AntRouteService");
            fpsManager = GetOrAdd(fpsManager, "FPSManager");
            tray = GetOrAdd(tray, "ColonyTray");
            tileBoard = GetOrAdd(tileBoard, "ColonyTileBoard");
            antManager = GetOrAdd(antManager, "AntManager");
            lightingRig = GetOrAdd(lightingRig, "GameplayLighting");
            lightingRig.Initialize();
            // One authored 1080x1920 composition: picture, nest/tray and choice grid
            // must stay in three separate vertical bands on every aspect ratio.
            pixelBoard.transform.localPosition = new Vector3(0f, 3.05f, 0f);
            pixelBoard.transform.localScale = Vector3.one * 1.34f;
            tray.transform.localPosition = new Vector3(0f, -3.10f, 0f);
            tray.transform.localScale = Vector3.one;
            tileBoard.transform.localPosition = new Vector3(0f, -4.72f, 0f);
            tileBoard.transform.localScale = Vector3.one * 1.14f;
            if (antRoot == null)
            {
                antRoot = new GameObject("Ants").transform;
                antRoot.SetParent(transform, false);
            }
        }

        private T GetOrAdd<T>(T current, string childName) where T : Component
        {
            if (current != null) return current;
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        public void FitBoardToPictureViewport(Rect viewport)
        {
            if (level == null || pixelBoard == null || borderPath == null || gameplayCamera == null || cameraRig == null) return;
            
            float orthoSize = gameplayCamera.orthographicSize;
            float tiltCos = UnityEngine.Mathf.Cos(cameraRig.TiltAngle * UnityEngine.Mathf.Deg2Rad);
            
            // Screen dimensions in camera world units
            float availableWidth = viewport.width * 2f * orthoSize * gameplayCamera.aspect;
            float availableHeight = viewport.height * 2f * orthoSize;
            
            // Screen aspect ratio of the UI frame (NO tiltCos needed for screen projection)
            float screenAspect = availableWidth / availableHeight;
            
            int baseH = Mathf.Max(1, borderPath.BaseHorizontalMarginCells);
            int baseV = Mathf.Max(1, borderPath.BaseVerticalMarginCells);
            
            int minW = (level.width - 1) + 2 * baseH;
            int minH = (level.height - 1) + 2 * baseV;
            
            int finalHMargin = baseH;
            int finalVMargin = baseV;
            
            float minGridAspect = (float)minW / minH;
            if (minGridAspect > screenAspect)
            {
                // Grid is wider than screen frame -> expand vertical margin so border matches frame height
                int desiredH = Mathf.CeilToInt(minW / screenAspect);
                finalVMargin = Mathf.Max(baseV, Mathf.CeilToInt((desiredH - (level.height - 1)) / 2f));
            }
            else if (minGridAspect < screenAspect)
            {
                // Grid is taller than screen frame -> expand horizontal margin so border matches frame width
                int desiredW = Mathf.CeilToInt(minH * screenAspect);
                finalHMargin = Mathf.Max(baseH, Mathf.CeilToInt((desiredW - (level.width - 1)) / 2f));
            }
            
            borderPath.SetDynamicMargins(finalHMargin, finalVMargin);
            
            float localWidth = (level.width - 1 + finalHMargin * 2) * level.cellSize;
            float localHeight = (level.height - 1 + finalVMargin * 2) * level.cellSize;
            if (localWidth <= 0f || localHeight <= 0f) return;

            // Direct screen fit: projected size local * fitScale fits into available screen dimensions
            float fitScale = UnityEngine.Mathf.Min(availableWidth / localWidth, availableHeight / localHeight);
            float centerX = (viewport.center.x - 0.5f) * 2f * orthoSize * gameplayCamera.aspect;
            float centerY = (viewport.center.y - 0.5f) * 2f * orthoSize;

            // tiltCos is applied to scale.y and localPosition.y to compensate for the 25 degree camera tilt
            UnityEngine.Vector3 scale = new(fitScale, fitScale / tiltCos, fitScale);
            pixelBoard.transform.localPosition = new UnityEngine.Vector3(centerX, centerY / tiltCos, 0f);
            pixelBoard.transform.localScale = scale;
            if (antRoot != null) antRoot.localScale = scale;

            // Hybrid world bounds:
            //   X axis → viewport edges (wLeft/wRight): ants walk at the physical left/right frame borders.
            //   Y axis → board.GridToWorld corners: SAME coordinate system as the inner A* path
            //            (which always calls board.GridToWorld). Using viewport Y here creates a
            //            mismatch that makes ants appear to jump vertically at border→inner transitions
            //            (visually: "going straight up" instead of entering the picture perpendicularly).
            float wLeft  = (viewport.xMin - 0.5f) * 2f * orthoSize * gameplayCamera.aspect;
            float wRight = (viewport.xMax - 0.5f) * 2f * orthoSize * gameplayCamera.aspect;
            UnityEngine.Vector2Int bMin = new(-finalHMargin, -finalVMargin);
            UnityEngine.Vector2Int bMax = new(pixelBoard.Width - 1 + finalHMargin, pixelBoard.Height - 1 + finalVMargin);
            UnityEngine.Vector3 boardBL = pixelBoard.GridToWorld(bMin);
            UnityEngine.Vector3 boardTR = pixelBoard.GridToWorld(bMax);
            borderPath.SetWorldBounds(new UnityEngine.Vector2(wLeft,  boardBL.y),
                                      new UnityEngine.Vector2(wRight, boardTR.y));
        }
        private void EnsureRuntimeAntPrefab()
        {
            if (antPrefab != null) return;
            antPrefab = Resources.Load<AntAgent>("Prefabs/AntChibi");
            if (antPrefab != null) return;
            var go = new GameObject("Ant_RuntimePrefab");
            go.SetActive(false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.Square;
            antPrefab = go.AddComponent<AntAgent>();
            antPrefab.Configure(renderer);
            antPrefab.poolType = PoolType.Ant;
            go.transform.SetParent(transform, false);
        }

        private void OnDestroy() => Shutdown();
    }
}
