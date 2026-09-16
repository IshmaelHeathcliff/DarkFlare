using System;

namespace DarkFlare
{
    public enum ItemUseCode
    {
        Success, NoChange, Unavailable, InvalidTarget, NotOwned, NotUsable, InvalidConfiguration,
        InsufficientQuantity, ActionBlocked, StaleVersion, AlreadyUsed, Busy, StatusRejected
    }

    public sealed class ItemUseRequest
    {
        internal ItemUseSystem Owner { get; }
        internal CombatActor Actor { get; }
        internal ItemInstance Item { get; }
        internal int Revision { get; }
        public StatusTargetId Target { get; }
        public string ItemId => Item?.InstanceId;
        public long OperationId { get; }

        internal ItemUseRequest(ItemUseSystem owner, CombatActor actor, ItemInstance item, StatusTargetId target, long operationId)
        {
            Owner = owner; Actor = actor; Item = item; Target = target; OperationId = operationId;
            Revision = item?.Revision ?? -1;
        }
    }

    public sealed class ItemUseResult
    {
        public ItemUseCode Code { get; }
        public bool Succeeded => Code == ItemUseCode.Success;
        public CombatResourceSnapshot Resources { get; }
        public StatusTargetSnapshot Statuses { get; }
        public int ConsumeCount { get; }
        public StatusResultCode? StatusCode { get; }

        internal ItemUseResult(ItemUseCode code, CombatResourceSnapshot resources = default,
            StatusTargetSnapshot statuses = null, int consumeCount = 0, StatusResultCode? statusCode = null)
        {
            Code = code; Resources = resources; Statuses = statuses; ConsumeCount = consumeCount; StatusCode = statusCode;
        }
    }

    public sealed class ItemUsePreparation : IDisposable
    {
        internal ItemUseRequest Request { get; }
        internal StatusPreparation StatusPreparation { get; }
        internal bool Used { get; set; }
        public ItemUseResult Preview { get; }

        internal ItemUsePreparation(ItemUseRequest request, ItemUseResult preview, StatusPreparation status = null)
        {
            Request = request; Preview = preview; StatusPreparation = status;
        }

        public void Dispose()
        {
            Used = true;
        }
    }

    public readonly struct ItemUsedEvent
    {
        public ItemUseRequest Request { get; }
        public ItemUseResult Result { get; }
        public ItemUsedEvent(ItemUseRequest request, ItemUseResult result) { Request = request; Result = result; }
    }

    public sealed class CreateItemUseRequestCommand : AbstractCommand<ItemUseRequest>
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;
        public CreateItemUseRequestCommand(CombatActor actor, ItemInstance item) { _actor = actor; _item = item; }
        protected override ItemUseRequest OnExecute() { return this.GetSystem<ItemUseSystem>().CreateRequest(_actor, _item); }
    }

    public sealed class GetItemUseSnapshotQuery : AbstractQuery<ItemUseResult>
    {
        readonly ItemUseRequest _request;
        public GetItemUseSnapshotQuery(ItemUseRequest request) { _request = request; }
        protected override ItemUseResult OnDo()
        {
            using (ItemUsePreparation preparation = this.GetSystem<ItemUseSystem>().Prepare(_request)) { return preparation.Preview; }
        }
    }

    public sealed class UseItemCommand : AbstractCommand<ItemUseResult>
    {
        readonly ItemUseRequest _request;
        public UseItemCommand(ItemUseRequest request) { _request = request; }
        protected override ItemUseResult OnExecute()
        {
            ItemUseSystem system = this.GetSystem<ItemUseSystem>();
            using (ItemUsePreparation preparation = system.Prepare(_request)) { return system.Commit(preparation); }
        }
    }
}
