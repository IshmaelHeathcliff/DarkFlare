using System;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum NavigationProfile
    {
        [LabelText("小型（0.30）")] Compact,
        [LabelText("标准（0.36）")] Standard,
        [LabelText("大型（0.50）")] Large,
        [LabelText("巨型（0.75）")] Huge
    }
    public enum NavigationTargetKind { Player, Actor, Point, None }
    public sealed class NavigationIntentDto : IPersistenceDto
    {
        public NavigationMode? Mode { get; set; }
        public NavigationTargetKind Target { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
        public Vector3Dto Point { get; set; } = new();
        public float? StopDistance { get; set; }
        public NavigationIntentDto Copy()
        {
            return new NavigationIntentDto { Mode = Mode, Target = Target, ActorId = ActorId, MapId = MapId,
                Point = new Vector3Dto { X = Point.X, Y = Point.Y, Z = Point.Z }, StopDistance = StopDistance };
        }
        [Newtonsoft.Json.JsonIgnore]
        public bool IsValid => (!Mode.HasValue || Enum.IsDefined(typeof(NavigationMode), Mode.Value))
            && Enum.IsDefined(typeof(NavigationTargetKind), Target) && Point != null
            && float.IsFinite(Point.X) && float.IsFinite(Point.Y) && float.IsFinite(Point.Z)
            && (!StopDistance.HasValue || float.IsFinite(StopDistance.Value) && StopDistance.Value >= 0 && StopDistance.Value <= 10)
            && ActorId != null && MapId != null && ActorId.Length <= 160 && MapId.Length <= 160
            && (Target != NavigationTargetKind.Actor || !string.IsNullOrEmpty(ActorId) && !string.IsNullOrEmpty(MapId))
            && (Target != NavigationTargetKind.Point || !string.IsNullOrEmpty(MapId));
    }
    public sealed class NavigationModel : AbstractModel
    {
        public int FlowCacheHits { get; internal set; }
        public int FlowCacheMisses { get; internal set; }
        public long PeakNativeBytes => NavigationMemory.PeakBytes;
        public int TickSequence { get; internal set; }
        public int ResultSequence { get; internal set; }
        public long AllocationEvents { get; internal set; }
        public int AgentCount { get; internal set; }
        public int GridRevision { get; internal set; }
        public int PendingSearches { get; internal set; }
        public int SaturatedAgents { get; internal set; }
        public int CandidateVisits { get; internal set; }
        public double MainThreadMilliseconds { get; internal set; }
        public double SearchCollectMilliseconds { get; internal set; }
        public double AgentSampleMilliseconds { get; internal set; }
        public double SearchScheduleMilliseconds { get; internal set; }
        public double SteeringWaitMilliseconds { get; internal set; }
        public double LastPathLatencyMilliseconds { get; internal set; }
        public long NativeBytes => NavigationMemory.Bytes;
        protected override void OnInit() { }
    }
    public readonly struct NavigationAgentHandle
    {
        public int Session { get; }
        public int Map { get; }
        public int Serial { get; }
        public NavigationAgentHandle(int session, int map, int serial) { Session = session; Map = map; Serial = serial; }
    }
    public sealed class SetNavigationIntentCommand : AbstractCommand<bool>
    {
        readonly MonsterController _monster;
        readonly NavigationIntentDto _intent;
        readonly NavigationAgentHandle _expected;
        public SetNavigationIntentCommand(MonsterController monster, NavigationIntentDto intent, NavigationAgentHandle expected)
        { _monster = monster; _intent = intent; _expected = expected; }
        protected override bool OnExecute() { return this.GetSystem<NavigationSystem>().SetIntent(_monster, _intent, _expected); }
    }
    public sealed class RegisterNavigationAgentCommand : AbstractCommand
    {
        readonly MonsterController _monster;
        readonly bool _register;
        public RegisterNavigationAgentCommand(MonsterController monster, bool register) { _monster = monster; _register = register; }
        protected override void OnExecute()
        {
            NavigationSystem system = this.GetSystem<NavigationSystem>();
            if (_register) { system.Register(_monster); } else { system.Unregister(_monster); }
        }
    }
    public sealed class GetNavigationStatusQuery : AbstractQuery<NavigationStatus>
    {
        readonly MonsterController _monster;
        public GetNavigationStatusQuery(MonsterController monster) { _monster = monster; }
        protected override NavigationStatus OnDo() { return this.GetSystem<NavigationSystem>().Active?.Status(_monster) ?? NavigationStatus.Stale; }
    }
    public readonly struct NavigationIntentChangedEvent
    {
        public NavigationAgentHandle Handle { get; }
        public NavigationIntentChangedEvent(NavigationAgentHandle handle) { Handle = handle; }
    }
    public sealed class NavigationSystem : AbstractSystem
    {
        public NavigationMap Active { get; internal set; }
        protected override void OnInit() { }
        internal void Register(MonsterController monster) { Active?.Register(monster); }
        internal void Unregister(MonsterController monster) { Active?.Unregister(monster); }
        internal bool SetIntent(MonsterController monster, NavigationIntentDto intent, NavigationAgentHandle expected)
        {
            if (Active == null || !Active.Matches(monster, expected) || !Active.ValidateIntent(monster, intent)) { return false; }
            monster.RestoreNavigation(intent);
            this.SendEvent(new NavigationIntentChangedEvent(expected)); return true;
        }
        protected override void OnDeinit() { Active?.Suspend(); Active = null; }
    }
}
