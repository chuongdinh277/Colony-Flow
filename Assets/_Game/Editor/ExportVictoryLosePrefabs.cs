using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ColonyFlow.Editor
{
    public static class ExportVictoryLosePrefabs
    {
        [MenuItem("Colony Flow/Export Victory & Lose Prefabs", false, 30)]
        public static void ExportPrefabs()
        {
            Debug.Log("[ColonyFlow] Running ExportPrefabs...");
            string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene sampleScene = default;
            bool opened = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path == scenePath)
                {
                    sampleScene = s;
                    break;
                }
            }

            if (!sampleScene.IsValid() || !sampleScene.isLoaded)
            {
                sampleScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                opened = true;
            }

            ExportFromScene(sampleScene);

            if (opened)
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }
        }

        private static void ExportFromScene(Scene scene)
        {
            string prefabFolder = "Assets/_Game/Resources/UI/Prefabs";
            if (!Directory.Exists(prefabFolder)) Directory.CreateDirectory(prefabFolder);

            GameObject[] rootObjects = scene.GetRootGameObjects();
            GameObject victoryObj = null;
            GameObject loseObj = null;

            foreach (var go in rootObjects)
            {
                if (go.name == "UICanvasVictory") victoryObj = go;
                else if (go.name == "UICanvasLose") loseObj = go;
            }

            if (victoryObj != null)
            {
                UICanvasVictory comp = victoryObj.GetComponent<UICanvasVictory>();
                if (comp == null) comp = victoryObj.AddComponent<UICanvasVictory>();

                SerializedObject so = new SerializedObject(comp);
                Button btnGold = null;
                TextMeshProUGUI txtLevel = null;

                foreach (var b in victoryObj.GetComponentsInChildren<Button>(true))
                {
                    if (b.name.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.name.IndexOf("Next", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        btnGold = b;
                        break;
                    }
                }

                foreach (var txt in victoryObj.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (txt.transform.parent != null && txt.transform.parent.name.IndexOf("Btn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (txt.text.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        txt.name.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        txtLevel = txt;
                        break;
                    }
                }

                SerializedProperty pGold = so.FindProperty("btnGold");
                SerializedProperty pLevel = so.FindProperty("txtLevel");
                if (pGold != null) pGold.objectReferenceValue = btnGold;
                if (pLevel != null) pLevel.objectReferenceValue = txtLevel;
                so.ApplyModifiedProperties();

                string prefabPath = $"{prefabFolder}/UICanvasVictory.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(victoryObj, prefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[ColonyFlow] Successfully exported UICanvasVictory prefab to {prefabPath}");
            }
            else
            {
                Debug.LogWarning("[ColonyFlow] UICanvasVictory not found in scene!");
            }

            if (loseObj != null)
            {
                UICanvasLose comp = loseObj.GetComponent<UICanvasLose>();
                if (comp == null) comp = loseObj.AddComponent<UICanvasLose>();

                SerializedObject so = new SerializedObject(comp);
                Button btnRetry = null;
                Button btnHome = null;
                TextMeshProUGUI txtLevel = null;

                foreach (var b in loseObj.GetComponentsInChildren<Button>(true))
                {
                    if (b.name.IndexOf("Retry", System.StringComparison.OrdinalIgnoreCase) >= 0) btnRetry = b;
                    else if (b.name.IndexOf("Home", System.StringComparison.OrdinalIgnoreCase) >= 0) btnHome = b;
                }

                foreach (var txt in loseObj.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (txt.transform.parent != null && txt.transform.parent.name.IndexOf("Btn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (txt.text.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        txt.name.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        txtLevel = txt;
                        break;
                    }
                }

                SerializedProperty pRetry = so.FindProperty("btnRetry");
                SerializedProperty pHome = so.FindProperty("btnHome");
                SerializedProperty pLevel = so.FindProperty("txtLevel");
                if (pRetry != null) pRetry.objectReferenceValue = btnRetry;
                if (pHome != null) pHome.objectReferenceValue = btnHome;
                if (pLevel != null) pLevel.objectReferenceValue = txtLevel;
                so.ApplyModifiedProperties();

                string prefabPath = $"{prefabFolder}/UICanvasLose.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(loseObj, prefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[ColonyFlow] Successfully exported UICanvasLose prefab to {prefabPath}");
            }
            else
            {
                Debug.LogWarning("[ColonyFlow] UICanvasLose not found in scene!");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
