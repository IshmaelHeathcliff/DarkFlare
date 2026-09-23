using DarkFlare;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare.Editor
{
    public sealed class BehaviorDebugWindow : OdinEditorWindow
    {
        readonly BehaviorDebugSelection _selection = new BehaviorDebugSelection();
        BehaviorDebugView _view;
        IVisualElementScheduledItem _refresh;
        [MenuItem("DarkFlare/调试/行为实时图")]
        public static void Open() { GetWindow<BehaviorDebugWindow>("Behavior · HFSM / BT").Show(); }
        public void CreateGUI()
        {
            _view?.Dispose(); rootVisualElement.Clear();
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Behavior/BehaviorDebug.uxml");
            if (asset == null) { rootVisualElement.Add(new Label("请先执行 DarkFlare/行为/创建诊断样例")); return; }
            asset.CloneTree(rootVisualElement); rootVisualElement.AddToClassList("editor-mode");
            _view = new BehaviorDebugView(rootVisualElement); _view.Cycle += Cycle; _view.Control += Control;
            _refresh?.Pause(); _refresh = rootVisualElement.schedule.Execute(Refresh).Every(100);
        }
        protected override void OnEnable()
        {
            base.OnEnable(); Selection.selectionChanged += Selected; EditorApplication.playModeStateChanged += PlayChanged;
            SceneView.duringSceneGui += DrawPerception;
        }
        protected override void OnDisable()
        {
            Selection.selectionChanged -= Selected; EditorApplication.playModeStateChanged -= PlayChanged;
            SceneView.duringSceneGui -= DrawPerception;
            _refresh?.Pause(); _selection.Dispose(); _view?.Dispose(); _view = null; base.OnDisable();
        }
        void PlayChanged(PlayModeStateChange state) { _selection.Dispose(); _view?.Show(null); }
        void Selected()
        {
            BehaviorSampleActor actor = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<BehaviorSampleActor>() : null;
            if (actor != null && actor.Handle.IsValid && GameArchitectureProvider.TryGetCurrent(out IArchitecture owner)) { _selection.Select(owner, actor.Handle); }
            MonsterController monster = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MonsterController>() : null;
            if (monster != null && monster.BehaviorHandle.IsValid) { _selection.Select(monster.GetArchitecture(), monster.BehaviorHandle); }
        }
        void Cycle(int direction)
        {
            if (GameArchitectureProvider.TryGetCurrent(out IArchitecture owner)) { _selection.Cycle(owner, direction); }
        }
        void Control(BehaviorControl control) { _selection.Control(control); }
        void DrawPerception(SceneView scene)
        {
            MonsterController monster = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MonsterController>() : null;
            if (monster == null || monster.BehaviorRuntime == null) { return; }
            MonsterBehaviorDefinition config = monster.Definition.Behavior;
            MonsterBehaviorRuntime runtime = monster.BehaviorRuntime;
            Vector3 position = monster.transform.position;
            Color previous = Handles.color;
            Handles.color = Color.gray; Handles.DrawWireDisc(position, Vector3.forward, config.DeactivationDistance);
            Handles.color = Color.cyan; Handles.DrawWireDisc(position, Vector3.forward, config.ActivationDistance);
            Handles.color = Color.yellow; Handles.DrawWireDisc(runtime.Anchor, Vector3.forward, config.WanderRadius);
            Vector3 start = Quaternion.AngleAxis(-config.SightAngle * .5f, Vector3.forward) * runtime.Facing;
            Vector3 end = Quaternion.AngleAxis(config.SightAngle * .5f, Vector3.forward) * runtime.Facing;
            Handles.color = runtime.IsVisible ? Color.green : Color.red;
            Handles.DrawWireArc(position, Vector3.forward, start, config.SightAngle, config.SightDistance);
            Handles.DrawLine(position, position + start * config.SightDistance);
            Handles.DrawLine(position, position + end * config.SightDistance);
            MonsterBehaviorStateDto state = runtime.CaptureState();
            if (state.MemoryRemaining > 0) { Handles.DrawDottedLine(position, new Vector3(state.LastSeen.X, state.LastSeen.Y), 4); }
            Handles.Label(position + Vector3.up, $"{runtime.Mode} / {runtime.AttackPhase} · 可见 {runtime.IsVisible}");
            Handles.color = previous;
        }
        void Refresh()
        {
            if (!EditorApplication.isPlaying || !GameArchitectureProvider.HasCurrent) { _selection.Dispose(); _view?.Show(null); return; }
            BehaviorDebugSnapshot snapshot = _selection.Capture();
            if (snapshot == null) { Cycle(1); snapshot = _selection.Capture(); }
            _view?.Localize(); _view?.Show(snapshot);
        }
    }
}
