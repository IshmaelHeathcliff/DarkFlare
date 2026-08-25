using System;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ApplicationShellController : IDisposable
    {
        const string BrandName = "DARKFLARE";
        const string AlphaVersion = "alpha 0.2.6";

        readonly UIDocument _document;
        readonly ApplicationHost _host;

        ApplicationFrontEndController _frontEndController;
        ApplicationSettingsController _settingsController;
        VisualElement _root;
        VisualElement _frontEndPage;
        Label _frontEndBrand;
        Label _frontEndVersion;
        VisualElement _busyLayer;
        VisualElement _modalLayer;
        VisualElement _toastLayer;
        VisualElement _fatalLayer;
        Label _busyLabel;
        ProgressBar _busyProgress;
        Button _busyCancel;
        Label _modalTitle;
        Label _modalMessage;
        Button _modalRetry;
        Button _modalCancel;
        Label _toastMessage;
        Label _fatalMessage;
        Button _fatalQuit;
        VisualElement _focusBeforeModal;
        SceneFlowRequest? _retryRequest;
        SceneFlowRequest? _lastRequest;
        Action _modalPrimaryAction;
        bool _bound;

        public ApplicationShellController(UIDocument document, ApplicationHost host)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            _root = _document.rootVisualElement;
            _frontEndPage = Require<VisualElement>("application-front-end");
            _frontEndBrand = Require<Label>("front-end-brand");
            _frontEndVersion = Require<Label>("front-end-version");
            _busyLayer = Require<VisualElement>("application-busy");
            _modalLayer = Require<VisualElement>("application-modal");
            _toastLayer = Require<VisualElement>("application-toast");
            _fatalLayer = Require<VisualElement>("application-fatal");
            _busyLabel = Require<Label>("application-busy-label");
            _busyProgress = Require<ProgressBar>("application-busy-progress");
            _busyCancel = Require<Button>("application-busy-cancel");
            _modalTitle = Require<Label>("application-modal-title");
            _modalMessage = Require<Label>("application-modal-message");
            _modalRetry = Require<Button>("application-modal-retry");
            _modalCancel = Require<Button>("application-modal-cancel");
            _toastMessage = Require<Label>("application-toast-message");
            _fatalMessage = Require<Label>("application-fatal-message");
            _fatalQuit = Require<Button>("application-fatal-quit");
            _frontEndController = new ApplicationFrontEndController(
                _root,
                _host,
                RunRequest,
                () => OpenSettings(_frontEndController.FocusDefault));
            _settingsController = new ApplicationSettingsController(
                _root,
                _host,
                ShowToast,
                ShowConfirmation);
            _frontEndBrand.text = BrandName;
            _frontEndVersion.text = AlphaVersion;
            _frontEndController.Bind();
            _settingsController.Bind();
            _busyCancel.clicked += OnBusyCancel;
            _modalRetry.clicked += OnModalPrimary;
            _modalCancel.clicked += HideModal;
            _fatalQuit.clicked += _host.RequestQuit;
            _host.SceneFlow.StateChanged += OnStateChanged;
            _host.SceneFlow.ProgressChanged += OnProgressChanged;
            _host.SceneFlow.RequestCompleted += OnRequestCompleted;
            _bound = true;
            ApplyState(_host.SceneFlow.State);
            HideBusy();
            HideModal();
            HideToast();
            HideFatal();
        }

        public void ShowFailure(SceneFlowResult result)
        {
            if (result == null)
            {
                return;
            }

            PlayerErrorPresentation presentation = PlayerErrorCatalog.From(result);

            if (_host.SceneFlow.State == GameFlowState.FatalError
                || presentation.Severity == PlayerErrorSeverity.Fatal)
            {
                _fatalMessage.text = Resolve(presentation.Message, result.ErrorCode.ToString());
                SetVisible(_fatalLayer, true);
                _fatalQuit.Focus();
                return;
            }

            _modalPrimaryAction = !presentation.HasAction(PlayerErrorAction.Retry)
                && presentation.HasAction(PlayerErrorAction.ReturnFrontEnd)
                ? () => RunRequest(SceneFlowRequest.ReturnToFrontEnd())
                : null;
            _retryRequest = presentation.HasAction(PlayerErrorAction.Retry)
                ? CreateRetryRequest(result)
                : null;
            _focusBeforeModal = _root?.panel?.focusController?.focusedElement as VisualElement;
            _modalTitle.text = Resolve(
                LocalizedMessage.Ui("flow.modal.title"),
                "Operation Failed");
            _modalMessage.text = Resolve(presentation.Message, result.ErrorCode.ToString());
            _modalRetry.text = _retryRequest.HasValue
                ? Resolve(LocalizedMessage.Ui("flow.modal.retry"), "Retry")
                : Resolve(LocalizedMessage.Ui("menu.return_confirm.confirm"), "Return");
            _modalCancel.text = Resolve(
                LocalizedMessage.Ui("flow.modal.cancel"),
                "Cancel");
            _modalRetry.style.display = _retryRequest.HasValue || _modalPrimaryAction != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            SetVisible(_modalLayer, true);

            if (_retryRequest.HasValue || _modalPrimaryAction != null)
            {
                _modalRetry.Focus();
            }
            else
            {
                _modalCancel.Focus();
            }
        }

        public void ShowFatal(LocalizedMessage message, string fallback)
        {
            _fatalMessage.text = Resolve(message, fallback);
            SetVisible(_fatalLayer, true);
            _fatalQuit.Focus();
        }

        public void RequestReturnToFrontEnd()
        {
            if (!_bound
                || _host.SceneFlow == null
                || (_host.SceneFlow.State != GameFlowState.InGame
                    && _host.SceneFlow.State != GameFlowState.Paused))
            {
                return;
            }

            _retryRequest = null;
            _modalPrimaryAction = () => RunRequest(SceneFlowRequest.ReturnToFrontEnd());
            _focusBeforeModal = _root?.panel?.focusController?.focusedElement as VisualElement;
            _modalTitle.text = Resolve(
                LocalizedMessage.Ui("menu.return_confirm.title"),
                "Return to Front End");
            _modalMessage.text = Resolve(
                LocalizedMessage.Ui("menu.return_confirm.message"),
                "Save and return to the front end?");
            _modalRetry.text = Resolve(
                LocalizedMessage.Ui("menu.return_confirm.confirm"),
                "Return");
            _modalCancel.text = Resolve(
                LocalizedMessage.Ui("flow.modal.cancel"),
                "Cancel");
            _modalRetry.style.display = DisplayStyle.Flex;
            SetVisible(_modalLayer, true);
            _modalRetry.Focus();
        }

        public void OpenSettings(Action restoreFocus = null)
        {
            if (!_bound || _settingsController == null)
            {
                return;
            }

            _settingsController.Open(restoreFocus ?? _frontEndController.FocusDefault);
        }

        public bool IsSettingsOpen => _settingsController?.IsOpen ?? false;

        public void ShowConfirmation(
            LocalizedMessage title,
            LocalizedMessage message,
            Action confirm)
        {
            if (!_bound || confirm == null)
            {
                return;
            }

            _retryRequest = null;
            _modalPrimaryAction = confirm;
            _focusBeforeModal = _root?.panel?.focusController?.focusedElement as VisualElement;
            _modalTitle.text = Resolve(title, title.EntryKey);
            _modalMessage.text = Resolve(message, message.EntryKey);
            _modalRetry.text = Resolve(
                LocalizedMessage.Ui("flow.modal.confirm"),
                "Confirm");
            _modalCancel.text = Resolve(
                LocalizedMessage.Ui("flow.modal.cancel"),
                "Cancel");
            _modalRetry.style.display = DisplayStyle.Flex;
            SetVisible(_modalLayer, true);
            _modalRetry.Focus();
        }

        public void Dispose()
        {
            if (!_bound)
            {
                return;
            }

            SceneFlowService flow = _host.SceneFlow;

            if (flow != null)
            {
                flow.StateChanged -= OnStateChanged;
                flow.ProgressChanged -= OnProgressChanged;
                flow.RequestCompleted -= OnRequestCompleted;
            }
            _busyCancel.clicked -= OnBusyCancel;
            _modalRetry.clicked -= OnModalPrimary;
            _modalCancel.clicked -= HideModal;
            _fatalQuit.clicked -= _host.RequestQuit;
            _frontEndController?.Dispose();
            _frontEndController = null;
            _settingsController?.Dispose();
            _settingsController = null;
            _host.Audio?.StopOwner(this);
            _bound = false;
        }

        void RunRequest(SceneFlowRequest request)
        {
            if (_host.ApplicationScope == null || !_host.ApplicationScope.CanAcceptWork)
            {
                return;
            }

            _lastRequest = request;

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    $"application-shell-scene-flow:{request.Operation}",
                    async token =>
                    {
                        await _host.SceneFlow.RequestAsync(request, token);
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                _modalPrimaryAction = null;
                _retryRequest = null;
                _focusBeforeModal = _root?.panel?.focusController?.focusedElement as VisualElement;
                _modalTitle.text = Resolve(
                    LocalizedMessage.Ui("flow.modal.title"),
                    "Operation Failed");
                _modalMessage.text = exception.Message;
                _modalRetry.style.display = DisplayStyle.None;
                _modalCancel.text = Resolve(
                    LocalizedMessage.Ui("flow.modal.cancel"),
                    "Cancel");
                SetVisible(_modalLayer, true);
                _modalCancel.Focus();
            }
        }

        void OnStateChanged(GameFlowState previous, GameFlowState current)
        {
            ApplyState(current);
        }

        void OnProgressChanged(SceneFlowProgress progress)
        {
            if (progress.Phase == SceneFlowPhase.Completed)
            {
                HideBusy();
                return;
            }

            SetVisible(_busyLayer, true);
            _busyLabel.text = Resolve(
                LocalizedMessage.Ui($"flow.phase.{ToSnakeCase(progress.Phase.ToString())}"),
                progress.Phase.ToString());
            _busyProgress.style.display = progress.HasMeasuredProgress
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (progress.Progress01.HasValue)
            {
                _busyProgress.value = progress.Progress01.Value * 100f;
            }

            _busyCancel.style.display = progress.CanCancel
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _busyCancel.SetEnabled(progress.CanCancel);

            if (progress.CanCancel)
            {
                _busyCancel.Focus();
            }
        }

        void OnRequestCompleted(SceneFlowResult result)
        {
            HideBusy();

            if (result.Succeeded)
            {
                if (result.Operation == SceneFlowOperation.ReturnToFrontEnd)
                {
                    ShowToast(LocalizedMessage.Ui("flow.toast.returned_front_end"));
                }

                return;
            }

            if (result.ErrorCode == SceneFlowErrorCode.Cancelled
                || result.ErrorCode == SceneFlowErrorCode.Superseded)
            {
                return;
            }

            ShowFailure(result);
        }

        void ApplyState(GameFlowState state)
        {
            bool isFrontEnd = state == GameFlowState.FrontEnd;
            SetVisible(_frontEndPage, isFrontEnd);
            _frontEndController?.SetActive(isFrontEnd);

            if (isFrontEnd)
            {
                _frontEndController?.FocusDefault();
            }

            if (state == GameFlowState.FatalError)
            {
                _fatalMessage.text = Resolve(
                    LocalizedMessage.Ui("flow.error.fatal_cleanup_failed"),
                    "Fatal Error");
                SetVisible(_fatalLayer, true);
            }
        }

        void OnBusyCancel()
        {
            _host.SceneFlow.CancelActive();
        }

        void OnModalPrimary()
        {
            if (_host.Audio != null
                && _host.ApplicationScope != null
                && _host.ApplicationScope.CanAcceptWork)
            {
                _host.ApplicationScope.Tasks.Run(
                    "application-shell-ui-confirm",
                    async token =>
                    {
                        await _host.Audio.PlayAsync(
                            AudioCueIds.UiConfirm,
                            this,
                            token);
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }

            SceneFlowRequest? request = _retryRequest;
            Action action = _modalPrimaryAction;
            HideModal();

            if (action != null)
            {
                action.Invoke();
            }
            else if (request.HasValue)
            {
                RunRequest(request.Value);
            }
        }

        void HideBusy()
        {
            SetVisible(_busyLayer, false);
        }

        void HideModal()
        {
            SetVisible(_modalLayer, false);
            _retryRequest = null;
            _modalPrimaryAction = null;
            VisualElement focus = _focusBeforeModal;
            _focusBeforeModal = null;

            if (focus != null && focus.panel != null && focus.enabledInHierarchy)
            {
                focus.Focus();
            }
            else
            {
                _frontEndController?.FocusDefault();
            }
        }

        void HideToast()
        {
            SetVisible(_toastLayer, false);
        }

        void HideFatal()
        {
            SetVisible(_fatalLayer, false);
        }

        public void ShowToast(LocalizedMessage message)
        {
            _toastMessage.text = Resolve(message, message.EntryKey);
            SetVisible(_toastLayer, true);
            _toastLayer.schedule.Execute(HideToast).StartingIn(2500);
        }

        T Require<T>(string name) where T : VisualElement
        {
            T element = _root.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException($"Application Shell 缺少元素 {name}");
            }

            return element;
        }

        string Resolve(LocalizedMessage message, string fallback)
        {
            string value = _host.Localization?.GetString(message);
            return string.IsNullOrWhiteSpace(value)
                || value.StartsWith("[", StringComparison.Ordinal)
                ? fallback
                : value;
        }

        SceneFlowRequest? CreateRetryRequest(SceneFlowResult result)
        {
            if (_lastRequest.HasValue
                && _lastRequest.Value.Operation == result.Operation)
            {
                return _lastRequest;
            }

            switch (result.Operation)
            {
                case SceneFlowOperation.EnterFrontEnd:
                    return SceneFlowRequest.EnterFrontEnd();
                case SceneFlowOperation.StartGame:
                    return SceneFlowRequest.StartGame(GameStartIntent.NewGame);
                case SceneFlowOperation.ReturnToFrontEnd:
                    return SceneFlowRequest.ReturnToFrontEnd();
                default:
                    return null;
            }
        }

        static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static string ToSnakeCase(string value)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (char.IsUpper(character) && i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }
    }
}
