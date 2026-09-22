using System;
using System.Linq;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class BehaviorDebugView : IDisposable
    {
        readonly VisualElement _root;
        readonly BehaviorGraphElement _hfsm = new BehaviorGraphElement();
        readonly BehaviorGraphElement _tree = new BehaviorGraphElement();
        readonly Button _previous, _next, _pause, _interrupt, _attack;
        readonly Label _name, _summary, _state, _trace, _guards;
        public event Action<int> Cycle;
        public event Action<BehaviorControl> Control;
        public BehaviorDebugView(VisualElement root)
        {
            _root = root;
            root.Q("hfsmGraph").Add(_hfsm); root.Q("treeGraph").Add(_tree);
            _name = root.Q<Label>("actorName"); _summary = root.Q<Label>("summary"); _state = root.Q<Label>("state");
            _trace = root.Q<Label>("trace"); _guards = root.Q<Label>("guards");
            _previous = root.Q<Button>("previous"); _next = root.Q<Button>("next"); _pause = root.Q<Button>("pause");
            _interrupt = root.Q<Button>("interrupt"); _attack = root.Q<Button>("attack");
            _previous.clicked += Previous; _next.clicked += Next; _pause.clicked += Pause; _interrupt.clicked += Interrupt; _attack.clicked += Attack;
            Localize();
        }
        public static string Text(string key)
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) { return key; }
            if (LocalizationSettings.SelectedLocale is UnityEngine.Localization.Pseudo.PseudoLocale pseudo)
            {
                string original = LocalizationSettings.StringDatabase.GetLocalizedString("ui", "behavior." + key,
                    LocalizationSettings.AvailableLocales.GetLocale("en"));
                return pseudo.GetPseudoString(original);
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString("ui", "behavior." + key);
        }
        public void Localize()
        {
            foreach (string key in new[] { "title", "hint", "previous", "next", "pause", "interrupt", "attack", "open", "close", "hfsmTitle", "treeTitle", "traceTitle" })
            {
                TextElement element = _root.Q<TextElement>(key);
                if (element != null) { element.text = Text(key); }
            }
        }
        public void Show(BehaviorDebugSnapshot snapshot)
        {
            _pause.SetEnabled(snapshot != null); _interrupt.SetEnabled(snapshot != null); _attack.SetEnabled(snapshot != null);
            if (snapshot == null)
            {
                _name.text = Text("unavailable"); _summary.text = _state.text = _trace.text = _guards.text = string.Empty;
                _hfsm.Show(null); _tree.Show(null); return;
            }
            _name.text = snapshot.Name + $"   [{snapshot.Handle.Generation}:{snapshot.Handle.Registration}]";
            _summary.text = snapshot.Summary;
            _state.text = $"{snapshot.Presentation.Semantic} | {(snapshot.Presentation.Paused ? Text("paused") : Text("running"))} | {snapshot.Presentation.ActionElapsedSeconds:F2}/{snapshot.Presentation.ActionDurationSeconds:F2}s | ({snapshot.Presentation.Position.x:F2}, {snapshot.Presentation.Position.y:F2})";
            _trace.text = string.Join("\n", snapshot.Trace.Skip(Math.Max(0, snapshot.Trace.Count - 8)));
            _guards.text = string.Join("\n", snapshot.Tree.Where(n => n.Detail.Contains("gate ") && !n.Detail.EndsWith("unobserved")).Select(n => n.Id + "  " + n.Detail));
            _hfsm.Show(snapshot.Hfsm); _tree.Show(snapshot.Tree);
        }
        void Previous() { Cycle?.Invoke(-1); }
        void Next() { Cycle?.Invoke(1); }
        void Pause() { Control?.Invoke(BehaviorControl.TogglePause); }
        void Interrupt() { Control?.Invoke(BehaviorControl.Interrupt); }
        void Attack() { Control?.Invoke(BehaviorControl.RequestAttack); }
        public void Dispose()
        {
            _previous.clicked -= Previous; _next.clicked -= Next; _pause.clicked -= Pause;
            _interrupt.clicked -= Interrupt; _attack.clicked -= Attack;
            _hfsm.RemoveFromHierarchy(); _tree.RemoveFromHierarchy(); Cycle = null; Control = null;
        }
    }

    // 查询与租约的共同所有者；无 Session / 对象失效时释放旧核心引用。
    public sealed class BehaviorDebugSelection : IDisposable
    {
        IArchitecture _owner;
        IDisposable _lease;
        BehaviorHandle _handle;
        public BehaviorHandle Handle => _handle;
        public void Select(IArchitecture owner, BehaviorHandle handle)
        {
            if (ReferenceEquals(owner, _owner) && handle.Equals(_handle)) { return; }
            Dispose(); _owner = owner; _handle = handle;
            if (_owner != null && handle.IsValid) { _lease = owner.SendCommand(new AcquireBehaviorDiagnosticsCommand(handle)); }
        }
        public BehaviorDebugSnapshot Capture()
        {
            if (_owner == null || !GameArchitectureProvider.TryGetCurrent(out IArchitecture current) || !ReferenceEquals(current, _owner))
            {
                Dispose(); return null;
            }
            BehaviorDebugSnapshot snapshot = _owner?.SendQuery(new GetBehaviorSnapshotQuery(_handle));
            if (snapshot == null) { Dispose(); }
            return snapshot;
        }
        public void Cycle(IArchitecture owner, int direction)
        {
            BehaviorHandle[] handles = owner?.SendQuery(new GetBehaviorHandlesQuery());
            if (handles == null || handles.Length == 0) { Dispose(); return; }
            int current = Array.IndexOf(handles, _handle);
            int index = current < 0 ? 0 : (current + direction + handles.Length) % handles.Length;
            Select(owner, handles[index]);
        }
        public void Control(BehaviorControl control) { _owner?.SendCommand(new ControlBehaviorCommand(_handle, control)); }
        public void Dispose() { _lease?.Dispose(); _lease = null; _owner = null; _handle = default; }
    }
}
