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
        }
        protected override void OnDisable()
        {
            Selection.selectionChanged -= Selected; EditorApplication.playModeStateChanged -= PlayChanged;
            _refresh?.Pause(); _selection.Dispose(); _view?.Dispose(); _view = null; base.OnDisable();
        }
        void PlayChanged(PlayModeStateChange state) { _selection.Dispose(); _view?.Show(null); }
        void Selected()
        {
            BehaviorSampleActor actor = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<BehaviorSampleActor>() : null;
            if (actor != null && actor.Handle.IsValid && GameArchitectureProvider.TryGetCurrent(out IArchitecture owner)) { _selection.Select(owner, actor.Handle); }
        }
        void Cycle(int direction)
        {
            if (GameArchitectureProvider.TryGetCurrent(out IArchitecture owner)) { _selection.Cycle(owner, direction); }
        }
        void Control(BehaviorControl control) { _selection.Control(control); }
        void Refresh()
        {
            if (!EditorApplication.isPlaying || !GameArchitectureProvider.HasCurrent) { _selection.Dispose(); _view?.Show(null); return; }
            BehaviorDebugSnapshot snapshot = _selection.Capture();
            if (snapshot == null) { Cycle(1); snapshot = _selection.Capture(); }
            _view?.Localize(); _view?.Show(snapshot);
        }
    }
}
