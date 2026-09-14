#if UNITY_EDITOR
using UnityEditor;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class PixelBox3DAutoRun
    {
        private const string Key = "ColonyFlow.PixelBox3D.Session.V5";

        static PixelBox3DAutoRun()
        {
            if (SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            EditorApplication.delayCall += PixelBox3DGenerator.Generate;
        }
    }
}
#endif
