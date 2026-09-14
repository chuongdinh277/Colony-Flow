using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ColonyFlow;
using System.Reflection;

public static class PaletteCleaner
{
    [MenuItem("Colony Flow/Clean Up Palettes")]
    [InitializeOnLoadMethod]
    public static void Clean()
    {
        if (SessionState.GetBool("PaletteCleaned", false)) return;
        SessionState.SetBool("PaletteCleaned", true);
        string[] guids = AssetDatabase.FindAssets("t:PixelPalette");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PixelPalette palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(path);
            
            var colorsField = typeof(PixelPalette).GetField("colors", BindingFlags.NonPublic | BindingFlags.Instance);
            var oldColors = (List<PaletteColor>)colorsField.GetValue(palette);
            
            var newColors = new List<PaletteColor>();
            foreach(var c in oldColors)
            {
                bool tooClose = false;
                foreach(var k in newColors)
                {
                    Vector3 lab1 = PixelPalette.ToOklab(c.color);
                    Vector3 lab2 = PixelPalette.ToOklab(k.color);
                    float dist = (lab1 - lab2).sqrMagnitude;
                    
                    if (dist < 0.0065f) // Adjust threshold here
                    {
                        tooClose = true;
                        Debug.Log($"Removed {c.id} (too close to {k.id}, dist {dist})");
                        break;
                    }
                }
                if (!tooClose) newColors.Add(c);
            }
            
            colorsField.SetValue(palette, newColors);
            EditorUtility.SetDirty(palette);
            Debug.Log($"Cleaned {path}: {oldColors.Count} -> {newColors.Count} colors");
        }
        AssetDatabase.SaveAssets();
    }
}
