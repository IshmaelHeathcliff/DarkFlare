using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    // Owned by the menu's SceneSessionBinding, with no independent input or pause lease.
    public sealed class AttributePanelController : IController, IDisposable
    {
        readonly IArchitecture _architecture;
        readonly VisualElement _root;
        readonly ScrollView _scroll;
        readonly LocalizationService _localization;
        readonly ItemDetailFormatter _formatter;
        readonly List<IUnRegister> _registrations = new List<IUnRegister>();
        readonly List<Action> _textBindings = new List<Action>();
        readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>();
        readonly LifecycleScope _sceneScope;
        LifecycleScope _refreshScope;
        AttributeDetailsSnapshot _details;
        AttributeRuntimeSnapshot _runtime;
        Label _resourceStatus;
        Label _skillStatus;
        bool _dirty = true;
        bool _disposed;

        public bool IsVisible { get; private set; }
        public AttributeDetailsSnapshot LastSnapshot => _details;
        public AttributeRuntimeSnapshot RuntimeSnapshot => _runtime;

        public AttributePanelController(IArchitecture architecture, VisualElement root, LocalizationService localization, LifecycleScope sceneScope)
        {
            _architecture = architecture;
            _sceneScope = sceneScope;
            _root = root;
            _scroll = root.Q<ScrollView>("attributes-scroll");
            _localization = localization;
            _formatter = new ItemDetailFormatter((table, key, args) => _localization.GetString(table, key, args));
            _registrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => Invalidate()));
            _registrations.Add(this.RegisterEvent<ActorRegisteredEvent>(_ => Invalidate()));
            _registrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(_ => Invalidate()));
            _registrations.Add(this.RegisterEvent<ActorResourceChangedEvent>(e =>
            {
                if (e.Actor?.Team == ActorTeam.Player) { RefreshRuntime(); }
            }));
            _registrations.Add(this.RegisterEvent<ActorDiedEvent>(_ => RefreshRuntime()));
            _registrations.Add(this.RegisterEvent<ActorRevivedEvent>(_ => RefreshRuntime()));
            _localization.LocaleChanged += OnLocaleChanged;
        }

        public IArchitecture GetArchitecture()
        {
            return _architecture;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || IsVisible == visible) { return; }
            IsVisible = visible;
            _refreshScope?.BeginStop();
            _refreshScope = null;
            if (!visible) { return; }
            Invalidate();
            _refreshScope = _sceneScope.CreateChild("attribute-window");
            _refreshScope.Tasks.Run("refresh-dynamic-values", RefreshLoopAsync, failurePolicy: LifecycleTaskFailurePolicy.Report);
        }

        public bool FocusDefault()
        {
            Toggle toggle = _scroll.Q<Foldout>()?.Q<Toggle>();
            if (toggle == null) { return false; }
            toggle.Focus();
            return true;
        }

        void Invalidate()
        {
            _dirty = true;
            if (!IsVisible || _disposed) { return; }
            _details = this.SendQuery(new GetAttributeDetailsQuery());
            _dirty = false;
            Build();
            RefreshRuntime();
        }

        async UniTask RefreshLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(0.2), ignoreTimeScale: true,
                    cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) { return; }
                if (_dirty) { Invalidate(); }
                else { RefreshRuntime(); }
            }
        }

        void RefreshRuntime()
        {
            if (!IsVisible || _disposed) { return; }
            _runtime = this.SendQuery(new GetAttributeRuntimeQuery());
            if (_details != null && (_details.StatsRevision != _runtime.StatsRevision || _details.HasPlayer != _runtime.HasPlayer))
            {
                Invalidate();
                return;
            }
            RefreshDynamicText();
        }

        void RefreshDynamicText()
        {
            if (_resourceStatus == null) { return; }
            if (!_runtime.HasPlayer)
            {
                _resourceStatus.text = L("state.waiting");
                _skillStatus.text = L("state.waiting");
                return;
            }
            _resourceStatus.text = L("resources.summary", N(_runtime.Health), N(_runtime.MaxHealth),
                N(_runtime.MaxHealth > 0f ? _runtime.Health / _runtime.MaxHealth * 100f : 0f),
                N(_runtime.Mana), N(_runtime.MaxMana), N(_runtime.MaxMana > 0f ? _runtime.Mana / _runtime.MaxMana * 100f : 0f),
                N(_runtime.HealthRegeneration), N(_runtime.ManaRegeneration), L("state." + _runtime.ResourceState));
            _skillStatus.text = L("skill.summary", N(_details.ManaCost), N(_details.Interval),
                N(_runtime.RemainingSeconds), L("state." + _runtime.SkillState));
        }

        void Build()
        {
            Vector2 offset = _scroll.scrollOffset;
            VisualElement focus = _root.panel?.focusController.focusedElement as VisualElement;
            string focusName = focus != null && _root.Contains(focus) ? focus.name : null;
            foreach (Foldout foldout in _scroll.Query<Foldout>().ToList()) { _expanded[foldout.name] = foldout.value; }
            _scroll.Clear();
            _textBindings.Clear();
            Foldout resources = Group("resources");
            _resourceStatus = AddLabel(resources, () => string.Empty, "attributes-resource-status");
            AddLabel(resources, () => L("resources.explanation"));
            Foldout basic = Group("basic");
            Foldout attack = Group("attack");
            _skillStatus = AddLabel(attack, () => string.Empty, "attributes-skill-status");
            AddLabel(attack, () => string.IsNullOrEmpty(_details.SkillId) ? L("state.no_source") : L("skill." + _details.SkillId));
            AddLabel(attack, () => L("base_damage", Message(_details.DamageSourceName)));
            foreach (DamageDetailSnapshot damage in _details.BaseDamages)
            {
                AddLabel(attack, () => _formatter.FormatDamage(damage.DamageType, damage.Minimum, damage.Maximum));
            }
            AddLabel(attack, () => L("attack.conditions"));
            Foldout defense = Group("defense");
            AddLabel(defense, () => L("defense.conditions"));
            foreach (AttributeDetail attribute in _details.Attributes)
            {
                if (CombatStatResolver.IsDamageStat(attribute.Id)) { continue; }
                Foldout group = attribute.Id == StatIds.MaxHealth || attribute.Id == StatIds.Mana
                    || attribute.Id == StatIds.HealthRegeneration || attribute.Id == StatIds.ManaRegeneration ? resources
                    : attribute.Id == StatIds.Strength || attribute.Id == StatIds.Dexterity || attribute.Id == StatIds.Intelligence
                    || attribute.Id == StatIds.MoveSpeed ? basic
                    : attribute.Id == StatIds.CriticalChance || attribute.Id == StatIds.CriticalDamage || attribute.Id == StatIds.Accuracy ? attack : defense;
                AddAttribute(group, attribute);
            }
            Foldout modifiers = Group("modifiers");
            if (_details.Modifiers.Count == 0) { AddLabel(modifiers, () => L("modifiers.empty")); }
            foreach (string id in StatIds.All)
            {
                foreach (ModifierScope scope in Enum.GetValues(typeof(ModifierScope)))
                {
                    var matching = new List<ModifierInstance>();
                    foreach (ModifierInstance modifier in _details.Modifiers)
                    {
                        if (modifier.StatId == id && modifier.Scope == scope) { matching.Add(modifier); }
                    }
                    if (matching.Count == 0) { continue; }
                    Foldout section = Fold(modifiers, "modifiers-" + id + "-" + scope, () => Stat(id) + " · " + L("scope." + scope), false);
                    foreach (ModifierInstance modifier in matching)
                    {
                        AddLabel(section, () => FormatModifier(modifier));
                    }
                }
            }
            OnLocaleChanged(null);
            _scroll.schedule.Execute(() =>
            {
                if (_disposed) { return; }
                _scroll.scrollOffset = offset;
                if (!string.IsNullOrEmpty(focusName)) { _root.Q(focusName)?.Focus(); }
            });
        }

        void AddAttribute(Foldout group, AttributeDetail attribute)
        {
            bool percent = CombatStatValues.IsPercentage(attribute.Id);
            Foldout row = Fold(group, "attribute-" + attribute.Id,
                () => Stat(attribute.Id) + "   " + N(attribute.EffectiveValue) + (percent ? "%" : string.Empty), false);
            AddLabel(row, () => L("calculation.base", N(attribute.BaseValue)));
            foreach (StatCalculationStep step in attribute.Steps)
            {
                AddLabel(row, () => string.IsNullOrEmpty(step.DerivedFrom)
                    ? L("calculation.step", L("operation." + step.Operation), N(step.Operand), N(step.Result))
                    : L("calculation.derived", Stat(step.DerivedFrom), N(step.Operand), N(step.Result)));
            }
            AddLabel(row, () => L("calculation.result", N(attribute.RawValue), N(attribute.EffectiveValue)));
            if (attribute.Id == StatIds.CriticalDamage)
            {
                AddLabel(row, () => L("critical_multiplier", N(CombatStatValues.CriticalMultiplier(attribute.RawValue))));
            }
            foreach (ModifierInstance modifier in _details.Modifiers)
            {
                if (modifier.StatId == attribute.Id) { AddLabel(row, () => FormatModifier(modifier)); }
            }
        }

        string FormatModifier(ModifierInstance modifier)
        {
            var text = new StringBuilder();
            text.Append(_formatter.FormatModifier(modifier, Stat(modifier.StatId), CombatStatValues.IsPercentage(modifier.StatId)));
            text.Append("\n").Append(Origin(modifier.Origin));
            bool direct = CombatStatResolver.CanAggregate(modifier);
            bool supportedDamage = CombatStatResolver.IsDamageStat(modifier.StatId)
                && (modifier.Scope == ModifierScope.GlobalActor || modifier.Scope == ModifierScope.LocalItem
                    || modifier.Scope == ModifierScope.Skill || modifier.Scope == ModifierScope.TargetTaken)
                && (modifier.Operation == ModifierOperation.Flat || modifier.Operation == ModifierOperation.Increase
                    || modifier.Operation == ModifierOperation.More || modifier.Operation == ModifierOperation.Conversion
                    || modifier.Operation == ModifierOperation.GainAsExtra);
            if (modifier.Scope == ModifierScope.TargetTaken)
            {
                supportedDamage &= modifier.Operation == ModifierOperation.Increase || modifier.Operation == ModifierOperation.More;
            }
            string status = direct ? modifier.Matches(new CombatTagContext(sourceActorTags: _details.ActorTags, legacyTags: _details.ActorTags)) ? "active" : "unmatched"
                : supportedDamage ? "conditional" : "unsupported";
            text.Append("\n").Append(L("scope." + modifier.Scope)).Append(" · ").Append(L("modifier." + status));
            if (modifier.Query.HasConditions)
            {
                text.Append("\n").Append(L("conditions", ScopeNames(modifier.Query.ScopeMask),
                    TagNames(modifier.RequiredTags), TagNames(modifier.RequiredAnyTags), TagNames(modifier.BlockedTags)));
            }
            return text.ToString();
        }

        string Origin(ModifierOrigin origin)
        {
            if (origin.Kind == ModifierOriginKind.Status) { return L("source.status", origin.StatusId, origin.StackCount); }
            if (origin.Kind == ModifierOriginKind.Monster) { return L("source.monster"); }
            if (string.IsNullOrEmpty(origin.ItemId)) { return L("source.actor"); }
            string slot = EquipmentSlots.GetKey(origin.Slot);
            return _localization.GetString("ui", "equipment.slot." + slot) + " · " + Message(origin.ItemName) + " · " + Message(origin.AffixName);
        }

        string ScopeNames(CombatTagScope scopes)
        {
            var names = new List<string>();
            foreach (CombatTagScope scope in Enum.GetValues(typeof(CombatTagScope)))
            {
                int value = (int)scope;
                if (value > 0 && (value & (value - 1)) == 0 && (scopes & scope) != 0) { names.Add(L("tag_scope." + scope)); }
            }
            return names.Count == 0 ? L("none") : string.Join(" / ", names);
        }

        string TagNames(TagSet tags)
        {
            var names = new List<string>();
            foreach (string id in tags.Ids) { names.Add(_localization.GetString("items", "tag." + id + ".name")); }
            return names.Count == 0 ? L("none") : string.Join(" / ", names);
        }

        Foldout Group(string id)
        {
            return Fold(_scroll, "attributes-group-" + id, () => L("group." + id), true);
        }

        Foldout Fold(VisualElement parent, string name, Func<string> title, bool expanded)
        {
            var foldout = new Foldout { name = name, value = _expanded.TryGetValue(name, out bool saved) ? saved : expanded };
            foldout.AddToClassList("attribute-foldout");
            foldout.Q<Toggle>().name = name + "-toggle";
            parent.Add(foldout);
            _textBindings.Add(() => foldout.text = title());
            return foldout;
        }

        Label AddLabel(VisualElement parent, Func<string> text, string name = null)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList("attribute-explanation");
            parent.Add(label);
            _textBindings.Add(() => label.text = text());
            return label;
        }

        void OnLocaleChanged(string code)
        {
            foreach (Action binding in _textBindings) { binding(); }
            RefreshDynamicText();
        }

        string L(string key, params object[] arguments)
        {
            return _localization.GetString("ui", "attributes." + key, arguments);
        }

        string Stat(string id)
        {
            return _localization.GetString("stats", id);
        }

        string Message(LocalizedMessage message)
        {
            return message.IsEmpty ? L("none") : _localization.GetString(message);
        }

        static string N(float value)
        {
            return ItemDetailFormatter.FormatNumber(value);
        }

        public void Dispose()
        {
            _disposed = true;
            IsVisible = false;
            _refreshScope?.BeginStop();
            _refreshScope = null;
            foreach (IUnRegister registration in _registrations) { registration.UnRegister(); }
            _registrations.Clear();
            _localization.LocaleChanged -= OnLocaleChanged;
            _textBindings.Clear();
            _expanded.Clear();
            _scroll.Clear();
            _details = null;
        }
    }
}
