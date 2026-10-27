using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed partial class SkillBoardPanelController : IController, IDisposable
    {
        readonly GameMenuController _menu;
        readonly IArchitecture _architecture;
        readonly VisualElement _root;
        readonly LocalizationService _locale;
        readonly LifecycleScope _scene;
        readonly ItemDetailFormatter _formatter;
        readonly List<IUnRegister> _registrations = new List<IUnRegister>();
        readonly Dictionary<Button, Action> _buttons = new Dictionary<Button, Action>();
        readonly HashSet<FirewoodConnection> _chosen = new HashSet<FirewoodConnection>();
        readonly List<string> _targets = new List<string>();
        LifecycleScope _refresh;
        SkillBoardState _board;
        PlayerGrowthState _growth;
        InventorySnapshot _inventory;
        InstalledFirewood _wood;
        string _node = string.Empty;
        ItemInstance _bagItem;
        ItemInstance _moving;
        int _rotation;
        SkillBoardPreview _preview;
        PlayerGrowthPreview _growthPreview;
        EnergyBottleSelection _bottle;
        bool _charging;
        bool _disposed;
        bool _executing;
        SkillBoardFailure _feedback;
        HexCoordinate _lastAnchor;
        int _lastRotation = -1;
        HexCoordinate _selectedCell;

        public SkillBoardViewport Canvas { get; }
        public bool IsVisible { get; private set; }
        public bool HasPlacement => _moving?.BaseDefinition.ItemType == ItemType.Firewood;
        public ItemInstance MovingItem => _moving;
        public SkillBoardPreview Preview => _preview;
        public string PlayerId => _inventory.HasPlayer ? SkillBoardPlayerIdentity.GetProfileId(_inventory.Player) : string.Empty;
        public IArchitecture GetArchitecture() { return _architecture; }

        public SkillBoardPanelController(GameMenuController menu, IArchitecture architecture, VisualElement root,
            LocalizationService locale, LifecycleScope scene)
        {
            _menu = menu; _architecture = architecture; _root = root; _locale = locale; _scene = scene;
            _formatter = new ItemDetailFormatter((table, key, args) => locale.GetString(table, key, args));
            Canvas = new SkillBoardViewport();
            root.Q("board-viewport").Add(Canvas);
            Canvas.CursorChanged += OnCell;
            Canvas.Activated += OnActivateCell;
            Canvas.TakeRequested += TakeMountedPointer;
            Bind("tools", () => SetTools(!_toolsOpen));
            Bind("tools-close", () => SetTools(false));
            Bind("details-toggle", () => ShowDetails(!_detailsOpen));
            Bind("center", () => ViewAction(() => Jump(default))); Bind("fit", () => ViewAction(Canvas.Fit));
            Bind("zoom-in", () => ViewAction(() => Canvas.Zoom(1.25f))); Bind("zoom-out", () => ViewAction(() => Canvas.Zoom(0.8f)));
            for (int i = 0; i < 6; i++)
            {
                int slot = i;
                Bind("root-" + i, () => ViewAction(() => Jump(SkillBoardGeometry.Roots[slot])));
                Bind("slot-" + i, () => SelectSlot(slot));
                Bind("clear-" + i, () => ClearSlot(slot));
            }
            Bind("take", TakeBag); Bind("move", TakeMounted); Bind("activate", Activate);
            Bind("extract", Extract); Bind("remove", Remove); Bind("confirm", Confirm); Bind("cancel", () => Cancel());
            Bind("choose-bottle", () => SelectBottle(SelectedBag()));
            Bind("charge-equipped", () => SelectBottle(_board?.EnergyBottle));
            foreach (string stat in new[] { StatIds.Strength, StatIds.Dexterity, StatIds.Intelligence })
            {
                string id = stat; Bind("allocate-" + stat, () => Allocate(id));
            }
            _menu.Workspace.InventorySelectionChanged += OnBag;
            _menu.Workspace.Suspending += OnSuspend;
            _locale.LocaleChanged += OnLocale;
            ApplicationHost.Current.Input.SuspensionChanged += OnSuspended;
            InputSystem.onDeviceChange += OnDeviceChanged;
            _root.RegisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            _registrations.Add(this.RegisterEvent<InventoryChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<SkillBoardChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<PlayerGrowthChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<PlayerSkillsChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<EnergyCollectedEvent>(_ => OnSourceChanged()));
            _registrations.Add(this.RegisterEvent<ActorDiedEvent>(e => { if (e.Actor?.Team == ActorTeam.Player) { OnSuspend(); _menu.ClosePage(GameMenuPage.SkillBoard); } }));
            _registrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(e => { if (e.Actor?.Team == ActorTeam.Player) { OnSuspend(); } }));
        }

        void Bind(string name, Action action)
        {
            Button button = _root.Q<Button>("board-" + name);
            if (button == null) { throw new InvalidOperationException("棋盘缺少控件：" + name); }
            button.clicked += action; _buttons.Add(button, action);
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || IsVisible == visible) { return; }
            IsVisible = visible;
            _refresh?.BeginStop(); _refresh = null;
            if (!visible) { SetTools(false, false); ResetOperation(); ShowDetails(false); return; }
            _bagItem = _menu.Workspace.SelectedInventoryItem;
            Refresh();
            _refresh = _scene.CreateChild("player-board");
            _refresh.Tasks.Run("cooldowns", RefreshClockAsync, failurePolicy: LifecycleTaskFailurePolicy.Report);
        }

        async UniTask RefreshClockAsync(CancellationToken token)
        {
            if (await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token).SuppressCancellationThrow()) { return; }
            if (!_initialFit) { Canvas.Fit(); _initialFit = true; }
            while (!token.IsCancellationRequested)
            {
                if (await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow()) { return; }
                if (IsVisible) { RefreshSlots(); }
            }
        }

        public bool FocusDefault()
        {
            if (!IsVisible) { return false; }
            Canvas.Focus(); return true;
        }

        public bool Navigate(Vector2 direction)
        {
            if (!IsVisible) { return false; }
            if (_toolsOpen) { NavigateTools(direction); return true; }
            if (Canvas.panel?.focusController.focusedElement != Canvas) { return false; }
            Canvas.Navigate(direction); return true;
        }

        public void Jump(HexCoordinate cell)
        {
            Canvas.CenterOn(cell); Canvas.Select(cell); Canvas.Focus();
        }

        void OnCell(HexCoordinate cell)
        {
            if (!IsVisible) { return; }
            StopFocusRequest();
            if (!_selectedCell.Equals(cell) && _moving == null && !_reviewingLinks) { ResetOperation(); }
            _selectedCell = cell;
            ReadState(); SelectNode();
            _selectedSlot = -1;
            if (!_menu.Workspace.IsDragging) { _root.Q<ScrollView>("board-details-scroll").scrollOffset = Vector2.zero; }
            if (_moving != null) { UpdatePlacement(); }
            RefreshDetails(); Draw();
        }

        void SelectNode()
        {
            _wood = null; _node = string.Empty;
            foreach (InstalledFirewood wood in _board.Firewoods)
            {
                if (wood.Placement.Anchor.Equals(Canvas.Cursor) && wood.Placement.Layout.Size == FirewoodSize.Large)
                { _wood = wood; _node = SkillBoardGeometry.CenterNodeId; return; }
                foreach (FirewoodNode node in wood.Placement.Layout.Nodes)
                {
                    if (wood.Placement.ToWorld(node.Position).Equals(Canvas.Cursor)) { _wood = wood; _node = node.Id; return; }
                }
                if (wood.Placement.Layout.Body.Any(c => wood.Placement.ToWorld(c).Equals(Canvas.Cursor))) { _wood = wood; }
            }
        }

        void OnActivateCell()
        {
            if (!IsVisible || _menu.Workspace.IsDragging) { return; }
            ShowDetails(true);
            Button focus = _root.Q<Button>(_preview != null || _growthPreview != null ? "board-confirm" : "board-activate");
            string attribute = SkillBoardProjection.AttributeAt(Canvas.Cursor);
            if (attribute.Length > 0) { focus = _root.Q<Button>("board-allocate-" + attribute); }
            else if (Canvas.Cursor.Equals(default))
            { focus = _root.Q<Button>(SelectedBag()?.BaseDefinition.ItemType == ItemType.EnergyBottle ? "board-choose-bottle" : "board-charge-equipped"); }
            if (!focus.enabledSelf || focus.ClassListContains("board-hidden")) { focus = _root.Q<Button>("board-take"); }
            if (!focus.enabledSelf || focus.ClassListContains("board-hidden")) { focus = _root.Q<Button>("board-tools"); }
            FocusControl(focus);
        }

        void ReadState()
        {
            _board = this.SendQuery(new GetSkillBoardQuery());
            _growth = this.SendQuery(new GetPlayerGrowthQuery());
            _inventory = this.SendQuery(new GetInventorySnapshotQuery());
        }

        void OnBag(ItemInstance item)
        {
            StopFocusRequest();
            _bagItem = item;
            if (!IsVisible || _menu.Workspace.IsDragging) { return; }
            _wood = null; _node = string.Empty; _selectedSlot = -1;
            if (item != null) { ShowDetails(true); }
            Refresh();
        }
        void OnLocale(string locale)
        {
            if (!IsVisible) { return; }
            Refresh();
            if (_preview != null || _growthPreview != null || _bottle != null) { FocusPrepared(); }
        }
        void OnSuspend() { SetTools(false, false); ResetOperation(); }
        void OnSuspended(InputSuspensionReason reasons) { if (reasons != InputSuspensionReason.None) { _menu.Workspace.CancelDrag(); OnSuspend(); } }
        void OnDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected) { _menu.Workspace.CancelDrag(); OnSuspend(); }
        }
        void OnSourceChanged()
        {
            if (!IsVisible || _executing || _disposed) { return; }
            ReadState();
            if (_moving != null && !IsItemCurrent(_moving)) { _menu.Workspace.CancelDrag(); ResetOperation(); _feedback = SkillBoardFailure.Stale; }
            if (_bottle != null && !this.SendQuery(new IsEnergyBottleSelectionCurrentQuery(_bottle))) { ResetOperation(); _feedback = SkillBoardFailure.Stale; }
            SelectNode(); Refresh();
            if (_feedback == SkillBoardFailure.Stale) { Text("preview", L("error.Stale")); }
        }

        public bool IsItemCurrent(ItemInstance item)
        {
            SkillBoardState board = this.SendQuery(new GetSkillBoardQuery());
            return board.Firewoods.Any(w => w.Item == item) || this.SendQuery(new GetInventorySnapshotQuery()).Items.Any(i => i.Item == item);
        }

        ItemInstance SelectedBag()
        {
            ItemInstance item = _menu.Workspace.SelectedInventoryItem ?? _bagItem;
            return _inventory.Items.Any(i => i.Item == item) ? item : null;
        }
        SkillBoardEmbedding Embedding => _wood == null ? null : _board.Embeddings.FirstOrDefault(e => e.Slot.Equals(new FirewoodPortReference(_wood.Item.InstanceId, _node)));
        ItemInstance SelectedFlame => Embedding?.Item.BaseDefinition.ItemType == ItemType.Flame ? Embedding.Item : null;

        public void Refresh()
        {
            if (_disposed || !IsVisible) { return; }
            ReadState();
            Text("summary", L("growth_compact", _growth.TalentPoints, _growth.AttributePoints, _growth.Energy, _growth.Rules.NextCost(_growth.EarnedPoints)));
            for (int i = 0; i < 6; i++) { _root.Q<Button>("board-root-" + i).text = L("root", i + 1); }
            for (int i = 0; i < 3; i++)
            {
                string id = new[] { StatIds.Strength, StatIds.Dexterity, StatIds.Intelligence }[i];
                int spent = i == 0 ? _growth.Strength : i == 1 ? _growth.Dexterity : _growth.Intelligence;
                string name = _locale.GetString("stats", id);
                Text("attribute-" + id, L("attribute", name, spent, _inventory.Player?.Stats.GetValue(id) ?? 0));
                _root.Q<Button>("board-allocate-" + id).text = L("allocate", name);
            }
            ItemInstance bottle = _board.EnergyBottle;
            ItemVisualPresenter.ApplyIcon(_root.Q("board-bottle-icon"), bottle?.BaseDefinition.Icon.AssetGUID);
            Visible("bottle-icon", bottle != null && Canvas.Cursor.Equals(default) && _selectedSlot < 0);
            Text("bottle-summary", bottle == null ? L("bottle_empty")
                : L("bottle", Name(bottle), bottle.SkillBoardState.Energy, bottle.BaseDefinition.SkillBoard.EnergyCapacity,
                    bottle.SkillBoardState.Energy >= bottle.BaseDefinition.SkillBoard.EnergyCapacity ? L("full") : string.Empty));
            RefreshDetails(); RefreshSlots(); Draw();
        }

        void RefreshDetails()
        {
            ItemInstance bag = SelectedBag();
            ItemInstance inspected = Embedding?.Item ?? _wood?.Item ?? bag;
            Text("selected-name", inspected == null ? L("no_selection") : Name(inspected));
            ItemVisualPresenter.ApplyIcon(_root.Q("board-selected-icon"), inspected?.BaseDefinition.Icon.AssetGUID);
            Visible("selected-icon", inspected != null && SkillBoardProjection.AttributeAt(Canvas.Cursor).Length == 0 && _selectedSlot < 0);
            var text = new StringBuilder();
            _targets.Clear();
            if (_wood != null)
            {
                FirewoodNode? node = _wood.Placement.Layout.Nodes.Where(n => n.Id == _node).Select(n => (FirewoodNode?)n).FirstOrDefault();
                text.AppendLine(L("node", _node == "center" ? L("flame") : node.HasValue ? L(node.Value.Kind == FirewoodNodeKind.Crack ? "crack" : "talent") : L("blank"),
                    Canvas.Cursor, Name(_wood.Item), L(_growth.IsActive(_wood.Item.InstanceId, node?.Kind == FirewoodNodeKind.Talent ? _node : "") ? "lit" : "unlit")));
                SkillBoardTalentState talent = _wood.Item.SkillBoardState.Talents.FirstOrDefault(t => t.NodeId == _node);
                if (talent != null)
                {
                    text.AppendLine(talent.IsGlobal ? L("global") : L("range", talent.Radius));
                    foreach (ModifierInstance modifier in talent.Modifiers) { text.AppendLine(Describe(modifier)); }
                    if (!talent.IsGlobal) { DescribeRange(text, new FirewoodPortReference(_wood.Item.InstanceId, _node), talent.Radius, ModifierOriginKind.Talent, _wood.Item.InstanceId + ":" + _node); }
                }
                if (Embedding?.Item.BaseDefinition.ItemType == ItemType.Ember)
                {
                    ItemInstance ember = Embedding.Item;
                    text.AppendLine(L("range", ember.SkillBoardState.EmberRadius));
                    DescribeRange(text, Embedding.Slot, ember.SkillBoardState.EmberRadius, ModifierOriginKind.Ember, ember.InstanceId);
                }
            }
            if (inspected != null)
            {
                text.AppendLine(_formatter.GetItemTypeText(inspected.BaseDefinition.ItemType));
                foreach (ModifierInstance modifier in inspected.CollectModifiers()) { text.AppendLine(Describe(modifier)); }
            }
            Text("selected-details", text.ToString());
            _root.Q<Button>("board-take").SetEnabled(bag?.BaseDefinition.IsSkillBoardItem == true && bag.BaseDefinition.ItemType != ItemType.EnergyBottle);
            _root.Q<Button>("board-move").SetEnabled(_wood != null);
            _root.Q<Button>("board-activate").SetEnabled(_wood != null);
            _root.Q<Button>("board-extract").SetEnabled(Embedding != null);
            _root.Q<Button>("board-remove").SetEnabled(_wood != null);
            _root.Q<Button>("board-charge-equipped").SetEnabled(_board.EnergyBottle?.SkillBoardState.Energy > 0);
            _root.Q<Button>("board-choose-bottle").SetEnabled(bag?.BaseDefinition.ItemType == ItemType.EnergyBottle);
            _root.Q<Button>("board-confirm").SetEnabled(_preview?.Success == true || _growthPreview?.Success == true);
            _root.Q<Button>("board-confirm").text = L(_charging ? "consume" : "confirm");
            RefreshContext();
        }

        string Describe(ModifierInstance modifier)
        {
            return _formatter.FormatModifier(modifier, _locale.GetString("stats", modifier.StatId), false);
        }
        void DescribeRange(StringBuilder text, FirewoodPortReference source, int radius, ModifierOriginKind kind, string id)
        {
            SkillBoardEffectProjection projection = SkillBoardEffects.Resolve(_board, _growth);
            var names = new List<string>();
            foreach (SkillBoardEmbedding flame in _board.Embeddings.Where(e => e.Item.BaseDefinition.ItemType == ItemType.Flame))
            {
                if (projection.Flames.TryGetValue(flame.Item.InstanceId, out IReadOnlyList<ModifierInstance> effects)
                    && effects.Any(m => m.Origin.Kind == kind && m.Origin.StatusId == id)
                    && projection.FlameSteps[flame.Item.InstanceId].TryGetValue(source, out int steps) && steps <= radius)
                { _targets.Add(flame.Item.InstanceId); names.Add(L("target", Name(flame.Item), steps)); }
            }
            text.AppendLine(L("targets", names.Count == 0 ? L("no_targets") : string.Join(", ", names)));
        }

        void RefreshSlots()
        {
            string previous = ApplicationHost.Current.Input.GetBindingDisplayString(new InputBindingTarget(RebindableInputAction.UiPreviousWindow, InputBindingPart.Primary, ApplicationHost.Current.Input.DisplayDeviceFamily));
            string next = ApplicationHost.Current.Input.GetBindingDisplayString(new InputBindingTarget(RebindableInputAction.UiNextWindow, InputBindingPart.Primary, ApplicationHost.Current.Input.DisplayDeviceFamily));
            string submit = ApplicationHost.Current.Input.GetBindingDisplayString(new InputBindingTarget(RebindableInputAction.UiSubmit, InputBindingPart.Primary, ApplicationHost.Current.Input.DisplayDeviceFamily));
            string cancel = ApplicationHost.Current.Input.GetBindingDisplayString(new InputBindingTarget(RebindableInputAction.UiCancel, InputBindingPart.Primary, ApplicationHost.Current.Input.DisplayDeviceFamily));
            Text("help", L("help_compact", submit, cancel, previous, next));
            IReadOnlyList<PlayerSkillState> skills = this.SendQuery(new GetPlayerSkillsQuery());
            for (int i = 0; i < skills.Count; i++)
            {
                PlayerSkillState skill = skills[i];
                AttributeRuntimeSnapshot state = this.SendQuery(new GetPlayerSkillRuntimeQuery(i));
                string binding = ApplicationHost.Current.Input.GetBindingDisplayString(new InputBindingTarget(
                    RebindableInputAction.PlayerSkill1 + i, InputBindingPart.Primary, ApplicationHost.Current.Input.DisplayDeviceFamily));
                string description = L("slot", binding,
                    skill.Skill == null ? L("unbound") : _locale.GetString("ui", "attributes.skill." + skill.Skill.Id),
                    skill.Skill == null ? string.Empty : _locale.GetString("ui", "hud.flame.details", new object[] { skill.Skill.ManaCost, _locale.GetString("ui", "attributes.state." + state.SkillState) + " " + ItemDetailFormatter.FormatNumber(skill.RemainingSeconds) }));
                Button slot = _root.Q<Button>("board-slot-" + i);
                slot.text = L("slot_compact", binding, i + 1);
                slot.tooltip = description;
                slot.EnableInClassList("board-skill-slot--filled", skill.Skill != null);
                slot.EnableInClassList("board-skill-slot--selected", _selectedSlot == i);
                if (_selectedSlot == i) { Text("slot-details", description); }
                _root.Q<Button>("board-clear-" + i).SetEnabled(skill.Skill != null);
                Visible("clear-" + i, _detailsOpen && _selectedSlot == i && skill.Skill != null);
            }
            PlayerSkillState normal = this.SendQuery(new GetPlayerNormalSkillQuery(_inventory.Player));
            Text("normal", L("normal", normal.Skill == null ? L("none") : _locale.GetString("ui", "attributes.skill." + normal.Skill.Id)));
        }

        void Draw()
        {
            Canvas.SetState(_board, _growth, _moving == null ? null : _preview?.Result, _moving, _preview?.Success == true,
                L("center_short"), new[] { L("strength_short"), L("dexterity_short"), L("intelligence_short") }, _targets);
        }
        void Text(string name, string value)
        {
            _root.Q<Label>("board-" + name).text = value;
            if (name == "preview") { Visible("preview", !string.IsNullOrEmpty(value)); }
        }
        string Name(ItemInstance item) { return _locale.GetString(item.BaseDefinition.LocalizedName.Message); }
        string L(string key, params object[] args) { return _locale.GetString("ui", "board." + key, args); }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true; _refresh?.BeginStop(); StopFocusRequest(); ResetOperation();
            foreach (IUnRegister registration in _registrations) { registration.UnRegister(); }
            foreach (KeyValuePair<Button, Action> pair in _buttons) { pair.Key.clicked -= pair.Value; }
            _menu.Workspace.InventorySelectionChanged -= OnBag; _menu.Workspace.Suspending -= OnSuspend;
            _locale.LocaleChanged -= OnLocale;
            ApplicationHost.Current.Input.SuspensionChanged -= OnSuspended;
            InputSystem.onDeviceChange -= OnDeviceChanged;
            _root.UnregisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            Canvas.CursorChanged -= OnCell; Canvas.Activated -= OnActivateCell; Canvas.TakeRequested -= TakeMountedPointer;
            Canvas.Dispose(); Canvas.RemoveFromHierarchy();
        }
    }
}
