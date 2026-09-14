using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [CustomEditor(typeof(LevelData))]
    public sealed class LevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(6f);
            if (GUILayout.Button("Validate Level")) Validate((LevelData)target);
        }

        private static void Validate(LevelData level)
        {
            var pixels = new Dictionary<int, int>();
            var colonies = new Dictionary<int, int>();
            var positions = new HashSet<Vector2Int>();
            var ids = new HashSet<int>();
            bool valid = level.palette != null;

            foreach (PixelData pixel in level.pixels)
            {
                if (level.palette == null || pixel.colorIndex < 0 || pixel.colorIndex >= level.palette.Count)
                {
                    Debug.LogError($"Pixel {pixel.position} has invalid palette index {pixel.colorIndex}.", level);
                    valid = false;
                }
                if (!positions.Add(pixel.position))
                {
                    Debug.LogError($"Duplicate pixel at {pixel.position}", level);
                    valid = false;
                }
                pixels[pixel.colorIndex] = pixels.TryGetValue(pixel.colorIndex, out int count) ? count + 1 : 1;
            }

            foreach (ColonyTileData colony in level.colonyTiles)
            {
                if (level.palette == null || colony.colorIndex < 0 || colony.colorIndex >= level.palette.Count || colony.count <= 0)
                {
                    Debug.LogError($"Colony {colony.id} has invalid color/count.", level);
                    valid = false;
                }
                if (!ids.Add(colony.id))
                {
                    Debug.LogError($"Duplicate colony id {colony.id}", level);
                    valid = false;
                }
                colonies[colony.colorIndex] = colonies.TryGetValue(colony.colorIndex, out int count) ? count + colony.count : colony.count;
            }

            foreach (KeyValuePair<int, int> pair in pixels)
            {
                int colonyCount = colonies.TryGetValue(pair.Key, out int count) ? count : 0;
                if (colonyCount != pair.Value)
                {
                    Debug.LogError($"Color {pair.Key}: {pair.Value} pixels but {colonyCount} ants.", level);
                    valid = false;
                }
            }

            foreach (KeyValuePair<int, int> pair in colonies)
            {
                if (!pixels.ContainsKey(pair.Key))
                {
                    Debug.LogError($"Color {pair.Key}: {pair.Value} ants but no pixels.", level);
                    valid = false;
                }
            }

            if (valid) Debug.Log($"Level valid: {level.pixels.Count} pixels, {level.colonyTiles.Count} colonies.", level);
        }
    }
}
