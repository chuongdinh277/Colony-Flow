using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(menuName = "Colony Flow/Ant Material Palette", fileName = "AntMaterialPalette")]
    public sealed class AntMaterialPalette : ScriptableObject
    {
        [SerializeField] private PixelPalette sourcePalette;
        [SerializeField] private List<Material> materials = new();

        public PixelPalette SourcePalette => sourcePalette;
        public IReadOnlyList<Material> Materials => materials;
        public Material GetMaterial(int colorIndex) =>
            colorIndex >= 0 && colorIndex < materials.Count ? materials[colorIndex] : null;

        public Material GetClosestMaterial(Color color)
        {
            if (sourcePalette == null || materials.Count == 0) return null;
            int bestIndex = 0;
            float bestDistance = float.MaxValue;
            int count = Mathf.Min(sourcePalette.Count, materials.Count);
            for (int i = 0; i < count; i++)
            {
                Color candidate = sourcePalette.GetColor(i);
                float dr = candidate.r - color.r;
                float dg = candidate.g - color.g;
                float db = candidate.b - color.b;
                float distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = i;
            }
            return GetMaterial(bestIndex);
        }
    }
}
