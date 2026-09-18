using System;
using System.Linq;
using UnityEditor;

namespace DarkFlare.Editor
{
    // projectChanged 不保证为同路径内容重导入触发；依赖重导入也经此入口。
    sealed class MapPreviewSourceChanges : AssetPostprocessor
    {
        internal static event Action Changed;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path.EndsWith(".dfmap", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))) { return; }
            EditorApplication.delayCall -= Notify;
            EditorApplication.delayCall += Notify;
        }

        static void Notify()
        {
            Changed?.Invoke();
        }
    }
}
