using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColonyFlow
{
    public sealed class ColonyTileBoard : MonoBehaviour
    {
        [SerializeField] private ColonyTileView tilePrefab;
        [SerializeField, Range(1, 4)] private int maxColumns = 4;
        [SerializeField] private Vector2 tileSpacing = new(1.30f, 1.03f);
        [SerializeField] private Vector2 tileSize = new(1.06f, 0.97f);
        [SerializeField, Min(1)] private int visibleRows = 3;
        private readonly Dictionary<int, ColonyTile> tiles = new();
        private readonly Dictionary<int, ColonyTileView> viewsByCollider = new();
        private readonly List<List<ColonyTile>> columns = new();
        private LevelData level;
        private Camera gameplayCamera;

        public event Func<ColonyTile, bool> TileSelected;
        public bool HasAvailableTile
        {
            get
            {
                foreach (ColonyTile tile in tiles.Values) if (tile.State == TileState.Available) return true;
                return false;
            }
        }

        public int ColumnCount => columns.Count;
        public int MaxRowCount
        {
            get
            {
                int max = 0;
                for (int i = 0; i < columns.Count; i++)
                    if (columns[i].Count > max) max = columns[i].Count;
                return max;
            }
        }

        public ColonyTile GetTileAtBottomLeftCoordinate(int col, int row)
        {
            if (col < 0 || col >= columns.Count) return null;
            List<ColonyTile> column = columns[col];
            int indexInColumn = column.Count - 1 - row;
            if (indexInColumn < 0 || indexInColumn >= column.Count) return null;
            return column[indexInColumn];
        }

        public bool TryGetBoxCoordinate(ColonyTile tile, out Vector2Int coord)
        {
            coord = new Vector2Int(-1, -1);
            for (int col = 0; col < columns.Count; col++)
            {
                int idx = columns[col].IndexOf(tile);
                if (idx >= 0)
                {
                    int row = columns[col].Count - 1 - idx;
                    coord = new Vector2Int(col, row);
                    return true;
                }
            }
            return false;
        }

        public void ClearBoard()
        {
            foreach (ColonyTile tile in tiles.Values)
            {
                if (tile.View != null)
                {
                    tile.View.DetachColony();
                    SimplePool.Despawn(tile.View);
                    tile.View = null;
                }
            }
            tiles.Clear();
            viewsByCollider.Clear();
            columns.Clear();
        }

        private void OnDestroy()
        {
            ClearBoard();
        }

        public void Build(LevelData data, Camera camera)
        {
            ClearBoard();
            level = data;
            gameplayCamera = camera != null ? camera : Camera.main;
            EnsureRuntimePrefab();
            int columnCount = Mathf.Min(maxColumns, Mathf.Max(1, data.colonyTiles.Count));
            for (int i = 0; i < columnCount; i++) columns.Add(new List<ColonyTile>());

            for (int i = 0; i < data.colonyTiles.Count; i++)
            {
                ColonyTileData item = data.colonyTiles[i];
                var tile = new ColonyTile(item);
                tiles[item.id] = tile;
                int columnIndex = i % columnCount;
                columns[columnIndex].Add(tile);
            }
            RefreshColumns(true);
        }

        public bool TrySelect(int id)
        {
            if (!tiles.TryGetValue(id, out ColonyTile tile) || tile.State != TileState.Available) return false;
            Delegate[] handlers = TileSelected?.GetInvocationList();
            if (handlers == null || handlers.Length == 0) return false;
            foreach (Func<ColonyTile, bool> handler in handlers)
                if (!handler(tile)) return false;

            if (tile.View != null && tile.View.CachedCollider != null)
            {
                viewsByCollider.Remove(tile.View.CachedCollider.GetInstanceID());
            }

            tile.MoveToTray();
            RemoveFromColumn(tile);
            RefreshColumns(false);
            return true;
        }

        private void Update()
        {
            if (!TryGetPointerDown(out Vector2 screenPosition)) return;
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
                if (gameplayCamera == null) return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Check 1: 3D Ray into 2D colliders
            Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
            RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray);
            if (hits2D != null && hits2D.Length > 0)
            {
                for (int i = 0; i < hits2D.Length; i++)
                {
                    Collider2D col = hits2D[i].collider;
                    if (col == null) continue;
                    if (!viewsByCollider.TryGetValue(col.GetInstanceID(), out ColonyTileView view))
                    {
                        view = col.GetComponentInParent<ColonyTileView>();
                    }
                    if (view != null && view.TryClick())
                    {
                        return;
                    }
                }
            }

            // Check 2: 2D Screen-to-World Overlap
            Vector3 clickPos = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z));
            Vector3 world = gameplayCamera.ScreenToWorldPoint(clickPos);
            Collider2D[] hits = Physics2D.OverlapPointAll((Vector2)world);
            if (hits != null && hits.Length > 0)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D hit = hits[i];
                    if (hit == null) continue;
                    if (!viewsByCollider.TryGetValue(hit.GetInstanceID(), out ColonyTileView view))
                    {
                        view = hit.GetComponentInParent<ColonyTileView>();
                    }
                    if (view != null && view.TryClick())
                    {
                        return;
                    }
                }
            }
        }

        private static bool TryGetPointerDown(out Vector2 position)
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                position = Pointer.current.position.ReadValue();
                return true;
            }
            position = default;
            return false;
        }

        public void MarkCompleted(ColonyTile tile)
        {
            tile?.Complete();
        }

        private void RemoveFromColumn(ColonyTile tile)
        {
            foreach (List<ColonyTile> column in columns)
                if (column.Remove(tile)) return;
        }

        private void RefreshColumns(bool initial = false)
        {
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<ColonyTile> column = columns[columnIndex];
                for (int rowIndex = 0; rowIndex < column.Count; rowIndex++)
                {
                    ColonyTile tile = column[rowIndex];
                    bool isVisible = rowIndex < visibleRows;
                    bool isFront = rowIndex == 0;

                    // Only the front box of each column is selectable
                    tile.SetColumnAvailability(isFront);

                    if (isVisible)
                    {
                        if (tile.View == null)
                        {
                            Vector3 spawnPos = initial 
                                ? GetWorldPosition(columnIndex, rowIndex) 
                                : GetWorldPosition(columnIndex, rowIndex + 1);

                            ColonyTileView view = SimplePool.Spawn(tilePrefab, spawnPos, Quaternion.identity, transform);
                            Color tileColor = (level != null && level.palette != null) 
                                ? level.palette.GetColor(tile.ColorIndex) 
                                : Color.white;
                            view.Bind(tile, this, tileColor);
                            if (view.CachedCollider != null)
                            {
                                viewsByCollider[view.CachedCollider.GetInstanceID()] = view;
                            }
                        }

                        float emphasis = isFront ? 1.04f : 1f;
                        tile.View.TF.localScale = new Vector3(tileSize.x * emphasis, tileSize.y * emphasis, 1f);
                        tile.View.SetEmphasis(isFront);
                        tile.View.SetBoardVisible(true);
                        tile.View.MoveTo(GetWorldPosition(columnIndex, rowIndex));
                    }
                    else
                    {
                        // Any tile beyond visibleRows must NOT have an active view
                        if (tile.View != null)
                        {
                            if (tile.View.CachedCollider != null)
                            {
                                viewsByCollider.Remove(tile.View.CachedCollider.GetInstanceID());
                            }
                            tile.View.DetachColony();
                            SimplePool.Despawn(tile.View);
                            tile.View = null;
                        }
                    }
                }
            }
        }

        private Vector3 GetWorldPosition(int columnIndex, int rowIndex)
        {
            float centeredColumn = columnIndex - (columns.Count - 1) * 0.5f;
            return transform.TransformPoint(new Vector3(centeredColumn * tileSpacing.x, -rowIndex * tileSpacing.y, -rowIndex * 0.01f));
        }

        private void EnsureRuntimePrefab()
        {
            if (tilePrefab != null) return;
            tilePrefab = Resources.Load<ColonyTileView>("Prefabs/ColonyTile3D");
            if (tilePrefab != null) return;
            var go = new GameObject("ColonyTile_RuntimePrefab");
            go.SetActive(false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.TrayBox;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            tilePrefab = go.AddComponent<ColonyTileView>();
            tilePrefab.Configure(renderer, collider);
            tilePrefab.poolType = PoolType.ColonyTile;
            go.transform.SetParent(transform, false);
        }
    }
}
