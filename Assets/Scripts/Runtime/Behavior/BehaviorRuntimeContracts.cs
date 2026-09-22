using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct BehaviorHandle : IEquatable<BehaviorHandle>
    {
        public int Generation { get; }
        public Guid Scope { get; }
        public long Registration { get; }
        public bool IsValid => Generation > 0 && Scope != Guid.Empty && Registration > 0;
        internal BehaviorHandle(int generation, Guid scope, long registration)
        {
            Generation = generation; Scope = scope; Registration = registration;
        }
        public bool Equals(BehaviorHandle other) { return Generation == other.Generation && Scope == other.Scope && Registration == other.Registration; }
        public override bool Equals(object obj) { return obj is BehaviorHandle other && Equals(other); }
        public override int GetHashCode() { return HashCode.Combine(Generation, Scope, Registration); }
    }

    public enum BehaviorSemantic { Idle, Move, AttackWindup, Attack, Recovery, Interrupted, Stopped }
    public enum BehaviorControl { TogglePause, Interrupt, RequestAttack }

    public readonly struct BehaviorPresentationEvent
    {
        public BehaviorHandle Handle { get; }
        public long ActionInstanceId { get; }
        public BehaviorSemantic Semantic { get; }
        public Vector2 Position { get; }
        public bool Paused { get; }
        public double ActionElapsedSeconds { get; }
        public double ActionDurationSeconds { get; }
        public BehaviorPresentationEvent(BehaviorHandle handle, long action, BehaviorSemantic semantic, Vector2 position, bool paused,
            double actionElapsedSeconds = 0, double actionDurationSeconds = 0)
        {
            Handle = handle; ActionInstanceId = action; Semantic = semantic; Position = position; Paused = paused;
            ActionElapsedSeconds = actionElapsedSeconds; ActionDurationSeconds = actionDurationSeconds;
        }
    }

    public sealed class BehaviorDebugNode
    {
        public string Id { get; }
        public int Parent { get; }
        public int Depth { get; }
        public string State { get; }
        public string Detail { get; }
        public BehaviorDebugNode(string id, int parent, int depth, string state, string detail = "")
        {
            Id = id; Parent = parent; Depth = depth; State = state; Detail = detail;
        }
    }

    public sealed class BehaviorDebugSnapshot
    {
        public BehaviorHandle Handle { get; }
        public string Name { get; }
        public string Summary { get; }
        public IReadOnlyList<BehaviorDebugNode> Hfsm { get; }
        public IReadOnlyList<BehaviorDebugNode> Tree { get; }
        public IReadOnlyList<string> Trace { get; }
        public BehaviorPresentationEvent Presentation { get; }
        public BehaviorDebugSnapshot(BehaviorHandle handle, string name, string summary, BehaviorDebugNode[] hfsm,
            BehaviorDebugNode[] tree, string[] trace, BehaviorPresentationEvent presentation)
        {
            Handle = handle; Name = name; Summary = summary; Hfsm = Array.AsReadOnly(hfsm);
            Tree = Array.AsReadOnly(tree); Trace = Array.AsReadOnly(trace); Presentation = presentation;
        }
    }

    public interface IBehaviorRuntime
    {
        void Start(BehaviorHandle handle, Action<BehaviorPresentationEvent> publish);
        void Tick(float deltaTime);
        void Revoke();
        void Stop();
        void Control(BehaviorControl control);
        bool ObserveAnimation(long actionInstanceId);
        BehaviorDebugSnapshot Capture();
        IDisposable AcquireDiagnostics();
        BehaviorPresentationEvent Presentation { get; }
    }
}
