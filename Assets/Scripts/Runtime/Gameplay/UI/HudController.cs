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

        ProgressBar _healthBar;
        Label _goldLabel;
        Label _armorLabel;
        Label _evasionLabel;
        Label _moveSpeedLabel;
        Label _criticalChanceLabel;
        Label _fireResistanceLabel;
        Label _coldResistanceLabel;
        Label _lightningResistanceLabel;
        Label _chaosResistanceLabel;

        public HudSnapshot LastSnapshot { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void RefreshHud()
        {
            if (_healthBar == null
                || _goldLabel == null
                || _armorLabel == null
                || _evasionLabel == null
                || _moveSpeedLabel == null
                || _criticalChanceLabel == null
                || _fireResistanceLabel == null
                || _coldResistanceLabel == null
                || _lightningResistanceLabel == null
                || _chaosResistanceLabel == null)
            {
                return;
            }

            HudSnapshot snapshot = this.SendQuery(new GetHudSnapshotQuery());
            LastSnapshot = snapshot;
            _healthBar.value = snapshot.HealthNormalized;
            _healthBar.title = snapshot.HasPlayer
                ? $"{snapshot.CurrentHealth:0.#} / {snapshot.MaxHealth:0.#}"
                : "等待玩家...";
            _goldLabel.text = $"金币 {snapshot.Gold}";
            HudAttributeSnapshot attributes = snapshot.Attributes;
            _armorLabel.text = FormatNumber(attributes.Armor, snapshot.HasPlayer);
            _evasionLabel.text = FormatNumber(attributes.Evasion, snapshot.HasPlayer);
            _moveSpeedLabel.text = FormatNumber(attributes.MoveSpeed, snapshot.HasPlayer);
            _criticalChanceLabel.text = FormatPercentage(attributes.CriticalChance, snapshot.HasPlayer);
            _fireResistanceLabel.text = FormatPercentage(attributes.FireResistance, snapshot.HasPlayer);
            _coldResistanceLabel.text = FormatPercentage(attributes.ColdResistance, snapshot.HasPlayer);
            _lightningResistanceLabel.text = FormatPercentage(attributes.LightningResistance, snapshot.HasPlayer);
            _chaosResistanceLabel.text = FormatPercentage(attributes.ChaosResistance, snapshot.HasPlayer);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            RegisterEvents();
            BindVisualTree();
        }

        void OnDisable()
        {
            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _healthBar = null;
            _goldLabel = null;
            _armorLabel = null;
            _evasionLabel = null;
            _moveSpeedLabel = null;
            _criticalChanceLabel = null;
            _fireResistanceLabel = null;
            _coldResistanceLabel = null;
            _lightningResistanceLabel = null;
            _chaosResistanceLabel = null;
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
            _eventRegistrations.Add(this.RegisterEvent<ActorDamagedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ActorRevivedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<GoldChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => RefreshHud()));
        }

        void BindVisualTree()
        {
            if (_document == null)
            {
                Debug.LogError("[HudController] 缺少 UIDocument，无法初始化 HUD", this);
                return;
            }

            VisualElement root = _document.rootVisualElement;
            _healthBar = root.Q<ProgressBar>("health-bar");
            _goldLabel = root.Q<Label>("gold-label");
            _armorLabel = root.Q<Label>("attribute-armor");
            _evasionLabel = root.Q<Label>("attribute-evasion");
            _moveSpeedLabel = root.Q<Label>("attribute-move-speed");
            _criticalChanceLabel = root.Q<Label>("attribute-critical-chance");
            _fireResistanceLabel = root.Q<Label>("attribute-fire-resistance");
            _coldResistanceLabel = root.Q<Label>("attribute-cold-resistance");
            _lightningResistanceLabel = root.Q<Label>("attribute-lightning-resistance");
            _chaosResistanceLabel = root.Q<Label>("attribute-chaos-resistance");

            if (_healthBar == null
                || _goldLabel == null
                || _armorLabel == null
                || _evasionLabel == null
                || _moveSpeedLabel == null
                || _criticalChanceLabel == null
                || _fireResistanceLabel == null
                || _coldResistanceLabel == null
                || _lightningResistanceLabel == null
                || _chaosResistanceLabel == null)
            {
                Debug.LogError("[HudController] HUD UXML 缺少生命、金币或当前属性元素", this);
                return;
            }

            _healthBar.lowValue = 0f;
            _healthBar.highValue = 1f;
            RefreshHud();
            Debug.Log("[HudController] HUD 初始化完成", this);
        }

        static string FormatNumber(float value, bool hasPlayer)
        {
            return hasPlayer ? value.ToString("0.##") : "--";
        }

        static string FormatPercentage(float value, bool hasPlayer)
        {
            return hasPlayer ? $"{value:0.#}%" : "--";
        }
    }
}
