using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    // 菜单持有输入和暂停；本组件只读取状态并管理展示资源。
    public sealed class StatusPanelController : IController, IDisposable
    {
        readonly IArchitecture _architecture;
        readonly VisualElement _root;
        readonly VisualElement _window;
        readonly VisualElement _hud;
        readonly VisualElement _hudBar;
        readonly VisualElement _bar;
        readonly ScrollView _actors;
        readonly ScrollView _detailScroll;
        readonly VisualElement _tooltip;
        readonly Label _details;
        readonly Label _empty;
        readonly Label _actorTitle;
        readonly LocalizationService _locale;
        readonly LifecycleScope _scope;
        readonly SpriteAssetLoader _sprites;
        readonly Dictionary<string, StatusDefinition> _definitions;
        readonly Dictionary<string, StatDefinition> _stats;
        readonly Dictionary<string, Button> _actorButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> _hudButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        readonly List<IUnRegister> _registrations = new List<IUnRegister>();
        readonly ItemDetailFormatter _formatter;
        readonly Button _close;
        readonly Button _visibilityToggle;
        readonly Button _hudVisibilityToggle;
        bool _hideOptional;
        string _selected;
        string _detailId;
        bool _detailFromHud;
        bool _open;
        bool _hudVisible;
        bool _loaded;
        bool _disposed;
        bool _pointerInDetails;
        VisualElement _anchor;
        StatusViewSnapshot _snapshot;

        public StatusPanelController(IArchitecture architecture, VisualElement root, LocalizationService locale, LifecycleScope sceneScope)
        {
            _architecture = architecture; _root = root; _locale = locale;
            _window = root.Q("status-window"); _hud = root.Q("status-hud"); _hudBar = root.Q("status-hud-icons");
            _bar = root.Q("status-icons"); _actors = root.Q<ScrollView>("status-actors");
            _tooltip = root.Q("status-tooltip"); _details = root.Q<Label>("status-details");
            _detailScroll = root.Q<ScrollView>("status-detail-scroll");
            _empty = root.Q<Label>("status-empty"); _actorTitle = root.Q<Label>("status-actor-title");
            _close = root.Q<Button>("status-close");
            _visibilityToggle = root.Q<Button>("status-visibility-toggle");
            _hudVisibilityToggle = root.Q<Button>("status-hud-visibility-toggle");
            _visibilityToggle.clicked += ToggleOptional;
            _hudVisibilityToggle.clicked += ToggleOptional;
            _hudVisibilityToggle.focusable = false;
            _detailScroll.focusable = true;
            _detailScroll.RegisterCallback<NavigationMoveEvent>(OnDetailNavigation, TrickleDown.TrickleDown);
            _tooltip.RegisterCallback<PointerEnterEvent>(OnDetailEnter);
            _tooltip.RegisterCallback<PointerLeaveEvent>(OnDetailLeave);
            ContentCatalog catalog = ApplicationHost.Current.ContentCatalog;
            _definitions = catalog.GetAll<StatusDefinition>().ToDictionary(value => value.Id);
            _stats = catalog.GetAll<StatDefinition>().ToDictionary(value => value.Id);
            _sprites = architecture.GetUtility<SpriteAssetLoader>();
            _formatter = new ItemDetailFormatter((table, key, args) => locale.GetString(table, key, args));
            _scope = sceneScope.CreateChild("status-view");
            _registrations.Add(this.RegisterEvent<StatusChangedEvent>(_ => Refresh()));
            _registrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(_ => Refresh()));
            _locale.LocaleChanged += OnLocaleChanged;
            _scope.Tasks.Run("status-icons-and-refresh", RunAsync, failurePolicy: LifecycleTaskFailurePolicy.Report);
        }

        public IArchitecture GetArchitecture() { return _architecture; }

        void ToggleOptional()
        {
            _hideOptional = !_hideOptional;
            Refresh();
        }

        bool ShouldDisplay(StatusLayerSnapshot layer)
        {
            return !_definitions.TryGetValue(layer.Rules.Id.LocalId, out StatusDefinition definition)
                || definition.ShouldDisplay(_hideOptional);
        }

        bool HasDetailFocus => _detailScroll.Contains(_root.panel?.focusController.focusedElement as VisualElement)
            || _root.panel?.focusController.focusedElement == _detailScroll;

        public bool LeaveDetails()
        {
            if (!HasDetailFocus || _anchor == null) { return false; }
            _anchor.Focus();
            return true;
        }

        void OnDetailNavigation(NavigationMoveEvent evt)
        {
            if (evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down)
            {
                float direction = evt.direction == NavigationMoveEvent.Direction.Up ? -1 : 1;
                _detailScroll.scrollOffset += new Vector2(0, direction * 64);
            }
            evt.PreventDefault(); evt.StopPropagation();
        }

        void OnDetailEnter(PointerEnterEvent evt) { _pointerInDetails = true; }
        void OnDetailLeave(PointerLeaveEvent evt)
        {
            _pointerInDetails = false;
            ScheduleHide();
        }

        void ScheduleHide()
        {
            _tooltip.schedule.Execute(() =>
            {
                if (!_disposed && !_pointerInDetails && !HasDetailFocus
                    && _root.panel?.focusController.focusedElement != _anchor) { HideDetails(); }
            }).StartingIn(150);
        }

        public void SetVisible(bool open, bool hudVisible)
        {
            if (_disposed) { return; }
            bool changed = _open != open || _hudVisible != hudVisible;
            _open = open; _hudVisible = hudVisible;
            _window.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _hud.style.display = hudVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (changed) { HideDetails(); Refresh(); }
        }

        public void FocusDefault()
        {
            Refresh();
            (_actorButtons.GetValueOrDefault(_selected ?? string.Empty) ?? _actorButtons.Values.FirstOrDefault() ?? _close).Focus();
        }

        public void Navigate(NavigationMoveEvent evt)
        {
            if (!_open) { return; }
            Vector2 direction = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => Vector2.left,
                NavigationMoveEvent.Direction.Right => Vector2.right,
                NavigationMoveEvent.Direction.Up => Vector2.up,
                NavigationMoveEvent.Direction.Down => Vector2.down,
                _ => Vector2.zero
            };
            if (direction == Vector2.zero) { return; }
            evt.StopPropagation(); evt.PreventDefault();
            VisualElement current = _root.panel?.focusController.focusedElement as VisualElement;
            if (current == null) { FocusDefault(); return; }
            var candidates = _window.Query<Button>().ToList().Where(button => button != current && button.enabledInHierarchy
                && button.resolvedStyle.display != DisplayStyle.None).ToList();
            int index = SpatialNavigation.FindNeighbor(current.worldBound, candidates.Select(button => button.worldBound).ToList(), direction);
            if (index >= 0)
            {
                Button next = candidates[index]; next.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(next); next.Focus();
            }
        }

        async UniTask RunAsync(CancellationToken token)
        {
            await _sprites.PreloadAsync(_definitions.Values.Select(value => value.Icon), token);
            _loaded = true;
            while (!token.IsCancellationRequested)
            {
                Refresh();
                await UniTask.Delay(TimeSpan.FromSeconds(.1), ignoreTimeScale: true, cancellationToken: token);
            }
        }

        void OnLocaleChanged(string _) { Refresh(); }

        void Refresh()
        {
            if (_disposed || (!_open && !_hudVisible)) { return; }
            _snapshot = this.SendQuery(new GetStatusViewQuery(_selected, _open));
            string toggleText = L(_hideOptional ? "status.show_optional" : "status.hide_optional");
            _visibilityToggle.text = toggleText;
            _hudVisibilityToggle.text = toggleText;
            _hudVisibilityToggle.style.display = _snapshot.Player.Layers.Any(layer =>
                _definitions.TryGetValue(layer.Rules.Id.LocalId, out StatusDefinition definition)
                && definition.Visibility == StatusVisibility.Hideable) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_selected != _snapshot.Selected.Target.ActorKey)
            {
                _selected = _snapshot.Selected.Target.ActorKey;
                HideDetails();
            }
            if (_hudVisible) { UpdateBar(_hudBar, _hudButtons, _snapshot.Player, true); }
            if (_open)
            {
                UpdateActors();
                UpdateBar(_bar, _buttons, _snapshot.Selected, false);
                _empty.text = L("status.empty");
                _empty.style.display = _snapshot.Selected.Layers.Any(ShouldDisplay) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_detailId != null) { RefreshDetails(); }
        }

        void UpdateActors()
        {
            HashSet<string> present = _snapshot.Actors.Select(choice => choice.Key).ToHashSet();
            RemoveMissing(_actorButtons, present, _close);
            foreach (StatusActorChoice choice in _snapshot.Actors)
            {
                if (!_actorButtons.TryGetValue(choice.Key, out Button button))
                {
                    string key = choice.Key;
                    button = new Button(() => { _selected = key; HideDetails(); Refresh(); }) { name = "status-actor-" + key };
                    button.AddToClassList("status-actor"); _actors.Add(button); _actorButtons.Add(key, button);
                }
                string suffix = choice.Key.StartsWith("player:", StringComparison.Ordinal) ? string.Empty : " #" + choice.Key.Split(':').Last();
                button.text = _locale.GetString(choice.Name) + suffix;
                button.EnableInClassList("status-actor--selected", choice.Key == _selected);
                if (choice.Key == _selected) { _actorTitle.text = button.text; }
            }
            if (string.IsNullOrEmpty(_selected)) { _actorTitle.text = L("status.actor_unavailable"); }
        }

        void UpdateBar(VisualElement bar, Dictionary<string, Button> buttons, StatusTargetSnapshot snapshot, bool hud)
        {
            var groups = snapshot.Layers.Where(ShouldDisplay).GroupBy(layer => layer.Rules.Id.LocalId).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
            RemoveMissing(buttons, groups.Select(group => group.Key).ToHashSet(), _close);
            foreach (var group in groups)
            {
                string id = group.Key;
                if (!buttons.TryGetValue(id, out Button button))
                {
                    button = new Button { name = (hud ? "status-hud-" : "status-icon-") + id };
                    button.AddToClassList("status-icon");
                    var picture = new VisualElement { name = "picture", pickingMode = PickingMode.Ignore }; picture.AddToClassList("status-icon-picture");
                    var count = new Label { name = "count", pickingMode = PickingMode.Ignore }; count.AddToClassList("status-icon-count");
                    var time = new Label { name = "time", pickingMode = PickingMode.Ignore }; time.AddToClassList("status-icon-time");
                    button.Add(picture); button.Add(count); button.Add(time);
                    Button captured = button;
                    button.RegisterCallback<PointerEnterEvent>(_ => { _pointerInDetails = true; ShowDetails(id, captured, hud); });
                    button.RegisterCallback<PointerLeaveEvent>(_ => { _pointerInDetails = false; ScheduleHide(); });
                    button.RegisterCallback<FocusInEvent>(_ => ShowDetails(id, captured, hud));
                    button.RegisterCallback<FocusOutEvent>(evt =>
                    {
                        if (_anchor == captured && evt.relatedTarget != _detailScroll
                            && !_detailScroll.Contains(evt.relatedTarget as VisualElement)) { HideDetails(); }
                    });
                    button.clicked += () => { ShowDetails(id, captured, hud); if (!hud) { _detailScroll.Focus(); } };
                    buttons.Add(id, button); bar.Add(button);
                }
                _definitions.TryGetValue(id, out StatusDefinition definition);
                Sprite sprite = _loaded && definition?.Icon != null && !string.IsNullOrEmpty(definition.Icon.AssetGUID)
                    ? _sprites.GetSprite(definition.Icon.AssetGUID) : null;
                button.Q("picture").style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                button.Q<Label>("count").text = group.Count().ToString();
                double remaining = group.Min(layer => layer.RemainingSeconds);
                button.Q<Label>("time").text = double.IsPositiveInfinity(remaining) ? "∞" : Math.Ceiling(remaining).ToString("0");
                button.text = sprite == null ? Name(id) : string.Empty;
                button.focusable = !hud || _open;
                button.tabIndex = hud ? -1 : 0;
            }
        }

        void RemoveMissing(Dictionary<string, Button> buttons, HashSet<string> present, Button fallback)
        {
            foreach (string key in buttons.Keys.Where(key => !present.Contains(key)).ToArray())
            {
                Button removed = buttons[key];
                bool focused = removed.panel?.focusController.focusedElement == removed || (_anchor == removed && HasDetailFocus);
                if (_anchor == removed) { HideDetails(); }
                VisualElement parent = removed.parent;
                int index = parent.IndexOf(removed);
                removed.RemoveFromHierarchy(); buttons.Remove(key);
                if (focused)
                {
                    Button next = parent.Children().OfType<Button>().ElementAtOrDefault(Math.Min(index, parent.childCount - 1));
                    (next ?? fallback).Focus();
                }
            }
        }

        void ShowDetails(string id, VisualElement anchor, bool hud)
        {
            if (_disposed || (hud ? !_hudVisible : !_open)) { return; }
            _detailId = id; _anchor = anchor; _detailFromHud = hud;
            _detailScroll.scrollOffset = Vector2.zero;
            RefreshDetails();
        }

        void RefreshDetails()
        {
            StatusTargetSnapshot snapshot = _detailFromHud ? _snapshot?.Player : _snapshot?.Selected;
            StatusLayerSnapshot[] layers = snapshot?.Layers.Where(layer => layer.Rules.Id.LocalId == _detailId && ShouldDisplay(layer)).ToArray();
            if (layers == null || layers.Length == 0 || _anchor?.panel == null) { HideDetails(); return; }
            StatusRules rules = layers[0].Rules;
            var text = new StringBuilder(Name(_detailId));
            text.AppendLine().Append(L("status.category." + rules.Category.ToString().ToLowerInvariant()));
            text.AppendLine().Append(L("status.layers", layers.Length, layers.Count(layer => layer.IsActive)));
            text.AppendLine().Append(L("status.repeat." + rules.Repeat.ToString().ToLowerInvariant()));
            if (_definitions.TryGetValue(_detailId, out StatusDefinition definition) && rules.Ailment == AilmentKind.None)
            {
                text.AppendLine().Append(_locale.GetString(definition.LocalizedDescription.Message));
            }
            int layerIndex = 0;
            foreach (StatusLayerSnapshot layer in layers)
            {
                text.AppendLine().AppendLine(L("status.layer", ++layerIndex, L(layer.IsActive ? "status.active" : "status.suppressed"), Duration(layer)));
                text.AppendLine(L("status.source", L("status.source." + layer.Source.Kind.ToString().ToLowerInvariant()), layer.Source.Key));
                foreach (ModifierInstance modifier in layer.Effects.Modifiers)
                {
                    string statName = _stats.TryGetValue(modifier.StatId ?? string.Empty, out StatDefinition stat)
                        ? _locale.GetString(stat.LocalizedName.Message) : modifier.StatId;
                    text.AppendLine(_formatter.FormatModifier(modifier, statName, CombatStatValues.IsPercentage(modifier.StatId)));
                }
                foreach (DamagePacket packet in layer.Effects.PeriodicDamage)
                {
                    text.AppendLine(L("status.periodic", rules.Interval.ToString("0.###"), packet.Amount.ToString("0.##"), _formatter.GetDamageTypeText(packet.CurrentType)));
                }
                foreach (StatusActionBlock block in new[] { StatusActionBlock.Move, StatusActionBlock.Attack, StatusActionBlock.Cast, StatusActionBlock.UseItem })
                {
                    if ((layer.Effects.BlockedActions & block) != 0) { text.AppendLine(L("status.block." + block.ToString().ToLowerInvariant())); }
                }
                if (layer.Effects.Resistance != null)
                {
                    text.AppendLine(L("status.resistance", layer.Effects.Resistance.EffectiveResistance.ToString("0.##")));
                }
            }
            _details.text = text.ToString().TrimEnd();
            _tooltip.style.display = DisplayStyle.Flex;
            Rect anchor = _root.WorldToLocal(_anchor.worldBound);
            float width = 390;
            float maxHeight = Math.Min(480, _root.resolvedStyle.height - 32);
            _tooltip.style.width = width; _tooltip.style.maxHeight = maxHeight;
            _detailScroll.style.maxHeight = Math.Max(40, maxHeight - 32);
            Rect window = _root.WorldToLocal(_window.worldBound);
            float left = !_detailFromHud && window.xMax + width + 24 <= _root.resolvedStyle.width
                ? window.xMax + 8 : anchor.xMax + 8;
            _tooltip.style.left = Math.Clamp(left, 16, Math.Max(16, _root.resolvedStyle.width - width - 16));
            _tooltip.style.top = Math.Clamp(_detailFromHud ? anchor.yMax + 8 : anchor.y, 16,
                Math.Max(16, _root.resolvedStyle.height - maxHeight - 16));
        }

        string Duration(StatusLayerSnapshot layer)
        {
            return layer.Rules.Lifetime == StatusLifetime.Timed ? L("status.seconds", layer.RemainingSeconds.ToString("0.0"))
                : L(layer.Rules.Lifetime == StatusLifetime.SourceOwned ? "status.source_owned" : "status.infinite");
        }
        string Name(string id) { return _definitions.TryGetValue(id, out StatusDefinition definition) ? _locale.GetString(definition.LocalizedName.Message) : id; }
        string L(string key, params object[] args) { return _locale.GetString("ui", key, args); }
        void HideDetails() { _detailId = null; _anchor = null; _tooltip.style.display = DisplayStyle.None; }
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true; _scope.BeginStop(); _locale.LocaleChanged -= OnLocaleChanged;
            _visibilityToggle.clicked -= ToggleOptional;
            _hudVisibilityToggle.clicked -= ToggleOptional;
            _detailScroll.UnregisterCallback<NavigationMoveEvent>(OnDetailNavigation, TrickleDown.TrickleDown);
            _tooltip.UnregisterCallback<PointerEnterEvent>(OnDetailEnter);
            _tooltip.UnregisterCallback<PointerLeaveEvent>(OnDetailLeave);
            foreach (IUnRegister registration in _registrations) { registration.UnRegister(); }
            HideDetails(); _hudBar.Clear(); _bar.Clear(); _actors.Clear();
            _window.style.display = DisplayStyle.None; _hud.style.display = DisplayStyle.None;
        }
    }
}
