namespace DarkFlare
{
    // 核心只依赖事务合同，不依赖 Unity 对象或外部事件订阅顺序。
    internal interface IStatusParticipant
    {
        StatusMutation Normalize(StatusTargetId target, StatusMutation mutation);
        IStatusProjection Prepare(StatusTargetSnapshot snapshot);
        void Tick(StatusTick tick);
    }

    internal interface IStatusProjection
    {
        bool IsCurrent { get; }
        void Apply();
        void Publish();
    }
}
