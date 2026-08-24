using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class AccessibilityAndPlatformLifecycleTests : InputTestFixture
    {
        InMemorySettingsStorage _storage;
        SettingsService _settings;
        ApplicationInputService _input;
        AudioService _audio;
        GameTimeService _time;
        AccessibilityService _accessibility;
        PlatformLifecycleService _platform;
        SaveOperationResult _checkpointResult;
        PlatformCheckpointUrgency _lastUrgency;
        System.Exception _checkpointException;
        int _checkpointCount;

        public override void Setup()
        {
            base.Setup();
            _storage = new InMemorySettingsStorage();
            _settings = new SettingsService(_storage);
            Assert.IsTrue(_settings.Initialize().Succeeded);
            _input = new ApplicationInputService(_settings);
            _audio = new AudioService(null, _settings);
            _time = new GameTimeService();
            _accessibility = new AccessibilityService(_settings);
            _checkpointCount = 0;
            _lastUrgency = default;
            _checkpointException = null;
            _checkpointResult = SaveOperationResult.Failure(
                SaveOperation.Save,
                SaveCoordinator.AutoSlot,
                SaveErrorCode.SessionUnavailable);
            _platform = new PlatformLifecycleService(
                _input,
                _audio,
                _time,
                CheckpointAsync);
        }

        public override void TearDown()
        {
            _platform?.Close();
            _accessibility?.Dispose();
            _audio?.Dispose();
            _input?.Dispose();
            _settings?.Close();
            _time?.RestoreAll();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator AccessibilityService_UpdatesAfterCommitAndPublishesOnce()
        {
            return VerifyAccessibilityAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator AccessibilityService_FailedCommitKeepsCurrentProfile()
        {
            return VerifyAccessibilityRollbackAsync().ToCoroutine();
        }

        [Test]
        public void ReduceMotionConsumers_StopContinuousLootMotionAndKeepShortCombatText()
        {
            Assert.IsTrue(LootPickupVisual.ShouldRunContinuousMotion(MotionProfile.Default));
            Assert.IsFalse(LootPickupVisual.ShouldRunContinuousMotion(MotionProfile.Reduced));
            Assert.AreEqual(0.65f, DamageNumberVisual.ResolveTravelDistance(
                MotionProfile.Default));
            Assert.AreEqual(0f, DamageNumberVisual.ResolveTravelDistance(
                MotionProfile.Reduced));
            Assert.AreEqual(0.65f, DamageNumberVisual.ResolveDuration(
                MotionProfile.Default));
            Assert.AreEqual(0.13f, DamageNumberVisual.ResolveDuration(
                MotionProfile.Reduced), 0.0001f);
        }

        [UnityTest]
        public IEnumerator PlatformLifecycle_CoalescesOverlapAndRestoresOnlyAfterAllReasons()
        {
            return VerifyPlatformOverlapAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PlatformLifecycle_MapsBusyAndRejectsWorkDuringShutdown()
        {
            return VerifyPlatformFailuresAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PlatformLifecycle_MapsSaveFailureTimeoutAndCancellation()
        {
            return VerifyPlatformFailureMatrixAsync().ToCoroutine();
        }

        async UniTask VerifyAccessibilityAsync()
        {
            int changed = 0;
            _accessibility.ProfileChanged += profile =>
            {
                changed++;
                Assert.IsTrue(profile.ReduceMotion);
                Assert.IsFalse(profile.AllowContinuousMotion);
            };

            SettingsOperationResult result = await _accessibility.SetReduceMotionAsync(true);
            SettingsOperationResult duplicate = await _accessibility.SetReduceMotionAsync(true);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(duplicate.Succeeded);
            Assert.IsTrue(_settings.Current.ReduceMotion);
            Assert.AreEqual(MotionProfile.Reduced, _accessibility.Profile);
            Assert.AreEqual(1, changed);
        }

        async UniTask VerifyAccessibilityRollbackAsync()
        {
            _storage.FailCommit = true;

            SettingsOperationResult result =
                await _accessibility.SetReduceMotionAsync(true);

            Assert.AreEqual(SettingsOperationCode.StorageFailure, result.Code);
            Assert.AreEqual(MotionProfile.Default, _accessibility.Profile);
            Assert.IsFalse(_settings.Current.ReduceMotion);
        }

        async UniTask VerifyPlatformOverlapAsync()
        {
            PlatformLifecycleResult focusLost = await _platform.HandleFocusChangedAsync(false);
            PlatformLifecycleResult suspended = await _platform.HandlePauseChangedAsync(true);

            Assert.AreEqual(PlatformLifecycleResultCode.SessionUnavailable, focusLost.Code);
            Assert.IsTrue(suspended.Succeeded);
            Assert.IsFalse(suspended.CheckpointRequested);
            Assert.AreEqual(1, _checkpointCount);
            Assert.AreEqual(PlatformCheckpointUrgency.Regular, _lastUrgency);
            Assert.AreEqual(
                PlatformSuspensionReason.FocusLost
                | PlatformSuspensionReason.PlatformSuspended,
                _platform.SuspensionReasons);
            Assert.IsTrue(_input.IsSuspended);
            Assert.IsTrue(_audio.IsSuspended);
            Assert.AreEqual(1, _time.PauseLeaseCount);

            PlatformLifecycleResult focusRestored =
                await _platform.HandleFocusChangedAsync(true);

            Assert.IsTrue(focusRestored.Succeeded);
            Assert.AreEqual(
                PlatformSuspensionReason.PlatformSuspended,
                _platform.SuspensionReasons);
            Assert.IsTrue(_input.IsSuspended);
            Assert.IsTrue(_audio.IsSuspended);
            Assert.AreEqual(1, _time.PauseLeaseCount);

            PlatformLifecycleResult resumed = await _platform.HandlePauseChangedAsync(false);

            Assert.IsTrue(resumed.Succeeded);
            Assert.AreEqual(PlatformLifecycleState.Active, _platform.State);
            Assert.AreEqual(PlatformSuspensionReason.None, _platform.SuspensionReasons);
            Assert.IsFalse(_input.IsSuspended);
            Assert.IsFalse(_audio.IsSuspended);
            Assert.AreEqual(0, _time.PauseLeaseCount);

            await _platform.HandlePauseChangedAsync(true);
            Assert.AreEqual(2, _checkpointCount);
            Assert.AreEqual(PlatformCheckpointUrgency.Urgent, _lastUrgency);
        }

        async UniTask VerifyPlatformFailuresAsync()
        {
            _checkpointResult = SaveOperationResult.Failure(
                SaveOperation.Save,
                SaveCoordinator.AutoSlot,
                SaveErrorCode.OperationInProgress,
                true);

            PlatformLifecycleResult deferred = await _platform.HandleFocusChangedAsync(false);
            PlatformLifecycleResult duplicate = await _platform.HandleFocusChangedAsync(false);

            Assert.AreEqual(PlatformLifecycleResultCode.Deferred, deferred.Code);
            Assert.AreEqual(PlatformLifecycleResultCode.AlreadyApplied, duplicate.Code);
            Assert.AreEqual(1, _checkpointCount);

            _platform.BeginShutdown();
            PlatformLifecycleResult closed = await _platform.HandleFocusChangedAsync(true);

            Assert.AreEqual(PlatformLifecycleResultCode.Closed, closed.Code);
            Assert.AreEqual(PlatformLifecycleState.ShuttingDown, _platform.State);
            _platform.Close();
            _platform.Close();
            Assert.AreEqual(PlatformLifecycleState.Shutdown, _platform.State);
            Assert.AreEqual(0, _time.PauseLeaseCount);
        }

        async UniTask VerifyPlatformFailureMatrixAsync()
        {
            _checkpointResult = SaveOperationResult.Failure(
                SaveOperation.Save,
                SaveCoordinator.AutoSlot,
                SaveErrorCode.CommitFailed,
                true,
                new System.InvalidOperationException("injected save failure"));

            PlatformLifecycleResult failed = await _platform.HandleFocusChangedAsync(false);

            Assert.AreEqual(PlatformLifecycleResultCode.SaveFailed, failed.Code);
            Assert.AreEqual(PlatformLifecycleState.Suspended, _platform.State);
            await _platform.HandleFocusChangedAsync(true);

            _checkpointException = new System.TimeoutException("injected timeout");
            PlatformLifecycleResult timedOut = await _platform.HandlePauseChangedAsync(true);

            Assert.AreEqual(PlatformLifecycleResultCode.TimedOut, timedOut.Code);
            Assert.AreEqual(PlatformLifecycleState.Suspended, _platform.State);
            await _platform.HandlePauseChangedAsync(false);
            _checkpointException = null;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            PlatformLifecycleResult cancelled = await _platform.HandleFocusChangedAsync(
                false,
                cancellation.Token);

            cancellation.Dispose();
            Assert.AreEqual(PlatformLifecycleResultCode.Cancelled, cancelled.Code);
            Assert.AreEqual(PlatformLifecycleState.Active, _platform.State);
            Assert.AreEqual(PlatformSuspensionReason.None, _platform.SuspensionReasons);
        }

        UniTask<SaveOperationResult> CheckpointAsync(
            PlatformCheckpointUrgency urgency,
            CancellationToken cancellationToken)
        {
            _checkpointCount++;
            _lastUrgency = urgency;

            if (_checkpointException != null)
            {
                return UniTask.FromException<SaveOperationResult>(_checkpointException);
            }

            return UniTask.FromResult(_checkpointResult);
        }

        sealed class InMemorySettingsStorage : ILocalSettingsStorage
        {
            UserSettingsDocumentDto _document;

            public bool FailCommit { get; set; }

            public LocalSettingsCommitResult Commit(
                UserSettingsDocumentDto document,
                CancellationToken cancellationToken = default)
            {
                if (FailCommit)
                {
                    return new LocalSettingsCommitResult(
                        LocalSettingsStorageCode.IoFailure,
                        null,
                        SettingsSerializationCode.Success,
                        new System.InvalidOperationException("injected settings failure"));
                }

                _document = document;
                return new LocalSettingsCommitResult(
                    LocalSettingsStorageCode.Success,
                    document,
                    SettingsSerializationCode.Success,
                    null);
            }

            public LocalSettingsLoadResult Load(CancellationToken cancellationToken = default)
            {
                return _document == null
                    ? new LocalSettingsLoadResult(
                        LocalSettingsStorageCode.NotFound,
                        null,
                        SettingsRecoverySource.DefaultMissing,
                        SettingsSerializationCode.Success,
                        null)
                    : new LocalSettingsLoadResult(
                        LocalSettingsStorageCode.Success,
                        _document,
                        SettingsRecoverySource.Current,
                        SettingsSerializationCode.Success,
                        null);
            }
        }
    }
}
