using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public sealed class ItemUseSystem : AbstractSystem
    {
        InventoryModel _inventory;
        StatusSystem _statuses;
        LifecycleScope _scope;
        long _issued;
        long _completed;
        bool _busy;
        bool _disposed;

        protected override void OnInit()
        {
            _inventory = this.GetModel<InventoryModel>();
            _statuses = this.GetSystem<StatusSystem>();
            GameArchitectureProvider.TryGetOwnerScope(out _scope);
        }

        protected override void OnDeinit() { _disposed = true; }

        public ItemUseRequest CreateRequest(CombatActor actor, ItemInstance item)
        {
            if (_disposed || _issued == long.MaxValue) { return null; }
            return new ItemUseRequest(this, actor, item, _statuses.GetTarget(actor), ++_issued);
        }

        ItemUseCode Validate(ItemUseRequest request)
        {
            if (_disposed || _scope?.CanAcceptWork != true
                || !GameArchitectureProvider.TryGetCurrent(out IArchitecture current) || current.GetSystem<ItemUseSystem>() != this)
            {
                return ItemUseCode.Unavailable;
            }
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.CurrentSession != null)
            {
                if (host.CurrentSession.Architecture != current || !host.CurrentSession.SceneScope.CanAcceptWork
                    || host.CurrentSession.State != GameSessionState.Running || host.ApplicationShell?.BlocksGameplay == true
                    || host.Input.IsSuspended || host.SceneFlow.State != GameFlowState.InGame && host.SceneFlow.State != GameFlowState.Paused)
                {
                    return ItemUseCode.Unavailable;
                }
            }
            if (request == null || request.Owner != this || !request.Target.IsValid || request.Target != _statuses.GetTarget(request.Actor))
            {
                return ItemUseCode.InvalidTarget;
            }
            if (request.OperationId <= _completed) { return ItemUseCode.AlreadyUsed; }
            CombatActor player = this.GetModel<CombatModel>().GetActorsByTeam(ActorTeam.Player).FirstOrDefault(actor => actor != null);
            if (player != request.Actor || !player.IsConfigured || !player.isActiveAndEnabled || !player.IsAlive
                || (player.BlockedActions & StatusActionBlock.UseItem) != 0) { return ItemUseCode.ActionBlocked; }
            if (request.Item == null || !_inventory.Grid.Placements.ContainsKey(request.Item)) { return ItemUseCode.NotOwned; }
            if (request.Item.Revision != request.Revision) { return ItemUseCode.StaleVersion; }
            return ItemUseCode.Success;
        }

        public ItemUsePreparation Prepare(ItemUseRequest request)
        {
            ItemUsePreparation Reject(ItemUseCode code, StatusResultCode? statusCode = null)
            {
                return new ItemUsePreparation(request, new ItemUseResult(code, statusCode: statusCode));
            }
            if (_busy || _statuses.IsClockHeld || !_statuses.Store.IsQuiescent) { return Reject(ItemUseCode.Busy); }
            ItemUseCode valid = Validate(request);
            if (valid != ItemUseCode.Success) { return Reject(valid); }
            ItemInstance item = request.Item;
            ItemUseDefinition definition = item.BaseDefinition?.UseDefinition;
            if (definition == null) { return Reject(ItemUseCode.NotUsable); }
            if (definition.ValidateConfiguration(item.BaseDefinition).Count > 0) { return Reject(ItemUseCode.InvalidConfiguration); }
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.CurrentSession != null && host.ContentCatalog != null
                && (!host.ContentCatalog.Contains(item.BaseDefinition)
                    || definition.Effects.Any(effect => effect.Status != null && !host.ContentCatalog.Contains(effect.Status))))
            {
                return Reject(ItemUseCode.InvalidConfiguration);
            }
            int quantity = definition.ConsumeCount;
            if (quantity > item.Quantity) { return Reject(ItemUseCode.InsufficientQuantity); }
            var consumption = new Consumption(_inventory, item, quantity);
            var mutations = new List<StatusMutation>();
            StatusPreparation status = _statuses.Store.Prepare(request.Target, mutations);
            CombatResourceSnapshot resources = request.Actor.Resources;
            CombatResourceSnapshot before = resources;
            var source = new StatusSource(StatusSourceKind.Consumable, item.InstanceId + ":use:" + request.OperationId, request.Target);
            var damageSource = new DamageSourceSnapshot(request.Target.ActorKey, request.Actor.Team, itemId: item.InstanceId,
                tags: new CombatTagContext(sourceActorTags: request.Actor.Tags, targetActorTags: request.Actor.Tags, legacyTags: request.Actor.Tags));
            try
            {
                int index = 0;
                foreach (ItemUseEffect effect in definition.Effects)
                {
                    if (effect.Kind == ItemUseEffectKind.Heal || effect.Kind == ItemUseEffectKind.RestoreMana)
                    {
                        resources = ItemResourceRecovery.Apply(resources, effect.Kind == ItemUseEffectKind.Heal, effect.Amount, effect.RecoveryMode);
                    }
                    else
                    {
                        StatusMutation mutation;
                        if (effect.Kind == ItemUseEffectKind.ApplyStatus)
                        {
                            StatusApplication application = StatusApplication.Capture(effect.Status, source, request.Actor.Stats,
                                request.Actor.Modifiers, damageSource, unchecked(item.Seed + index));
                            mutation = StatusMutation.Apply(application.Rules, source, application.Effects, effect.Count);
                        }
                        else
                        {
                            mutation = StatusMutation.Dispel(new StatusFilter(effect.Status?.Id, StatusCategory.Ailment, effect.RequiredTags), effect.Count);
                        }
                        StatusPreparation previous = status;
                        mutations.Add(mutation);
                        status = _statuses.Store.Prepare(request.Target, mutations);
                        if (status.Preview.Code == StatusResultCode.Resisted)
                        {
                            mutations.RemoveAt(mutations.Count - 1);
                            status = previous;
                            index++;
                            continue;
                        }
                        if (!status.Preview.Succeeded) { return Reject(ItemUseCode.StatusRejected, status.Preview.Code); }
                        CombatActor.ActorEffectPreparation candidate = _statuses.GetEffectPreparation(status);
                        if (candidate == null) { return Reject(ItemUseCode.InvalidTarget); }
                        resources = candidate.ProjectResources(resources);
                    }
                    index++;
                }
                if (!status.Preview.Succeeded) { return Reject(ItemUseCode.StatusRejected, status.Preview.Code); }
                bool changed = !resources.Equals(before) || status.Preview.Code == StatusResultCode.Success;
                var preview = new ItemUseResult(changed ? ItemUseCode.Success : ItemUseCode.NoChange, resources,
                    status.Preview.Snapshot, changed ? quantity : 0);
                if (!changed) { return new ItemUsePreparation(request, preview); }
                CombatActor.ActorEffectPreparation finalEffects = _statuses.GetEffectPreparation(status);
                if (finalEffects == null) { return Reject(ItemUseCode.InvalidTarget); }
                finalEffects.ResourceOverride = resources;
                status.Projection = new UseProjection(this, request, status.Projection, consumption, preview);
                status.CommitExternalProjection = true;
                return new ItemUsePreparation(request, preview, status);
            }
            catch (ArgumentException) { return Reject(ItemUseCode.InvalidConfiguration); }
        }

        public ItemUseResult Commit(ItemUsePreparation preparation)
        {
            if (_busy) { return new ItemUseResult(ItemUseCode.Busy); }
            if (preparation == null || preparation.Used) { return new ItemUseResult(ItemUseCode.AlreadyUsed); }
            preparation.Used = true;
            if (!preparation.Preview.Succeeded) { return preparation.Preview; }
            ItemUseCode valid = Validate(preparation.Request);
            if (valid != ItemUseCode.Success) { return new ItemUseResult(valid); }
            if (_statuses.IsClockHeld || !_statuses.Store.IsQuiescent) { return new ItemUseResult(ItemUseCode.Busy); }
            _busy = true;
            try
            {
                using (StatusCommit commit = _statuses.Store.Commit(preparation.StatusPreparation))
                {
                    if (!commit.Result.Succeeded || !commit.Publish())
                    {
                        return new ItemUseResult(ItemUseCode.StaleVersion, statusCode: commit.Result.Code);
                    }
                }
                return preparation.Preview;
            }
            finally { _busy = false; }
        }

        sealed class UseProjection : IStatusProjection
        {
            readonly ItemUseSystem _owner;
            readonly ItemUseRequest _request;
            readonly IStatusProjection _status;
            readonly Consumption _consumption;
            readonly ItemUseResult _result;

            internal UseProjection(ItemUseSystem owner, ItemUseRequest request, IStatusProjection status, Consumption consumption, ItemUseResult result)
            {
                _owner = owner; _request = request; _status = status; _consumption = consumption; _result = result;
            }

            public bool IsCurrent => _status.IsCurrent && _consumption.IsCurrent && _owner.Validate(_request) == ItemUseCode.Success;
            public void Apply()
            {
                _status.Apply();
                _consumption.Apply();
                _owner._completed = _request.OperationId;
            }
            public void Publish()
            {
                try { _status.Publish(); }
                finally
                {
                    try { _consumption.Publish(); }
                    finally { _owner.SendEvent(new ItemUsedEvent(_request, _result)); }
                }
            }
        }

        sealed class Consumption
        {
            readonly InventoryModel _model;
            readonly InventoryGrid _grid;
            readonly ItemInstance _item;
            readonly int _quantity;
            readonly int _before;
            readonly int _revision;
            int _gold;
            readonly RectInt _placement;
            readonly ItemUseDefinition _definition;
            readonly int _quality;
            readonly float _durability;

            internal Consumption(InventoryModel model, ItemInstance item, int quantity)
            {
                _model = model; _grid = model.Grid; _item = item; _quantity = quantity; _before = item.Quantity;
                _revision = item.Revision; _placement = _grid.Placements[item]; _definition = item.BaseDefinition.UseDefinition;
                _quality = item.Quality; _durability = item.Durability;
            }
            internal bool IsCurrent => _model.Grid == _grid && _item.Revision == _revision && _item.Quantity == _before
                && _item.Quality == _quality && _item.Durability == _durability
                && _grid.Placements.TryGetValue(_item, out RectInt placement) && placement == _placement
                && _item.BaseDefinition.UseDefinition == _definition && _definition != null
                && _definition.ConsumeCount == _quantity && (_quantity == 0 || _item.BaseDefinition.IsConsumable)
                && _item.BaseDefinition.MaxStackSize >= _before;

            internal void Apply()
            {
                if (_quantity == 0) { return; }
                _gold = _model.Gold;
                if (_quantity == _before) { _model.RemoveItemWithoutEvents(_item); }
                else { _item.TrySetQuantity(_before - _quantity); }
            }
            internal void Publish()
            {
                if (_quantity == 0) { return; }
                _model.NotifyItemChanged(_item, _quantity == _before ? InventoryChangeType.Removed : InventoryChangeType.Added);
                _model.NotifyGoldChanged(_gold);
            }
        }
    }
}
