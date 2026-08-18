using System;
using System.Reflection;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class ArchitectureExceptionSafetyTests
    {
        [TestCase(InitializationFailurePoint.Architecture)]
        [TestCase(InitializationFailurePoint.RegisterPatch)]
        [TestCase(InitializationFailurePoint.Model)]
        [TestCase(InitializationFailurePoint.System)]
        public void InitializationFailure_ClearsStaticAndBestEffortRollsBack(
            InitializationFailurePoint failurePoint)
        {
            ConfigurableInitializationArchitecture.Reset(failurePoint);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = ConfigurableInitializationArchitecture.Interface;
            });

            Assert.AreEqual(1, ConfigurableInitializationArchitecture.DeinitCount);

            if (failurePoint == InitializationFailurePoint.Model)
            {
                Assert.AreEqual(1, SuccessfulModel.InitCount);
                Assert.AreEqual(1, SuccessfulModel.DeinitCount);
                Assert.AreEqual(1, ConfigurableModel.InitCount);
                Assert.AreEqual(1, ConfigurableModel.DeinitCount);
            }

            if (failurePoint == InitializationFailurePoint.System)
            {
                Assert.AreEqual(1, SuccessfulModel.DeinitCount);
                Assert.AreEqual(1, ConfigurableModel.DeinitCount);
                Assert.AreEqual(1, SuccessfulSystem.InitCount);
                Assert.AreEqual(1, SuccessfulSystem.DeinitCount);
                Assert.AreEqual(1, ConfigurableSystem.InitCount);
                Assert.AreEqual(1, ConfigurableSystem.DeinitCount);
            }

            ConfigurableInitializationArchitecture.FailurePoint = InitializationFailurePoint.None;
            ConfigurableInitializationArchitecture.OnRegisterPatch = architecture => { };
            IArchitecture recovered = ConfigurableInitializationArchitecture.Interface;

            Assert.IsNotNull(recovered);
            recovered.Deinit();
        }

        [Test]
        public void Deinit_ContinuesAfterFailuresAndAlwaysClearsState()
        {
            CleanupFailureArchitecture.Reset();
            CleanupFailureArchitecture architecture =
                (CleanupFailureArchitecture)CleanupFailureArchitecture.Interface;

            AggregateException exception = Assert.Throws<AggregateException>(architecture.Deinit);

            Assert.AreEqual(3, exception.InnerExceptions.Count);
            Assert.AreEqual("architecture-cleanup", exception.InnerExceptions[0].Message);
            Assert.AreEqual(1, SuccessfulCleanupSystem.DeinitCount);
            Assert.AreEqual(1, ThrowingCleanupSystem.DeinitCount);
            Assert.AreEqual(1, SuccessfulCleanupModel.DeinitCount);
            Assert.AreEqual(1, ThrowingCleanupModel.DeinitCount);
            Assert.IsNull(architecture.GetSystem<SuccessfulCleanupSystem>());
            Assert.IsNull(architecture.GetModel<SuccessfulCleanupModel>());

            CleanupFailureArchitecture.ThrowCleanupFailures = false;
            IArchitecture recreated = CleanupFailureArchitecture.Interface;

            Assert.AreNotSame(architecture, recreated);
            recreated.Deinit();
        }

        [Test]
        public void Provider_NormalStopRequiresStoppedOwnerAndEmergencyStopIsExplicit()
        {
            LifecycleScope normalOwner = LifecycleScope.CreateRoot("Provider-NormalStop-Test");
            LifecycleScope emergencyOwner = null;

            try
            {
                GameArchitectureProvider.StartSession(normalOwner);

                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.StopSession(normalOwner));
                Assert.IsTrue(GameArchitectureProvider.HasCurrent);

                normalOwner.StopAsync().GetAwaiter().GetResult();
                GameArchitectureProvider.StopSession(normalOwner);
                Assert.IsFalse(GameArchitectureProvider.HasCurrent);

                emergencyOwner = LifecycleScope.CreateRoot("Provider-EmergencyStop-Test");
                GameArchitectureProvider.StartSession(emergencyOwner);
                MethodInfo emergencyStop = typeof(GameArchitectureProvider).GetMethod(
                    "EmergencyStopSession",
                    BindingFlags.NonPublic | BindingFlags.Static);

                Assert.IsNotNull(emergencyStop);
                emergencyStop.Invoke(null, new object[] { emergencyOwner });

                Assert.AreEqual(LifecycleScopeState.Stopped, emergencyOwner.State);
                Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            }
            finally
            {
                StopProviderOwnerIfNeeded();
                StopScopeIfNeeded(normalOwner);
                StopScopeIfNeeded(emergencyOwner);
            }
        }

        static void StopProviderOwnerIfNeeded()
        {
            if (!GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope ownerScope))
            {
                return;
            }

            ownerScope.StopAsync().GetAwaiter().GetResult();
            GameArchitectureProvider.StopSession(ownerScope);
        }

        static void StopScopeIfNeeded(LifecycleScope scope)
        {
            scope?.StopAsync().GetAwaiter().GetResult();
        }

        public enum InitializationFailurePoint
        {
            None,
            Architecture,
            RegisterPatch,
            Model,
            System
        }

        public sealed class ConfigurableInitializationArchitecture
            : Architecture<ConfigurableInitializationArchitecture>
        {
            public static InitializationFailurePoint FailurePoint { get; set; }

            public static int DeinitCount { get; private set; }

            public static void Reset(InitializationFailurePoint failurePoint)
            {
                FailurePoint = failurePoint;
                DeinitCount = 0;
                SuccessfulModel.Reset();
                ConfigurableModel.Reset();
                SuccessfulSystem.Reset();
                ConfigurableSystem.Reset();
                OnRegisterPatch = failurePoint == InitializationFailurePoint.RegisterPatch
                    ? _ => throw new InvalidOperationException("register-patch")
                    : _ => { };
            }

            protected override void Init()
            {
                if (FailurePoint == InitializationFailurePoint.Architecture)
                {
                    throw new InvalidOperationException("architecture-init");
                }

                RegisterModel(new SuccessfulModel());
                RegisterModel(new ConfigurableModel());
                RegisterSystem(new SuccessfulSystem());
                RegisterSystem(new ConfigurableSystem());
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class SuccessfulModel : AbstractModel
        {
            public static int InitCount { get; private set; }

            public static int DeinitCount { get; private set; }

            public static void Reset()
            {
                InitCount = 0;
                DeinitCount = 0;
            }

            protected override void OnInit()
            {
                InitCount++;
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class ConfigurableModel : AbstractModel
        {
            public static int InitCount { get; private set; }

            public static int DeinitCount { get; private set; }

            public static void Reset()
            {
                InitCount = 0;
                DeinitCount = 0;
            }

            protected override void OnInit()
            {
                InitCount++;

                if (ConfigurableInitializationArchitecture.FailurePoint
                    == InitializationFailurePoint.Model)
                {
                    throw new InvalidOperationException("model-init");
                }
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class SuccessfulSystem : AbstractSystem
        {
            public static int InitCount { get; private set; }

            public static int DeinitCount { get; private set; }

            public static void Reset()
            {
                InitCount = 0;
                DeinitCount = 0;
            }

            protected override void OnInit()
            {
                InitCount++;
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class ConfigurableSystem : AbstractSystem
        {
            public static int InitCount { get; private set; }

            public static int DeinitCount { get; private set; }

            public static void Reset()
            {
                InitCount = 0;
                DeinitCount = 0;
            }

            protected override void OnInit()
            {
                InitCount++;

                if (ConfigurableInitializationArchitecture.FailurePoint
                    == InitializationFailurePoint.System)
                {
                    throw new InvalidOperationException("system-init");
                }
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class CleanupFailureArchitecture : Architecture<CleanupFailureArchitecture>
        {
            public static bool ThrowCleanupFailures { get; set; }

            public static void Reset()
            {
                ThrowCleanupFailures = true;
                SuccessfulCleanupSystem.DeinitCount = 0;
                ThrowingCleanupSystem.DeinitCount = 0;
                SuccessfulCleanupModel.DeinitCount = 0;
                ThrowingCleanupModel.DeinitCount = 0;
                OnRegisterPatch = architecture => { };
            }

            protected override void Init()
            {
                RegisterModel(new SuccessfulCleanupModel());
                RegisterModel(new ThrowingCleanupModel());
                RegisterSystem(new SuccessfulCleanupSystem());
                RegisterSystem(new ThrowingCleanupSystem());
            }

            protected override void OnDeinit()
            {
                if (ThrowCleanupFailures)
                {
                    throw new InvalidOperationException("architecture-cleanup");
                }
            }
        }

        public sealed class SuccessfulCleanupSystem : AbstractSystem
        {
            public static int DeinitCount { get; set; }

            protected override void OnInit()
            {
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class ThrowingCleanupSystem : AbstractSystem
        {
            public static int DeinitCount { get; set; }

            protected override void OnInit()
            {
            }

            protected override void OnDeinit()
            {
                DeinitCount++;

                if (CleanupFailureArchitecture.ThrowCleanupFailures)
                {
                    throw new InvalidOperationException("system-cleanup");
                }
            }
        }

        public sealed class SuccessfulCleanupModel : AbstractModel
        {
            public static int DeinitCount { get; set; }

            protected override void OnInit()
            {
            }

            protected override void OnDeinit()
            {
                DeinitCount++;
            }
        }

        public sealed class ThrowingCleanupModel : AbstractModel
        {
            public static int DeinitCount { get; set; }

            protected override void OnInit()
            {
            }

            protected override void OnDeinit()
            {
                DeinitCount++;

                if (CleanupFailureArchitecture.ThrowCleanupFailures)
                {
                    throw new InvalidOperationException("model-cleanup");
                }
            }
        }
    }
}
