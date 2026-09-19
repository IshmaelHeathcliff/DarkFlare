using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DarkFlare
{
    [BurstCompile]
    internal struct NavigationSearchJob : IJob
    {
        internal NavigationGridView Grid;
        internal int Start;
        internal int Goal;
        internal bool Reverse;
        internal bool Initialize;
        internal int Budget;
        internal NativeArray<ulong> Distance;
        internal NativeArray<ulong> Priority;
        internal NativeArray<int> Parent;
        internal NativeArray<int> Heap;
        internal NativeArray<int> Positions;
        // heap count, status (0 pending, 1 succeeded, 2 unreachable), expanded, minimum cost
        internal NativeArray<int> State;
        public void Execute()
        {
            if (Initialize)
            {
                State[0] = 0; State[1] = 0; State[2] = 0; State[3] = ushort.MaxValue;
                for (int i = 0; i < Grid.Count; i++)
                {
                    Distance[i] = ulong.MaxValue; Parent[i] = -1; Positions[i] = -1;
                    if (Grid.Edges[i] != 0) { State[3] = math.min(State[3], Grid.Costs[i]); }
                }
                int root = Reverse ? Goal : Start;
                if (root < 0 || Goal < 0 || root >= Grid.Count || Goal >= Grid.Count || Grid.Edges[root] == 0 || Grid.Edges[Goal] == 0)
                { State[1] = 2; return; }
                Distance[root] = 0; Priority[root] = Heuristic(root); Push(root);
            }
            if (State[1] != 0) { return; }
            for (int step = 0; step < Budget && State[0] > 0; step++)
            {
                int current = Pop(); State[2]++;
                if (!Reverse && current == Goal) { State[1] = 1; return; }
                for (int d = 0; d < 8; d++)
                {
                    int next = Grid.Neighbor(current, d);
                    if (next < 0 || Positions[next] == -2) { continue; }
                    int opposite = d < 4 ? (d + 2) % 4 : 4 + (d - 4 + 2) % 4;
                    if (Reverse ? !Grid.Edge(next, opposite) : !Grid.Edge(current, d)) { continue; }
                    ulong edge = Grid.Cost(Reverse ? current : next, d);
                    if (Distance[current] > ulong.MaxValue - edge) { continue; }
                    ulong distance = Distance[current] + edge;
                    if (distance >= Distance[next]) { continue; }
                    Distance[next] = distance; Parent[next] = current;
                    Priority[next] = distance + Heuristic(next);
                    Push(next);
                }
            }
            if (State[0] == 0) { State[1] = Reverse ? 1 : 2; }
        }
        ulong Heuristic(int index)
        {
            if (Reverse) { return 0; }
            int x = math.abs(index % Grid.Width - Goal % Grid.Width), y = math.abs(index / Grid.Width - Goal / Grid.Width);
            return ((ulong)math.min(x, y) * 14 + (ulong)math.abs(x - y) * 10) * (ulong)State[3];
        }
        bool Before(int a, int b) { return Priority[a] < Priority[b] || Priority[a] == Priority[b] && a < b; }
        void Put(int position, int node) { Heap[position] = node; Positions[node] = position; }
        void Push(int node)
        {
            int position = Positions[node];
            if (position < 0) { position = State[0]; State[0] = position + 1; }
            while (position > 0)
            {
                int parent = (position - 1) / 2;
                if (!Before(node, Heap[parent])) { break; }
                Put(position, Heap[parent]); position = parent;
            }
            Put(position, node);
        }
        int Pop()
        {
            int result = Heap[0], count = State[0] - 1, node = Heap[count];
            State[0] = count; Positions[result] = -2;
            if (count == 0) { return result; }
            int position = 0;
            while (position * 2 + 1 < count)
            {
                int child = position * 2 + 1;
                if (child + 1 < count && Before(Heap[child + 1], Heap[child])) { child++; }
                if (!Before(Heap[child], node)) { break; }
                Put(position, Heap[child]); position = child;
            }
            Put(position, node); return result;
        }
    }

    internal sealed class NavigationSearch : IDisposable
    {
        readonly NavigationMemory _memory;
        internal NativeArray<ulong> Distance;
        internal NativeArray<ulong> Priority;
        internal NativeArray<int> Parent;
        NativeArray<int> _heap;
        NativeArray<int> _positions;
        NativeArray<int> _state;
        JobHandle _job;
        bool _scheduled;
        bool _initialized;
        internal int Start { get; private set; }
        internal int Goal { get; private set; }
        internal int Revision { get; private set; }
        internal bool Reverse { get; private set; }
        internal bool Running => _scheduled && !_job.IsCompleted;
        internal int Status => _initialized && !_scheduled ? _state[1] : 0;
        internal int Expanded => _state[2];
        internal NavigationSearch(int count)
        {
            _memory = new NavigationMemory(count * 28L + 16);
            try
            {
                Distance = new NativeArray<ulong>(count, Allocator.Persistent);
                Priority = new NativeArray<ulong>(count, Allocator.Persistent);
                Parent = new NativeArray<int>(count, Allocator.Persistent);
                _heap = new NativeArray<int>(count, Allocator.Persistent);
                _positions = new NativeArray<int>(count, Allocator.Persistent);
                _state = new NativeArray<int>(4, Allocator.Persistent);
            }
            catch { Dispose(); throw; }
        }
        internal void Begin(int start, int goal, bool reverse, int revision)
        {
            Complete(); Start = start; Goal = goal; Reverse = reverse; Revision = revision; _initialized = false; _state[1] = 0;
        }
        internal void Schedule(NavigationGridView grid, int budget)
        {
            if (_scheduled) { throw new InvalidOperationException("搜索尚未完成依赖"); }
            _job = new NavigationSearchJob { Grid = grid, Start = Start, Goal = Goal, Reverse = Reverse, Initialize = !_initialized,
                Budget = budget, Distance = Distance, Priority = Priority, Parent = Parent, Heap = _heap, Positions = _positions, State = _state }.Schedule();
            _scheduled = true; _initialized = true;
        }
        internal bool Poll()
        {
            if (_scheduled && !_job.IsCompleted) { return false; }
            Complete(); return true;
        }
        internal void Complete() { if (_scheduled) { _job.Complete(); _scheduled = false; } }
        public void Dispose()
        {
            Complete();
            if (Distance.IsCreated) { Distance.Dispose(); }
            if (Priority.IsCreated) { Priority.Dispose(); }
            if (Parent.IsCreated) { Parent.Dispose(); }
            if (_heap.IsCreated) { _heap.Dispose(); }
            if (_positions.IsCreated) { _positions.Dispose(); }
            if (_state.IsCreated) { _state.Dispose(); }
            _memory?.Dispose();
        }
    }
}
