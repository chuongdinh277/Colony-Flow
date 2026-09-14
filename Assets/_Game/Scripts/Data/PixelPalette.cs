using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(menuName = "Colony Flow/Pixel Palette", fileName = "PixelPalette")]
    public sealed class PixelPalette : ScriptableObject
    {
        [SerializeField] private List<PaletteColor> colors = new()
        {
            new PaletteColor("Dark", new Color32(54, 46, 67, 255)),
            new PaletteColor("Orange", new Color32(240, 126, 45, 255)),
            new PaletteColor("Green", new Color32(75, 200, 92, 255)),
            new PaletteColor("Red", new Color32(224, 63, 88, 255)),
            new PaletteColor("Pink", new Color32(225, 69, 183, 255)),
            new PaletteColor("Cyan", new Color32(105, 220, 226, 255)),
            new PaletteColor("White", new Color32(232, 235, 229, 255)),
            new PaletteColor("Brown", new Color32(126, 69, 51, 255)),
            new PaletteColor("Black #211C2B", new Color32(33, 28, 43, 255)),
            new PaletteColor("Outline #4B405E", new Color32(75, 64, 94, 255)),
            new PaletteColor("Purple Dark #6C4389", new Color32(108, 67, 137, 255)),
            new PaletteColor("Purple #9B5BC4", new Color32(155, 91, 196, 255)),
            new PaletteColor("Purple Light #C98DE5", new Color32(201, 141, 229, 255)),
            new PaletteColor("Pink Dark #B82791", new Color32(184, 39, 145, 255)),
            new PaletteColor("Pink Bright #F23CC7", new Color32(242, 60, 199, 255)),
            new PaletteColor("Pink Light #FF8DDF", new Color32(255, 141, 223, 255)),
            new PaletteColor("Red Dark #B93652", new Color32(185, 54, 82, 255)),
            new PaletteColor("Red Light #F57889", new Color32(245, 120, 137, 255)),
            new PaletteColor("Orange Dark #C86626", new Color32(200, 102, 38, 255)),
            new PaletteColor("Orange Light #FFAB42", new Color32(255, 171, 66, 255)),
            new PaletteColor("Yellow #FFD45A", new Color32(255, 212, 90, 255)),
            new PaletteColor("Yellow Light #FFECA0", new Color32(255, 236, 160, 255)),
            new PaletteColor("Brown Dark #633D38", new Color32(99, 61, 56, 255)),
            new PaletteColor("Tan #D99768", new Color32(217, 151, 104, 255)),
            new PaletteColor("Green Dark #278A55", new Color32(39, 138, 85, 255)),
            new PaletteColor("Green Light #83E37D", new Color32(131, 227, 125, 255)),
            new PaletteColor("Cyan Dark #36A7BC", new Color32(54, 167, 188, 255)),
            new PaletteColor("Cyan Bright #57DDEB", new Color32(87, 221, 235, 255)),
            new PaletteColor("Cyan Light #B8F3F4", new Color32(184, 243, 244, 255)),
            new PaletteColor("Blue #518DD8", new Color32(81, 141, 216, 255)),
            new PaletteColor("Blue Dark #395A9D", new Color32(57, 90, 157, 255)),
            new PaletteColor("Ice White #F7FAF5", new Color32(247, 250, 245, 255)),
            new PaletteColor("Gray Light #C7CDD2", new Color32(199, 205, 210, 255)),
            new PaletteColor("Gray #89909D", new Color32(137, 144, 157, 255)),
            new PaletteColor("Skin #F3B68F", new Color32(243, 182, 143, 255)),
            new PaletteColor("Peach #FFD0AE", new Color32(255, 208, 174, 255))
        };

        public IReadOnlyList<PaletteColor> Colors => colors;
        public int Count => colors.Count;

        public Color GetColor(int index) => index >= 0 && index < colors.Count ? colors[index].color : Color.magenta;
        public string GetId(int index) => index >= 0 && index < colors.Count ? colors[index].id : "Invalid";

        public int FindNearest(Color sample)
        {
            if (colors.Count == 0) return -1;
            int best = 0;
            float bestDistance = float.MaxValue;
            Vector3 source = ToOklab(sample);

            for (int i = 0; i < colors.Count; i++)
            {
                Vector3 candidate = ToOklab(colors[i].color);
                float distance = (source - candidate).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        public static Vector3 ToOklab(Color srgb)
        {
            Color rgb = srgb.linear;
            float l = .41222147f * rgb.r + .53633254f * rgb.g + .05144599f * rgb.b;
            float m = .21190350f * rgb.r + .68069955f * rgb.g + .10739696f * rgb.b;
            float s = .08830246f * rgb.r + .28171884f * rgb.g + .62997870f * rgb.b;
            l = Mathf.Pow(Mathf.Max(0f, l), 1f / 3f);
            m = Mathf.Pow(Mathf.Max(0f, m), 1f / 3f);
            s = Mathf.Pow(Mathf.Max(0f, s), 1f / 3f);
            return new Vector3(
                .21045426f * l + .79361779f * m - .00407205f * s,
                1.97799850f * l - 2.42859221f * m + .45059371f * s,
                .02590404f * l + .78277177f * m - .80867577f * s);
        }
    }
}
