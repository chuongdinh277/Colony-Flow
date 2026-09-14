using UnityEditor;
using UnityEngine;
using ColonyFlow;

public class AIUIFixer
{
    [MenuItem("Colony Flow/Auto-Attach UI Scripts")]
    public static void AttachScripts()
    {
        Attach<UICanvasGameMenu>("Assets/_Game/Resources/UI/Prefabs/UICanvasGameMenu.prefab");
        Attach<UICanvasGameplay>("Assets/_Game/Resources/UI/Prefabs/UICanvasGamePlay.prefab");
        Attach<UICanvasGameSetting>("Assets/_Game/Resources/UI/Prefabs/UICanvasGameSetting.prefab");
        Debug.Log("UI Scripts automatically attached to prefabs!");
    }

    private static void Attach<T>(string path) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            if (prefab.GetComponent<T>() == null)
            {
                // Remove old scripts that might be conflicting
                var oldScript = prefab.GetComponent("MainMenuUI");
                if (oldScript != null) Object.DestroyImmediate(oldScript, true);
                
                var oldScript2 = prefab.GetComponent("GameplayHUD");
                if (oldScript2 != null) Object.DestroyImmediate(oldScript2, true);

                prefab.AddComponent<T>();
                PrefabUtility.SavePrefabAsset(prefab);
                Debug.Log($"Attached {typeof(T).Name} to {prefab.name}");
            }
        }
    }
}
