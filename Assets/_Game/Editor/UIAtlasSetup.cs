#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ColonyFlow.UI;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    public static class UIAtlasSetup
    {
        // Source sheets are Editor-only; runtime UI references only exported individual PNG files.
        private const string IconPath = "Assets/_Game/Editor/UIAtlasSources/UI_Icons_Source.png";
        private const string PanelPath = "Assets/_Game/Editor/UIAtlasSources/UI_Panels_Source.png";
        private const string LibraryPath = "Assets/_Game/Resources/UI/UIVisualLibrary.asset";
        private const string SessionKey = "ColonyFlow.GeneratedUIArt.V7.BoosterBases";
        private const string IconFolder = "Assets/_Game/Texture/UI/Icons";
        private const string PanelFolder = "Assets/_Game/Texture/UI/Panels";
        private static readonly string[] IconNames = {
            "pause","speed","settings","plus","avatar",
            "boosterAdd","boosterShuffle","boosterVacuum","boosterFan","hint",
            "ranking","shop","tasks","rewards","cube",
            "heart","retry","home","sound","music",
            "vibration","theme","close","loadingTip","padlock"
        };
        private static readonly string[] PanelNames = {
            "playButton","blueButton","speedButton",
            "boosterButton","popupPanel","popupHeader",
            "cyanButton","redButton","toggles"
        };

        static UIAtlasSetup() => EditorApplication.delayCall += SetupIfNeeded;

        [MenuItem("Colony Flow/UI/Import Generated Art")]
        public static void Setup()
        {
            RemoveBakedCheckerboard(IconPath);
            RemoveBakedCheckerboard(PanelPath);
            ExportIndividualSprites(IconPath, IconFolder, 5, 5, IconNames, false);
            ExportIndividualSprites(PanelPath, PanelFolder, 3, 3, PanelNames, true);
            RemoveBakedCheckerboard(IconFolder + "/boosterAdd.png");
            RemoveBakedCheckerboard(IconFolder + "/boosterShuffle.png");
            RemoveBakedCheckerboard(IconFolder + "/boosterVacuum.png");
            RemoveBakedCheckerboard(IconFolder + "/boosterFan.png");
            TrimTransparent(PanelFolder + "/boosterBar.png", 12);
            ConfigureSingleSprite(PanelFolder + "/boosterBar.png", true, 60f);
            CreateLibrary();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("Imported generated UI art and updated UIVisualLibrary. Hand-authored prefabs were not changed.");
        }

        private static void TrimTransparent(string assetPath, int padding)
        {
            string absolute = Path.GetFullPath(assetPath); if (!File.Exists(absolute)) return;
            Texture2D source = new Texture2D(2,2,TextureFormat.RGBA32,false); if(!source.LoadImage(File.ReadAllBytes(absolute))) { UnityEngine.Object.DestroyImmediate(source); return; }
            Color32[] pixels = source.GetPixels32(); int minX=source.width,minY=source.height,maxX=-1,maxY=-1;
            for(int y=0;y<source.height;y++) for(int x=0;x<source.width;x++) if(pixels[y*source.width+x].a>8){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            if(maxX<minX){UnityEngine.Object.DestroyImmediate(source);return;}
            minX=Mathf.Max(0,minX-padding);minY=Mathf.Max(0,minY-padding);maxX=Mathf.Min(source.width-1,maxX+padding);maxY=Mathf.Min(source.height-1,maxY+padding);
            int width=maxX-minX+1,height=maxY-minY+1; Texture2D crop=new Texture2D(width,height,TextureFormat.RGBA32,false);
            crop.SetPixels(source.GetPixels(minX,minY,width,height));crop.Apply(false,false);File.WriteAllBytes(absolute,crop.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(crop);UnityEngine.Object.DestroyImmediate(source);AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureSingleSprite(string assetPath, bool sliced, float border)
        {
            TextureImporter importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;if(importer==null)return;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.spriteBorder=sliced?new Vector4(border,border,border,border):Vector4.zero;importer.SaveAndReimport();
        }

        // Image generators sometimes bake their transparency preview checkerboard into RGB.
        // Flood-filling only neutral bright pixels connected to an outer edge preserves the
        // enclosed cream/white artwork while producing genuine PNG alpha around every item.
        private static void RemoveBakedCheckerboard(string assetPath)
        {
            string absolute = Path.GetFullPath(assetPath);
            if (!File.Exists(absolute)) return;
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(absolute))) { UnityEngine.Object.DestroyImmediate(source); return; }
            Color32[] pixels = source.GetPixels32();
            int width = source.width, height = source.height;
            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();
            Action<int> enqueue = index => { if (!visited[index] && IsChecker(pixels[index])) { visited[index] = true; queue.Enqueue(index); } };
            for (int x = 0; x < width; x++) { enqueue(x); enqueue((height - 1) * width + x); }
            for (int y = 0; y < height; y++) { enqueue(y * width); enqueue(y * width + width - 1); }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                Color32 color = pixels[index]; color.a = 0; pixels[index] = color;
                int x = index % width, y = index / width;
                if (x > 0) enqueue(index - 1); if (x + 1 < width) enqueue(index + 1);
                if (y > 0) enqueue(index - width); if (y + 1 < height) enqueue(index + width);
            }
            source.SetPixels32(pixels); source.Apply(false, false);
            File.WriteAllBytes(absolute, source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static bool IsChecker(Color32 c)
        {
            if (c.a == 0) return false;
            int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max - min <= 12 && max >= 175;
        }

        private static void SetupIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(PanelPath) == null) return;
            UIVisualLibrary library = AssetDatabase.LoadAssetAtPath<UIVisualLibrary>(LibraryPath);
            if (!SessionState.GetBool(SessionKey, false) || library == null || library.pause == null || library.playButton == null) Setup();
        }

        private static void ExportIndividualSprites(string sourcePath, string outputFolder, int columns, int rows, string[] names, bool sliced)
        {
            EnsureFolder(outputFolder);
            Texture2D atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!atlas.LoadImage(File.ReadAllBytes(Path.GetFullPath(sourcePath)))) { UnityEngine.Object.DestroyImmediate(atlas); return; }
            int cellW = atlas.width / columns;
            int cellH = atlas.height / rows;
            for (int i = 0; i < names.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int sourceY = atlas.height - (row + 1) * cellH;
                Texture2D item = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
                item.SetPixels(atlas.GetPixels(column * cellW, sourceY, cellW, cellH));
                Color[] itemPixels = item.GetPixels();
                const int clearEdge = 7;
                for (int y = 0; y < cellH; y++) for (int x = 0; x < cellW; x++)
                    if (x < clearEdge || y < clearEdge || x >= cellW - clearEdge || y >= cellH - clearEdge)
                        itemPixels[y * cellW + x] = Color.clear;
                item.SetPixels(itemPixels);
                item.Apply(false, false);
                string itemPath = outputFolder + "/" + names[i] + ".png";
                if (!File.Exists(Path.GetFullPath(itemPath))) File.WriteAllBytes(Path.GetFullPath(itemPath), item.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(item);
            }
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in names)
            {
                string itemPath = outputFolder + "/" + name + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(itemPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = sliced ? new Vector4(cellW * .18f, cellH * .18f, cellW * .18f, cellH * .18f) : Vector4.zero;
                importer.SaveAndReimport();
            }
        }

        private static void CreateLibrary()
        {
            EnsureFolder("Assets/_Game/Resources/UI");
            UIVisualLibrary library = AssetDatabase.LoadAssetAtPath<UIVisualLibrary>(LibraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<UIVisualLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            foreach (var field in typeof(UIVisualLibrary).GetFields())
            {
                if (field.FieldType != typeof(Sprite)) continue;
                string folder = Array.IndexOf(IconNames, field.Name) >= 0 ? IconFolder : PanelFolder;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + field.Name + ".png");
                field.SetValue(library, sprite);
            }
            EditorUtility.SetDirty(library);
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring(7).Split('/')) { string next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
        }
    }
}
#endif
