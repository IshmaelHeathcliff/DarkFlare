using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public class HudController : MonoBehaviour, IController
    {
        const float LowHealthThreshold = 0.25f;
        [SerializeField] RuntimePanelView _uiPanel;
        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        SceneSessionBinding _sessionBinding;
        LifecycleScope _scope;
        ProgressBar _healthBar;
        ProgressBar _manaBar;
        ProgressBar _cooldownBar;
        Label _goldLabel;
        Label _skillStatusLabel;
        Label _skillName;
        Label _skillDetails;
        Label _healthState;
        Label _manaState;
        VisualElement _healthCard;
        VisualElement _lowWarning;
        LocalizationService _localization;
        AccessibilityService _accessibility;
        GameInput _input;
        Tween _warningTween;

        public HudSnapshot LastSnapshot { get; private set; }
        public AttributeRuntimeSnapshot RuntimeSnapshot { get; private set; }
        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;
        public bool IsWarningAnimating => _warningTween.isAlive;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void RefreshHud()
        {
            if (_healthBar == null) { return; }
            LastSnapshot = this.SendQuery(new GetHudSnapshotQuery());
            _goldLabel.text = L("hud.gold", LastSnapshot.Gold);
            RefreshDynamic();
        }

        void RefreshDynamic()
        {
            if (_healthBar == null) { return; }
            RuntimeSnapshot = this.SendQuery(new GetAttributeRuntimeQuery());
            AttributeRuntimeSnapshot state = RuntimeSnapshot;
            float health = state.MaxHealth > 0f ? Mathf.Clamp01(state.Health / state.MaxHealth) : 0f;
            _healthBar.value = health;
            _manaBar.value = state.MaxMana > 0f ? Mathf.Clamp01(state.Mana / state.MaxMana) : 0f;
            _healthBar.title = state.HasPlayer ? $"{state.Health:0.#} / {state.MaxHealth:0.#}" : L("hud.waiting_player");
            _manaBar.title = state.HasPlayer ? $"{state.Mana:0.#} / {state.MaxMana:0.#}" : L("hud.waiting_player");
            _cooldownBar.value = state.CooldownNormalized;
            _skillName.text = string.IsNullOrEmpty(state.SkillId) ? L("attributes.state.no_source") : L("attributes.skill." + state.SkillId);
            _skillDetails.text = L("hud.combat.details", N(state.ManaCost), N(state.Interval), N(state.RemainingSeconds));
            _skillStatusLabel.text = L("attributes.state." + state.SkillState);
            _skillStatusLabel.style.display = DisplayStyle.Flex;
            bool low = state.HasPlayer && state.Health > 0f && health <= LowHealthThreshold;
            _healthCard.EnableInClassList("hud-resource--low", low);
            _healthState.text = !state.HasPlayer ? L("hud.waiting_player") : state.Health <= 0f ? L("attributes.state.dead")
                : low ? L("hud.health.low") : string.Empty;
            _manaState.text = state.ResourceState == "recovering" ? L("hud.mana.recovery")
                : L("attributes.state." + state.ResourceState);
            bool animate = low && state.ResourceState != "paused" && state.ResourceState != "inactive"
                && _input.IsGameplayEnabled && _accessibility.Profile.AllowContinuousMotion;
            if (animate && !_warningTween.isAlive)
            {
                _warningTween = Tween.Custom(0.25f, 0.7f, 0.8f, ApplyWarning, Ease.InOutSine, -1, CycleMode.Yoyo);
            }
            else if (!animate) { StopWarning(); }
        }

        async UniTask RefreshLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(0.1), ignoreTimeScale: true,
                    cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) { return; }
                RefreshDynamic();
            }
        }

        void ApplyWarning(float opacity) { if (_lowWarning != null) { _lowWarning.style.opacity = opacity; } }
        void StopWarning()
        {
            if (_warningTween.isAlive) { _warningTween.Stop(); }
            ApplyWarning(0f);
        }
        void OnMotionChanged(MotionProfile profile) { RefreshDynamic(); }
        void OnInputModeChanged(GameInputMode mode) { RefreshDynamic(); }
        void OnLocaleChanged(string locale) { RefreshHud(); RefreshBindings(); }

        void RefreshBindings()
        {
            VisualElement root = _uiPanel.Root;
            ApplicationInputService input = ApplicationHost.Current.Input;
            Set("hud-inventory-binding", RebindableInputAction.PlayerToggleMenu);
            Set("hud-pause-binding", RebindableInputAction.PlayerPause);
            void Set(string name, RebindableInputAction action)
            {
                Label label = root.Q<Label>(name);
                if (label == null) { return; }
                foreach (string className in new List<string>(label.GetClasses()))
                {
                    if (className.StartsWith("input-glyph--", StringComparison.Ordinal)) { label.RemoveFromClassList(className); }
                }
                InputGlyphToken glyph = input.GetGlyphToken(action);
                bool text = glyph.UsesTextFallback || glyph.GlyphId == "keyboard.keycap";
                if (!text) { label.AddToClassList("input-glyph--" + glyph.GlyphId.Replace('.', '-')); }
                label.EnableInClassList("hud-binding--text", text);
                label.text = text ? glyph.FallbackText : string.Empty;
                label.style.width = text ? new StyleLength(StyleKeyword.Auto) : new StyleLength(28f);
            }
        }

        void Awake() { EnsureComponents(); }
        void OnEnable()
        {
            EnsureComponents();
            _sessionBinding ??= new SceneSessionBinding(this, BindSession, UnbindSession);
            _uiPanel.Reloading += OnPanelReloading;
            _uiPanel.Reloaded += OnPanelReloaded;
            _sessionBinding.Enable();
        }
        void OnPanelReloading()
        {
            _sessionBinding?.Disable();
        }

        void OnPanelReloaded()
        {
            _sessionBinding?.Enable();
        }

        void OnDisable()
        {
            _uiPanel.Reloading -= OnPanelReloading;
            _uiPanel.Reloaded -= OnPanelReloaded;
            _sessionBinding?.Disable();
        }
        void OnValidate() { EnsureComponents(); }
        void EnsureComponents()
        {
            if (_uiPanel == null) { _uiPanel = GetComponent<RuntimePanelView>(); }
            if (_uiPanel == null && gameObject.scene.IsValid()) { _uiPanel = gameObject.AddComponent<RuntimePanelView>(); }
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_uiPanel == null || _uiPanel.Renderer.panelSettings == null) { return SceneSessionBindResult.Failed; }
            VisualElement root = _uiPanel.Root;
            if (root?.panel == null) { return SceneSessionBindResult.Retry; }
            _healthBar = root.Q<ProgressBar>("health-bar");
            _manaBar = root.Q<ProgressBar>("mana-bar");
            _cooldownBar = root.Q<ProgressBar>("skill-cooldown");
            _goldLabel = root.Q<Label>("gold-label");
            _skillStatusLabel = root.Q<Label>("skill-status-label");
            _skillName = root.Q<Label>("hud-skill-name");
            _skillDetails = root.Q<Label>("hud-skill-details");
            _healthState = root.Q<Label>("hud-health-state");
            _manaState = root.Q<Label>("hud-mana-state");
            _healthCard = root.Q("health-card");
            _lowWarning = root.Q("hud-low-warning");
            if (_healthBar == null || _manaBar == null || _cooldownBar == null || _goldLabel == null
                || _skillStatusLabel == null || _skillName == null || _skillDetails == null
                || _healthState == null || _manaState == null || _healthCard == null || _lowWarning == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[HudController] HUD 缺少必要元素", this);
                return SceneSessionBindResult.Failed;
            }
            ApplicationHost host = ApplicationHost.Current;
            _localization = host.Localization;
            _accessibility = host.Accessibility;
            _input = architecture.GetUtility<GameInput>();
            _localization.LocaleChanged += OnLocaleChanged;
            _accessibility.ProfileChanged += OnMotionChanged;
            _input.BindingDisplayChanged += RefreshBindings;
            _input.ModeChanged += OnInputModeChanged;
            _eventRegistrations.Add(this.RegisterEvent<ActorRegisteredEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorResourceChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorDiedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorRevivedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<GoldChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<SkillCastRejectedEvent>(_ => RefreshDynamic()));
            _eventRegistrations.Add(this.RegisterEvent<ActorAttackedEvent>(_ => RefreshDynamic()));
            _scope = host.CurrentSession.SceneScope.CreateChild("combat-hud");
            RefreshHud();
            RefreshBindings();
            _scope.Tasks.Run("refresh-combat-state", RefreshLoopAsync, failurePolicy: LifecycleTaskFailurePolicy.Report);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            _scope?.BeginStop();
            _scope = null;
            StopWarning();
            foreach (IUnRegister registration in _eventRegistrations) { registration.UnRegister(); }
            _eventRegistrations.Clear();
            if (_localization != null) { _localization.LocaleChanged -= OnLocaleChanged; }
            if (_accessibility != null) { _accessibility.ProfileChanged -= OnMotionChanged; }
            if (_input != null)
            {
                _input.BindingDisplayChanged -= RefreshBindings;
                _input.ModeChanged -= OnInputModeChanged;
            }
            _localization = null;
            _accessibility = null;
            _input = null;
            _healthBar = null;
            _manaBar = null;
            _cooldownBar = null;
            _lowWarning = null;
            LastSnapshot = default;
            RuntimeSnapshot = default;
        }
        string L(string key, params object[] args) { return _localization.GetString("ui", key, args); }
        static string N(float value) { return ItemDetailFormatter.FormatNumber(value); }
    }
}
