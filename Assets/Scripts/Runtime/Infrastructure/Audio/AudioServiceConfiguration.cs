using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace DarkFlare
{
    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField]
        [LabelText("Cue ID")]
        string _cueId;

        [SerializeField]
        [LabelText("分类")]
        AudioCategory _category = AudioCategory.SoundEffects;

        [SerializeField]
        [LabelText("音频 Addressable")]
        AssetReferenceT<AudioClip> _clip;

        [SerializeField]
        [LabelText("基础音量")]
        [Range(0f, 1f)]
        float _volume = 1f;

        [SerializeField]
        [LabelText("循环")]
        bool _loop;

        [SerializeField]
        [LabelText("最大并发")]
        [Min(1)]
        int _maxConcurrency = 1;

        [SerializeField]
        [LabelText("超限策略")]
        AudioConcurrencyPolicy _concurrencyPolicy = AudioConcurrencyPolicy.RejectNew;

        [SerializeField]
        [LabelText("淡入秒数")]
        [Min(0f)]
        float _fadeInSeconds;

        [SerializeField]
        [LabelText("淡出秒数")]
        [Min(0f)]
        float _fadeOutSeconds;

        public AudioCueId CueId => new AudioCueId(_cueId);

        public string CueIdValue => _cueId ?? string.Empty;

        public AudioCategory Category => _category;

        public AssetReferenceT<AudioClip> Clip => _clip;

        public float Volume => _volume;

        public bool Loop => _loop;

        public int MaxConcurrency => _maxConcurrency;

        public AudioConcurrencyPolicy ConcurrencyPolicy => _concurrencyPolicy;

        public float FadeInSeconds => _fadeInSeconds;

        public float FadeOutSeconds => _fadeOutSeconds;

        internal AudioCueDefinition(
            AudioCueId cueId,
            AudioCategory category,
            AssetReferenceT<AudioClip> clip,
            float volume,
            bool loop,
            int maxConcurrency,
            AudioConcurrencyPolicy concurrencyPolicy,
            float fadeInSeconds,
            float fadeOutSeconds)
        {
            _cueId = cueId.Value;
            _category = category;
            _clip = clip;
            _volume = volume;
            _loop = loop;
            _maxConcurrency = maxConcurrency;
            _concurrencyPolicy = concurrencyPolicy;
            _fadeInSeconds = fadeInSeconds;
            _fadeOutSeconds = fadeOutSeconds;
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(_cueId)
                && _clip != null
                && !string.IsNullOrWhiteSpace(_clip.AssetGUID)
                && _volume >= 0f
                && _volume <= 1f
                && _maxConcurrency > 0
                && _fadeInSeconds >= 0f
                && _fadeOutSeconds >= 0f;
        }
    }

    [CreateAssetMenu(
        fileName = "AudioServiceConfiguration",
        menuName = "DarkFlare/基础设施/音频服务配置")]
    public sealed class AudioServiceConfiguration : ScriptableObject
    {
        public const string Address = "infrastructure/audio/configuration";
        public const string MasterVolumeParameter = "MasterVolumeDb";
        public const string MusicVolumeParameter = "MusicVolumeDb";
        public const string SoundEffectsVolumeParameter = "SfxVolumeDb";
        public const string UiVolumeParameter = "UiVolumeDb";

        [SerializeField]
        [LabelText("混音器")]
        AudioMixer _mixer;

        [SerializeField]
        [LabelText("音乐组")]
        AudioMixerGroup _musicGroup;

        [SerializeField]
        [LabelText("音效组")]
        AudioMixerGroup _soundEffectsGroup;

        [SerializeField]
        [LabelText("界面组")]
        AudioMixerGroup _uiGroup;

        [SerializeField]
        [LabelText("Cue 定义")]
        List<AudioCueDefinition> _cues = new List<AudioCueDefinition>();

        public AudioMixer Mixer => _mixer;

        public IReadOnlyList<AudioCueDefinition> Cues => _cues;

        public bool TryGetCue(AudioCueId cueId, out AudioCueDefinition cue)
        {
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] != null
                    && string.Equals(
                        _cues[i].CueIdValue,
                        cueId.Value,
                        StringComparison.Ordinal))
                {
                    cue = _cues[i];
                    return true;
                }
            }

            cue = null;
            return false;
        }

        public AudioMixerGroup GetGroup(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music:
                    return _musicGroup;
                case AudioCategory.SoundEffects:
                    return _soundEffectsGroup;
                case AudioCategory.UI:
                    return _uiGroup;
                default:
                    return null;
            }
        }

        public bool IsValid()
        {
            if (_mixer == null
                || _musicGroup == null
                || _soundEffectsGroup == null
                || _uiGroup == null
                || _cues == null)
            {
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _cues.Count; i++)
            {
                AudioCueDefinition cue = _cues[i];

                if (cue == null || !cue.IsValid() || !ids.Add(cue.CueIdValue))
                {
                    return false;
                }
            }

            return true;
        }

        internal void ConfigureForTests(
            AudioMixer mixer,
            AudioMixerGroup musicGroup,
            AudioMixerGroup soundEffectsGroup,
            AudioMixerGroup uiGroup,
            IEnumerable<AudioCueDefinition> cues)
        {
            _mixer = mixer;
            _musicGroup = musicGroup;
            _soundEffectsGroup = soundEffectsGroup;
            _uiGroup = uiGroup;
            _cues = cues == null
                ? new List<AudioCueDefinition>()
                : new List<AudioCueDefinition>(cues);
        }
    }
}
