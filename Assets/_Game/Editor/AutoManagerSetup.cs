using UnityEditor;
using UnityEngine;
using ColonyFlow;

public class AutoManagerSetup
{
    [MenuItem("Colony Flow/Setup Managers Hierarchy")]
    public static void SetupManagers()
    {
        // Try to find existing
        GameObject root = GameObject.Find("Managers");
        if (root == null)
        {
            root = new GameObject("Managers");
            root.AddComponent<ManagerRoot>();
            Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
        }
        else if (root.GetComponent<ManagerRoot>() == null)
        {
            root.AddComponent<ManagerRoot>();
        }

        CreateManager<GameManager>("GameManager", root.transform);
        CreateManager<LevelManager>("LevelManager", root.transform);
        CreateManager<SoundManager>("SoundManager", root.transform);
        CreateManager<DataManager>("DataManager", root.transform);
        CreateManager<BoosterManager>("BoosterManager", root.transform);
        CreateManager<UIManager>("UIManager", root.transform);

        Debug.Log("Managers Hierarchy created successfully!");
    }

    private static void CreateManager<T>(string name, Transform parent) where T : MonoBehaviour
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }
        else
        {
            if (existing.GetComponent<T>() == null)
            {
                existing.gameObject.AddComponent<T>();
            }
        }
    }
}
