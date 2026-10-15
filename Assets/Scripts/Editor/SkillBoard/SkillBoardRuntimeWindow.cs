using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare.Editor
{
    public sealed class SkillBoardRuntimeWindow : OdinEditorWindow, IController
    {
        [SerializeField, LabelText("生成道具配置")] ItemBaseDefinition _definition;
        [SerializeField, LabelText("生成种子")] int _seed = 42;
        [SerializeField, LabelText("操作")] SkillBoardOperation _operation;
        [SerializeField, LabelText("道具实例 ID")] string _itemId = string.Empty;
        [SerializeField, LabelText("锚点 q")] int _q = 16;
        [SerializeField, LabelText("锚点 r")] int _r;
        [SerializeField, Range(0, 5), LabelText("旋转")] int _rotation;
        [SerializeField, LabelText("插槽柴薪 ID")] string _woodId = string.Empty;
        [SerializeField, LabelText("插槽节点 ID（中心填 center）")] string _nodeId = string.Empty;
        [SerializeField, LabelText("替换所有连接（否则追加）")] bool _replaceConnections;
        [SerializeField, TextArea(2, 8), LabelText("显式连接：每行 柴薪A 节点A 柴薪B 节点B")] string _links = string.Empty;
        [ShowInInspector, ReadOnly, MultiLineProperty(5), LabelText("结果")] string _result = "请在有效游戏 Session 中使用";
        [ShowInInspector, ReadOnly, MultiLineProperty(8), LabelText("相接候选（尚未提交）")] string _candidates = string.Empty;
        [SerializeField, LabelText("分配属性稳定 ID")] string _attributeId = StatIds.Strength;
        [SerializeField, MinValue(1), LabelText("本次属性分配点数")] int _attributeCount = 1;

        PropertyTree _tree;
        Label _snapshot;
        SkillBoardPreview _preview;
        EnergyBottleSelection _centerSelection;
        SkillBoardPreview _chargePreview;
        PlayerGrowthPreview _growthPreview;
        IVisualElementScheduledItem _refresh;

        [MenuItem("DarkFlare/Debug/柴薪安装与能量瓶")]
        public static void Open() { GetWindow<SkillBoardRuntimeWindow>("柴薪安装与能量瓶").Show(); }
        public IArchitecture GetArchitecture() { return GameArchitectureProvider.RequireCurrent(); }

        public void CreateGUI()
        {
            rootVisualElement.Clear(); _tree?.Dispose(); _tree = PropertyTree.Create(this);
            rootVisualElement.style.flexDirection = FlexDirection.Row;
            var controls = new ScrollView(); controls.style.width = 480; controls.style.flexShrink = 0;
            controls.Add(new IMGUIContainer(() => _tree?.Draw(false)));
            var inspection = new ScrollView(); inspection.style.flexGrow = 1;
            _snapshot = new Label(); _snapshot.style.whiteSpace = WhiteSpace.Normal; _snapshot.style.paddingLeft = 12;
            inspection.Add(_snapshot); rootVisualElement.Add(controls); rootVisualElement.Add(inspection);
            _refresh?.Pause(); _refresh = rootVisualElement.schedule.Execute(Refresh).Every(500);
            Refresh();
        }
        void OnDisable()
        {
            _refresh?.Pause();
            _tree?.Dispose();
            _tree = null;
            _preview = null;
            _centerSelection = null;
            _chargePreview = null;
            _growthPreview = null;
        }

        [Button("选择当前 ID 的能量瓶放入中心（不移动资产）")]
        void SelectCenterBottle()
        {
            if (!TryPlayer(out string player)) { return; }
            SkillBoardState board = this.SendQuery(new GetSkillBoardQuery());
            ItemInstance item = this.SendQuery(new BoardDebugItemsQuery()).Concat(board.Items)
                .FirstOrDefault(i => i.InstanceId == _itemId);
            _centerSelection = this.SendQuery(new SelectEnergyBottleQuery(player, item));
            _chargePreview = null;
            _result = _centerSelection == null ? "选择失败：需要本玩家背包或专栏中的能量瓶" : "中心仅持有短暂引用；取消 / 关闭不消耗";
            Refresh();
        }

        [Button("预览中心充能（不消费）")]
        void PreviewCharge()
        {
            if (!TryPlayer(out _)) { return; }
            _chargePreview = this.SendQuery(new PreviewEnergyChargeQuery(_centerSelection));
            if (!_chargePreview.Success) { _result = _chargePreview.Error; return; }
            PlayerGrowthState before = this.SendQuery(new GetPlayerGrowthQuery());
            PlayerGrowthState after = _chargePreview.GrowthResult;
            _result = $"充入 {_centerSelection.Item.SkillBoardState.Energy} 能量 · 同时新增 {after.EarnedPoints - before.EarnedPoints} 天赋点 / 属性点\n剩余 {after.Energy} · 下一点成本 {after.Rules.NextCost(after.EarnedPoints)}";
        }

        [Button("确认中心充能并消费整瓶")]
        void CommitCharge()
        {
            if (!TryPlayer(out _)) { return; }
            var command = new CommitSkillBoardCommand(_chargePreview);
            bool success = this.SendCommand(command);
            _result = success ? "充能成功，整瓶已消费" : command.Error;
            _chargePreview = null;
            if (success) { _centerSelection = null; }
            Refresh();
        }

        [Button("取消中心选择与成长预览")]
        void CancelGrowth()
        {
            _centerSelection = null;
            _chargePreview = null;
            _growthPreview = null;
            _result = "已取消，资产和点数未变更";
            Refresh();
        }

        [Button("预览属性分配")]
        void PreviewAttribute()
        {
            if (!TryPlayer(out string player)) { return; }
            _growthPreview = this.SendQuery(new PreviewPlayerGrowthQuery(player, _attributeId, _attributeCount));
            DescribeGrowthPreview();
        }

        [Button("预览点亮当前柴薪 ID")]
        void PreviewFirewoodActivation()
        {
            if (!TryPlayer(out string player)) { return; }
            _growthPreview = this.SendQuery(new PreviewPlayerGrowthQuery(player, woodId: _woodId));
            DescribeGrowthPreview();
        }

        [Button("预览点亮当前柴薪 / 普通天赋节点")]
        void PreviewTalentActivation()
        {
            if (!TryPlayer(out string player)) { return; }
            _growthPreview = this.SendQuery(new PreviewPlayerGrowthQuery(player, woodId: _woodId, nodeId: _nodeId));
            DescribeGrowthPreview();
        }

        void DescribeGrowthPreview()
        {
            _result = _growthPreview.Success
                ? $"预检通过 · 天赋点剩余 {_growthPreview.Result.TalentPoints} · 属性点剩余 {_growthPreview.Result.AttributePoints}"
                : _growthPreview.Error;
        }

        [Button("提交当前属性 / 点亮预览")]
        void CommitGrowth()
        {
            if (!TryPlayer(out _)) { return; }
            var command = new CommitPlayerGrowthCommand(_growthPreview);
            _result = this.SendCommand(command) ? "成长提交成功" : command.Error;
            _growthPreview = null;
            Refresh();
        }

        [Button("生成到背包（调试供给）")]
        void Grant()
        {
            if (!TryPlayer(out _)) { return; }
            ItemInstance item = this.SendCommand(new GrantBoardDebugItemCommand(_definition, _seed));
            _result = item == null ? "生成失败：配置非法或背包已满" : "生成 " + item.InstanceId;
            if (item != null) { _itemId = item.InstanceId; }
            _preview = null; Refresh();
        }
        [Button("预检操作（不转移资产）")]
        void Preview()
        {
            _preview = null;
            if (!TryPlayer(out string player)) { return; }
            try
            {
                SkillBoardState state = this.SendQuery(new GetSkillBoardQuery());
                ItemInstance item = this.SendQuery(new BoardDebugItemsQuery()).Concat(state.Items).FirstOrDefault(i => i.InstanceId == _itemId);
                var links = _replaceConnections ? new List<FirewoodConnection>() : state.Connections.ToList();
                foreach (string line in _links.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 4) { throw new ArgumentException("连接行需要四项身份"); }
                    links.Add(new FirewoodConnection(new FirewoodPortReference(fields[0], fields[1]), new FirewoodPortReference(fields[2], fields[3])));
                }
                _preview = this.SendQuery(new PreviewSkillBoardQuery(player, new SkillBoardRequest(_operation, item,
                    new HexCoordinate(_q, _r), _rotation, new FirewoodPortReference(_woodId, _nodeId), links)));
                _result = _preview.Success ? "预检通过，可提交；任何资产或瓶内能量变化都会使预览失效" : _preview.Error;
            }
            catch (ArgumentException error) { _result = error.Message; }
            catch (OverflowException) { _result = "坐标超出表示范围"; }
        }
        [Button("提交当前预览")]
        void Commit()
        {
            if (!TryPlayer(out _)) { return; }
            var command = new CommitSkillBoardCommand(_preview);
            _result = this.SendCommand(command) ? "提交成功" : command.Error;
            _preview = null; Refresh();
        }
        [Button("列出新位置的相接候选")]
        void FindConnections()
        {
            if (!TryPlayer(out _)) { return; }
            SkillBoardState state = this.SendQuery(new GetSkillBoardQuery());
            ItemInstance item = this.SendQuery(new BoardDebugItemsQuery()).Concat(state.Items).FirstOrDefault(i => i.InstanceId == _itemId);
            if (item?.SkillBoardState?.Layout == null) { _result = "请选定柴薪实例"; return; }
            try
            {
                var placements = state.Firewoods.Where(w => w.Item != item).Select(w => w.Placement).ToList();
                placements.Add(new FirewoodPlacement(item.InstanceId, item.SkillBoardState.Layout, new HexCoordinate(_q, _r), _rotation));
                _candidates = string.Join("\n", SkillBoardPlacementValidator.FindConnections(placements)
                    .Select(c => $"{c.First.PlacementId} {c.First.NodeId} {c.Second.PlacementId} {c.Second.NodeId}"));
            }
            catch (OverflowException) { _result = "坐标超出表示范围"; }
        }
        [Button("明确采用候选连接（需重新预检）")]
        void UseCandidates() { _links = _candidates; _replaceConnections = true; _preview = null; }

        bool TryPlayer(out string player)
        {
            player = null;
            if (!GameArchitectureProvider.TryGetCurrent(out _)) { _result = "当前没有有效 Session"; _preview = null; return false; }
            IReadOnlyList<CombatActor> players = this.SendQuery(new GetActorsByTeamQuery(ActorTeam.Player));
            if (players.Count != 1) { _result = "当前没有唯一玩家"; return false; }
            player = SkillBoardPlayerIdentity.GetProfileId(players[0]);
            return true;
        }
        void Refresh()
        {
            if (_snapshot == null) { return; }
            if (!TryPlayer(out string player))
            {
                _snapshot.text = "当前没有有效玩家。启动游戏后可操作。";
                _centerSelection = null;
                _chargePreview = null;
                _growthPreview = null;
                return;
            }
            SkillBoardState state = this.SendQuery(new GetSkillBoardQuery());
            PlayerGrowthState growth = this.SendQuery(new GetPlayerGrowthQuery());
            if (_centerSelection != null && !this.SendQuery(new IsEnergyBottleSelectionCurrentQuery(_centerSelection)))
            {
                _centerSelection = null;
                _chargePreview = null;
            }
            var lines = new List<string> { "玩家 " + player, "能量瓶专栏：" + Describe(state.EnergyBottle), "", "棋盘" };
            lines.Insert(2, $"中心选择：{Describe(_centerSelection?.Item)}\n角色余量 {growth.Energy} · 累计 {growth.EarnedPoints} · 下一点成本 {growth.Rules.NextCost(growth.EarnedPoints)}\n可用天赋点 {growth.TalentPoints} · 属性点 {growth.AttributePoints}\n分配：力量 {growth.Strength} / 敏捷 {growth.Dexterity} / 智力 {growth.Intelligence}");
            foreach (InstalledFirewood wood in state.Firewoods)
            {
                lines.Add($"{Describe(wood.Item)}\n锚点 {wood.Placement.Anchor} · 旋转 {wood.Placement.Rotation}");
                lines.Add($"柴薪点亮 / 中心火焰资格：{growth.IsActive(wood.Item.InstanceId)}");
                lines.Add("节点 " + string.Join(", ", wood.Placement.Layout.Nodes.Select(n => n.Id + ":" + n.Kind
                    + (growth.IsActive(wood.Item.InstanceId, n.Id) ? "（已点亮）" : ""))));
            }
            lines.Add("连接 " + string.Join("\n", state.Connections.Select(c => c.First + " ↔ " + c.Second)));
            lines.Add("嵌入 " + string.Join("\n", state.Embeddings.Select(e => e.Slot + " = " + Describe(e.Item))));
            lines.Add("\n背包（棋盘道具）");
            lines.AddRange(this.SendQuery(new BoardDebugItemsQuery()).Select(Describe));
            _snapshot.text = string.Join("\n", lines);
        }
        static string Describe(ItemInstance item)
        {
            if (item == null) { return "空"; }
            string energy = item.BaseDefinition.ItemType == ItemType.EnergyBottle ? $" · 能量 {item.SkillBoardState.Energy}/{item.BaseDefinition.SkillBoard.EnergyCapacity}" : "";
            return $"{item.BaseDefinition.DisplayName} · {item.InstanceId} · Revision {item.Revision}{energy}";
        }
        sealed class BoardDebugItemsQuery : AbstractQuery<IReadOnlyList<ItemInstance>>
        {
            protected override IReadOnlyList<ItemInstance> OnDo()
            { return this.GetModel<InventoryModel>().Grid.Placements.Keys.Where(i => i.BaseDefinition.IsSkillBoardItem).ToList().AsReadOnly(); }
        }
        sealed class GrantBoardDebugItemCommand : AbstractCommand<ItemInstance>
        {
            readonly ItemBaseDefinition _definition;
            readonly int _seed;
            public GrantBoardDebugItemCommand(ItemBaseDefinition definition, int seed) { _definition = definition; _seed = seed; }
            protected override ItemInstance OnExecute()
            {
                if (_definition == null || !_definition.IsSkillBoardItem || SkillBoardItemConfiguration.Validate(_definition).Count > 0) { return null; }
                ItemInstance item = _definition.CreateInstance(this.GetUtility<IItemInstanceIdGenerator>().Next(), 1, _seed, ItemRarity.Normal);
                return this.GetModel<InventoryModel>().TryAddItem(item) ? item : null;
            }
        }
    }
}
