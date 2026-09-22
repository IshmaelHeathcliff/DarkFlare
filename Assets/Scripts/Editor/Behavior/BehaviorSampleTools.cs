using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFlare.Editor
{
    public static class BehaviorSampleTools
    {
        public const string ScenePath = "Assets/Scenes/Diagnostics/BehaviorSample.unity";
        public const string ConfigPath = "Assets/Data/Diagnostics/BehaviorSampleConfig.asset";
        [MenuItem("DarkFlare/行为/创建诊断样例")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) { throw new InvalidOperationException("请在 Edit 模式创建样例"); }
            Directory.CreateDirectory("Assets/UI/Behavior"); Directory.CreateDirectory("Assets/Data/Diagnostics");
            Directory.CreateDirectory("Assets/Scenes/Diagnostics");
            File.WriteAllText("Assets/UI/Behavior/BehaviorDebug.uxml", Uxml, new UTF8Encoding(false));
            File.WriteAllText("Assets/UI/Behavior/BehaviorDebug.uss", Uss, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            BehaviorSampleConfig config = AssetDatabase.LoadAssetAtPath<BehaviorSampleConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<BehaviorSampleConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            BehaviorAnimatorMap mapping = AssetDatabase.LoadAssetAtPath<BehaviorAnimatorMap>("Assets/Data/Diagnostics/BehaviorAnimatorMap.asset");
            if (mapping == null)
            {
                mapping = ScriptableObject.CreateInstance<BehaviorAnimatorMap>();
                AssetDatabase.CreateAsset(mapping, "Assets/Data/Diagnostics/BehaviorAnimatorMap.asset");
            }
            Localize();
            if (File.Exists(ScenePath)) { AssetDatabase.SaveAssets(); return; }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var hostObject = new GameObject("Behavior Sample");
                BehaviorSampleHost host = hostObject.AddComponent<BehaviorSampleHost>();
                var events = new GameObject("Sample EventSystem"); events.transform.SetParent(hostObject.transform);
                events.AddComponent(FindType("UnityEngine.EventSystems.EventSystem"));
                Component input = events.AddComponent(FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
                input.GetType().GetProperty("enabled").SetValue(input, false);
                Set(host, "_config", config); Set(host, "_inputModule", input);
                var cameraObject = new GameObject("Sample Camera");
                Camera camera = cameraObject.AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.orthographicSize = 4.5f;
                camera.transform.position = new Vector3(1, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .08f);
                RuntimeAnimatorController animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/Controllers/Monster_Basic.controller");
                Sprite sprite = animator.animationClips.SelectMany(clip => AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding))).Select(frame => frame.value).OfType<Sprite>().First();
                for (int i = 0; i < 2; i++)
                {
                    var actorObject = new GameObject(i == 0 ? "Patrol A" : "Patrol B");
                    actorObject.transform.SetParent(hostObject.transform); actorObject.transform.position = new Vector3(-4, i == 0 ? 1 : -1, 0);
                    SpriteRenderer renderer = actorObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                    renderer.color = i == 0 ? Color.white : new Color(.55f, .9f, 1);
                    Animator actorAnimator = actorObject.AddComponent<Animator>(); actorAnimator.runtimeAnimatorController = animator;
                    BehaviorAnimatorBridge bridge = actorObject.AddComponent<BehaviorAnimatorBridge>(); Set(bridge, "_mapping", mapping);
                    actorObject.AddComponent<BehaviorSampleActor>();
                }
                var panelObject = new GameObject("Behavior Diagnostics"); panelObject.transform.SetParent(hostObject.transform);
                PanelRenderer panel = panelObject.AddComponent<PanelRenderer>();
                panel.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/UI/GamePanelSettings.asset");
                panel.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Behavior/BehaviorDebug.uxml");
                panelObject.AddComponent<RuntimePanelView>(); panelObject.AddComponent<BehaviorDebugPanel>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) { SceneManager.SetActiveScene(previous); }
            }
            AssetDatabase.SaveAssets();
        }

        static Type FindType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>().First(t => t.FullName == fullName);
        }
        static void Set(UnityEngine.Object owner, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner); serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Localize()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection("ui");
            string[,] entries =
            {
                { "title", "行为观察台", "Behavior observatory" },
                { "hint", "Tab / Start 打开 · Esc / B 关闭\n方向键导航 · Enter / A 确认", "Tab / Start: open · Esc / B: close\nArrows / D-pad: navigate · Enter / A: select" },
                { "previous", "上一个", "Previous" }, { "next", "下一个", "Next" },
                { "pause", "暂停 / 继续", "Pause / resume" }, { "interrupt", "打断", "Interrupt" },
                { "attack", "攻击样例", "Attack sample" }, { "open", "打开行为观察台", "Open behavior observatory" },
                { "close", "关闭", "Close" }, { "hfsmTitle", "层级状态机 · 活动路径", "HFSM · active path" },
                { "treeTitle", "行为树 · 实时执行状态", "Behavior tree · live execution" },
                { "traceTitle", "条件与转移轨迹（按需采集）", "Conditions & transitions (on demand)" },
                { "unavailable", "没有有效的行为实例", "No live behavior instance" },
                { "paused", "暂停", "Paused" }, { "running", "运行中", "Running" }
            };
            for (int i = 0; i < entries.GetLength(0); i++)
            {
                string key = "behavior." + entries[i, 0];
                long id = (collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key)).Id;
                foreach (string locale in new[] { "zh-Hans", "en" })
                {
                    var table = (StringTable)collection.GetTable(new LocaleIdentifier(locale));
                    string value = entries[i, locale == "en" ? 2 : 1];
                    StringTableEntry entry = table.GetEntry(id) ?? table.AddEntry(id, value); entry.Value = value; EditorUtility.SetDirty(table);
                }
            }
            EditorUtility.SetDirty(collection.SharedData);
        }

        const string Uxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
  <ui:Style src=""BehaviorDebug.uss"" />
  <ui:VisualElement name=""behaviorRoot"" class=""behavior-root"" picking-mode=""Ignore"">
    <ui:Button name=""open"" text=""Open behavior observatory"" />
    <ui:VisualElement name=""debugPanel"" class=""debug-panel"">
      <ui:Label name=""title"" class=""title"" />
      <ui:Label name=""hint"" class=""hint"" />
      <ui:VisualElement class=""toolbar"">
        <ui:Button name=""previous"" /><ui:Button name=""next"" /><ui:Button name=""pause"" />
        <ui:Button name=""interrupt"" /><ui:Button name=""attack"" /><ui:Button name=""close"" />
      </ui:VisualElement>
      <ui:Label name=""actorName"" class=""actor-name"" />
      <ui:Label name=""state"" class=""state"" />
      <ui:Label name=""summary"" class=""summary"" />
      <ui:ScrollView class=""body"">
        <ui:VisualElement class=""graphs"">
          <ui:Label name=""hfsmTitle"" class=""section-title"" />
          <ui:VisualElement name=""hfsmGraph"" class=""hfsm-graph"" />
          <ui:Label name=""treeTitle"" class=""section-title"" />
          <ui:VisualElement name=""treeGraph"" class=""tree-graph"" />
        </ui:VisualElement>
        <ui:Label name=""traceTitle"" class=""section-title"" />
        <ui:Label name=""guards"" class=""trace"" /><ui:Label name=""trace"" class=""trace"" />
      </ui:ScrollView>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
