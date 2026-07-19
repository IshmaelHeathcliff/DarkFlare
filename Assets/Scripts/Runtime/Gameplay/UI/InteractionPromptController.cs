using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class InteractionPromptController : MonoBehaviour, IController
    {
        [SerializeField]
        UIDocument _document;

        GameInput _gameInput;
        IUnRegister _focusRegistration;
        VisualElement _prompt;
        Label _promptLabel;
        WorldInteractionTarget _target;

        public WorldInteractionTarget CurrentTarget => _target;

        public bool IsVisible { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();

            if (!BindVisualTree())
            {
                return;
            }

            _gameInput = this.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                Debug.LogError("[InteractionPromptController] 缺少 GameInput，无法显示交互提示", this);
                return;
            }

            _focusRegistration = this.RegisterEvent<InteractionFocusChangedEvent>(OnFocusChanged);
            _gameInput.ModeChanged += OnInputModeChanged;
            RefreshPrompt();
        }

        void OnDisable()
        {
            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
            }

            _focusRegistration?.UnRegister();
            _focusRegistration = null;
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
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_document == null && gameObject.scene.IsValid())
            {
                _document = gameObject.AddComponent<UIDocument>();
            }
        }

        bool BindVisualTree()
        {
            if (_document == null)
            {
                Debug.LogError("[InteractionPromptController] 缺少 UIDocument，无法初始化交互提示", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _prompt = root.Q<VisualElement>("interaction-prompt");
            _promptLabel = root.Q<Label>("interaction-prompt-label");

            if (_prompt == null || _promptLabel == null)
            {
                Debug.LogError("[InteractionPromptController] HUD UXML 缺少交互提示元素", this);
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
                _promptLabel.text = $"E / Y · {_target.DisplayName}";
            }
        }
    }
}
