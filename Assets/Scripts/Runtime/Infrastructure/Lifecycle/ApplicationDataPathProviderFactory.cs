using System;

namespace DarkFlare
{
    internal static class ApplicationDataPathProviderFactory
    {
#if UNITY_INCLUDE_TESTS
        static Func<ISettingsPathProvider> s_settingsFactory;
        static Func<ISavePathProvider> s_saveFactory;
        static int s_installationVersion;
#endif

        internal static ISettingsPathProvider CreateSettingsPathProvider()
        {
#if UNITY_INCLUDE_TESTS
            return s_settingsFactory?.Invoke() ?? new PersistentSettingsPathProvider();
#else
            return new PersistentSettingsPathProvider();
#endif
        }

        internal static ISavePathProvider CreateSavePathProvider()
        {
#if UNITY_INCLUDE_TESTS
            return s_saveFactory?.Invoke() ?? new PersistentSavePathProvider();
#else
            return new PersistentSavePathProvider();
#endif
        }

#if UNITY_INCLUDE_TESTS
        internal static IDisposable InstallForTests(
            Func<ISettingsPathProvider> settingsFactory,
            Func<ISavePathProvider> saveFactory)
        {
            if (settingsFactory == null)
            {
                throw new ArgumentNullException(nameof(settingsFactory));
            }

            if (saveFactory == null)
            {
                throw new ArgumentNullException(nameof(saveFactory));
            }

            if (s_settingsFactory != null || s_saveFactory != null)
            {
                throw new InvalidOperationException("应用数据路径工厂已安装");
            }

            s_installationVersion++;
            s_settingsFactory = settingsFactory;
            s_saveFactory = saveFactory;
            return new Installation(s_installationVersion);
        }

        internal static void ResetStaticState()
        {
            s_installationVersion++;
            s_settingsFactory = null;
            s_saveFactory = null;
        }
#endif

        internal static void ResetForSubsystemRegistration()
        {
#if UNITY_INCLUDE_TESTS
            if (s_settingsFactory != null && s_saveFactory != null)
            {
                return;
            }

            ResetStaticState();
#endif
        }

#if UNITY_INCLUDE_TESTS
        sealed class Installation : IDisposable
        {
            readonly int _version;
            bool _disposed;

            public Installation(int version)
            {
                _version = version;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_version != s_installationVersion)
                {
                    return;
                }

                ResetStaticState();
            }
        }
#endif
    }
}