";
        const string Uss = @".behavior-root { flex-grow: 1; padding: 18px; color: rgb(225, 234, 243); align-items: flex-end; }
.debug-panel { width: 100%; flex-grow: 1; padding: 18px; background-color: rgba(12, 20, 31, 0.97); border-width: 1px; border-color: rgb(58, 77, 94); border-radius: 8px; }
.title { font-size: 23px; -unity-font-style: bold; margin-bottom: 8px; }
.hint { font-size: 12px; color: rgb(153, 172, 190); white-space: normal; margin-bottom: 12px; }
.toolbar { flex-direction: row; flex-wrap: wrap; margin-bottom: 12px; }
.behavior-root Button { background-color: rgb(30, 47, 62); color: rgb(233, 241, 245); border-width: 1px; border-color: rgb(68, 93, 113); border-radius: 4px; min-height: 34px; padding: 4px 12px; margin-right: 6px; margin-bottom: 6px; }
.behavior-root Button:focus, .behavior-root Button:hover { border-color: rgb(112, 215, 175); background-color: rgb(38, 64, 72); }
.actor-name { font-size: 19px; color: rgb(116, 223, 180); margin-bottom: 8px; }
.state { font-size: 15px; margin-bottom: 6px; }
.summary { font-size: 12px; white-space: normal; margin-bottom: 12px; color: rgb(174, 191, 209); }
.body { flex-grow: 1; }
.section-title { font-size: 15px; margin-top: 12px; margin-bottom: 6px; color: rgb(161, 184, 208); }
.trace { font-size: 12px; white-space: normal; margin-bottom: 10px; }
.hfsm-graph { height: 135px; }
.tree-graph { height: 430px; }
.behavior-graph { flex-grow: 1; }
.closed { display: none; }
.player-mode .debug-panel { width: 58%; max-height: 75%; }
.player-mode .graphs { display: none; }
.player-mode #pause, .player-mode #interrupt, .player-mode #attack { display: none; }
.editor-mode #open, .editor-mode #close { display: none; }
";
    }
}
