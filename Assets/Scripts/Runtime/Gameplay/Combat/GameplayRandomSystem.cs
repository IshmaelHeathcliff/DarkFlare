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
            _sequences.TryGetValue(channel, out int sequence);
            _sequences[channel] = unchecked(sequence + 1);
            return Mix(_rootSeed, channel, sequence);
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
