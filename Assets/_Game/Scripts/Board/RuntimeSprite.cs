using UnityEngine;

namespace ColonyFlow
{
    internal static class RuntimeSprite
    {
        private static readonly System.Collections.Generic.Dictionary<Color32, Sprite> beveledPixels = new();
        private static readonly System.Collections.Generic.Dictionary<Color32, Sprite> colonyBoxes = new();
        private static Sprite antSprite;
        private static Material unlitMaterial;
        private static readonly System.Collections.Generic.Dictionary<Texture, Material> textureMaterials = new();
        public static Material MaterialFor(Sprite sprite)
        {
            if (!textureMaterials.TryGetValue(sprite.texture, out Material material) || material == null)
            {
                material = new Material(UnlitMaterial) { mainTexture = sprite.texture, name = "Box_" + sprite.texture.name };
                textureMaterials[sprite.texture] = material;
            }
            return material;
        }
        public static Material UnlitMaterial
        {
            get
            {
                if (unlitMaterial == null)
                    unlitMaterial = new Material(Shader.Find("Sprites/Default")) { name = "GameplayBoxUnlit" };
                return unlitMaterial;
            }
        }

        public static Sprite ColonyBox(Color color)
        {
            Color32 key = color;
            if (colonyBoxes.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float bodyAlpha = RoundedCoverage(u, v, .04f, .035f, .96f, .84f, .15f, size);
                float lidAlpha = RoundedCoverage(u, v, .025f, .20f, .975f, .985f, .16f, size);
                Color body = color * Mathf.Lerp(.48f, .78f, Mathf.Clamp01(v / .25f));
                float edge = Mathf.Min(Mathf.Min(u-.025f,.975f-u), Mathf.Min(v-.20f,.985f-v));
                float rim = 1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(edge/.055f));
                Color lid = color * Mathf.Lerp(.87f,1f,Mathf.Clamp01((v-.20f)/.6f));
                lid = Color.Lerp(lid, Color.white, rim * (v>.65f ? .32f : .06f));
                float spot = Mathf.Exp(-Mathf.Pow((u-.19f)/.085f,2f)-Mathf.Pow((v-.85f)/.055f,2f));
                lid = Color.Lerp(lid,Color.white,spot*.32f);
                Color result = Color.Lerp(body,lid,lidAlpha);
                result.a = Mathf.Max(bodyAlpha,lidAlpha);
                pixels[y*size+x]=result;
            }
            texture.SetPixels(pixels);texture.Apply(true,true);
            Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size,0,SpriteMeshType.FullRect);
            colonyBoxes[key]=sprite;
            return sprite;
        }

        private static float RoundedCoverage(float x,float y,float minX,float minY,float maxX,float maxY,float radius,int size)
        {
            float qx=Mathf.Abs(x-(minX+maxX)*.5f)-(maxX-minX)*.5f+radius;
            float qy=Mathf.Abs(y-(minY+maxY)*.5f)-(maxY-minY)*.5f+radius;
            float distance=Mathf.Sqrt(Mathf.Max(qx,0f)*Mathf.Max(qx,0f)+Mathf.Max(qy,0f)*Mathf.Max(qy,0f))+Mathf.Min(Mathf.Max(qx,qy),0f)-radius;
            return Mathf.Clamp01(.5f-distance*size);
        }

        // One cell-sized sprite: the bevel belongs inside the cell, so adjacent
        // pixels meet without exposing the cream board through a shrunken face.
        public static Sprite BeveledPixel(Color color)
        {
            Color32 key = color;
            if (beveledPixels.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "BeveledPixel_" + ColorUtility.ToHtmlStringRGB(color),
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size;
                float v = (y + .5f) / size;
                // A tiny version of the stack box: dark body extends below and
                // to the right, while the bright lid is shifted up and left.
                float bodyAlpha = RoundedCoverage(u, v, .025f, .025f, .985f, .875f, .075f, size);
                float lidAlpha = RoundedCoverage(u, v, .015f, .17f, .945f, .985f, .075f, size);
                Color body = color * Mathf.Lerp(.48f, .68f, Mathf.Clamp01(v / .24f));
                float lidHeight = Mathf.Clamp01((v - .17f) / .815f);
                Color lid = color * Mathf.Lerp(.92f, 1.08f, lidHeight);
                float leftLight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.02f, .13f, u));
                float topLight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.78f, .975f, v));
                lid = Color.Lerp(lid, Color.white, .12f * leftLight + .23f * topLight);
                float glint = Mathf.Exp(-Mathf.Pow((u - .19f) / .075f, 2f) - Mathf.Pow((v - .82f) / .07f, 2f));
                lid = Color.Lerp(lid, Color.white, glint * .30f);
                Color result = Color.Lerp(body, lid, lidAlpha);
                result.a = Mathf.Max(bodyAlpha, lidAlpha);
                pixels[y * size + x] = result;
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
            beveledPixels[key] = sprite;
            return sprite;
        }

        public static Sprite Ant
        {
            get
            {
                if (antSprite != null) return antSprite;
                Texture2D source = Resources.Load<Texture2D>("Ant/ant_chibi_topdown");
                if (source == null) return RoundedSquare;

                Color32[] input = source.GetPixels32();
                int width = source.width, height = source.height;
                var transparent = new bool[input.Length];
                var queue = new System.Collections.Generic.Queue<int>();
                void Seed(int index)
                {
                    if (transparent[index] || !IsGeneratedBackdrop(input[index])) return;
                    transparent[index] = true;
                    queue.Enqueue(index);
                }
                for (int x = 0; x < width; x++) { Seed(x); Seed((height - 1) * width + x); }
                for (int y = 0; y < height; y++) { Seed(y * width); Seed(y * width + width - 1); }
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), px = i % width, py = i / width;
                    if (px > 0) Seed(i - 1);
                    if (px + 1 < width) Seed(i + 1);
                    if (py > 0) Seed(i - width);
                    if (py + 1 < height) Seed(i + width);
                }
                for (int i = 0; i < input.Length; i++) if (transparent[i]) input[i].a = 0;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = "AntChibiTransparent", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(input);
                texture.Apply(true, true);
                antSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), Mathf.Max(width, height), 0, SpriteMeshType.FullRect);
                antSprite.name = "AntChibiTopDown";
                return antSprite;
            }
        }

        private static bool IsGeneratedBackdrop(Color32 pixel)
        {
            int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            return max - min < 22 && min > 175;
        }
        private static Sprite square;
        private static Sprite roundedSquare;
        public static Sprite Square
        {
            get
            {
                if (square != null) return square;

                // Tự động tìm ảnh khối Pixel (Box_Pixel.png)
                Sprite customSprite = Resources.Load<Sprite>("Box_Pixel");
                if (customSprite != null)
                {
                    square = customSprite;
                    return square;
                }

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeSquare",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                square.name = "RuntimeSquare";
                return square;
            }
        }

        private static Sprite trayBox;
        public static Sprite TrayBox
        {
            get
            {
                if (trayBox != null) return trayBox;
                Sprite customSprite = Resources.Load<Sprite>("Box_Tray");
                if (customSprite != null)
                {
                    trayBox = customSprite;
                    return trayBox;
                }
                return Square; // Fallback
            }
        }

        public static Sprite RoundedSquare
        {
            get
            {
                if (roundedSquare != null) return roundedSquare;
                const int size = 64;
                const float radius = 13f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeRoundedSquare",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
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
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                roundedSquare = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                    0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                roundedSquare.name = "RuntimeRoundedSquare";
                return roundedSquare;
            }
        }
    }
}
