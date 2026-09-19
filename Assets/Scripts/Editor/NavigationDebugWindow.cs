using DarkFlare;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class NavigationDebugWindow : OdinEditorWindow, IController
    {
        [SerializeField, LabelText("选中怪物")] MonsterController _monster;
        [SerializeField, LabelText("寻路策略")] NavigationMode _mode;
        [SerializeField, LabelText("固定点目标")] Vector2 _goal;
        [SerializeField, LabelText("阻挡稳定 ID")] string _stamp = "debug_wall";
        [SerializeField, LabelText("阻挡中心")] Vector2 _center = new(10, 10);
        [SerializeField, LabelText("阻挡尺寸")] Vector2 _size = new(1, 4);
        [ShowInInspector, ReadOnly, LabelText("统计"), ShowIf(nameof(HasSession))]
        NavigationModel Metrics => HasSession ? this.SendQuery(new GetNavigationDebugQuery()) : null;
        [ShowInInspector, ReadOnly, LabelText("单位状态"), ShowIf(nameof(HasSession))]
        string Status => !HasSession ? "未运行" : _monster == null ? "未选择" : this.SendQuery(new GetNavigationStatusQuery(_monster)).ToString();
        [ShowInInspector, ReadOnly, LabelText("最近阻挡操作")]
        string StampResult => _ticket?.Status.ToString() ?? "无";
        NavigationStampTicket _ticket;
        bool HasSession => GameArchitectureProvider.TryGetCurrent(out _);
        NavigationMap Map => HasSession ? this.SendQuery(new GetNavigationMapQuery()) : null;
        [MenuItem("DarkFlare/调试/批量导航")]
        static void Open() { GetWindow<NavigationDebugWindow>("批量导航").Show(); }
        public IArchitecture GetArchitecture() { return GameArchitectureProvider.TryGetCurrent(out IArchitecture value) ? value : null; }
        [Button("追踪玩家"), EnableIf(nameof(HasSession))]
        void Player() { Set(new NavigationIntentDto { Mode = _mode }); }
        [Button("前往固定点"), EnableIf(nameof(HasSession))]
        void Point() { if (Map != null) { Set(new NavigationIntentDto { Mode = _mode, Target = NavigationTargetKind.Point, MapId = Map.MapId, Point = new Vector3Dto { X = _goal.x, Y = _goal.y } }); } }
        [Button("停止导航"), EnableIf(nameof(HasSession))]
        void Stop() { Set(new NavigationIntentDto { Target = NavigationTargetKind.None }); }
        void Set(NavigationIntentDto intent)
        { if (Map != null && _monster != null) { this.SendCommand(new SetNavigationIntentCommand(_monster, intent, Map.Handle(_monster))); } }
        [Button("添加临时阻挡"), EnableIf(nameof(HasSession))]
        void AddStamp()
        {
            if (Map == null) { return; }
            _ticket = this.SendCommand(new ChangeNavigationStampCommand(Map.Generation, _stamp,
                new MapGeometryRect(_center.x - _size.x / 2, _center.y - _size.y / 2, _center.x + _size.x / 2, _center.y + _size.y / 2)));
        }
        [Button("移除临时阻挡"), EnableIf(nameof(HasSession))]
        void RemoveStamp() { if (Map != null) { _ticket = this.SendCommand(new ChangeNavigationStampCommand(Map.Generation, _stamp, default, true)); } }
        [Button("补充至 500 怪物（480 流场 / 20 A*）"), EnableIf(nameof(HasSession))]
        void Spawn500() { this.SendCommand(new SpawnNavigationStressCommand(500)); }
        [Button("补充至 1000 怪物（950 流场 / 50 A*）"), EnableIf(nameof(HasSession))]
        void Spawn1000() { this.SendCommand(new SpawnNavigationStressCommand(1000)); }
        protected override void OnEnable() { base.OnEnable(); SceneView.duringSceneGui += DrawScene; }
        protected override void OnDisable() { SceneView.duringSceneGui -= DrawScene; base.OnDisable(); }
        void DrawScene(SceneView scene)
        {
            NavigationMap map = Map;
            if (_monster == null || map == null || map.Handle(_monster).Serial == 0) { return; }
            Vector3[] points = map.CaptureRoute(_monster);
            Handles.color = Color.cyan;
            if (points.Length > 1) { Handles.DrawAAPolyLine(2, points); }
            Handles.DrawWireDisc(_monster.transform.position, Vector3.forward, _monster.NavigationRadius);
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(_monster.transform.position, Vector3.forward, _monster.Definition.SeparationRadius);
            Vector3 position = _monster.transform.position;
            Handles.color = Color.green;
            Handles.DrawWireCube(new Vector3(Mathf.Floor(position.x) + .5f, Mathf.Floor(position.y) + .5f, position.z), new Vector3(1, 1, 0));
            Handles.Label(_monster.transform.position + Vector3.up, Status + " / grid " + map.Revision);
        }
    }
}
