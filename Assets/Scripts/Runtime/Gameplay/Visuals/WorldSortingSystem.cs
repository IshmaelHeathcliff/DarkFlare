using System;
using System.Collections.Generic;
using Unity.Profiling;
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
        Effect = 5,
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

            int categoryComparison = ((int)Category).CompareTo((int)other.Category);
            return categoryComparison != 0
                ? categoryComparison
                : string.CompareOrdinal(StableSortId, other.StableSortId);
        }
    }

    public sealed class WorldSortRegistration : IDisposable
    {
        readonly WorldSortingSystem _owner;
        internal WorldSortParticipant Participant { get; }
        internal string Identity { get; }
        public bool IsActive { get; internal set; }

        internal WorldSortRegistration(WorldSortingSystem owner, WorldSortParticipant participant)
        {
            _owner = owner; Participant = participant; Identity = participant.StableSortId; IsActive = true;
        }

        public void Dispose() { _owner.Release(this); }
    }

    public sealed class WorldSortingSystem : IUtility
    {
        public const float QuantizationUnitsPerWorldUnit = 64f;
        public const int FirstSortingOrder = 0;
        public const int MaximumParticipants = short.MaxValue + 1;
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("DarkFlare.WorldSorting.Tick");

        static readonly Comparison<WorldSortParticipant> Comparison = CompareParticipants;

        readonly List<WorldSortParticipant> _participants = new List<WorldSortParticipant>();
        readonly HashSet<string> _stableIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<WorldSortParticipant, WorldSortRegistration> _registrations = new Dictionary<WorldSortParticipant, WorldSortRegistration>();
        readonly int _capacity;

        bool _orderDirty;

        public int ParticipantCount => _participants.Count;
        public long RejectedCount { get; private set; }
        public long SortCount { get; private set; }
        public int LastRefreshedKeys { get; private set; }
        public int LastOrderWrites { get; private set; }

        public WorldSortingSystem(int capacity = MaximumParticipants)
        {
            if (capacity < 1 || capacity > MaximumParticipants) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
            _capacity = capacity;
        }

        public static string ComposeIdentity(string map, string obj, string part)
        {
            if (string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(obj) || string.IsNullOrWhiteSpace(part))
            { throw new ArgumentException("排序身份各段不能为空"); }
            return $"map:{map.Length}:{map}{obj.Length}:{obj}{part.Length}:{part}";
        }

        public static int QuantizeY(float worldY)
        {
            if (!float.IsFinite(worldY) || Math.Abs((double)worldY * QuantizationUnitsPerWorldUnit) > int.MaxValue - 1d)
            { throw new ArgumentOutOfRangeException(nameof(worldY), "排序坐标超出有效范围"); }
            return Mathf.RoundToInt(worldY * QuantizationUnitsPerWorldUnit);
        }

        public static WorldSortKey CreateKey(float worldY, WorldSortCategory category, string stableSortId)
        {
            return new WorldSortKey(QuantizeY(worldY), category, stableSortId);
        }

        public bool Register(WorldSortParticipant participant)
        {
            if (participant == null || participant.IsRegistered || _registrations.ContainsKey(participant))
            {
                return false;
            }

            string stableSortId = participant.StableSortId;

            if (string.IsNullOrWhiteSpace(stableSortId))
            {
                RejectedCount++;
                ApplicationLog.Warning(LogEventIds.GameplayVisual, "[WorldSortingSystem] StableSortId 不能为空", participant);
                return false;
            }

            if (_stableIds.Contains(stableSortId))
            {
                RejectedCount++;
                ApplicationLog.Warning(LogEventIds.GameplayVisual, $"[WorldSortingSystem] StableSortId 重复：{stableSortId}", participant);
                return false;
            }

            if (_participants.Count >= _capacity || !participant.HasValidAnchor)
            {
                RejectedCount++;
                ApplicationLog.Warning(LogEventIds.GameplayVisual, "[WorldSortingSystem] 排序容量或锚点无效", participant);
                return false;
            }

            participant.RefreshSortKey();
            var registration = new WorldSortRegistration(this, participant);
            _stableIds.Add(stableSortId); _registrations.Add(participant, registration);
            participant.AttachRegistration(registration);
            _participants.Add(participant);
            _orderDirty = true;
            return true;
        }

        public void Unregister(WorldSortParticipant participant)
        {
            if (ReferenceEquals(participant, null) || !_registrations.TryGetValue(participant, out WorldSortRegistration registration))
            {
                return;
            }
            Release(registration);
        }

        internal void Release(WorldSortRegistration registration)
        {
            if (!registration.IsActive) { return; }
            registration.IsActive = false;
            if (_registrations.TryGetValue(registration.Participant, out WorldSortRegistration current) && ReferenceEquals(current, registration))
            {
                _registrations.Remove(registration.Participant); _participants.Remove(registration.Participant);
                _stableIds.Remove(registration.Identity); _orderDirty = true;
            }
        }

        public bool TryRegisterBatch(IReadOnlyList<WorldSortParticipant> participants, out WorldSortRegistration[] registrations, out string error)
        {
            if (participants == null) { throw new ArgumentNullException(nameof(participants)); }
            registrations = Array.Empty<WorldSortRegistration>(); error = null;
            if (participants.Count > _capacity - _participants.Count) { error = "排序容量不足"; }
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; error == null && i < participants.Count; i++)
            {
                WorldSortParticipant p = participants[i];
                if (p == null || p.IsRegistered || !p.HasValidAnchor || string.IsNullOrWhiteSpace(p.StableSortId)) { error = "参与者或锚点无效"; }
                else if (_stableIds.Contains(p.StableSortId) || !identities.Add(p.StableSortId)) { error = "排序身份重复：" + p.StableSortId; }
            }
            if (error != null) { RejectedCount++; return false; }
            registrations = new WorldSortRegistration[participants.Count];
            for (int i = 0; i < participants.Count; i++) { Register(participants[i]); registrations[i] = participants[i].Registration; }
            return true;
        }

        public void Tick()
        {
            using var measurement = TickMarker.Auto();
            LastRefreshedKeys = 0; LastOrderWrites = 0;
            bool removedDestroyedParticipant = false;

            for (int i = _participants.Count - 1; i >= 0; i--)
            {
                WorldSortParticipant participant = _participants[i];

                if (participant == null || !participant.TryRefreshSortKey(out bool changed))
                {
                    if (participant != null) { participant.SortingGroup.enabled = false; RejectedCount++; }
                    Unregister(participant);
                    removedDestroyedParticipant = true;
                    _orderDirty = true;
                    continue;
                }

                if (changed)
                {
                    LastRefreshedKeys++;
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

            _participants.Sort(Comparison);
            SortCount++;
            int layer = SortingLayer.NameToID("WorldObject");

            for (int i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].ApplySortingOrder(FirstSortingOrder + i, layer)) { LastOrderWrites++; }
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
            foreach (WorldSortRegistration registration in _registrations.Values) { registration.IsActive = false; }
            _registrations.Clear();
            _participants.Clear();
            _stableIds.Clear();
            _orderDirty = false;
        }

        static int CompareParticipants(WorldSortParticipant left, WorldSortParticipant right)
        {
            // 缓存 Comparison，避免 Mono 的 IComparer 适配路径逐次创建委托。
            // Tick 已清除失效对象，比较期间没有 Unity 生命周期回调。
            return left.SortKey.CompareTo(right.SortKey);
        }
    }
}
