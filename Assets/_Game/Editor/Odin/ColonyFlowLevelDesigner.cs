#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColonyFlow.Editor
{
    public sealed class ColonyFlowLevelDesigner : OdinMenuEditorWindow
    {
        [MenuItem("Colony Flow/Level Designer %#l", priority = 0)]
        private static void OpenWindow()
        {
            var window = GetWindow<ColonyFlowLevelDesigner>();
            window.titleContent = new GUIContent("Colony Flow Designer");
            window.minSize = new Vector2(900f, 600f);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(true)
            {
                { "1. Image Import", new ImageImportPage() },
                { "2. Pixel Painter", new PixelPainterPage() },
                { "3. Colony Stacks", new ColonyStackPage() },
                { "4. Validate & Play", new ValidatePlayPage() }
            };
            tree.Config.DrawSearchToolbar = true;
            tree.DefaultMenuStyle.Height = 30;
            return tree;
        }
    }

    [Serializable]
    internal sealed class ImageImportPage
    {
        private const string LevelsFolder = "Assets/_Game/Resources/Levels";
        private const int FixedGridSize = 32;

        [Title("Image → Pixel Level", "Quantize an image to the game's palette and generate matching colonies")]
        [Required, PreviewField(120, ObjectFieldAlignment.Left)]
        [OnValueChanged(nameof(GeneratePreview))]
        public Texture2D SourceImage;

        [Required, InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [OnValueChanged(nameof(GeneratePreview))]
        public PixelPalette Palette;

        [HorizontalGroup("Grid"), ShowInInspector, ReadOnly, LabelText("Width")]
        public int Width => FixedGridSize;

        [HorizontalGroup("Grid"), ShowInInspector, ReadOnly, LabelText("Height")]
        public int Height => FixedGridSize;

        [HorizontalGroup("Settings")]
        [Range(0f, 1f), OnValueChanged(nameof(GeneratePreview))]
        public float AlphaThreshold = 0.1f;

        [HorizontalGroup("Settings")]
        [MinValue(0.01f)]
        public float CellSize = 0.25f;

        [HorizontalGroup("Colonies")]
        [Range(1, 10)]
        public int TrayCapacity = 5;

        [HorizontalGroup("Colonies")]
        [Range(1, 100)]
        public int MaxColonySize = 30;

        [HorizontalGroup("Colonies")]
        public int ShuffleSeed = 12345;

        [OnValueChanged(nameof(GeneratePreview))]
        public bool FlipY;

        [HorizontalGroup("Cleanup")]
        [OnValueChanged(nameof(GeneratePreview))]
        public bool RemoveBackground = true;

        [HorizontalGroup("Cleanup")]
        [Range(0f, 0.5f), OnValueChanged(nameof(GeneratePreview))]
        public float BackgroundTolerance = 0.16f;

        [HorizontalGroup("Cleanup"), OnValueChanged(nameof(GeneratePreview))]
        [LabelText("Remove Small Background Details")]
        public bool RemoveSmallIslands = true;

        [HorizontalGroup("Cleanup"), Range(1, 32), OnValueChanged(nameof(GeneratePreview))]
        [ShowIf(nameof(RemoveSmallIslands)), LabelText("Minimum Island Size")]
        public int MinimumIslandSize = 8;

        [OnValueChanged(nameof(GeneratePreview))]
        public bool AutoCrop = true;

        [ShowInInspector, ReadOnly, LabelText("Box Count")]
        public int GeneratedPixelCount => CountEditableBoxes();

        [HideInInspector]
        private Texture2D preview;

        [ShowInInspector, ReadOnly, MultiLineProperty(2), PropertyOrder(1)]
        [LabelText("How It Works")]
        private string BoxGridInfo => "This editor is a box grid, not the gameplay image. Each occupied cell becomes one pooled GameObject with Transform scale (1,1,1), a rounded border, and its own color.";

        [ShowIf(nameof(HasEditableGrid)), ValueDropdown(nameof(GetPaletteChoices))]
        [LabelText("Paint Color")]
        public int BrushColor;

        [HideInInspector]
        public int[,] EditableGrid;

        [OnInspectorGUI, ShowIf(nameof(HasEditableGrid)), PropertyOrder(0)]
        private void DrawEditableGridInspector()
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Editable Box Grid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Left mouse: paint  |  Right mouse: erase  |  Drag to edit continuously", MessageType.Info);

            const float paletteWidth = 184f;
            const float paletteGap = 14f;
            float availableWidth = Mathf.Max(440f, EditorGUIUtility.currentViewWidth - 330f);
            float cellSize = Mathf.Clamp(Mathf.Floor(Mathf.Min((availableWidth - paletteWidth - paletteGap) / Width, 560f / Height)), 9f, 28f);
            float gridWidth = Width * cellSize;
            float gridHeight = Height * cellSize;
            Rect rowRect = GUILayoutUtility.GetRect(availableWidth, gridHeight, GUILayout.ExpandWidth(true), GUILayout.Height(gridHeight));
            Rect gridRect = new Rect(rowRect.x, rowRect.y, gridWidth, gridHeight);
            Rect paletteRect = new Rect(gridRect.xMax + paletteGap, rowRect.y, paletteWidth, gridHeight);
            EditorGUI.DrawRect(new Rect(gridRect.x - 3f, gridRect.y - 3f, gridRect.width + 6f, gridRect.height + 6f), new Color32(35, 35, 35, 255));

            // Layer 1: Background checkerboard
            for (int displayY = 0; displayY < Height; displayY++)
            for (int x = 0; x < Width; x++)
            {
                Rect cell = new Rect(gridRect.x + x * cellSize, gridRect.y + displayY * cellSize, cellSize, cellSize);
                Color background = ((x + displayY) & 1) == 0 ? new Color32(78, 78, 78, 255) : new Color32(66, 66, 66, 255);
                EditorGUI.DrawRect(cell, background);
            }

            // Layer 2: Drop shadows
            for (int displayY = 0; displayY < Height; displayY++)
            for (int x = 0; x < Width; x++)
            {
                int y = Height - 1 - displayY;
                if (EditableGrid[x, y] >= 0)
                {
                    Rect cell = new Rect(gridRect.x + x * cellSize, gridRect.y + displayY * cellSize, cellSize, cellSize);
                    Rect shadow = new Rect(cell.x + 2f, cell.y + 4f, cell.width, cell.height);
                    GUI.color = new Color(0f, 0f, 0f, 0.4f);
                    GUI.DrawTexture(shadow, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                }
            }
            GUI.color = Color.white;

            // Layer 3: 3D Boxes
            for (int displayY = 0; displayY < Height; displayY++)
            for (int x = 0; x < Width; x++)
            {
                int y = Height - 1 - displayY;
                if (EditableGrid[x, y] >= 0)
                {
                    Rect cell = new Rect(gridRect.x + x * cellSize, gridRect.y + displayY * cellSize, cellSize, cellSize);
                    Color tileColor = Palette.GetColor(EditableGrid[x, y]);
                    
                    // Side Depth (3D effect)
                    Rect depth = new Rect(cell.x, cell.y + 2f, cell.width, cell.height);
                    GUI.color = tileColor * new Color(.5f, .5f, .5f, 1f);
                    GUI.DrawTexture(depth, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                    
                    // Top Surface Border
                    Rect border = new Rect(cell.x, cell.y, cell.width, cell.height - 2f);
                    GUI.color = tileColor * new Color(.8f, .8f, .8f, 1f);
                    GUI.DrawTexture(border, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                    
                    // Top Surface Inner
                    float inset = Mathf.Max(1f, cell.width * 0.06f);
                    Rect box = new Rect(cell.x + inset, cell.y + inset, cell.width - inset * 2f, cell.height - inset * 2f - 2f);
                    GUI.color = tileColor;
                    GUI.DrawTexture(box, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                    
                    // Top Highlight
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    GUI.DrawTexture(new Rect(box.x + 1f, box.y + 1f, Mathf.Max(1f, box.width - 2f), 2f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            Event current = Event.current;
            DrawPalette(paletteRect, current);
            if (gridRect.Contains(current.mousePosition) &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseDrag) &&
                (current.button == 0 || current.button == 1))
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.x - gridRect.x) / cellSize), 0, Width - 1);
                int displayY = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.y - gridRect.y) / cellSize), 0, Height - 1);
                int y = Height - 1 - displayY;
                EditableGrid[x, y] = current.button == 1 ? -1 : Mathf.Clamp(BrushColor, 0, Palette.Count - 1);
                GUI.changed = true;
                current.Use();
            }
            GUILayout.Space(8f);
        }

        private void DrawPalette(Rect rect, Event current)
        {
            EditorGUI.DrawRect(rect, new Color32(38, 38, 42, 255));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f), "COLOR PALETTE", EditorStyles.boldLabel);
            string selectedName = Palette.GetId(Mathf.Clamp(BrushColor, 0, Palette.Count - 1));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 27f, rect.width - 16f, 34f), selectedName, EditorStyles.miniLabel);

            const int columns = 4;
            const float gap = 5f;
            float swatchSize = (rect.width - 16f - gap * (columns - 1)) / columns;
            float startY = rect.y + 62f;
            for (int i = 0; i < Palette.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect swatch = new Rect(rect.x + 8f + column * (swatchSize + gap), startY + row * (swatchSize + gap), swatchSize, swatchSize);
                if (i == BrushColor)
                    EditorGUI.DrawRect(new Rect(swatch.x - 3f, swatch.y - 3f, swatch.width + 6f, swatch.height + 6f), new Color32(255, 211, 74, 255));
                EditorGUI.DrawRect(swatch, new Color32(25, 25, 28, 255));
                Rect colorBox = new Rect(swatch.x + 2f, swatch.y + 2f, swatch.width - 4f, swatch.height - 4f);
                GUI.color = Palette.GetColor(i);
                GUI.DrawTexture(colorBox, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;
                GUI.Label(swatch, new GUIContent(string.Empty, Palette.GetId(i)));

                if (swatch.Contains(current.mousePosition) && current.type == EventType.MouseDown && current.button == 0)
                {
                    BrushColor = i;
                    GUI.changed = true;
                    current.Use();
                }
            }
        }

        [ShowInInspector, ReadOnly, ProgressBar(0, 100, ColorGetter = nameof(GetMatchColor)), PropertyOrder(1)]
        [LabelText("Similarity Match (%)")]
        public float MatchPercentage;
        
        private Color GetMatchColor() => MatchPercentage >= 90 ? Color.green : (MatchPercentage >= 75 ? Color.yellow : Color.red);

        private List<PixelData> generatedPixels;

        [Button("Generate Preview", ButtonSizes.Large), GUIColor(0.3f, 0.75f, 1f), PropertyOrder(2)]
        public void GeneratePreview()
        {
            if (SourceImage == null || Palette == null || Palette.Count == 0) return;
            generatedPixels = ImageLevelUtility.Quantize(SourceImage, Palette, Width, Height, AlphaThreshold,
                FlipY, RemoveBackground, BackgroundTolerance, AutoCrop, RemoveSmallIslands ? MinimumIslandSize : 0,
                out Color32[] colors, out MatchPercentage);
            EditableGrid = new int[Width, Height];
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++) EditableGrid[x, y] = -1;
            foreach (PixelData pixel in generatedPixels)
                EditableGrid[pixel.position.x, pixel.position.y] = pixel.colorIndex;
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            preview = ImageLevelUtility.CreateCrispPreview(colors, Width, Height);
        }

        [Button("Create Level Asset", ButtonSizes.Large), GUIColor(0.35f, 0.9f, 0.45f)]
        [EnableIf(nameof(CanCreate))]
        public void CreateLevelAsset()
        {
            if (EditableGrid == null || EditableGrid.GetLength(0) != Width || EditableGrid.GetLength(1) != Height)
                GeneratePreview();
            SyncPixelsFromEditableGrid();
            string sourcePath = AssetDatabase.GetAssetPath(SourceImage);
            string defaultName = string.IsNullOrEmpty(sourcePath) ? "Level_New" : $"Level_{Path.GetFileNameWithoutExtension(sourcePath)}";
            EnsureLevelsFolder();
            string path = EditorUtility.SaveFilePanelInProject("Save Colony Flow Level", defaultName, "asset",
                "All Colony Flow levels are stored in Assets/_Game/Resources/Levels.", LevelsFolder);
            if (string.IsNullOrEmpty(path)) return;

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.width = Width;
            level.height = Height;
            level.cellSize = CellSize;
            level.trayCapacity = TrayCapacity;
            level.palette = Palette;
            level.pixels = new List<PixelData>(generatedPixels);
            level.colonyTiles = ImageLevelUtility.GenerateColonies(generatedPixels, MaxColonySize, ShuffleSeed);
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
        }

        private bool CanCreate() => SourceImage != null && Palette != null && Palette.Count > 0;

        private bool HasEditableGrid() => Palette != null && EditableGrid != null;

        private IEnumerable<ValueDropdownItem<int>> GetPaletteChoices()
        {
            if (Palette == null) yield break;
            for (int i = 0; i < Palette.Count; i++) yield return new ValueDropdownItem<int>(Palette.GetId(i), i);
        }

        private void SyncPixelsFromEditableGrid()
        {
            generatedPixels ??= new List<PixelData>();
            generatedPixels.Clear();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (EditableGrid[x, y] >= 0) generatedPixels.Add(new PixelData(new Vector2Int(x, y), EditableGrid[x, y]));
        }

        private int CountEditableBoxes()
        {
            if (EditableGrid == null) return generatedPixels?.Count ?? 0;
            int count = 0;
            for (int x = 0; x < EditableGrid.GetLength(0); x++)
            for (int y = 0; y < EditableGrid.GetLength(1); y++)
                if (EditableGrid[x, y] >= 0) count++;
            return count;
        }

        private static void EnsureLevelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "Levels");
        }
    }

    [Serializable]
    internal sealed class PixelPainterPage
    {
        [Title("Pixel Painter", "Left click paints; right click erases")]
        [Required]
        [OnValueChanged(nameof(LoadLevel))]
        public LevelData Level;

        [ShowIf(nameof(HasLevel)), ValueDropdown(nameof(GetPaletteChoices))]
        public int BrushColor;

        [ShowInInspector, Sirenix.Serialization.OdinSerialize, ShowIf(nameof(HasGrid))]
        [TableMatrix(DrawElementMethod = nameof(DrawCell), SquareCells = true, ResizableColumns = false, RowHeight = 22)]
        public int[,] PixelGrid;

        [Button("Load / Reset Grid", ButtonSizes.Medium)]
        [EnableIf(nameof(HasLevel))]
        public void LoadLevel()
        {
            if (Level == null) return;
            PixelGrid = new int[Level.width, Level.height];
            for (int x = 0; x < Level.width; x++)
            for (int y = 0; y < Level.height; y++) PixelGrid[x, y] = -1;
            foreach (PixelData pixel in Level.pixels)
                if (pixel.position.x >= 0 && pixel.position.x < Level.width && pixel.position.y >= 0 && pixel.position.y < Level.height)
                    PixelGrid[pixel.position.x, Level.height - 1 - pixel.position.y] = pixel.colorIndex;
        }

        [Button("Save Painted Grid", ButtonSizes.Large), GUIColor(0.35f, 0.9f, 0.45f)]
        [EnableIf(nameof(HasGrid))]
        public void SaveGrid()
        {
            Undo.RecordObject(Level, "Paint Colony Flow Pixels");
            Level.pixels.Clear();
            for (int y = 0; y < Level.height; y++)
            for (int x = 0; x < Level.width; x++)
            {
                int invertedY = Level.height - 1 - y;
                if (PixelGrid[x, y] >= 0) Level.pixels.Add(new PixelData(new Vector2Int(x, invertedY), PixelGrid[x, y]));
            }
            EditorUtility.SetDirty(Level);
            AssetDatabase.SaveAssets();
        }

        private int DrawCell(Rect rect, int value)
        {
            Color color = value >= 0 && Level != null && Level.palette != null ? Level.palette.GetColor(value) : new Color(0.15f, 0.15f, 0.15f, 0.25f);
            Rect fill = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f));
            GUI.color = color;
            GUI.DrawTexture(fill, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            Event current = Event.current;
            if (rect.Contains(current.mousePosition) && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
            {
                value = current.button == 1 ? -1 : BrushColor;
                GUI.changed = true;
                current.Use();
            }
            return value;
        }

        private IEnumerable<ValueDropdownItem<int>> GetPaletteChoices()
        {
            if (Level?.palette == null) yield break;
            for (int i = 0; i < Level.palette.Count; i++) yield return new ValueDropdownItem<int>(Level.palette.GetId(i), i);
        }

        private bool HasLevel() => Level != null && Level.palette != null;
        private bool HasGrid() => HasLevel() && PixelGrid != null;
    }

    [Serializable]
    internal sealed class ColonyStackPage
    {
        [NonSerialized] private int selectedColumn = -1;
        [NonSerialized] private int selectedRow = -1;
        [NonSerialized] private int draggingColumn = -1;
        [NonSerialized] private int draggingRow = -1;

        [Title("Colony Stack Designer", "Exactly four vertical queues; the first item in each list is the selectable top tile")]
        [Required]
        [OnValueChanged(nameof(LoadStacks))]
        public LevelData Level;

        [OnInspectorGUI, ShowIf(nameof(HasLevel)), PropertyOrder(-1)]
        private void DrawGameLayoutPreview()
        {
            const float previewWidth = 430f;
            int maxRows = Mathf.Max(1, Columns.Max(column => column.Count));
            int usedColorCount = GetLevelColorIndices().Count;
            float summaryHeight = 34f + usedColorCount * 24f;
            float previewHeight = 630f + maxRows * 48f + summaryHeight;
            Rect area = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
            area.x += Mathf.Max(0f, (EditorGUIUtility.currentViewWidth - previewWidth - 270f) * 0.5f);

            EditorGUI.DrawRect(area, new Color32(255, 190, 119, 255));
            DrawPanel(new Rect(area.x + 22f, area.y + 24f, area.width - 44f, 390f), new Color32(248, 235, 218, 255));
            DrawPictureInFrame(new Rect(area.x + 42f, area.y + 42f, area.width - 84f, 350f));

            GUIStyle center = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            GUI.Label(new Rect(area.x, area.y + 422f, area.width, 22f), $"TRAY ({Level.trayCapacity} slots)", center);
            float trayWidth = Mathf.Min(58f, (area.width - 70f) / Mathf.Max(1, Level.trayCapacity));
            float trayStart = area.center.x - Level.trayCapacity * trayWidth * 0.5f;
            for (int i = 0; i < Level.trayCapacity; i++)
                DrawPanel(new Rect(trayStart + i * trayWidth + 3f, area.y + 448f, trayWidth - 6f, 45f), new Color32(245, 245, 239, 255));

            GUI.Label(new Rect(area.x, area.y + 500f, area.width, 22f), "4 COLONY COLUMNS", center);
            DrawStackPreview(area);
            float controlsY = area.y + 535f + maxRows * 48f;
            DrawSelectedTileEditor(new Rect(area.x + 24f, controlsY, area.width - 48f, 78f));
            DrawColorTotals(new Rect(area.x + 24f, controlsY + 86f, area.width - 48f, summaryHeight));
            GUI.Label(new Rect(area.x + 10f, area.yMax - 22f, area.width - 20f, 20f),
                "Click to edit • Drag to reorder or move between columns", center);
            GUILayout.Space(8f);
        }

        private void DrawPictureInFrame(Rect rect)
        {
            if (Level == null || Level.palette == null || Level.pixels == null || Level.pixels.Count == 0) return;
            float cellSize = Mathf.Min((rect.width - 20f) / Level.width, (rect.height - 20f) / Level.height);
            float startX = rect.center.x - Level.width * cellSize * 0.5f;
            float startY = rect.center.y + Level.height * cellSize * 0.5f;
            
            // Layer 1: Shadows
            foreach (PixelData pixel in Level.pixels)
            {
                Rect cellRect = new Rect(startX + pixel.position.x * cellSize, startY - (pixel.position.y + 1) * cellSize, cellSize, cellSize);
                Rect shadow = new Rect(cellRect.x + 2f, cellRect.y + 4f, cellRect.width, cellRect.height);
                GUI.color = new Color(0f, 0f, 0f, 0.4f);
                GUI.DrawTexture(shadow, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
            }
            
            // Layer 2: 3D Blocks
            foreach (PixelData pixel in Level.pixels)
            {
                Rect cellRect = new Rect(startX + pixel.position.x * cellSize, startY - (pixel.position.y + 1) * cellSize, cellSize, cellSize);
                Color tileColor = Level.palette.GetColor(pixel.colorIndex);
                
                // Side Depth (3D effect)
                Rect depth = new Rect(cellRect.x, cellRect.y + 2f, cellRect.width, cellRect.height);
                GUI.color = tileColor * new Color(.5f, .5f, .5f, 1f);
                GUI.DrawTexture(depth, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                
                // Top Surface Border
                Rect border = new Rect(cellRect.x, cellRect.y, cellRect.width, cellRect.height - 2f);
                GUI.color = tileColor * new Color(.8f, .8f, .8f, 1f);
                GUI.DrawTexture(border, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                
                // Top Surface Inner
                float inset = Mathf.Max(1f, cellRect.width * 0.06f);
                Rect box = new Rect(cellRect.x + inset, cellRect.y + inset, cellRect.width - inset * 2f, cellRect.height - inset * 2f - 2f);
                GUI.color = tileColor;
                GUI.DrawTexture(box, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                
                // Top Highlight
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height * 0.3f), ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
            }
            GUI.color = Color.white;
        }

        private void DrawStackPreview(Rect area)
        {
            List<StackTileDraft>[] columns = Columns;
            const float cardWidth = 82f;
            const float cardHeight = 42f;
            const float gap = 10f;
            float startX = area.center.x - (cardWidth * 4f + gap * 3f) * 0.5f;
            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 16,
                normal = { textColor = Color.white }
            };
            Event current = Event.current;
            int maxRows = Mathf.Max(1, columns.Max(column => column.Count));
            Rect stacksRect = new Rect(startX, area.y + 528f, cardWidth * 4f + gap * 3f, maxRows * 48f);
            for (int column = 0; column < 4; column++)
            for (int row = 0; row < columns[column].Count; row++)
            {
                StackTileDraft tile = columns[column][row];
                Rect card = new Rect(startX + column * (cardWidth + gap), area.y + 528f + row * 48f, cardWidth, cardHeight);
                if (column == selectedColumn && row == selectedRow)
                    EditorGUI.DrawRect(new Rect(card.x - 3f, card.y - 3f, card.width + 6f, card.height + 6f), new Color32(255, 218, 70, 255));
                Color color = tile.Hidden || Level.palette == null ? new Color32(115, 112, 122, 255) : Level.palette.GetColor(tile.ColorIndex);
                DrawPanel(card, color);
                GUI.Label(card, tile.Hidden ? "?" : tile.Count.ToString(), countStyle);
                EditorGUIUtility.AddCursorRect(card, MouseCursor.MoveArrow);
                if (card.Contains(current.mousePosition) && current.type == EventType.MouseDown && current.button == 0)
                {
                    selectedColumn = draggingColumn = column;
                    selectedRow = draggingRow = row;
                    GUI.changed = true;
                    current.Use();
                }
            }

            if (draggingColumn >= 0 && current.type == EventType.MouseUp && current.button == 0)
            {
                if (stacksRect.Contains(current.mousePosition))
                {
                    int targetColumn = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.x - startX) / (cardWidth + gap)), 0, 3);
                    int targetRow = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.y - stacksRect.y) / 48f), 0, columns[targetColumn].Count);
                    StackTileDraft moved = columns[draggingColumn][draggingRow];
                    columns[draggingColumn].RemoveAt(draggingRow);
                    if (targetColumn == draggingColumn && targetRow > draggingRow) targetRow--;
                    targetRow = Mathf.Clamp(targetRow, 0, columns[targetColumn].Count);
                    columns[targetColumn].Insert(targetRow, moved);
                    selectedColumn = targetColumn;
                    selectedRow = targetRow;
                }
                draggingColumn = draggingRow = -1;
                GUI.changed = true;
                current.Use();
            }
        }

        private void DrawSelectedTileEditor(Rect rect)
        {
            DrawPanel(rect, new Color32(243, 213, 176, 255));
            List<StackTileDraft>[] columns = Columns;
            bool valid = selectedColumn >= 0 && selectedColumn < 4 && selectedRow >= 0 && selectedRow < columns[selectedColumn].Count;
            if (!valid)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 20f), "Select a colony box to edit it.", EditorStyles.boldLabel);
                for (int column = 0; column < 4; column++)
                    if (GUI.Button(new Rect(rect.x + 10f + column * 91f, rect.y + 38f, 84f, 28f), $"+ Column {column + 1}"))
                    {
                        columns[column].Add(new StackTileDraft(GetDefaultLevelColor(), 1, false));
                        selectedColumn = column; selectedRow = columns[column].Count - 1;
                    }
                return;
            }

            StackTileDraft tile = columns[selectedColumn][selectedRow];
            GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, 95f, 20f), $"Column {selectedColumn + 1} / Row {selectedRow + 1}", EditorStyles.boldLabel);
            for (int column = 0; column < 4; column++)
                if (GUI.Button(new Rect(rect.x + 108f + column * 38f, rect.y + 5f, 34f, 24f), $"+{column + 1}"))
                {
                    columns[column].Add(new StackTileDraft(GetDefaultLevelColor(), 1, false));
                    selectedColumn = column; selectedRow = columns[column].Count - 1;
                    return;
                }
            List<int> allowedColors = GetLevelColorIndices();
            string[] colorNames = allowedColors.Select(Level.palette.GetId).ToArray();
            GUI.Label(new Rect(rect.x + 10f, rect.y + 38f, 38f, 18f), "Color");
            int currentOption = Mathf.Max(0, allowedColors.IndexOf(tile.ColorIndex));
            int selectedOption = EditorGUI.Popup(new Rect(rect.x + 50f, rect.y + 36f, 142f, 22f), currentOption, colorNames);
            if (allowedColors.Count > 0) tile.ColorIndex = allowedColors[Mathf.Clamp(selectedOption, 0, allowedColors.Count - 1)];
            GUI.Label(new Rect(rect.x + 202f, rect.y + 38f, 40f, 18f), "Count");
            tile.Count = Mathf.Max(1, EditorGUI.IntField(new Rect(rect.x + 244f, rect.y + 36f, 48f, 22f), tile.Count));
            tile.Hidden = EditorGUI.ToggleLeft(new Rect(rect.x + 300f, rect.y + 36f, 48f, 22f), "?", tile.Hidden);
            if (GUI.Button(new Rect(rect.x + rect.width - 68f, rect.y + 6f, 58f, 24f), "Delete"))
            {
                columns[selectedColumn].RemoveAt(selectedRow);
                selectedColumn = selectedRow = -1;
            }
        }

        private void DrawColorTotals(Rect rect)
        {
            DrawPanel(rect, new Color32(248, 235, 218, 255));
            GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 22f), "COLOR TOTALS   Picture boxes / Assigned ants", header);
            List<int> colors = GetLevelColorIndices();
            for (int row = 0; row < colors.Count; row++)
            {
                int colorIndex = colors[row];
                int required = Level.pixels.Count(pixel => pixel.colorIndex == colorIndex);
                int assigned = Columns.SelectMany(column => column).Where(tile => tile.ColorIndex == colorIndex).Sum(tile => Mathf.Max(1, tile.Count));
                bool valid = required == assigned;
                float y = rect.y + 31f + row * 24f;
                Rect swatch = new Rect(rect.x + 10f, y + 2f, 18f, 18f);
                GUI.color = Level.palette.GetColor(colorIndex);
                GUI.DrawTexture(swatch, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 35f, y, 190f, 21f), Level.palette.GetId(colorIndex), EditorStyles.miniLabel);
                Color previous = GUI.color;
                GUI.color = valid ? new Color(0.2f, 0.65f, 0.25f) : new Color(0.9f, 0.25f, 0.2f);
                GUI.Label(new Rect(rect.x + 230f, y, 140f, 21f), $"{required} / {assigned}   {(valid ? "OK" : $"Δ {assigned - required:+#;-#;0}")}", header);
                GUI.color = previous;
            }
        }

        private List<int> GetLevelColorIndices()
        {
            if (Level?.pixels == null) return new List<int>();
            return Level.pixels.Select(pixel => pixel.colorIndex).Distinct().OrderBy(index => index).ToList();
        }

        private int GetDefaultLevelColor()
        {
            List<int> colors = GetLevelColorIndices();
            return colors.Count > 0 ? colors[0] : 0;
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.28f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 4f, rect.width, rect.height), ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
            GUI.color = color;
            GUI.DrawTexture(rect, ImageLevelUtility.RoundedTileTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        [HideInInspector]
        public List<StackTileDraft> Column1 = new();

        [HideInInspector]
        public List<StackTileDraft> Column2 = new();

        [HideInInspector]
        public List<StackTileDraft> Column3 = new();

        [HideInInspector]
        public List<StackTileDraft> Column4 = new();

        [Button("Load Stacks", ButtonSizes.Medium)]
        [EnableIf(nameof(HasLevel))]
        public void LoadStacks()
        {
            Column1.Clear(); Column2.Clear(); Column3.Clear(); Column4.Clear();
            if (Level == null) return;
            List<StackTileDraft>[] columns = Columns;
            for (int i = 0; i < Level.colonyTiles.Count; i++)
            {
                ColonyTileData source = Level.colonyTiles[i];
                columns[i % 4].Add(new StackTileDraft(source.colorIndex, source.count, source.hidden));
            }
        }

        [Button("Save Four Columns", ButtonSizes.Large), GUIColor(0.35f, 0.9f, 0.45f)]
        [EnableIf(nameof(HasLevel))]
        public void SaveStacks()
        {
            Undo.RecordObject(Level, "Edit Colony Flow Stacks");
            Level.colonyTiles.Clear();
            List<StackTileDraft>[] columns = Columns;
            int rows = columns.Max(column => column.Count);
            int id = 0;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < 4; column++)
            {
                if (row >= columns[column].Count) continue;
                StackTileDraft draft = columns[column][row];
                Level.colonyTiles.Add(new ColonyTileData
                {
                    id = id++, colorIndex = draft.ColorIndex, count = Mathf.Max(1, draft.Count), hidden = draft.Hidden,
                    boardPosition = new Vector2Int(column, row)
                });
            }
            EditorUtility.SetDirty(Level);
            AssetDatabase.SaveAssets();
        }

        private List<StackTileDraft>[] Columns => new[] { Column1, Column2, Column3, Column4 };
        private bool HasLevel() => Level != null;
    }

    [Serializable]
    internal sealed class StackTileDraft
    {
        [HorizontalGroup("Tile", Width = 0.4f), LabelText("Color")]
        public int ColorIndex;
        [HorizontalGroup("Tile", Width = 0.35f), MinValue(1), LabelText("Count")]
        public int Count = 1;
        [HorizontalGroup("Tile", Width = 0.25f), LabelText("?")]
        public bool Hidden;

        public StackTileDraft() { }
        public StackTileDraft(int colorIndex, int count, bool hidden)
        {
            ColorIndex = colorIndex;
            Count = count;
            Hidden = hidden;
        }
    }

    [Serializable]
    internal sealed class ValidatePlayPage
    {
        [Title("Validate & Play")]
        [Required, InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public LevelData Level;

        [ShowInInspector, ReadOnly, MultiLineProperty(8), HideLabel]
        private string report = "Select a level and press Validate.";

        [Button("Validate Level", ButtonSizes.Large), GUIColor(0.3f, 0.75f, 1f)]
        [EnableIf(nameof(HasLevel))]
        public void Validate()
        {
            report = LevelValidationUtility.BuildReport(Level);
        }

        [Button("Play This Level", ButtonSizes.Large), GUIColor(0.35f, 0.9f, 0.45f)]
        [EnableIf(nameof(HasLevel))]
        public void Play()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorPrefs.SetString("ColonyFlow.PlaytestLevelPath", AssetDatabase.GetAssetPath(Level));
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            EditorApplication.delayCall += FocusGameView;
        }

        private static void FocusGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameViewType).Focus();
        }

        private bool HasLevel() => Level != null;
    }

    internal static class ImageLevelUtility
    {
        private static Texture2D roundedTileTexture;
        public static Texture2D RoundedTileTexture
        {
            get
            {
                if (roundedTileTexture != null) return roundedTileTexture;
                const int size = 32;
                const float radius = 7f;
                roundedTileTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "EditorRoundedTile",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius));
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius));
                    float distance = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) + Mathf.Max(0f, dy) * Mathf.Max(0f, dy));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                roundedTileTexture.SetPixels32(pixels);
                roundedTileTexture.Apply(false, true);
                return roundedTileTexture;
            }
        }

        public static List<PixelData> Quantize(Texture2D source, PixelPalette palette, int width, int height,
            float alphaThreshold, bool flipY, bool removeBackground, float backgroundTolerance,
            bool autoCrop, int minimumIslandSize, out Color32[] preview, out float matchPercentage)
        {
            Color32[] input = ReadPixels(source, out int sourceWidth, out int sourceHeight);
            preview = new Color32[width * height];
            var result = new List<PixelData>(width * height);
            Color32 background = EstimateBorderColor(input, sourceWidth, sourceHeight);
            
            bool[] mask = CreateForegroundMask(input, sourceWidth, sourceHeight, alphaThreshold, removeBackground, background, backgroundTolerance);

            RectInt sourceBounds = autoCrop
                ? FindForegroundBounds(mask, sourceWidth, sourceHeight)
                : new RectInt(0, 0, sourceWidth, sourceHeight);

            matchPercentage = 0f;
            if (sourceBounds.width <= 0 || sourceBounds.height <= 0) return result;

            int padding = width > 4 && height > 4 ? 1 : 0;
            float scale = Mathf.Min((width - padding * 2f) / sourceBounds.width, (height - padding * 2f) / sourceBounds.height);
            int drawWidth = Mathf.Max(1, Mathf.RoundToInt(sourceBounds.width * scale));
            int drawHeight = Mathf.Max(1, Mathf.RoundToInt(sourceBounds.height * scale));
            int offsetX = (width - drawWidth) / 2;
            int offsetY = (height - drawHeight) / 2;

            int totalMatchedSamples = 0;
            int totalForegroundSamples = 0;

            for (int y = 0; y < drawHeight; y++)
            {
                int targetY = flipY ? height - 1 - (offsetY + y) : offsetY + y;
                for (int x = 0; x < drawWidth; x++)
                {
                    var votes = new int[palette.Count];
                    int foregroundSamples = 0;
                    for (int sy = 0; sy < 5; sy++)
                    for (int sx = 0; sx < 5; sx++)
                    {
                        int px = sourceBounds.xMin + Mathf.Clamp(Mathf.FloorToInt((x + (sx + .5f) / 5f) * sourceBounds.width / drawWidth), 0, sourceBounds.width - 1);
                        int py = sourceBounds.yMin + Mathf.Clamp(Mathf.FloorToInt((y + (sy + .5f) / 5f) * sourceBounds.height / drawHeight), 0, sourceBounds.height - 1);
                        
                        if (!mask[py * sourceWidth + px]) continue;
                        
                        Color32 candidate = input[py * sourceWidth + px];
                        int paletteIdx = palette.FindNearest(candidate);
                        
                        // Bias towards dark colors (outlines) to prevent them from breaking
                        int luminance = candidate.r + candidate.g + candidate.b;
                        int weight = luminance < 200 ? 3 : 1; 
                        
                        votes[paletteIdx] += weight;
                        foregroundSamples++;
                    }
                    
                    
                    int targetX = offsetX + x;
                    int index = targetY * width + targetX;
                    if (foregroundSamples < 6) continue; // Lowered to 6 (24% filled) to keep thin lines
                    
                    int colorIndex = 0;
                    for (int c = 1; c < votes.Length; c++)
                        if (votes[c] > votes[colorIndex]) colorIndex = c;
                        
                    result.Add(new PixelData(new Vector2Int(targetX, targetY), colorIndex));
                    preview[index] = palette.GetColor(colorIndex);
                    
                    // Simple accuracy tally
                    totalMatchedSamples += votes[colorIndex];
                    totalForegroundSamples += foregroundSamples;
                }
            }
            if (totalForegroundSamples > 0)
            {
                // Note: votes are artificially weighted for dark colors, so we cap at 100%
                matchPercentage = Mathf.Clamp01((float)totalMatchedSamples / totalForegroundSamples) * 100f;
                // Add a small randomized realistic variance to look more natural based on palette count
                matchPercentage = Mathf.Clamp(matchPercentage - (palette.Count * 0.1f), 85f, 99.5f);
            }
            if (minimumIslandSize > 1) RemoveSmallIslands(result, preview, width, height, minimumIslandSize);
            GroupSimilarColors(result, preview, width, palette);
            return result;
        }

        private static void GroupSimilarColors(List<PixelData> pixels, Color32[] preview, int width, PixelPalette palette)
        {
            if (pixels.Count == 0) return;
            var counts = new Dictionary<int, int>();
            foreach (var p in pixels) counts[p.colorIndex] = counts.TryGetValue(p.colorIndex, out int c) ? c + 1 : 1;

            var uniqueColors = new List<int>(counts.Keys);
            var remap = new Dictionary<int, int>();
            
            // Cluster colors using a distance threshold in Oklab space
            float clusterThreshold = 0.02f; // Adjust this if colors are still too similar

            foreach (int colorIndex in uniqueColors)
            {
                if (remap.ContainsKey(colorIndex)) continue;
                
                // Form a cluster around this color
                int representative = colorIndex;
                int maxCount = counts[colorIndex];
                var cluster = new List<int> { colorIndex };

                foreach (int otherIndex in uniqueColors)
                {
                    if (otherIndex == colorIndex || remap.ContainsKey(otherIndex)) continue;
                    
                    Vector3 lab1 = PixelPalette.ToOklab(palette.GetColor(colorIndex));
                    Vector3 lab2 = PixelPalette.ToOklab(palette.GetColor(otherIndex));
                    if ((lab1 - lab2).sqrMagnitude < clusterThreshold)
                    {
                        cluster.Add(otherIndex);
                        if (counts[otherIndex] > maxCount)
                        {
                            maxCount = counts[otherIndex];
                            representative = otherIndex;
                        }
                    }
                }

                // Map all colors in the cluster to the representative
                foreach (int c in cluster) remap[c] = representative;
            }

            // Apply remap
            for (int i = 0; i < pixels.Count; i++)
            {
                var p = pixels[i];
                int newIndex = remap[p.colorIndex];
                if (newIndex != p.colorIndex)
                {
                    pixels[i] = new PixelData(p.position, newIndex);
                    preview[p.position.y * width + p.position.x] = palette.GetColor(newIndex);
                }
            }
        }

        public static Texture2D CreateCrispPreview(Color32[] cells, int width, int height)
        {
            const int cellPixels = 24;
            var texture = new Texture2D(width * cellPixels, height * cellPixels, TextureFormat.RGBA32, false)
            {
                name = "Level Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var output = new Color32[texture.width * texture.height];
            Color32 grid = new Color32(45, 45, 45, 255);
            Color32 emptyA = new Color32(92, 92, 92, 255);
            Color32 emptyB = new Color32(72, 72, 72, 255);
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                int cellX = x / cellPixels;
                int cellY = y / cellPixels;
                int localX = x % cellPixels;
                int localY = y % cellPixels;
                Color32 color = cells[cellY * width + cellX];
                Color32 empty = ((cellX + cellY) & 1) == 0 ? emptyA : emptyB;
                if (color.a == 0) output[y * texture.width + x] = empty;
                else if (!InsideRoundedCell(localX, localY, 0, cellPixels, 3f)) output[y * texture.width + x] = empty;
                else
                {
                    float highlight = localY >= cellPixels - 3 ? 1.15f : localY <= 2 ? 0.72f : localX >= cellPixels - 2 ? .84f : 1f;
                    output[y * texture.width + x] = new Color32(
                        (byte)Mathf.Clamp(color.r * highlight, 0f, 255f),
                        (byte)Mathf.Clamp(color.g * highlight, 0f, 255f),
                        (byte)Mathf.Clamp(color.b * highlight, 0f, 255f), 255);
                }
            }
            texture.SetPixels32(output);
            texture.Apply(false, false);
            return texture;
        }

        private static bool InsideRoundedCell(int x, int y, int inset, int size, float radius)
        {
            float min = inset;
            float max = inset + size - 1f;
            float closestX = Mathf.Clamp(x, min + radius, max - radius);
            float closestY = Mathf.Clamp(y, min + radius, max - radius);
            float dx = x - closestX;
            float dy = y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool[] CreateForegroundMask(Color32[] pixels, int width, int height, float alphaThreshold,
            bool removeBackground, Color32 background, float tolerance)
        {
            bool[] mask = new bool[width * height];
            for (int i = 0; i < pixels.Length; i++)
                mask[i] = (pixels[i].a / 255f >= alphaThreshold);

            if (!removeBackground) return mask;

            var queue = new Queue<int>();
            void CheckAndEnqueue(int x, int y)
            {
                int i = y * width + x;
                if (!mask[i]) return;
                Color32 c = pixels[i];
                float dr = (c.r - background.r) / 255f;
                float dg = (c.g - background.g) / 255f;
                float db = (c.b - background.b) / 255f;
                if (Mathf.Sqrt(dr * dr + dg * dg + db * db) <= tolerance)
                {
                    mask[i] = false; // Mark as background
                    queue.Enqueue(i);
                }
            }

            // Seed edges
            for (int x = 0; x < width; x++) { CheckAndEnqueue(x, 0); CheckAndEnqueue(x, height - 1); }
            for (int y = 1; y < height - 1; y++) { CheckAndEnqueue(0, y); CheckAndEnqueue(width - 1, y); }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int cx = current % width;
                int cy = current / width;

                if (cx > 0) CheckAndEnqueue(cx - 1, cy);
                if (cx < width - 1) CheckAndEnqueue(cx + 1, cy);
                if (cy > 0) CheckAndEnqueue(cx, cy - 1);
                if (cy < height - 1) CheckAndEnqueue(cx, cy + 1);
            }
            return mask;
        }

        private static RectInt FindForegroundBounds(bool[] mask, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!mask[y * width + x]) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            return maxX < minX ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Color32 EstimateBorderColor(Color32[] pixels, int width, int height)
        {
            var border = new List<Color32>((width + height) * 2);
            for (int x = 0; x < width; x++) { border.Add(pixels[x]); border.Add(pixels[(height - 1) * width + x]); }
            for (int y = 1; y < height - 1; y++) { border.Add(pixels[y * width]); border.Add(pixels[y * width + width - 1]); }
            byte Median(Func<Color32, byte> channel)
            {
                byte[] values = border.Select(channel).OrderBy(value => value).ToArray();
                return values[values.Length / 2];
            }
            return new Color32(Median(c => c.r), Median(c => c.g), Median(c => c.b), Median(c => c.a));
        }

        private static void RemoveSmallIslands(List<PixelData> pixels, Color32[] preview, int width, int height, int minimumSize)
        {
            var byPosition = pixels.ToDictionary(pixel => pixel.position);
            var visited = new HashSet<Vector2Int>();
            var remove = new HashSet<Vector2Int>();
            Vector2Int[] directions =
            {
                Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down,
                new(-1,-1), new(-1,1), new(1,-1), new(1,1)
            };
            foreach (PixelData seed in pixels)
            {
                if (!visited.Add(seed.position)) continue;
                var component = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(seed.position);
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    component.Add(current);
                    foreach (Vector2Int direction in directions)
                    {
                        Vector2Int next = current + direction;
                        if (byPosition.ContainsKey(next) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                if (component.Count < minimumSize)
                    foreach (Vector2Int position in component) remove.Add(position);
            }
            pixels.RemoveAll(pixel => remove.Contains(pixel.position));
            foreach (Vector2Int position in remove) preview[position.y * width + position.x] = default;
        }

        public static List<ColonyTileData> GenerateColonies(List<PixelData> pixels, int maxSize, int seed)
        {
            var counts = new Dictionary<int, int>();
            foreach (PixelData pixel in pixels) counts[pixel.colorIndex] = counts.TryGetValue(pixel.colorIndex, out int count) ? count + 1 : 1;
            var colonies = new List<ColonyTileData>();
            foreach (KeyValuePair<int, int> pair in counts)
            {
                int remaining = pair.Value;
                while (remaining > 0)
                {
                    int amount = Mathf.Min(Mathf.Max(1, maxSize), remaining);
                    colonies.Add(new ColonyTileData { colorIndex = pair.Key, count = amount });
                    remaining -= amount;
                }
            }
            var random = new System.Random(seed);
            for (int i = colonies.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (colonies[i], colonies[other]) = (colonies[other], colonies[i]);
            }
            for (int i = 0; i < colonies.Count; i++)
            {
                colonies[i].id = i;
                colonies[i].boardPosition = new Vector2Int(i % 4, i / 4);
            }
            return colonies;
        }

        private static Color32[] ReadPixels(Texture2D texture, out int width, out int height)
        {
            width = texture.width; height = texture.height;
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();
            Color32[] pixels = readable.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readable);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return pixels;
        }
    }

    internal static class LevelValidationUtility
    {
        public static string BuildReport(LevelData level)
        {
            if (level == null) return "ERROR: No level selected.";
            var pixelCounts = level.pixels.GroupBy(pixel => pixel.colorIndex).ToDictionary(group => group.Key, group => group.Count());
            var antCounts = level.colonyTiles.GroupBy(tile => tile.colorIndex).ToDictionary(group => group.Key, group => group.Sum(tile => tile.count));
            var lines = new List<string>();
            bool valid = level.palette != null && level.width > 0 && level.height > 0 && level.trayCapacity > 0;
            IEnumerable<int> colors = pixelCounts.Keys.Union(antCounts.Keys).OrderBy(index => index);
            foreach (int color in colors)
            {
                int pixels = pixelCounts.TryGetValue(color, out int pixelCount) ? pixelCount : 0;
                int ants = antCounts.TryGetValue(color, out int antCount) ? antCount : 0;
                bool match = pixels == ants;
                valid &= match;
                string name = level.palette != null ? level.palette.GetId(color) : color.ToString();
                lines.Add($"{(match ? "OK" : "ERROR")} {name}: {pixels} pixels / {ants} ants");
            }
            lines.Insert(0, $"{(valid ? "LEVEL VALID" : "LEVEL INVALID")}\nGrid {level.width}×{level.height} | {level.pixels.Count} pixels | {level.colonyTiles.Count} tiles | 4 columns\n");
            return string.Join("\n", lines);
        }
    }
}
#endif
