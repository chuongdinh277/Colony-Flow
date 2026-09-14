using UnityEngine;
using UnityEditor;
using System.IO;

namespace ColonyFlow.Editor
{
    public class BoxSpriteGenerator
    {
        [MenuItem("Colony Flow/Generate 3D Box Sprites (Auto)")]
        public static void GenerateSprites()
        {
            // 1. Tạo ảnh khối Pixel cho bàn cờ (Không viền trắng, độ dày vừa phải)
            Generate("Assets/_Game/Resources/Box_Pixel.png", depth: 32f, outline: false);
            
            // 2. Tạo ảnh khối Thẻ bài ở khay chờ (Có viền trắng, độ dày lớn)
            Generate("Assets/_Game/Resources/Box_Tray.png", depth: 56f, outline: true);
            
            AssetDatabase.Refresh();
            Debug.Log("Đã tạo xong ảnh Box 3D! Bạn có thể xem trong Assets/_Game/Resources/");
        }

        private static void Generate(string path, float depth, bool outline)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            float radius = 36f;
            float padding = outline ? 12f : 8f;
            
            float minX = padding;
            float maxX = size - padding;
            float minY = padding;
            float maxY = size - padding;

            float faceMinY = minY + depth; 

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outDist = RoundedRectSDF(x, y, minX, maxX, minY, maxY, radius);
                    
                    if (outDist > 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    
                    float alpha = Mathf.Clamp01(1f - outDist);
                    Color col = Color.clear;

                    if (outline && outDist > -12f)
                    {
                        // Vẽ viền trắng
                        col = Color.white;
                    }
                    else
                    {
                        // Thu gọn kích thước để vẽ lõi màu bên trong
                        float innerMinX = outline ? minX + 12f : minX;
                        float innerMaxX = outline ? maxX - 12f : maxX;
                        float innerMinY = outline ? minY + 12f : minY;
                        float innerMaxY = outline ? maxY - 12f : maxY;
                        float innerRadius = outline ? radius - 8f : radius;
                        float innerFaceMinY = outline ? faceMinY + 6f : faceMinY;
                        
                        float faceDist = RoundedRectSDF(x, y, innerMinX, innerMaxX, innerFaceMinY, innerMaxY, innerRadius);
                        
                        if (faceDist <= 0f)
                        {
                            // MẶT TRÊN CỦA KHỐI (Màu sáng để ăn màu chuẩn)
                            col = new Color(0.95f, 0.95f, 0.95f);
                            
                            // Tạo Highlight (Bóng loáng) ở viền trên và trái
                            float edgeDistX = x - innerMinX;
                            float edgeDistY = innerMaxY - y;
                            if (edgeDistX < 16f || edgeDistY < 16f)
                            {
                                col = Color.Lerp(Color.white, col, Mathf.Min(edgeDistX, edgeDistY) / 16f);
                            }
                        }
                        else
                        {
                            // ĐỘ DÀY CỦA KHỐI (Phần dưới, tô màu xám tối)
                            float depthRatio = (y - innerMinY) / (innerFaceMinY - innerMinY);
                            depthRatio = Mathf.Clamp01(depthRatio);
                            
                            col = new Color(0.55f, 0.55f, 0.55f);
                            // Càng xuống đáy càng tối
                            col *= Mathf.Lerp(0.4f, 0.95f, depthRatio); 
                        }
                    }

                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
            
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 256f;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static float RoundedRectSDF(float px, float py, float minX, float maxX, float minY, float maxY, float r)
        {
            float cx = (minX + maxX) / 2f;
            float cy = (minY + maxY) / 2f;
            float extX = (maxX - minX) / 2f - r;
            float extY = (maxY - minY) / 2f - r;
            
            float dx = Mathf.Max(Mathf.Abs(px - cx) - extX, 0f);
            float dy = Mathf.Max(Mathf.Abs(py - cy) - extY, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
    }
}
