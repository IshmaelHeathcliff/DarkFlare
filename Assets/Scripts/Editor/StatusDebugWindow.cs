using System;
using DarkFlare;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class StatusDebugWindow : OdinEditorWindow, IController
    {
        [SerializeField, LabelText("目标角色")]
        CombatActor _target;
        [SerializeField, LabelText("状态定义")]
        StatusDefinition _definition;
        [SerializeField, LabelText("来源类型")]
        StatusSourceKind _sourceKind = StatusSourceKind.Mechanism;
        [SerializeField, LabelText("来源键")]
        string _sourceKey = "debug";
        [SerializeField, LabelText("层数"), MinValue(1)]
        int _count = 1;
        [SerializeField, LabelText("推进秒数"), MinValue(0)]
        double _seconds = 1;
        [ShowInInspector, ReadOnly, LabelText("最近结果")]
        string _result;
        [ShowInInspector, ReadOnly, LabelText("逐层快照"), ShowIf(nameof(HasSession))]
        StatusTargetSnapshot Snapshot => this.SendQuery(new GetActorStatusQuery(_target));
        bool HasSession => GameArchitectureProvider.TryGetCurrent(out _);

        [MenuItem("DarkFlare/调试/状态系统")]
        static void Open() { GetWindow<StatusDebugWindow>("状态系统").Show(); }

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.TryGetCurrent(out IArchitecture architecture) ? architecture : null;
        }

        [Button("施加状态"), EnableIf(nameof(HasSession))]
        void Apply()
        {
            Run(() =>
            {
                if (_target == null || _definition == null) { throw new ArgumentException("请先选择目标和状态"); }
                StatusTargetSnapshot snapshot = Snapshot;
                var source = new StatusSource(_sourceKind, _sourceKey,
                    _definition.CreateRules().Lifetime == StatusLifetime.SourceOwned ? snapshot.Target : default);
                StatusApplication application = StatusApplication.Capture(_definition, source, _target.Stats, _target.Modifiers,
                    new DamageSourceSnapshot("debug", _target.Team == ActorTeam.Player ? ActorTeam.Monster : ActorTeam.Player), 0);
                StatusOperation operation = this.SendCommand(new ChangeStatusesCommand(snapshot.Target,
                    new[] { StatusMutation.Apply(application.Rules, source, application.Effects, _count) }, snapshot.Version));
                return operation.Result?.Code.ToString() ?? "操作已排队";
            });
        }

        [Button("消耗指定状态层"), EnableIf(nameof(HasSession))]
        void Consume()
        {
            Run(() => this.SendCommand(new ChangeStatusesCommand(Snapshot.Target,
                new[] { StatusMutation.Consume(_definition.Id, _count) }, Snapshot.Version)).Result?.Code.ToString() ?? "操作已排队");
        }

        [Button("驱散全部异常"), EnableIf(nameof(HasSession))]
        void Dispel()
        {
            Run(() => this.SendCommand(new ChangeStatusesCommand(Snapshot.Target,
                new[] { StatusMutation.Dispel(new StatusFilter(category: StatusCategory.Ailment)) })).Result?.Code.ToString() ?? "操作已排队");
        }

        [Button("推进逻辑时间"), EnableIf(nameof(HasSession))]
        void Advance()
        {
            Run(() =>
            {
                StatusAdvanceResult result = this.SendCommand(new AdvanceStatusesCommand(_seconds));
                return $"{result.Code} / 时间 {result.Time:0.###} / 积压 {result.HasPending}";
            });
        }

        void Run(Func<string> action)
        {
            try { _result = action(); }
            catch (Exception exception) { _result = exception.Message; }
            Repaint();
        }
    }
}
