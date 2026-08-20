using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ApplicationFrontEndController : IDisposable
    {
        static readonly UserLanguagePreference[] LanguagePreferences =
        {
            UserLanguagePreference.Auto,
            UserLanguagePreference.SimplifiedChinese,
            UserLanguagePreference.English,
        };

        static readonly string[] LanguageKeys =
        {
            "settings.language.auto",
            "settings.language.zh_hans",
            "settings.language.en",
        };

        readonly VisualElement _root;
        readonly ApplicationHost _host;
        readonly Action<SceneFlowRequest> _runRequest;
        readonly List<string> _languageChoices = new List<string>();

        Button _newGame;
        Button _continue;
        Button _quit;
        DropdownField _language;
        CancellationTokenSource _continueProbeCancellation;
        int _continueRefreshGeneration;
        int _languageRefreshGeneration;
        bool _languageBusy;
        bool _updatingLanguage;
        bool _bound;

        public ApplicationFrontEndController(
            VisualElement root,
            ApplicationHost host,
            Action<SceneFlowRequest> runRequest)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _runRequest = runRequest ?? throw new ArgumentNullException(nameof(runRequest));
        }

        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            _newGame = Require<Button>("front-end-new-game");
            _continue = Require<Button>("front-end-continue");
            _quit = Require<Button>("front-end-quit");
            _language = Require<DropdownField>("front-end-language");
            _newGame.clicked += OnNewGame;
            _continue.clicked += OnContinue;
            _quit.clicked += _host.RequestQuit;
            _language.RegisterValueChangedCallback(OnLanguageChanged);
            _host.Localization.LocaleChanged += OnLocaleChanged;
            _bound = true;
            RefreshLanguageChoices();
        }

        public void RefreshContinueAvailability()
        {
            if (!_bound || _host.ApplicationScope == null || !_host.ApplicationScope.CanAcceptWork)
            {
                _continue?.SetEnabled(false);
                return;
            }

            CancelContinueProbe();
            int generation = ++_continueRefreshGeneration;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            _continueProbeCancellation = cancellation;
            _continue.SetEnabled(false);

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "front-end-probe-continue",
                    async token =>
                    {
                        SceneFlowContinuePreparation preparation =
                            await _host.PrepareContinueAsync(token);

                        if (generation == _continueRefreshGeneration && _continue != null)
                        {
                            _continue.SetEnabled(preparation.Succeeded);
                        }
                    },
                    cancellation.Token,
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                CancelContinueProbe();
                _continue.SetEnabled(false);
            }
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                RefreshContinueAvailability();
                return;
            }

            CancelContinueProbe();
            _continue?.SetEnabled(false);
        }

        public void FocusDefault()
        {
            if (_continue != null && _continue.enabledSelf)
            {
                _continue.Focus();
            }
            else
            {
                _newGame?.Focus();
            }
        }

        public void Dispose()
        {
            if (!_bound)
            {
                return;
            }

            CancelContinueProbe();
            _newGame.clicked -= OnNewGame;
            _continue.clicked -= OnContinue;
            _quit.clicked -= _host.RequestQuit;
            _language.UnregisterValueChangedCallback(OnLanguageChanged);
            if (_host.Localization != null)
            {
                _host.Localization.LocaleChanged -= OnLocaleChanged;
            }
            _bound = false;
        }

        void OnNewGame()
        {
            _runRequest(SceneFlowRequest.StartGame(GameStartIntent.NewGame));
        }

        void OnContinue()
        {
            _runRequest(SceneFlowRequest.StartGame(GameStartIntent.Continue));
        }

        void OnLanguageChanged(ChangeEvent<string> evt)
        {
            if (_updatingLanguage || _languageBusy)
            {
                return;
            }

            int index = _languageChoices.IndexOf(evt.newValue);

            if (index < 0 || index >= LanguagePreferences.Length)
            {
                return;
            }

            _languageBusy = true;
            _language.SetEnabled(false);

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "front-end-language-change",
                    async token =>
                    {
                        await _host.Localization.ChangeLanguageAsync(
                            LanguagePreferences[index],
                            token);
                        _languageBusy = false;
                        RefreshLanguageChoices();
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                _languageBusy = false;
                _language.SetEnabled(true);
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            RefreshLanguageChoices();
        }

        void RefreshLanguageChoices()
        {
            if (!_bound || _host.ApplicationScope == null || !_host.ApplicationScope.CanAcceptWork)
            {
                return;
            }

            int generation = ++_languageRefreshGeneration;

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "front-end-language-refresh",
                    async token =>
                    {
                        List<string> choices = new List<string>(LanguageKeys.Length);

                        for (int i = 0; i < LanguageKeys.Length; i++)
                        {
                            choices.Add(await _host.Localization.GetStringAsync(
                                "ui",
                                LanguageKeys[i],
                                cancellationToken: token));
                        }

                        if (generation != _languageRefreshGeneration || _language == null)
                        {
                            return;
                        }

                        _languageChoices.Clear();
                        _languageChoices.AddRange(choices);
                        int selected = Array.IndexOf(
                            LanguagePreferences,
                            _host.Settings.Current.Language);
                        selected = Math.Max(0, Math.Min(selected, choices.Count - 1));
                        _updatingLanguage = true;

                        try
                        {
                            _language.choices = new List<string>(choices);
                            _language.SetValueWithoutNotify(choices[selected]);
                            _language.SetEnabled(!_languageBusy);
                        }
                        finally
                        {
                            _updatingLanguage = false;
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                _language.SetEnabled(false);
            }
        }

        T Require<T>(string name) where T : VisualElement
        {
            T element = _root.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException($"FrontEnd 缺少元素 {name}");
            }

            return element;
        }

        void CancelContinueProbe()
        {
            _continueRefreshGeneration++;
            CancellationTokenSource cancellation = _continueProbeCancellation;
            _continueProbeCancellation = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
        }
    }
}
