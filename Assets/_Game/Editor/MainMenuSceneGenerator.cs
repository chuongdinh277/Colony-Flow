#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColonyFlow.Editor
{
    public static class MainMenuSceneGenerator
    {
        private const string ScenePath="Assets/_Game/Scenes/MainMenu.unity";
        private const string BackgroundPath="Assets/_Game/Resources/UI/MainMenuBackground.png";
        [InitializeOnLoadMethod] private static void AutoCreate()=>EditorApplication.delayCall+=()=>{if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)Create();};
        [MenuItem("Colony Flow/Create Main Menu Scene")]
        public static void Create()
        {
            if(AssetImporter.GetAtPath(BackgroundPath) is TextureImporter importer && importer.textureType!=TextureImporterType.Sprite)
            {importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            GameObject root=new("MainMenu");SceneManager.MoveGameObjectToScene(root,scene);root.AddComponent<MainMenuUI>();
            EditorSceneManager.SaveScene(scene,ScenePath);EditorSceneManager.CloseScene(scene,true);
            List<EditorBuildSettingsScene> scenes=new(EditorBuildSettings.scenes);scenes.RemoveAll(s=>s.path==ScenePath);scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();Debug.Log("Created main menu scene: "+ScenePath);
        }
    }
}
#endif
