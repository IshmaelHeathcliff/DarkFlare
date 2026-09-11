using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class AudioService : IDisposable
    {
        sealed class Playback
        {
            public int Id { get; }

            public AudioCueDefinition Cue { get; }

            public object Owner { get; }

            public AudioSource Source { get; }

            public IAudioClipLoadHandle LoadHandle { get; }

            public AudioPlaybackHandle PublicHandle { get; set; }

            public float StartedAt { get; }

            public bool Released { get; set; }

            public bool Stopping { get; set; }

            public Playback(
                int id,
                AudioCueDefinition cue,
                object owner,
                AudioSource source,
                IAudioClipLoadHandle loadHandle)
            {
                Id = id;
                Cue = cue;
                Owner = owner;
                Source = source;
                LoadHandle = loadHandle;
                StartedAt = Time.realtimeSinceStartup;
            }
        }

        sealed class PendingLoad
        {
            public object Owner { get; }

            public CancellationTokenSource Cancellation { get; }

            public IAudioClipLoadHandle LoadHandle { get; set; }

            public PendingLoad(object owner, CancellationTokenSource cancellation)
            {
                Owner = owner;
                Cancellation = cancellation;
            }
        }

        const float MinimumDecibels = -80f;

        readonly SettingsService _settings;
        readonly IAudioClipLoader _loader;
        readonly GameObject _root;
        readonly AudioListener _listener;
        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Stack<AudioSource> _availableSources = new Stack<AudioSource>();
        readonly Dictionary<int, Playback> _active = new Dictionary<int, Playback>();
        readonly Dictionary<int, PendingLoad> _pendingLoads =
            new Dictionary<int, PendingLoad>();
        readonly Dictionary<string, int> _pendingByCue =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly CancellationTokenSource _closeCancellation = new CancellationTokenSource();

        AudioServiceConfiguration _configuration;
        int _nextPlaybackId;
        int _nextPendingLoadId;
        bool _suspended;
        bool _closed;

        public AudioService(Transform owner, SettingsService settings)
            : this(owner, settings, new AddressableAudioClipLoader())
        {
        }

        internal AudioService(
            Transform owner,
            SettingsService settings,
            IAudioClipLoader loader)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _root = new GameObject("ApplicationAudio");
            _listener = _root.AddComponent<AudioListener>();

            if (owner != null)
            {
                _root.transform.SetParent(owner, false);
            }

            _settings.Changed += OnSettingsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshAudioListener();
        }

        public bool IsReady => !_closed
            && _configuration != null
            && _configuration.IsValid();

        public bool IsClosed => _closed;

        public bool IsSuspended => _suspended;

        public int ActivePlaybackCount => _active.Count;

        public int SourceCount => _sources.Count;

        public int LoadedHandleCount => _active.Count + _pendingLoads.Count;

        internal UserSettingsSnapshot LastAppliedSettings { get; private set; }

        public AudioOperationResult Configure(AudioServiceConfiguration configuration)
        {
            if (_closed)
            {
                return AudioOperationResult.Failure(
                    AudioOperationCode.Closed,
                    AudioCueIds.UiConfirm);
            }

            if (configuration == null || !configuration.IsValid())
            {
                return AudioOperationResult.Failure(
                    AudioOperationCode.InvalidConfiguration,
                    AudioCueIds.UiConfirm);
            }

            if (ReferenceEquals(_configuration, configuration))
            {
                return AudioOperationResult.AlreadyCompleted(AudioCueIds.UiConfirm);
            }

            if (_configuration != null || _active.Count > 0)
            {
                return AudioOperationResult.Failure(
                    AudioOperationCode.InvalidConfiguration,
                    AudioCueIds.UiConfirm);
            }

            _configuration = configuration;
            ApplySettings(_settings.Current);
            return AudioOperationResult.Success(AudioCueIds.UiConfirm);
        }

        public async UniTask<AudioPlaybackResult> PlayAsync(
            AudioCueId cueId,
            object owner,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return Failure(AudioOperationCode.Closed, cueId);
            }

            if (!IsReady)
            {
                return Failure(AudioOperationCode.InvalidConfiguration, cueId);
            }

            if (owner == null)
            {
                return Failure(AudioOperationCode.InvalidConfiguration, cueId);
            }

            if (!_configuration.TryGetCue(cueId, out AudioCueDefinition cue))
            {
                return Failure(AudioOperationCode.CueNotFound, cueId);
            }

            int concurrent = CountActive(cueId) + GetPendingCount(cueId);

            if (concurrent >= cue.MaxConcurrency)
            {
                if (cue.ConcurrencyPolicy == AudioConcurrencyPolicy.RejectNew)
                {
                    return Failure(AudioOperationCode.ConcurrencyRejected, cueId);
                }

                Playback oldest = FindOldest(cueId);

                if (oldest == null)
                {
                    return Failure(AudioOperationCode.ConcurrencyRejected, cueId);
                }

                ReleasePlayback(oldest);
            }

            IAudioClipLoadHandle loadHandle = null;
            int pendingLoadId = 0;
            IncrementPending(cueId);

            try
            {
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _closeCancellation.Token))
                {
                    pendingLoadId = ++_nextPendingLoadId;
                    _pendingLoads.Add(
                        pendingLoadId,
                        new PendingLoad(owner, linked));
                    loadHandle = _loader.StartLoad(cue.Clip);
                    _pendingLoads[pendingLoadId].LoadHandle = loadHandle;
                    AudioClip clip = await loadHandle.LoadAsync(linked.Token);
                    linked.Token.ThrowIfCancellationRequested();

                    if (_closed)
                    {
                        loadHandle.Release();
                        return Failure(AudioOperationCode.Closed, cueId);
                    }

                    AudioSource source = AcquireSource();
                    source.clip = clip;
                    source.outputAudioMixerGroup = _configuration.GetGroup(cue.Category);
                    source.loop = cue.Loop;
                    source.playOnAwake = false;
                    source.ignoreListenerPause = true;
                    source.volume = cue.FadeInSeconds > 0f ? 0f : cue.Volume;
                    int playbackId = ++_nextPlaybackId;
                    Playback playback = new Playback(
                        playbackId,
                        cue,
                        owner,
                        source,
                        loadHandle);
                    AudioPlaybackHandle publicHandle = new AudioPlaybackHandle(
                        this,
                        playbackId,
                        cueId);
                    playback.PublicHandle = publicHandle;
                    _active.Add(playbackId, playback);
                    source.Play();

                    if (_suspended)
                    {
                        source.Pause();
                    }

                    if (cue.FadeInSeconds > 0f)
                    {
                        FadeInAsync(playback, _closeCancellation.Token).Forget();
                    }

                    if (!cue.Loop)
                    {
                        MonitorNaturalCompletionAsync(
                            playback,
                            _closeCancellation.Token).Forget();
                    }

                    return new AudioPlaybackResult(
                        AudioOperationResult.Success(cueId),
                        publicHandle);
                }
            }
            catch (OperationCanceledException exception)
            {
                loadHandle?.Release();
                return new AudioPlaybackResult(
                    AudioOperationResult.Failure(
                        AudioOperationCode.Cancelled,
                        cueId,
                        exception),
                    null);
            }
            catch (Exception exception)
            {
                loadHandle?.Release();
                return new AudioPlaybackResult(
                    AudioOperationResult.Failure(
                        AudioOperationCode.LoadFailed,
                        cueId,
                        exception),
                    null);
            }
            finally
            {
                if (pendingLoadId != 0)
                {
                    _pendingLoads.Remove(pendingLoadId);
                }

                DecrementPending(cueId);
            }
        }

        public int StopOwner(object owner)
        {
            if (owner == null)
            {
                return 0;
            }

            List<Playback> owned = new List<Playback>();

            foreach (Playback playback in _active.Values)
            {
                if (ReferenceEquals(playback.Owner, owner))
                {
                    owned.Add(playback);
                }
            }

            for (int i = 0; i < owned.Count; i++)
            {
                StopPlayback(owned[i].Id, owned[i].Cue.CueId);
            }

            List<PendingLoad> pending = new List<PendingLoad>();

            foreach (PendingLoad load in _pendingLoads.Values)
            {
                if (ReferenceEquals(load.Owner, owner))
                {
                    pending.Add(load);
                }
            }

            for (int i = 0; i < pending.Count; i++)
            {
                pending[i].Cancellation.Cancel();
                pending[i].LoadHandle?.Release();
            }

            return owned.Count + pending.Count;
        }

        public void Suspend()
        {
            if (_closed || _suspended)
            {
                return;
            }

            _suspended = true;

            foreach (Playback playback in _active.Values)
            {
                playback.Source.Pause();
            }
        }

        public void Resume()
        {
            if (_closed || !_suspended)
            {
                return;
            }

            _suspended = false;

            foreach (Playback playback in _active.Values)
            {
                playback.Source.UnPause();
            }
        }

        public AudioOperationResult StopPlayback(int playbackId, AudioCueId cueId)
        {
            if (!_active.TryGetValue(playbackId, out Playback playback))
            {
                return AudioOperationResult.AlreadyCompleted(cueId);
            }

            if (!_closed
                && playback.Cue.Loop
                && playback.Cue.FadeOutSeconds > 0f
                && !playback.Stopping)
            {
                playback.Stopping = true;
                playback.PublicHandle?.MarkReleased();
                FadeOutAsync(playback, _closeCancellation.Token).Forget();
                return AudioOperationResult.Success(cueId);
            }

            ReleasePlayback(playback);
            return AudioOperationResult.Success(cueId);
        }

        public static float LinearToDecibels(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped <= 0.0001f
                ? MinimumDecibels
                : Mathf.Max(MinimumDecibels, 20f * Mathf.Log10(clamped));
        }

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _settings.Changed -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            List<PendingLoad> pendingLoads = new List<PendingLoad>(
                _pendingLoads.Values);
            _pendingLoads.Clear();

            for (int i = 0; i < pendingLoads.Count; i++)
            {
                pendingLoads[i].Cancellation.Cancel();
                pendingLoads[i].LoadHandle?.Release();
            }

            _closeCancellation.Cancel();
            List<Playback> playbacks = new List<Playback>(_active.Values);

            for (int i = 0; i < playbacks.Count; i++)
            {
                ReleasePlayback(playbacks[i]);
            }

            _active.Clear();
            _pendingByCue.Clear();
            _closeCancellation.Dispose();

            for (int i = 0; i < _sources.Count; i++)
            {
                DestroyObject(_sources[i]?.gameObject);
            }

            _sources.Clear();
            _availableSources.Clear();
            (_loader as IDisposable)?.Dispose();
            DestroyObject(_root);
            _configuration = null;
        }

        async UniTaskVoid MonitorNaturalCompletionAsync(
            Playback playback,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                while (!playback.Released)
                {
                    if (!_suspended
                        && (playback.Source == null || !playback.Source.isPlaying))
                    {
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (!playback.Released)
                {
                    ReleasePlayback(playback);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async UniTaskVoid FadeInAsync(Playback playback, CancellationToken cancellationToken)
        {
            float duration = playback.Cue.FadeInSeconds;
            float elapsed = 0f;

            try
            {
                while (!playback.Released && !playback.Stopping && elapsed < duration)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    elapsed += Time.unscaledDeltaTime;
                    playback.Source.volume = playback.Cue.Volume
                        * Mathf.Clamp01(elapsed / duration);
                }

                if (!playback.Released && !playback.Stopping)
                {
                    playback.Source.volume = playback.Cue.Volume;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async UniTaskVoid FadeOutAsync(
            Playback playback,
            CancellationToken cancellationToken)
        {
            float duration = playback.Cue.FadeOutSeconds;
            float startVolume = playback.Source.volume;
            float elapsed = 0f;

            try
            {
                while (!playback.Released && elapsed < duration)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    elapsed += Time.unscaledDeltaTime;
                    playback.Source.volume = Mathf.Lerp(
                        startVolume,
                        0f,
                        Mathf.Clamp01(elapsed / duration));
                }

                if (!playback.Released)
                {
                    ReleasePlayback(playback);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        void ReleasePlayback(Playback playback)
        {
            if (playback == null || playback.Released)
            {
                return;
            }

            playback.Released = true;
            _active.Remove(playback.Id);
            playback.Source.Stop();
            playback.Source.clip = null;
            playback.Source.outputAudioMixerGroup = null;
            playback.LoadHandle.Release();
            playback.PublicHandle?.MarkReleased();
            _availableSources.Push(playback.Source);
        }

        AudioSource AcquireSource()
        {
            if (_availableSources.Count > 0)
            {
                return _availableSources.Pop();
            }

            GameObject sourceObject = new GameObject($"AudioSource-{_sources.Count + 1}");
            sourceObject.transform.SetParent(_root.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            _sources.Add(source);
            return source;
        }

        int CountActive(AudioCueId cueId)
        {
            int count = 0;

            foreach (Playback playback in _active.Values)
            {
                if (string.Equals(
                    playback.Cue.CueIdValue,
                    cueId.Value,
                    StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        Playback FindOldest(AudioCueId cueId)
        {
            Playback oldest = null;

            foreach (Playback playback in _active.Values)
            {
                if (!string.Equals(
                    playback.Cue.CueIdValue,
                    cueId.Value,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (oldest == null || playback.StartedAt < oldest.StartedAt)
                {
                    oldest = playback;
                }
            }

            return oldest;
        }

        int GetPendingCount(AudioCueId cueId)
        {
            return _pendingByCue.TryGetValue(cueId.Value, out int count)
                ? count
                : 0;
        }

        void IncrementPending(AudioCueId cueId)
        {
            _pendingByCue.TryGetValue(cueId.Value, out int count);
            _pendingByCue[cueId.Value] = count + 1;
        }

        void DecrementPending(AudioCueId cueId)
        {
            if (!_pendingByCue.TryGetValue(cueId.Value, out int count) || count <= 1)
            {
                _pendingByCue.Remove(cueId.Value);
                return;
            }

            _pendingByCue[cueId.Value] = count - 1;
        }

        void OnSettingsChanged(UserSettingsSnapshot settings)
        {
            ApplySettings(settings);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshAudioListener();
        }

        void OnSceneUnloaded(Scene scene)
        {
            RefreshAudioListener();
        }

        void RefreshAudioListener()
        {
            if (_listener == null)
            {
                return;
            }

            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude);
            bool hasSceneListener = false;

            for (int i = 0; i < listeners.Length; i++)
            {
                if (!ReferenceEquals(listeners[i], _listener) && listeners[i].enabled)
                {
                    hasSceneListener = true;
                    break;
                }
            }

            _listener.enabled = !hasSceneListener;
        }

        void ApplySettings(UserSettingsSnapshot settings)
        {
            LastAppliedSettings = settings;

            if (_configuration?.Mixer == null)
            {
                return;
            }

            _configuration.Mixer.SetFloat(
                AudioServiceConfiguration.MasterVolumeParameter,
                settings.Muted ? MinimumDecibels : LinearToDecibels(settings.MasterVolume));
            _configuration.Mixer.SetFloat(
                AudioServiceConfiguration.MusicVolumeParameter,
                LinearToDecibels(settings.MusicVolume));
            _configuration.Mixer.SetFloat(
                AudioServiceConfiguration.SoundEffectsVolumeParameter,
                LinearToDecibels(settings.SoundEffectsVolume));
            _configuration.Mixer.SetFloat(
                AudioServiceConfiguration.UiVolumeParameter,
                LinearToDecibels(settings.UiVolume));
        }

        static AudioPlaybackResult Failure(AudioOperationCode code, AudioCueId cueId)
        {
            return new AudioPlaybackResult(AudioOperationResult.Failure(code, cueId), null);
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
