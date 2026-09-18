using System.Threading;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct WorldEffectContext
    {
        public WorldSortingSystem Sorting { get; }
        public string Identity { get; }
        public Vector3 GroundPosition { get; }
        public WorldSortParticipant Host { get; }
        public bool IsAttached { get; }
        public CancellationToken Token { get; }

        public WorldEffectContext(WorldSortingSystem sorting, string identity, Vector3 groundPosition, CancellationToken token = default)
        {
            Sorting = sorting; Identity = identity; GroundPosition = groundPosition; Host = null; IsAttached = false; Token = token;
        }

        public WorldEffectContext(WorldSortParticipant host, CancellationToken token = default)
        {
            Sorting = null; Identity = null; GroundPosition = default; Host = host; IsAttached = true; Token = token;
        }
    }
}
