#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only visual diagnostic. Capture the actual Game View after the level
// has settled; never changes the scene, level data or gameplay UI.
[InitializeOnLoad]
internal static class GameplayVisualCapture
{
    private static double readyAt;
    static GameplayVisualCapture()
    {
        readyAt = EditorApplication.timeSinceStartup + 8;
        EditorApplication.update += Capture;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        readyAt = EditorApplication.timeSinceStartup + 8;
        EditorApplication.update -= Capture;
        EditorApplication.update += Capture;
    }
    private static void Capture()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < readyAt) return;
        if (Object.FindFirstObjectByType<ColonyFlow.LevelController>() == null) return;
        EditorApplication.update -= Capture;
        System.IO.Directory.CreateDirectory("Temp/VisualChecks");
        ScreenCapture.CaptureScreenshot("Temp/VisualChecks/gameplay.png");
        Debug.Log("Visual check: requested Game View screenshot at Temp/VisualChecks/gameplay.png");
    }
}
#endif
