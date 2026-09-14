using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    // 提供方必须基于已验证的存档数据证明所有权；不得在预检时修改运行中的 Session。
    public interface IStatusSourceRestoreProvider
    {
        StatusSourceKind Kind { get; }
        void Validate(StatusActorDto actor, StatusLayerDto layer, SavePayloadDto payload,
            ContentCatalog catalog, List<DtoMapIssue> issues);
    }

    internal sealed class EquipmentStatusSourceRestoreProvider : IStatusSourceRestoreProvider
    {
        public StatusSourceKind Kind => StatusSourceKind.Equipment;
        public void Validate(StatusActorDto actor, StatusLayerDto layer, SavePayloadDto payload,
            ContentCatalog catalog, List<DtoMapIssue> issues)
        {
            if (layer.SourceKind != StatusSourceKind.Equipment || layer.SourceActorKey != actor.ActorKey
                || layer.SourceRegistration != actor.Registration || actor.ActorKey != "player:" + payload.Profile.PlayerId)
            {
                throw new ArgumentException("来源维持状态缺少可恢复的提供方");
            }
            bool found = payload.Profile.Equipment.SelectMany(loadout => loadout.Entries).Any(entry =>
                layer.SourceKey == entry.ItemInstanceId + ":" + layer.Rules.Id);
            if (!found) { throw new ArgumentException("装备维持状态不属于已装备物品"); }
            if (catalog == null) { return; }
            ItemInstanceDto item = payload.Items.FirstOrDefault(value => layer.SourceKey == value.InstanceId + ":" + layer.Rules.Id);
            if (item == null) { throw new ArgumentException("装备维持来源物品缺失"); }
            ItemBaseDefinition definition = RuntimeStateMapper.Resolve<ItemBaseDefinition>(item.BaseContentId, catalog, "statuses.provider", issues);
            if (definition?.ProvidedStatus == null || definition.ProvidedStatus.Id != layer.Rules.Id)
            {
                throw new ArgumentException("装备已不再提供此状态");
            }
        }
    }
}
