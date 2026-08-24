using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class AudioServiceTests
    {
        InMemorySettingsStorage _storage;
        SettingsService _settings;
        FakeAudioClipLoader _loader;
        AudioServiceConfiguration _configuration;
        AudioService _service;
        AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySettingsStorage();
            _settings = new SettingsService(_storage);
            Assert.IsTrue(_settings.Initialize().Succeeded);
            _clip = AudioClip.Create("AudioServiceTestClip", 44100, 1, 44100, false);
            _loader = new FakeAudioClipLoader(_clip);
            AudioMixer mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(
                "Assets/Audio/DarkFlareAudioMixer.mixer");
            Assert.IsNotNull(mixer);
            Dictionary<string, AudioMixerGroup> groups = new Dictionary<string, AudioMixerGroup>();

            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(string.Empty))
            {
                groups[group.name] = group;
            }

            AudioCueDefinition cue = new AudioCueDefinition(
                AudioCueIds.UiConfirm,
                AudioCategory.UI,
                new AssetReferenceT<AudioClip>("fake-audio-guid"),
                0.8f,
                false,
                1,
                AudioConcurrencyPolicy.RejectNew,
                0f,
                0f);
            _configuration = ScriptableObject.CreateInstance<AudioServiceConfiguration>();
            _configuration.ConfigureForTests(
                mixer,
                groups["Music"],
                groups["SFX"],
                groups["UI"],
                new[] { cue });
            _service = new AudioService(null, _settings, _loader);
            Assert.IsTrue(_service.Configure(_configuration).Succeeded);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _settings?.Close();
            Object.DestroyImmediate(_configuration);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void LinearToDecibels_ClampsAndUsesExpectedEndpoints()
        {
            Assert.AreEqual(-80f, AudioService.LinearToDecibels(0f));
            Assert.AreEqual(0f, AudioService.LinearToDecibels(1f), 0.001f);
            Assert.AreEqual(-6.0206f, AudioService.LinearToDecibels(0.5f), 0.001f);
            Assert.AreEqual(0f, AudioService.LinearToDecibels(2f), 0.001f);
        }

        [UnityTest]
        public IEnumerator Playback_EnforcesConcurrencyAndReleasesHandleExactlyOnce()
        {
            return VerifyPlaybackAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator StopOwner_CancelsPendingAddressableLoadAndReleasesHandle()
        {
            return VerifyPendingOwnerCancellationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SettingsChange_AppliesCommittedSnapshotAndMixerContractIsExposed()
        {
            return VerifyMixerSettingsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SettingsFailure_DoesNotPublishUncommittedAudioSnapshot()
        {
            return VerifySettingsFailureAsync().ToCoroutine();
        }

        async UniTask VerifyPlaybackAsync()
        {
            object owner = new object();
            AudioPlaybackResult first = await _service.PlayAsync(
                AudioCueIds.UiConfirm,
                owner);
            AudioPlaybackResult rejected = await _service.PlayAsync(
                AudioCueIds.UiConfirm,
                new object());

            Assert.IsTrue(first.Succeeded);
            Assert.AreEqual(AudioOperationCode.ConcurrencyRejected, rejected.Operation.Code);
            Assert.AreEqual(1, _service.ActivePlaybackCount);
            Assert.AreEqual(1, _loader.StartCount);
            Assert.AreEqual(AudioOperationCode.Success, first.Handle.Stop().Code);
            Assert.AreEqual(AudioOperationCode.AlreadyCompleted, first.Handle.Stop().Code);
            Assert.AreEqual(0, _service.ActivePlaybackCount);
            Assert.AreEqual(1, _loader.ReleaseCount);

            AudioPlaybackResult owned = await _service.PlayAsync(
                AudioCueIds.UiConfirm,
                owner);

            Assert.IsTrue(owned.Succeeded);
            Assert.AreEqual(1, _service.StopOwner(owner));
            Assert.IsTrue(owned.Handle.IsReleased);
            Assert.AreEqual(2, _loader.ReleaseCount);
        }

        async UniTask VerifyPendingOwnerCancellationAsync()
        {
            object owner = new object();
            _loader.BlockLoads = true;
            UniTask<AudioPlaybackResult> pending = _service.PlayAsync(
                AudioCueIds.UiConfirm,
                owner);
            await UniTask.Yield();

            int stopped = _service.StopOwner(owner);
            AudioPlaybackResult result = await pending;

            Assert.AreEqual(1, stopped);
            Assert.AreEqual(AudioOperationCode.Cancelled, result.Operation.Code);
            Assert.AreEqual(0, _service.ActivePlaybackCount);
            Assert.AreEqual(1, _loader.ReleaseCount);
        }

        async UniTask VerifyMixerSettingsAsync()
        {
            UserSettingsSnapshot updated = _settings.Current.WithAudio(
                0.5f,
                0.25f,
                0.75f,
                0.4f,
                true);

            SettingsOperationResult result = await _settings.UpdateAsync(updated);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(_service.LastAppliedSettings.Muted);
            Assert.AreEqual(0.5f, _service.LastAppliedSettings.MasterVolume);
            Assert.IsTrue(_configuration.Mixer.GetFloat(
                AudioServiceConfiguration.MasterVolumeParameter,
                out float master));
            Assert.IsTrue(_configuration.Mixer.GetFloat(
                AudioServiceConfiguration.MusicVolumeParameter,
                out float music));
            Assert.IsTrue(_configuration.Mixer.GetFloat(
                AudioServiceConfiguration.SoundEffectsVolumeParameter,
                out float soundEffects));
            Assert.IsTrue(_configuration.Mixer.GetFloat(
                AudioServiceConfiguration.UiVolumeParameter,
                out float ui));
            Assert.IsFalse(float.IsNaN(master));
            Assert.IsFalse(float.IsNaN(music));
            Assert.IsFalse(float.IsNaN(soundEffects));
            Assert.IsFalse(float.IsNaN(ui));
        }

        async UniTask VerifySettingsFailureAsync()
        {
            UserSettingsSnapshot applied = _service.LastAppliedSettings;
            _storage.FailCommit = true;

            SettingsOperationResult result = await _settings.UpdateAsync(
                _settings.Current.WithAudio(0.2f, 0.3f, 0.4f, 0.5f, true));

            Assert.AreEqual(SettingsOperationCode.StorageFailure, result.Code);
            Assert.AreSame(applied, _service.LastAppliedSettings);
            Assert.AreEqual(1f, _settings.Current.MasterVolume);
            Assert.IsFalse(_settings.Current.Muted);
        }

        sealed class FakeAudioClipLoader : IAudioClipLoader
        {
            readonly AudioClip _clip;

            public int StartCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public bool BlockLoads { get; set; }

            public FakeAudioClipLoader(AudioClip clip)
            {
                _clip = clip;
            }

            public IAudioClipLoadHandle StartLoad(AssetReferenceT<AudioClip> reference)
            {
                StartCount++;
                return new FakeAudioClipLoadHandle(
                    _clip,
                    BlockLoads,
                    () => ReleaseCount++);
            }
        }

        sealed class FakeAudioClipLoadHandle : IAudioClipLoadHandle
        {
            readonly AudioClip _clip;
            readonly bool _blockLoad;
            readonly System.Action _onRelease;

            bool _released;

            public FakeAudioClipLoadHandle(
                AudioClip clip,
                bool blockLoad,
                System.Action onRelease)
            {
                _clip = clip;
                _blockLoad = blockLoad;
                _onRelease = onRelease;
            }

            public UniTask<AudioClip> LoadAsync(CancellationToken cancellationToken)
            {
                if (!_blockLoad)
                {
                    return UniTask.FromResult(_clip);
                }

                UniTaskCompletionSource<AudioClip> completion =
                    new UniTaskCompletionSource<AudioClip>();
                return completion.Task.AttachExternalCancellation(cancellationToken);
            }

            public void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _onRelease.Invoke();
            }
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
