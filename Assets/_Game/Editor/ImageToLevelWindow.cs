using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    public sealed class ImageToLevelWindow : EditorWindow
    {
        private Texture2D source;
        private PixelPalette palette;
        private int gridWidth = 24;
        private int gridHeight = 24;
        private float alphaThreshold = 0.1f;
        private float cellSize = 0.25f;
        private int trayCapacity = 5;
        private int maxColonySize = 30;
        private int randomSeed = 12345;
        private bool flipY;
        private Texture2D preview;
        private List<PixelData> generatedPixels;

        [MenuItem("Colony Flow/Image To Level")]
        public static void Open() => GetWindow<ImageToLevelWindow>("Image To Level");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Image → Pixel Level", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ảnh được lấy mẫu về grid, bỏ pixel trong suốt và ghép mỗi pixel vào màu gần nhất trong palette.", MessageType.Info);
            source = (Texture2D)EditorGUILayout.ObjectField("Source Image", source, typeof(Texture2D), false);
            palette = (PixelPalette)EditorGUILayout.ObjectField("Palette", palette, typeof(PixelPalette), false);
            gridWidth = EditorGUILayout.IntSlider("Width", gridWidth, 2, LevelData.MaxGridSize);
            gridHeight = EditorGUILayout.IntSlider("Height", gridHeight, 2, LevelData.MaxGridSize);
            alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);
            cellSize = EditorGUILayout.FloatField("Cell Size", Mathf.Max(0.01f, cellSize));
            trayCapacity = EditorGUILayout.IntSlider("Tray Capacity", trayCapacity, 1, 10);
            maxColonySize = EditorGUILayout.IntSlider("Max Colony Size", maxColonySize, 1, 100);
            randomSeed = EditorGUILayout.IntField("Shuffle Seed", randomSeed);
            flipY = EditorGUILayout.Toggle("Flip Image Y", flipY);

            using (new EditorGUI.DisabledScope(source == null || palette == null || palette.Count == 0))
            {
                if (GUILayout.Button("Generate Preview")) GeneratePreview();
                if (GUILayout.Button("Create Level Asset")) CreateLevelAsset();
            }

            if (preview != null)
            {
                GUILayout.Space(8f);
                float size = Mathf.Min(position.width - 24f, 420f);
                Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField($"Pixels: {generatedPixels?.Count ?? 0}");
            }
        }

        private void GeneratePreview()
        {
            Color32[] sourcePixels = ReadPixels(source, out int sourceWidth, out int sourceHeight);
            generatedPixels = new List<PixelData>(gridWidth * gridHeight);
            var previewPixels = new Color32[gridWidth * gridHeight];
            Color32 clear = new(0, 0, 0, 0);

            for (int y = 0; y < gridHeight; y++)
            {
                int sampleY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / gridHeight * sourceHeight), 0, sourceHeight - 1);
                if (flipY) sampleY = sourceHeight - 1 - sampleY;
                for (int x = 0; x < gridWidth; x++)
                {
                    int sampleX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / gridWidth * sourceWidth), 0, sourceWidth - 1);
                    Color32 sample = sourcePixels[sampleY * sourceWidth + sampleX];
                    int previewIndex = y * gridWidth + x;
                    if (sample.a / 255f < alphaThreshold)
                    {
                        previewPixels[previewIndex] = clear;
                        continue;
                    }
                    int colorIndex = palette.FindNearest(sample);
                    generatedPixels.Add(new PixelData(new Vector2Int(x, y), colorIndex));
                    previewPixels[previewIndex] = palette.GetColor(colorIndex);
                }
            }

            if (preview != null) DestroyImmediate(preview);
            preview = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            preview.SetPixels32(previewPixels);
            preview.Apply();
        }

        private void CreateLevelAsset()
        {
            GeneratePreview();
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string defaultName = string.IsNullOrEmpty(sourcePath) ? "Level_New" : $"Level_{Path.GetFileNameWithoutExtension(sourcePath)}";
            string path = EditorUtility.SaveFilePanelInProject("Save Colony Flow Level", defaultName, "asset", "Choose a location for the level asset.");
            if (string.IsNullOrEmpty(path)) return;

            var level = CreateInstance<LevelData>();
            level.width = gridWidth;
            level.height = gridHeight;
            level.cellSize = cellSize;
            level.trayCapacity = trayCapacity;
            level.palette = palette;
            level.pixels = generatedPixels;
            level.colonyTiles = GenerateColonies(generatedPixels);
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
        }

        private List<ColonyTileData> GenerateColonies(List<PixelData> pixels)
        {
            var colorCounts = new Dictionary<int, int>();
            foreach (PixelData pixel in pixels)
                colorCounts[pixel.colorIndex] = colorCounts.TryGetValue(pixel.colorIndex, out int count) ? count + 1 : 1;

            var colonies = new List<ColonyTileData>();
            int id = 0;
            foreach (KeyValuePair<int, int> pair in colorCounts)
            {
                int remaining = pair.Value;
                while (remaining > 0)
                {
                    int amount = Mathf.Min(maxColonySize, remaining);
                    colonies.Add(new ColonyTileData { id = id++, colorIndex = pair.Key, count = amount });
                    remaining -= amount;
                }
            }

            var random = new System.Random(randomSeed);
            for (int i = colonies.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (colonies[i], colonies[j]) = (colonies[j], colonies[i]);
            }
            const int columns = 4;
            for (int i = 0; i < colonies.Count; i++)
                colonies[i].boardPosition = new Vector2Int(i % columns - columns / 2, -(i / columns));
            return colonies;
        }

        private static Color32[] ReadPixels(Texture2D texture, out int width, out int height)
        {
            width = texture.width;
            height = texture.height;
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();
            Color32[] pixels = readable.GetPixels32();
            DestroyImmediate(readable);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return pixels;
        }

        private void OnDisable()
        {
            if (preview != null) DestroyImmediate(preview);
        }
    }
}
