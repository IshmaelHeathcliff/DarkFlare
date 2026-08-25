using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class HudController : MonoBehaviour, IController
    {
        [SerializeField]
        UIDocument _document;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();

        SceneSessionBinding _sessionBinding;
        ProgressBar _healthBar;
        ProgressBar _manaBar;
        Label _goldLabel;
        Label _skillStatusLabel;
        LocalizationService _localizationService;
        float _lastRequiredMana;
        bool _showsInsufficientMana;

        public HudSnapshot LastSnapshot { get; private set; }

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void RefreshHud()
        {
            if (_healthBar == null || _manaBar == null || _goldLabel == null || _skillStatusLabel == null)
            {
                return;
            }

            HudSnapshot snapshot = this.SendQuery(new GetHudSnapshotQuery());
            LastSnapshot = snapshot;
            _healthBar.value = snapshot.HealthNormalized;
            _healthBar.title = snapshot.HasPlayer
                ? $"{snapshot.CurrentHealth:0.#} / {snapshot.MaxHealth:0.#}"
                : Localize("hud.waiting_player");
            _manaBar.value = snapshot.ManaNormalized;
            _manaBar.title = snapshot.HasPlayer
                ? $"{snapshot.CurrentMana:0.#} / {snapshot.MaxMana:0.#}"
                : Localize("hud.waiting_player");
            _goldLabel.text = Localize("hud.gold", snapshot.Gold);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            _sessionBinding ??= new SceneSessionBinding(
                this,
                BindSession,
                UnbindSession);
            _sessionBinding.Enable();
        }

        void OnDisable()
        {
            _sessionBinding?.Disable();
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_document == null || _document.panelSettings == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[HudController] 缺少 UIDocument 或 PanelSettings，无法初始化 HUD", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _document.rootVisualElement;

            if (root == null || root.panel == null)
            {
                return SceneSessionBindResult.Retry;
            }

            if (!BindVisualTree())
            {
                return SceneSessionBindResult.Failed;
            }

            BindLocalization();
            RefreshHud();
            RegisterEvents();
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

            _healthBar = null;
            _manaBar = null;
            _goldLabel = null;
            _skillStatusLabel = null;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_document == null && gameObject.scene.IsValid())
            {
                _document = gameObject.AddComponent<UIDocument>();
            }
        }

        void RegisterEvents()
        {
            if (_eventRegistrations.Count > 0)
            {
                return;
            }

            _eventRegistrations.Add(this.RegisterEvent<ActorRegisteredEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorResourceChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<SkillCastRejectedEvent>(OnSkillCastRejected));
            _eventRegistrations.Add(this.RegisterEvent<ActorAttackedEvent>(OnActorAttacked));
            _eventRegistrations.Add(this.RegisterEvent<GoldChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => RefreshHud()));
        }

        bool BindVisualTree()
        {
            if (_document == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[HudController] 缺少 UIDocument，无法初始化 HUD", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _healthBar = root.Q<ProgressBar>("health-bar");
            _manaBar = root.Q<ProgressBar>("mana-bar");
            _goldLabel = root.Q<Label>("gold-label");
            _skillStatusLabel = root.Q<Label>("skill-status-label");

            if (_healthBar == null || _manaBar == null || _goldLabel == null || _skillStatusLabel == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[HudController] HUD UXML 缺少生命、法力、技能状态或金币元素", this);
                return false;
            }

            _healthBar.lowValue = 0f;
            _healthBar.highValue = 1f;
            _manaBar.lowValue = 0f;
            _manaBar.highValue = 1f;
            _skillStatusLabel.style.display = DisplayStyle.None;
            RefreshHud();
            ApplicationLog.Info(LogEventIds.GameplayUi, "[HudController] HUD 初始化完成", this);
            return true;
        }

        void OnSkillCastRejected(SkillCastRejectedEvent e)
        {
            if (_skillStatusLabel == null
                || e.Actor == null
                || e.Actor.Team != ActorTeam.Player
                || e.Reason != SkillCastRejectionReason.InsufficientMana)
            {
                return;
            }

            _lastRequiredMana = e.RequiredMana;
            _showsInsufficientMana = true;
            _skillStatusLabel.text = Localize(
                "hud.skill.insufficient_mana",
                _lastRequiredMana);
            _skillStatusLabel.style.display = DisplayStyle.Flex;
        }

        void OnActorAttacked(ActorAttackedEvent e)
        {
            if (_skillStatusLabel == null || e.Actor == null || e.Actor.Team != ActorTeam.Player)
            {
                return;
            }

            _showsInsufficientMana = false;
            _skillStatusLabel.style.display = DisplayStyle.None;
        }

        void BindLocalization()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || ReferenceEquals(_localizationService, host.Localization))
            {
                return;
            }

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
            }

            _localizationService = host.Localization;

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged += OnLocaleChanged;
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            RefreshHud();

            if (_showsInsufficientMana && _skillStatusLabel != null)
            {
                _skillStatusLabel.text = Localize(
                    "hud.skill.insufficient_mana",
                    _lastRequiredMana);
            }
        }

        string Localize(string entryKey, params object[] arguments)
        {
            return _localizationService?.GetString("ui", entryKey, arguments)
                ?? $"[ui.{entryKey}]";
        }
    }
}
