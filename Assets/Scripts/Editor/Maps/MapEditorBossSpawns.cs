using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed partial class MapEditorWindow
    {
        [SerializeField, TabGroup("Boss 点位"), LabelText("显示点位")] bool _showBossSpawns = true;
        [SerializeField, TabGroup("Boss 点位"), LabelText("锁定点位")] bool _lockBossSpawns;
        [SerializeField, TabGroup("Boss 点位"), LabelText("点位 ID")] string _bossSpawnId = "guardian";
        [SerializeField, TabGroup("Boss 点位"), LabelText("Boss 定义"), AssetsOnly] MonsterDefinition _bossMonster;
        [SerializeField, TabGroup("Boss 点位"), LabelText("位置（局部世界）")] Vector2 _bossPosition;
        [SerializeField, TabGroup("Boss 点位"), LabelText("朝向（单位向量）")] Vector2 _bossFacing = Vector2.right;
        string _selectedBossSpawn;

        [TabGroup("Boss 点位"), Button("应用 Boss 点位")]
        void UpsertBossSpawn()
        {
            Execute(() =>
            {
                if (_lockBossSpawns) { throw new InvalidOperationException("Boss 点位层已锁定"); }
                if (_bossMonster == null || _bossMonster.BossBehavior == null) { throw new InvalidOperationException("需要 Boss 怪物定义"); }
                if (_bossFacing.sqrMagnitude < .0001f) { throw new InvalidOperationException("朝向不能为空"); }
                Vector2 facing = _bossFacing.normalized;
                Apply(new JObject
                {
                    ["type"] = "UpsertBossSpawn", ["record"] = new JObject
                    {
                        ["spawnId"] = _bossSpawnId, ["monsterDefinitionId"] = "monster:" + _bossMonster.Id,
                        ["position"] = new JObject { ["x"] = _bossPosition.x, ["y"] = _bossPosition.y },
                        ["facing"] = new JObject { ["x"] = facing.x, ["y"] = facing.y }
                    }
                });
                _selectedBossSpawn = _bossSpawnId;
            });
        }

        [TabGroup("Boss 点位"), Button("删除 Boss 点位")]
        void RemoveBossSpawn()
        {
            Execute(() =>
            {
                if (_lockBossSpawns) { throw new InvalidOperationException("Boss 点位层已锁定"); }
                Apply(new JObject { ["type"] = "RemoveBossSpawn", ["id"] = _bossSpawnId });
                _selectedBossSpawn = null;
            });
        }

        bool SelectBossSpawn(Vector2 world)
        {
            if (!_showBossSpawns || _lockBossSpawns) { return false; }
            MapBossSpawnRecord boss = _viewDocument.BossSpawns.FirstOrDefault(item => Vector2.Distance(world, BossWorld(item)) < .5f);
            if (boss == null) { return false; }
            _selectedBossSpawn = _bossSpawnId = boss.SpawnId;
            _bossMonster = _lookup.Resolve<MonsterDefinition>(ContentId.Parse(boss.MonsterDefinitionId));
            _bossPosition = new Vector2((float)boss.Position.X, (float)boss.Position.Y);
            _bossFacing = new Vector2((float)boss.Facing.X, (float)boss.Facing.Y);
            Repaint(); return true;
        }

        Vector2 BossWorld(MapBossSpawnRecord boss)
        {
            return new Vector2((float)(_session.Current.OriginX + boss.Position.X), (float)(_session.Current.OriginY + boss.Position.Y));
        }

        void DrawBossSpawns()
        {
            if (!_showBossSpawns) { return; }
            foreach (MapBossSpawnRecord boss in _viewDocument.BossSpawns)
            {
                Vector2 world = BossWorld(boss);
                Handles.color = boss.SpawnId == _selectedBossSpawn ? Color.yellow : Color.red;
                Handles.DrawWireDisc(world, Vector3.forward, .5f);
                Handles.DrawLine(world, world + new Vector2((float)boss.Facing.X, (float)boss.Facing.Y));
                Handles.Label(world, "Boss · " + boss.SpawnId);
                if (boss.SpawnId != _selectedBossSpawn || _lockBossSpawns || _tool != MapPaintTool.Select) { continue; }
                EditorGUI.BeginChangeCheck();
                Vector3 next = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    _bossPosition = (Vector2)next - new Vector2((float)_session.Current.OriginX, (float)_session.Current.OriginY);
                }
                if (Event.current.rawType == EventType.MouseUp && Vector2.Distance(_bossPosition, new Vector2((float)boss.Position.X, (float)boss.Position.Y)) > .0001f)
                { UpsertBossSpawn(); }
            }
        }
    }
}
