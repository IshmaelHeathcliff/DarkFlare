using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public class InteractionPromptController : MonoBehaviour, IController
    {
        [SerializeField]
        RuntimePanelView _uiPanel;

        SceneSessionBinding _sessionBinding;
        GameInput _gameInput;
        IUnRegister _focusRegistration;
        VisualElement _prompt;
        Label _promptLabel;
        WorldInteractionTarget _target;
        LocalizationService _localizationService;

        public WorldInteractionTarget CurrentTarget => _target;

        public bool IsVisible { get; private set; }

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
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

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_uiPanel == null || _uiPanel.Renderer.panelSettings == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InteractionPromptController] 缺少 RuntimePanelView 或 PanelSettings，无法初始化交互提示", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _uiPanel.Root;

            if (root == null || root.panel == null)
            {
                return SceneSessionBindResult.Retry;
            }

            if (!BindVisualTree())
            {
                return SceneSessionBindResult.Failed;
            }

            _gameInput = architecture.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InteractionPromptController] 缺少 GameInput，无法显示交互提示", this);
                return SceneSessionBindResult.Failed;
            }

            _focusRegistration = this.RegisterEvent<InteractionFocusChangedEvent>(OnFocusChanged);
            _gameInput.ModeChanged += OnInputModeChanged;
            _gameInput.BindingDisplayChanged += RefreshPrompt;
            BindLocalization();
            RefreshPrompt();
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
                _gameInput.BindingDisplayChanged -= RefreshPrompt;
            }

            _focusRegistration?.UnRegister();
            _focusRegistration = null;

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

            _gameInput = null;
            _target = null;
            IsVisible = false;
            _prompt = null;
            _promptLabel = null;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_uiPanel == null)
            {
                _uiPanel = GetComponent<RuntimePanelView>();
            }

            if (_uiPanel == null && gameObject.scene.IsValid())
            {
                _uiPanel = gameObject.AddComponent<RuntimePanelView>();
            }
        }

        bool BindVisualTree()
        {
            if (_uiPanel == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InteractionPromptController] 缺少 RuntimePanelView，无法初始化交互提示", this);
                return false;
            }

            VisualElement root = _uiPanel.Root;
            _prompt = root.Q<VisualElement>("interaction-prompt");
            _promptLabel = root.Q<Label>("interaction-prompt-label");

            if (_prompt == null || _promptLabel == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InteractionPromptController] HUD UXML 缺少交互提示元素", this);
                return false;
            }

            return true;
        }

        void OnFocusChanged(InteractionFocusChangedEvent e)
        {
            _target = e.Target;
            RefreshPrompt();
        }

        void OnInputModeChanged(GameInputMode mode)
        {
            RefreshPrompt();
        }

        void RefreshPrompt()
        {
            if (_prompt == null || _promptLabel == null)
            {
                return;
            }

            IsVisible = _target != null
                && _target.CanInteract
                && _gameInput != null
                && _gameInput.CurrentMode == GameInputMode.Gameplay;
            _prompt.style.display = IsVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (IsVisible)
            {
                _promptLabel.text = Localize(
                    "interaction.prompt",
                    _gameInput.GetInteractBindingDisplayString(),
                    Resolve(_target.LocalizedName?.Message ?? default));
            }
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
            RefreshPrompt();
        }

        string Localize(string entryKey, params object[] arguments)
        {
            LocalizedMessage message = LocalizedMessage.Ui(entryKey, arguments);
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }
    }
}
