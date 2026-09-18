using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class MapEditingStage : PreviewSceneStage
    {
        protected override GUIContent CreateHeaderContent()
        {
            return new GUIContent("地图编辑舞台（源文件由工作台保存）");
        }

        public static MapEditingStage Enter()
        {
            var current = StageUtility.GetCurrentStage() as MapEditingStage;
            if (current != null) { return current; }
            var stage = CreateInstance<MapEditingStage>();
            StageUtility.GoToStage(stage, true);
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = true;
            view.Show();
            return stage;
        }
    }
}
