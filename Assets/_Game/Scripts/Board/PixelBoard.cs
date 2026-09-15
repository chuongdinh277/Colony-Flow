using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class PixelBoard : MonoBehaviour
    {
        [SerializeField] private PixelCellView pixelPrefab;
        [SerializeField] private Transform pixelRoot;
        private readonly Dictionary<Vector2Int, PixelCell> cells = new();
        private readonly List<PixelCell> liveCells = new();
        private readonly Dictionary<int, List<PixelCell>> exposedByColor = new();
        private bool exposedCacheDirty = true;
        private LevelData level;

        public event Action<PixelCell> PixelDestroyed;
        public int RemainingPixels => liveCells.Count;
        public int Width => level != null ? level.width : 0;
        public int Height => level != null ? level.height : 0;
        public float CellSize => level != null ? level.cellSize : 1f;
        public float GameplayPlaneZ => transform.position.z;
        public int Revision { get; private set; }
        public IReadOnlyList<PixelCell> LiveCells => liveCells;

        public void Build(LevelData data)
        {
            Clear();
            level = data;
            if (level == null || level.palette == null) throw new InvalidOperationException("Level and palette are required.");
            if (level.width > LevelData.MaxGridSize || level.height > LevelData.MaxGridSize)
                throw new InvalidOperationException($"Pixel grid cannot exceed {LevelData.MaxGridSize}x{LevelData.MaxGridSize}.");
            if (pixelRoot == null) pixelRoot = transform;
            EnsureRuntimePrefab();

            foreach (PixelData item in level.pixels)
            {
                if (cells.ContainsKey(item.position)) continue;
                var cell = new PixelCell(item.position, item.colorIndex);
                cells.Add(item.position, cell);
                liveCells.Add(cell);
                PixelCellView view = SimplePool.Spawn(pixelPrefab, GridToWorld(item.position), Quaternion.identity, pixelRoot);
                view.Bind(cell, level.palette.GetColor(item.colorIndex), level.cellSize);
                cell.View = view;
            }
            exposedCacheDirty = true;
            Revision++;
        }

        public Vector3 GridToWorld(Vector2Int cell)
        {
            Vector2 offset = new((Width - 1) * 0.5f, (Height - 1) * 0.5f);
            return ProjectToGameplayPlane(transform.TransformPoint(
                new Vector3((cell.x - offset.x) * CellSize, (cell.y - offset.y) * CellSize, 0f)));
        }

        public Vector3 ProjectToGameplayPlane(Vector3 world) =>
            new(world.x, world.y, GameplayPlaneZ);

        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector2 offset = new((Width - 1) * 0.5f, (Height - 1) * 0.5f);
            return new Vector2Int(Mathf.RoundToInt(local.x / CellSize + offset.x), Mathf.RoundToInt(local.y / CellSize + offset.y));
        }

        public bool TryGetCell(Vector2Int position, out PixelCell cell) => cells.TryGetValue(position, out cell);
        public bool IsWalkable(Vector2Int position) => !cells.TryGetValue(position, out PixelCell cell) || cell.IsDestroyed;

        public bool IsExposed(PixelCell cell)
        {
            if (cell == null || cell.IsDestroyed) return false;
            foreach (Vector2Int direction in GridDirections.Four)
                if (IsWalkable(cell.Position + direction)) return true;
            return false;
        }

        public List<PixelCell> GetAvailableTargets(int colorIndex)
        {
            RebuildExposedCacheIfNeeded();
            var result = new List<PixelCell>();
            if (!exposedByColor.TryGetValue(colorIndex, out List<PixelCell> exposed)) return result;
            foreach (PixelCell cell in exposed)
                if (!cell.IsDestroyed && !cell.IsReserved) result.Add(cell);
            return result;
        }

        public bool TryCollectPixel(PixelCell cell, AntAgent ant, out PixelCellView collectedView)
        {
            collectedView = null;
            if (cell == null || cell.IsDestroyed ||
                (cell.IsReserved && !ReferenceEquals(cell.ReservedBy, ant))) return false;
            collectedView = cell.Collect();
            liveCells.Remove(cell);
            exposedCacheDirty = true;
            Revision++;
            PixelDestroyed?.Invoke(cell);
            return true;
        }

        public void Clear()
        {
            foreach (PixelCell cell in cells.Values)
            {
                if (cell.View != null)
                {
                    SimplePool.Despawn(cell.View);
                    cell.View = null;
                }
            }
            cells.Clear();
            liveCells.Clear();
            exposedByColor.Clear();
            exposedCacheDirty = true;
            Revision++;
        }

        private void RebuildExposedCacheIfNeeded()
        {
            if (!exposedCacheDirty) return;
            exposedByColor.Clear();
            foreach (PixelCell cell in liveCells)
            {
                if (!IsExposed(cell)) continue;
                if (!exposedByColor.TryGetValue(cell.ColorIndex, out List<PixelCell> list))
                {
                    list = new List<PixelCell>();
                    exposedByColor.Add(cell.ColorIndex, list);
                }
                list.Add(cell);
            }
            exposedCacheDirty = false;
        }

        private void EnsureRuntimePrefab()
        {
            if (pixelPrefab != null) return;
            pixelPrefab = Resources.Load<PixelCellView>("Prefabs/PixelBox");
            if (pixelPrefab != null) return;
            var go = new GameObject("PixelCell_RuntimePrefab");
            go.SetActive(false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.RoundedSquare;
            pixelPrefab = go.AddComponent<PixelCellView>();
            pixelPrefab.Configure(renderer);
            pixelPrefab.poolType = PoolType.Pixel;
            go.transform.SetParent(transform, false);
        }
    }
}
