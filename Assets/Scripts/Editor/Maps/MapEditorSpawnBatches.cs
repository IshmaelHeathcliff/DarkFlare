using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed partial class MapEditorWindow
    {
        [SerializeField, TabGroup("刷怪批次"), LabelText("显示触发范围")] bool _showSpawnBatches = true;
        [SerializeField, TabGroup("刷怪批次"), LabelText("批次 ID")] string _spawnBatchId = "initial";
        [SerializeField, TabGroup("刷怪批次"), LabelText("出生区域"), ValueDropdown(nameof(SpawnRegionChoices))] string _spawnRegionId;
        [SerializeField, TabGroup("刷怪批次"), LabelText("数量"), MinValue(1), MaxValue(1024)] int _spawnBatchCount = 4;
        [SerializeField, TabGroup("刷怪批次"), LabelText("激活方式")] MapSpawnActivation _spawnActivation;
        [SerializeField, TabGroup("刷怪批次"), LabelText("触发中心（局部世界）")] Vector2 _spawnTriggerCenter;
        [SerializeField, TabGroup("刷怪批次"), LabelText("触发半径"), MinValue(0)] float _spawnTriggerRadius = 6f;
        string _selectedSpawnBatch;

        IEnumerable<string> SpawnRegionChoices => _viewDocument?.Regions
            .Where(region => region.SpawnDefinitionId != null).Select(region => region.RegionId)
            ?? Array.Empty<string>();

        [TabGroup("刷怪批次"), Button("应用刷怪批次")]
        void UpsertSpawnBatch()
        {
            Execute(() =>
            {
                RequireSession();
                Apply(new JObject
                {
                    ["type"] = "UpsertSpawnBatch",
                    ["record"] = new JObject
                    {
                        ["batchId"] = _spawnBatchId,
                        ["regionId"] = _spawnRegionId,
                        ["count"] = _spawnBatchCount,
                        ["activation"] = _spawnActivation.ToString(),
                        ["triggerCenter"] = new JObject
                        {
                            ["x"] = _spawnActivation == MapSpawnActivation.OnEnter ? 0 : _spawnTriggerCenter.x,
                            ["y"] = _spawnActivation == MapSpawnActivation.OnEnter ? 0 : _spawnTriggerCenter.y
                        },
                        ["triggerRadius"] = _spawnActivation == MapSpawnActivation.OnEnter ? 0 : _spawnTriggerRadius
                    }
                });
                _selectedSpawnBatch = _spawnBatchId;
            });
        }

        [TabGroup("刷怪批次"), Button("删除刷怪批次")]
        void RemoveSpawnBatch()
        {
            Execute(() =>
            {
                RequireSession();
                Apply(new JObject { ["type"] = "RemoveSpawnBatch", ["id"] = _spawnBatchId });
                _selectedSpawnBatch = null;
            });
        }

        bool SelectSpawnBatch(Vector2 world)
        {
            if (!_showSpawnBatches || _viewDocument == null) { return false; }
            MapSpawnBatchRecord batch = _viewDocument.SpawnBatches.FirstOrDefault(item =>
                item.Activation == MapSpawnActivation.PlayerRange
                && Vector2.Distance(world, SpawnTriggerWorld(item)) < .5f);
            if (batch == null) { return false; }
            _selectedSpawnBatch = _spawnBatchId = batch.BatchId;
            _spawnRegionId = batch.RegionId;
            _spawnBatchCount = batch.Count;
            _spawnActivation = batch.Activation;
            _spawnTriggerCenter = new Vector2((float)batch.TriggerCenter.X, (float)batch.TriggerCenter.Y);
            _spawnTriggerRadius = (float)batch.TriggerRadius;
            Repaint();
            return true;
        }

        Vector2 SpawnTriggerWorld(MapSpawnBatchRecord batch)
        {
            return new Vector2((float)(_session.Current.OriginX + batch.TriggerCenter.X),
                (float)(_session.Current.OriginY + batch.TriggerCenter.Y));
        }

        void DrawSpawnBatches()
        {
            if (!_showSpawnBatches) { return; }
            foreach (MapSpawnBatchRecord batch in _viewDocument.SpawnBatches)
            {
                if (batch.Activation != MapSpawnActivation.PlayerRange) { continue; }
                Vector2 center = SpawnTriggerWorld(batch);
                Handles.color = batch.BatchId == _selectedSpawnBatch ? Color.yellow : Color.cyan;
                Handles.DrawWireDisc(center, Vector3.forward, (float)batch.TriggerRadius);
                Handles.DrawSolidDisc(center, Vector3.forward, .5f);
                Handles.Label(center + Vector2.one, "刷怪触发点 · " + batch.BatchId + " × " + batch.Count);
            }
        }
    }
}
