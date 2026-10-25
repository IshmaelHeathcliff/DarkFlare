using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    static class ScriptDomainReloadMenu
    {
        const string MenuPath = "DarkFlare/工具/手动刷新脚本域";

        [MenuItem(MenuPath)]
        static void Reload()
        {
            if (!CanReload())
            {
                return;
            }

            Debug.Log("[脚本域刷新] 请求重新加载脚本程序集并重置静态状态。");
            EditorUtility.RequestScriptReload();
        }

        [MenuItem(MenuPath, true)]
        static bool CanReload()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating;
        }
    }
}
