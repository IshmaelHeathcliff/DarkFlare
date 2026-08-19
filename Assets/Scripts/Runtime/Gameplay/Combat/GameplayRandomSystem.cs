using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public enum GameplayRandomChannel
    {
        SpawnPosition,
        MonsterInstance,
        PlayerAttack,
        MonsterAttack,
        Loot,
        Crafting,
    }

    public sealed class GameplayRandomState
    {
        readonly IReadOnlyDictionary<GameplayRandomChannel, int> _nextSequences;

        public int RootSeed { get; }

        public IReadOnlyDictionary<GameplayRandomChannel, int> NextSequences => _nextSequences;

        public GameplayRandomState(
            int rootSeed,
            IEnumerable<KeyValuePair<GameplayRandomChannel, int>> nextSequences)
        {
            if (nextSequences == null)
            {
                throw new ArgumentNullException(nameof(nextSequences));
            }

            Dictionary<GameplayRandomChannel, int> copied = new Dictionary<GameplayRandomChannel, int>();

            foreach (KeyValuePair<GameplayRandomChannel, int> pair in nextSequences)
            {
                if (!Enum.IsDefined(typeof(GameplayRandomChannel), pair.Key))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(nextSequences),
                        $"未知随机通道：{pair.Key}");
                }

                if (pair.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(nextSequences),
                        $"随机通道 {pair.Key} 的序号不能小于 0");
                }

                if (!copied.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException(
                        $"随机通道重复：{pair.Key}",
                        nameof(nextSequences));
                }
            }

            Array channels = Enum.GetValues(typeof(GameplayRandomChannel));

            if (copied.Count != channels.Length)
            {
                throw new ArgumentException("随机状态必须包含全部已登记通道", nameof(nextSequences));
            }

            for (int i = 0; i < channels.Length; i++)
            {
                GameplayRandomChannel channel = (GameplayRandomChannel)channels.GetValue(i);

                if (!copied.ContainsKey(channel))
                {
                    throw new ArgumentException(
                        $"随机状态缺少通道：{channel}",
                        nameof(nextSequences));
                }
            }

            RootSeed = rootSeed;
            _nextSequences = copied;
        }
    }

    public class GameplayRandomSystem : AbstractSystem
    {
        readonly Dictionary<GameplayRandomChannel, int> _sequences = new Dictionary<GameplayRandomChannel, int>();

        bool _configured;
        int _rootSeed;

        public int RootSeed
        {
            get
            {
                EnsureConfigured();
                return _rootSeed;
            }
        }

        protected override void OnInit()
        {
        }

        public void Configure(bool useFixedSeed, int fixedSeed)
        {
            _rootSeed = useFixedSeed ? fixedSeed : CreateRootSeed();
            _sequences.Clear();
            _configured = true;
            Debug.Log($"[GameplayRandomSystem] 根种子: {_rootSeed}（{(useFixedSeed ? "固定" : "随机")}）");
        }

        public int NextSeed(GameplayRandomChannel channel)
        {
            EnsureConfigured();

            if (!Enum.IsDefined(typeof(GameplayRandomChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            _sequences.TryGetValue(channel, out int sequence);
            _sequences[channel] = checked(sequence + 1);
            return Mix(_rootSeed, channel, sequence);
        }

        public GameplayRandomState CaptureState()
        {
            EnsureConfigured();
            Array channels = Enum.GetValues(typeof(GameplayRandomChannel));
            List<KeyValuePair<GameplayRandomChannel, int>> sequences =
                new List<KeyValuePair<GameplayRandomChannel, int>>(channels.Length);

            for (int i = 0; i < channels.Length; i++)
            {
                GameplayRandomChannel channel = (GameplayRandomChannel)channels.GetValue(i);
                _sequences.TryGetValue(channel, out int sequence);
                sequences.Add(new KeyValuePair<GameplayRandomChannel, int>(channel, sequence));
            }

            return new GameplayRandomState(_rootSeed, sequences);
        }

        public void RestoreState(GameplayRandomState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _rootSeed = state.RootSeed;
            _sequences.Clear();

            foreach (KeyValuePair<GameplayRandomChannel, int> pair in state.NextSequences)
            {
                _sequences.Add(pair.Key, pair.Value);
            }

            _configured = true;
        }

        void EnsureConfigured()
        {
            if (!_configured)
            {
                Configure(false, 0);
            }
        }

        static int CreateRootSeed()
        {
            return unchecked(Guid.NewGuid().GetHashCode() ^ Environment.TickCount);
        }

        static int Mix(int rootSeed, GameplayRandomChannel channel, int sequence)
        {
            unchecked
            {
                uint value = (uint)rootSeed;
                value ^= 0x9E3779B9u + (uint)channel * 0x85EBCA6Bu;
                value = (value ^ value >> 16) * 0x7FEB352Du;
                value ^= (uint)sequence * 0x846CA68Bu;
                value = (value ^ value >> 15) * 0x846CA68Bu;
                return (int)(value ^ value >> 16);
            }
        }
    }
}
