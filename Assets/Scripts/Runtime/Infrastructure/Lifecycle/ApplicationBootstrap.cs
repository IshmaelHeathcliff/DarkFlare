using UnityEngine;

namespace DarkFlare
{
    public static class ApplicationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            ApplicationDataPathProviderFactory.ResetForSubsystemRegistration();
            ApplicationHost.ResetStaticState();
            GameArchitectureProvider.ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateApplicationHost()
        {
            if (ApplicationHost.HasCurrent)
            {
                return;
            }

            GameObject hostObject = new GameObject("[ApplicationHost]");
            hostObject.AddComponent<ApplicationHost>();
        }
    }
}
