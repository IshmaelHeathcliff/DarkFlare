namespace DarkFlare
{
    public sealed class GetStatusSnapshotQuery : AbstractQuery<StatusTargetSnapshot>
    {
        readonly StatusTargetId _target;

        public GetStatusSnapshotQuery(StatusTargetId target)
        {
            _target = target;
        }

        protected override StatusTargetSnapshot OnDo()
        {
            return this.GetSystem<StatusSystem>().GetStatusSnapshot(_target);
        }
    }
}
