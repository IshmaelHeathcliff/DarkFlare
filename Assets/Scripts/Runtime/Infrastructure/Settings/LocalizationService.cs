using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace DarkFlare
{
    public enum LocalizationOperationCode
    {
        Success,
        AlreadySelected,
        Cancelled,
        InvalidPreference,
        NotInitialized,
        Closed,
        LocaleUnavailable,
        InitializationFailed,
        PreloadFailed,
        SettingsFailed,
    }

    public sealed class LocalizationOperationResult
    {
        public LocalizationOperationCode Code { get; }

        public UserLanguagePreference Preference { get; }

        public string LocaleCode { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == LocalizationOperationCode.Success
            || Code == LocalizationOperationCode.AlreadySelected;

        internal LocalizationOperationResult(
            LocalizationOperationCode code,
            UserLanguagePreference preference,
            string localeCode,
            Exception exception)
        {
            Code = code;
            Preference = preference;
            LocaleCode = localeCode ?? string.Empty;
            Exception = exception;
        }
    }

    public interface ILocalizationRuntime
    {
        string AutomaticLocaleCode { get; }

        bool IsLocaleAvailable(string localeCode);

        UniTask InitializeAsync(CancellationToken cancellationToken);

        UniTask ApplyLocaleAsync(
            string localeCode,
            IReadOnlyList<string> preloadTables,
            CancellationToken cancellationToken);

        UniTask<string> GetStringAsync(
            string tableName,
            string entryKey,
            string localeCode,
            IList<object> arguments,
            CancellationToken cancellationToken);

        string GetString(
            string tableName,
            string entryKey,
            string localeCode,
            IList<object> arguments);
    }

    public sealed class UnityLocalizationRuntime : ILocalizationRuntime
    {
        const string SimplifiedChineseLocaleCode = "zh-Hans";

        public string AutomaticLocaleCode
        {
            get
            {
                Locale selected = LocalizationSettings.SelectedLocale;

                if (selected != null && IsLocaleAvailable(selected.Identifier.Code))
                {
                    return selected.Identifier.Code;
                }

                return SimplifiedChineseLocaleCode;
            }
        }

        public bool IsLocaleAvailable(string localeCode)
        {
            return FindLocale(localeCode) != null;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(
                cancellationToken: cancellationToken);
        }

        public async UniTask ApplyLocaleAsync(
            string localeCode,
            IReadOnlyList<string> preloadTables,
            CancellationToken cancellationToken)
        {
            Locale locale = FindLocale(localeCode);

            if (locale == null)
            {
                throw new InvalidOperationException($"Locale {localeCode} 不可用");
            }

            LocalizationSettings.SelectedLocale = locale;
            await LocalizationSettings.SelectedLocaleAsync.ToUniTask(
                cancellationToken: cancellationToken);

            for (int i = 0; i < preloadTables.Count; i++)
            {
                await LocalizationSettings.StringDatabase
                    .GetTableAsync(preloadTables[i], locale)
                    .ToUniTask(cancellationToken: cancellationToken);
            }

            if (!string.Equals(
                    localeCode,
                    SimplifiedChineseLocaleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                Locale fallbackLocale = FindLocale(SimplifiedChineseLocaleCode);

                for (int i = 0; i < preloadTables.Count; i++)
                {
                    await LocalizationSettings.StringDatabase
                        .GetTableAsync(preloadTables[i], fallbackLocale)
                        .ToUniTask(cancellationToken: cancellationToken);
                }
            }
        }

        public async UniTask<string> GetStringAsync(
            string tableName,
            string entryKey,
            string localeCode,
            IList<object> arguments,
            CancellationToken cancellationToken)
        {
            Locale locale = FindLocale(localeCode);

            if (locale == null)
            {
                return string.Empty;
            }

            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<StringTable>
                tableOperation = LocalizationSettings.StringDatabase.GetTableAsync(
                    tableName,
                    locale);
            await tableOperation.ToUniTask(cancellationToken: cancellationToken);
            StringTable table = tableOperation.Result;
            StringTableEntry entry = table?.GetEntry(entryKey);

            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                return string.Empty;
            }

            return arguments == null || arguments.Count == 0
                ? entry.GetLocalizedString()
                : entry.GetLocalizedString(arguments);
        }

        public string GetString(
            string tableName,
            string entryKey,
            string localeCode,
            IList<object> arguments)
        {
            Locale locale = FindLocale(localeCode);

            if (locale == null)
            {
                return string.Empty;
            }

            StringTable table = LocalizationSettings.StringDatabase.GetTable(tableName, locale);
            StringTableEntry entry = table?.GetEntry(entryKey);

            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                return string.Empty;
            }

            return arguments == null || arguments.Count == 0
                ? entry.GetLocalizedString()
                : entry.GetLocalizedString(arguments);
        }

        static Locale FindLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode)
                || !LocalizationSettings.HasSettings)
            {
                return null;
            }

            IReadOnlyList<Locale> locales = LocalizationSettings.AvailableLocales.Locales;

            for (int i = 0; i < locales.Count; i++)
            {
                Locale locale = locales[i];

                if (locale != null
                    && string.Equals(
                        locale.Identifier.Code,
                        localeCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return locale;
                }
            }

            return null;
        }
    }

    public sealed class LocalizationService
    {
        public const string SimplifiedChineseLocaleCode = "zh-Hans";

        public const string EnglishLocaleCode = "en";

        static readonly IReadOnlyList<string> StartupTables = new[]
        {
            "ui",
            "system",
            "items",
            "stats",
            "affixes",
            "monsters",
        };

        readonly SettingsService _settings;
        readonly ILocalizationRuntime _runtime;
        CancellationToken _lifetimeToken;
        CancellationTokenSource _activeSwitch;
        string _committedLocaleCode = string.Empty;
        int _switchGeneration;
        bool _initialized;
        bool _closed;
        bool _settingsMutationInProgress;

        public LocalizationService(
            SettingsService settings,
            ILocalizationRuntime runtime = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _runtime = runtime ?? new UnityLocalizationRuntime();
        }

        public string CurrentLocaleCode { get; private set; } = string.Empty;

        public event Action<string> LocaleChanged;

        public async UniTask<LocalizationOperationResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            if (_closed)
            {
                return Failure(LocalizationOperationCode.Closed);
            }

            if (_initialized)
            {
                return Success(
                    LocalizationOperationCode.AlreadySelected,
                    _settings.Current.Language,
                    CurrentLocaleCode);
            }

            try
            {
                await _runtime.InitializeAsync(cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return Failure(LocalizationOperationCode.Cancelled, exception: exception);
            }
            catch (Exception exception)
            {
                return Failure(LocalizationOperationCode.InitializationFailed, exception: exception);
            }

            UserLanguagePreference preference = _settings.Current.Language;
            string localeCode = ResolveLocaleCode(preference);

            if (string.IsNullOrEmpty(localeCode) || !_runtime.IsLocaleAvailable(localeCode))
            {
                return Failure(
                    LocalizationOperationCode.LocaleUnavailable,
                    preference,
                    localeCode);
            }

            try
            {
                await _runtime.ApplyLocaleAsync(localeCode, StartupTables, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return Failure(
                    LocalizationOperationCode.Cancelled,
                    preference,
                    localeCode,
                    exception);
            }
            catch (Exception exception)
            {
                return Failure(
                    LocalizationOperationCode.PreloadFailed,
                    preference,
                    localeCode,
                    exception);
            }

            _lifetimeToken = cancellationToken;
            _committedLocaleCode = localeCode;
            CurrentLocaleCode = localeCode;
            _initialized = true;
            _settings.Changed += OnSettingsChanged;
            return Success(LocalizationOperationCode.Success, preference, localeCode);
        }

        public async UniTask<LocalizationOperationResult> ChangeLanguageAsync(
            UserLanguagePreference preference,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return Failure(LocalizationOperationCode.Closed, preference);
            }

            if (!_initialized)
            {
                return Failure(LocalizationOperationCode.NotInitialized, preference);
            }

            if (!Enum.IsDefined(typeof(UserLanguagePreference), preference))
            {
                return Failure(LocalizationOperationCode.InvalidPreference, preference);
            }

            string localeCode = ResolveLocaleCode(preference);

            if (string.IsNullOrEmpty(localeCode) || !_runtime.IsLocaleAvailable(localeCode))
            {
                return Failure(
                    LocalizationOperationCode.LocaleUnavailable,
                    preference,
                    localeCode);
            }

            if (preference == _settings.Current.Language
                && string.Equals(
                    CurrentLocaleCode,
                    localeCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Success(
                    LocalizationOperationCode.AlreadySelected,
                    preference,
                    localeCode);
            }

            int generation = Interlocked.Increment(ref _switchGeneration);
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken,
                cancellationToken);
            CancellationTokenSource previous = Interlocked.Exchange(ref _activeSwitch, source);
            previous?.Cancel();

            try
            {
                await _runtime.ApplyLocaleAsync(localeCode, StartupTables, source.Token);
                source.Token.ThrowIfCancellationRequested();
                EnsureLatest(generation, source.Token);
                await UniTask.WaitUntil(
                    () => !_settings.IsBusy,
                    cancellationToken: source.Token);
                EnsureLatest(generation, source.Token);
                _settingsMutationInProgress = true;
                SettingsOperationResult settingsResult;

                try
                {
                    settingsResult = await _settings.UpdateAsync(
                        _settings.Current.WithLanguage(preference),
                        source.Token);
                }
                finally
                {
                    _settingsMutationInProgress = false;
                }

                if (!settingsResult.Succeeded)
                {
                    await RollbackLatestAsync(generation);
                    return Failure(
                        settingsResult.Code == SettingsOperationCode.Cancelled
                            ? LocalizationOperationCode.Cancelled
                            : LocalizationOperationCode.SettingsFailed,
                        preference,
                        localeCode,
                        settingsResult.Exception);
                }

                EnsureLatest(generation, source.Token);
                _committedLocaleCode = localeCode;
                CurrentLocaleCode = localeCode;
                NotifyLocaleChanged(localeCode);
                return Success(LocalizationOperationCode.Success, preference, localeCode);
            }
            catch (OperationCanceledException exception)
            {
                return Failure(
                    LocalizationOperationCode.Cancelled,
                    preference,
                    localeCode,
                    exception);
            }
            catch (Exception exception)
            {
                await RollbackLatestAsync(generation);
                return Failure(
                    LocalizationOperationCode.PreloadFailed,
                    preference,
                    localeCode,
                    exception);
            }
            finally
            {
                Interlocked.CompareExchange(ref _activeSwitch, null, source);
                source.Dispose();
            }
        }

        public async UniTask<string> GetStringAsync(
            string tableName,
            string entryKey,
            IList<object> arguments = null,
            CancellationToken cancellationToken = default)
        {
            if (!_initialized || _closed)
            {
                return $"[{tableName}.{entryKey}]";
            }

            string value = await _runtime.GetStringAsync(
                tableName,
                entryKey,
                CurrentLocaleCode,
                arguments,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.Equals(
                    CurrentLocaleCode,
                    SimplifiedChineseLocaleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = await _runtime.GetStringAsync(
                    tableName,
                    entryKey,
                    SimplifiedChineseLocaleCode,
                    arguments,
                    cancellationToken);
            }

            return string.IsNullOrWhiteSpace(value)
                ? $"[{tableName}.{entryKey}]"
                : value;
        }

        public string GetString(
            string tableName,
            string entryKey,
            IList<object> arguments = null)
        {
            if (!_initialized || _closed)
            {
                return $"[{tableName}.{entryKey}]";
            }

            string value = _runtime.GetString(
                tableName,
                entryKey,
                CurrentLocaleCode,
                arguments);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.Equals(
                    CurrentLocaleCode,
                    SimplifiedChineseLocaleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = _runtime.GetString(
                    tableName,
                    entryKey,
                    SimplifiedChineseLocaleCode,
                    arguments);
            }

            return string.IsNullOrWhiteSpace(value)
                ? $"[{tableName}.{entryKey}]"
                : value;
        }

        public string GetString(LocalizedMessage message)
        {
            if (message.IsEmpty)
            {
                return string.Empty;
            }

            return GetString(
                message.TableName,
                message.EntryKey,
                message.Arguments);
        }

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _settings.Changed -= OnSettingsChanged;
            CancellationTokenSource active = Interlocked.Exchange(ref _activeSwitch, null);
            active?.Cancel();
            LocaleChanged = null;
        }

        void OnSettingsChanged(UserSettingsSnapshot settings)
        {
            if (_closed || !_initialized || _settingsMutationInProgress)
            {
                return;
            }

            ReconcileExternalSettingsAsync(settings.Language).Forget();
        }

        async UniTask ReconcileExternalSettingsAsync(UserLanguagePreference preference)
        {
            string localeCode = ResolveLocaleCode(preference);

            if (string.IsNullOrEmpty(localeCode)
                || !_runtime.IsLocaleAvailable(localeCode)
                || string.Equals(
                    localeCode,
                    CurrentLocaleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int generation = Interlocked.Increment(ref _switchGeneration);
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            CancellationTokenSource previous = Interlocked.Exchange(ref _activeSwitch, source);
            previous?.Cancel();

            try
            {
                await _runtime.ApplyLocaleAsync(localeCode, StartupTables, source.Token);
                EnsureLatest(generation, source.Token);
                _committedLocaleCode = localeCode;
                CurrentLocaleCode = localeCode;
                NotifyLocaleChanged(localeCode);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                await RollbackLatestAsync(generation);
            }
            finally
            {
                Interlocked.CompareExchange(ref _activeSwitch, null, source);
                source.Dispose();
            }
        }

        async UniTask RollbackLatestAsync(int generation)
        {
            if (generation != Volatile.Read(ref _switchGeneration)
                || string.IsNullOrEmpty(_committedLocaleCode))
            {
                return;
            }

            try
            {
                await _runtime.ApplyLocaleAsync(
                    _committedLocaleCode,
                    StartupTables,
                    _lifetimeToken);
                CurrentLocaleCode = _committedLocaleCode;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        string ResolveLocaleCode(UserLanguagePreference preference)
        {
            switch (preference)
            {
                case UserLanguagePreference.Auto:
                    return _runtime.AutomaticLocaleCode;
                case UserLanguagePreference.SimplifiedChinese:
                    return SimplifiedChineseLocaleCode;
                case UserLanguagePreference.English:
                    return EnglishLocaleCode;
                default:
                    return string.Empty;
            }
        }

        void NotifyLocaleChanged(string localeCode)
        {
            Action<string> changed = LocaleChanged;

            if (changed == null)
            {
                return;
            }

            Delegate[] handlers = changed.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<string>)handlers[i]).Invoke(localeCode);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        void EnsureLatest(int generation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (generation != Volatile.Read(ref _switchGeneration))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        static LocalizationOperationResult Success(
            LocalizationOperationCode code,
            UserLanguagePreference preference,
            string localeCode)
        {
            return new LocalizationOperationResult(code, preference, localeCode, null);
        }

        static LocalizationOperationResult Failure(
            LocalizationOperationCode code,
            UserLanguagePreference preference = UserLanguagePreference.Auto,
            string localeCode = null,
            Exception exception = null)
        {
            return new LocalizationOperationResult(code, preference, localeCode, exception);
        }
    }
}
