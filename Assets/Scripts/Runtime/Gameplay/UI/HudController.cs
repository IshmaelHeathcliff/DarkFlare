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
        Label _weaponLabel;
        VisualElement _weaponIcon;

        public HudSnapshot LastSnapshot { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void RefreshHud()
        {
            if (_healthBar == null || _goldLabel == null || _weaponLabel == null || _weaponIcon == null)
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
            _weaponLabel.text = snapshot.WeaponSummary;
            ItemVisualPresenter.ApplyIcon(_weaponIcon, snapshot.WeaponIconGuid);
            _weaponIcon.EnableInClassList("hud-weapon-icon--empty", string.IsNullOrWhiteSpace(snapshot.WeaponIconGuid));
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
            _weaponLabel = null;
            _weaponIcon = null;
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
            _eventRegistrations.Add(this.RegisterEvent<InventoryChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => RefreshHud()));
            _eventRegistrations.Add(this.RegisterEvent<ItemCraftedEvent>(_ => RefreshHud()));
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
            _weaponLabel = root.Q<Label>("weapon-label");
            _weaponIcon = root.Q<VisualElement>("weapon-icon");

            if (_healthBar == null || _goldLabel == null || _weaponLabel == null || _weaponIcon == null)
            {
                Debug.LogError("[HudController] HUD UXML 缺少 health-bar、gold-label、weapon-label 或 weapon-icon", this);
                return;
            }

            _healthBar.lowValue = 0f;
            _healthBar.highValue = 1f;
            RefreshHud();
            Debug.Log("[HudController] HUD 初始化完成", this);
        }
    }
}
