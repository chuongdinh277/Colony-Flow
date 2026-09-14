using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(menuName = "Colony Flow/Level", fileName = "Level_001")]
    public sealed class LevelData : ScriptableObject
    {
        public const int MaxGridSize = 64;

        [Range(1, MaxGridSize)] public int width = 16;
        [Range(1, MaxGridSize)] public int height = 16;
        [Min(1)] public int trayCapacity = 5;
        [Min(0.01f)] public float cellSize = 0.25f;
        public PixelPalette palette;
        public List<PixelData> pixels = new();
        public List<ColonyTileData> colonyTiles = new();

        public bool TryGetPixel(Vector2Int position, out PixelData data)
        {
            for (int i = 0; i < pixels.Count; i++)
            {
                if (pixels[i].position == position)
                {
                    data = pixels[i];
                    return true;
                }
            }
            data = default;
            return false;
        }

        private void OnValidate()
        {
            width = Mathf.Clamp(width, 1, MaxGridSize);
            height = Mathf.Clamp(height, 1, MaxGridSize);
        }
    }

    [Serializable]
    public struct PixelData
    {
        public Vector2Int position;
        public int colorIndex;

        public PixelData(Vector2Int position, int colorIndex)
        {
            this.position = position;
            this.colorIndex = colorIndex;
        }
    }

    [Serializable]
    public sealed class ColonyTileData
    {
        public int id;
        public Vector2Int boardPosition;
        public int layer;
        public int colorIndex;
        [Min(1)] public int count = 1;
        public bool hidden;
        public List<int> coveredBy = new();
    }
}
