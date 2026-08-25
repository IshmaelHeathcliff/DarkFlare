using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public enum WorldSortCategory
    {
        StaticProp = 0,
        Interactable = 1,
        Loot = 2,
        Monster = 3,
        Player = 4,
    }

    public readonly struct WorldSortKey : IComparable<WorldSortKey>
    {
        public int QuantizedY { get; }

        public WorldSortCategory Category { get; }

        public string StableSortId { get; }

        public WorldSortKey(int quantizedY, WorldSortCategory category, string stableSortId)
        {
            QuantizedY = quantizedY;
            Category = category;
            StableSortId = stableSortId ?? string.Empty;
        }

        public int CompareTo(WorldSortKey other)
        {
            int yComparison = other.QuantizedY.CompareTo(QuantizedY);

            if (yComparison != 0)
            {
                return yComparison;
            }

            int categoryComparison = Category.CompareTo(other.Category);
            return categoryComparison != 0
                ? categoryComparison
                : string.CompareOrdinal(StableSortId, other.StableSortId);
        }
    }

    public sealed class WorldSortingSystem : IUtility
    {
        public const float QuantizationUnitsPerWorldUnit = 64f;
        public const int FirstSortingOrder = 0;

        static readonly ParticipantComparer Comparer = new ParticipantComparer();

        readonly List<WorldSortParticipant> _participants = new List<WorldSortParticipant>();
        readonly HashSet<string> _stableIds = new HashSet<string>(StringComparer.Ordinal);

        bool _orderDirty;

        public int ParticipantCount => _participants.Count;

        public static int QuantizeY(float worldY)
        {
            return Mathf.RoundToInt(worldY * QuantizationUnitsPerWorldUnit);
        }

        public static WorldSortKey CreateKey(float worldY, WorldSortCategory category, string stableSortId)
        {
            return new WorldSortKey(QuantizeY(worldY), category, stableSortId);
        }

        public bool Register(WorldSortParticipant participant)
        {
            if (participant == null || _participants.Contains(participant))
            {
                return false;
            }

            string stableSortId = participant.StableSortId;

            if (string.IsNullOrWhiteSpace(stableSortId))
            {
                ApplicationLog.Warning(LogEventIds.GameplayVisual, "[WorldSortingSystem] StableSortId 不能为空", participant);
                return false;
            }

            if (!_stableIds.Add(stableSortId))
            {
                ApplicationLog.Warning(LogEventIds.GameplayVisual, $"[WorldSortingSystem] StableSortId 重复：{stableSortId}", participant);
                return false;
            }

            participant.RefreshSortKey();
            _participants.Add(participant);
            _orderDirty = true;
            return true;
        }

        public void Unregister(WorldSortParticipant participant)
        {
            if (participant == null || !_participants.Remove(participant))
            {
                return;
            }

            _stableIds.Remove(participant.StableSortId);
            _orderDirty = true;
        }

        public void Tick()
        {
            bool removedDestroyedParticipant = false;

            for (int i = _participants.Count - 1; i >= 0; i--)
            {
                WorldSortParticipant participant = _participants[i];

                if (participant == null)
                {
                    _participants.RemoveAt(i);
                    removedDestroyedParticipant = true;
                    _orderDirty = true;
                    continue;
                }

                if (participant.RefreshSortKey())
                {
                    _orderDirty = true;
                }
            }

            if (removedDestroyedParticipant)
            {
                RebuildStableIds();
            }

            if (!_orderDirty)
            {
                return;
            }

            _participants.Sort(Comparer);

            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i].ApplySortingOrder(FirstSortingOrder + i);
            }

            _orderDirty = false;
        }

        void RebuildStableIds()
        {
            _stableIds.Clear();

            for (int i = 0; i < _participants.Count; i++)
            {
                WorldSortParticipant participant = _participants[i];

                if (participant != null && !string.IsNullOrWhiteSpace(participant.StableSortId))
                {
                    _stableIds.Add(participant.StableSortId);
                }
            }
        }

        public void ReleaseAll()
        {
            _participants.Clear();
            _stableIds.Clear();
            _orderDirty = false;
        }

        sealed class ParticipantComparer : IComparer<WorldSortParticipant>
        {
            public int Compare(WorldSortParticipant left, WorldSortParticipant right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                return left.SortKey.CompareTo(right.SortKey);
            }
        }
    }
}
