using UnityEditor;

namespace MizoTake.VirtualLight.Editor
{
    [InitializeOnLoad]
    internal static class VirtualLightEditorLifecycle
    {
        static VirtualLightEditorLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= VirtualLightRenderBridge.Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += VirtualLightRenderBridge.Dispose;
        }
    }
}
