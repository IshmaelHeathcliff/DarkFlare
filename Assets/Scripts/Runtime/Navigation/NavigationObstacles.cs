using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public enum NavigationStampStatus { Queued, Applied, Occupied, Invalid, Capacity, Stale }
    public sealed class NavigationStampTicket
    {
        public NavigationStampStatus Status { get; internal set; } = NavigationStampStatus.Queued;
        internal string Id;
        internal MapGeometryRect Rectangle;
        internal bool Remove;
    }
    public sealed class ChangeNavigationStampCommand : AbstractCommand<NavigationStampTicket>
    {
        readonly int _generation;
        readonly string _id;
        readonly MapGeometryRect _rectangle;
        readonly bool _remove;
        public ChangeNavigationStampCommand(int generation, string id, MapGeometryRect rectangle, bool remove = false)
        { _generation = generation; _id = id; _rectangle = rectangle; _remove = remove; }
        protected override NavigationStampTicket OnExecute()
        {
            NavigationMap map = this.GetSystem<NavigationSystem>().Active;
            return map == null || map.Generation != _generation ? new NavigationStampTicket { Status = NavigationStampStatus.Stale }
                : map.QueueStamp(_id, _rectangle, _remove);
        }
    }
    public sealed partial class NavigationMap
    {
        readonly Dictionary<string, Stamp> _stamps = new(StringComparer.Ordinal);
        readonly Queue<NavigationStampTicket> _stampQueue = new();
        internal Transform ObstacleParent { get; set; }
        sealed class Stamp
        {
            internal MapGeometryRect Rectangle;
            internal GameObject Projection;
        }
        public int StampCount => _stamps.Count;
        internal NavigationStampTicket QueueStamp(string id, MapGeometryRect rectangle, bool remove)
        {
            var ticket = new NavigationStampTicket { Id = id, Rectangle = rectangle, Remove = remove };
            if (!_active || _disposed) { ticket.Status = NavigationStampStatus.Stale; }
            else if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !remove &&
                (!double.IsFinite(rectangle.MinX) || !double.IsFinite(rectangle.MaxX) || !double.IsFinite(rectangle.MinY) || !double.IsFinite(rectangle.MaxY)
                || rectangle.MinX >= rectangle.MaxX || rectangle.MinY >= rectangle.MaxY
                || rectangle.MinX < _geometry.Bounds.MinX || rectangle.MaxX > _geometry.Bounds.MaxX
                || rectangle.MinY < _geometry.Bounds.MinY || rectangle.MaxY > _geometry.Bounds.MaxY)) { ticket.Status = NavigationStampStatus.Invalid; }
            // Accepted additions must take effect at the next physics boundary, never behind a backlog.
            else if (_stampQueue.Count != 0) { ticket.Status = NavigationStampStatus.Capacity; }
            else { _stampQueue.Enqueue(ticket); }
            return ticket;
        }
        void ApplyStamps()
        {
            if (_stampQueue.Count == 0) { return; }
            // Limit expensive topology rebuilds to one mutation at a physics boundary.
            NavigationStampTicket ticket = _stampQueue.Dequeue();
            if (!ticket.Remove && !_stamps.ContainsKey(ticket.Id) && _stamps.Count >= 32) { ticket.Status = NavigationStampStatus.Capacity; return; }
            if (!ticket.Remove && (Occupied(ticket.Rectangle, ActorTeam.Monster) || Occupied(ticket.Rectangle, ActorTeam.Player)))
            { ticket.Status = NavigationStampStatus.Occupied; return; }
            var rectangles = new List<MapGeometryRect>(32);
            foreach (KeyValuePair<string, Stamp> pair in _stamps) { if (pair.Key != ticket.Id) { rectangles.Add(pair.Value.Rectangle); } }
            if (!ticket.Remove) { rectangles.Add(ticket.Rectangle); }
            var replacement = new NavigationGrid[4];
            try
            {
                MapGeometryRect[] additional = rectangles.ToArray();
                for (int i = 0; i < replacement.Length; i++) { replacement[i] = new NavigationGrid(_geometry, Radii[i], additional); }
            }
            catch (MapGeometryCapacityException)
            {
                foreach (NavigationGrid grid in replacement) { grid?.Dispose(); }
                ticket.Status = NavigationStampStatus.Capacity; return;
            }
            catch
            {
                foreach (NavigationGrid grid in replacement) { grid?.Dispose(); }
                throw;
            }
            _steering.Complete();
            foreach (Work work in _work)
            {
                work.Search?.Complete();
                if (work.Agent != null) { work.Agent.Work = null; }
                if (work.Field != null) { work.Field.Work = null; }
                work.Agent = null; work.Field = null;
            }
            Revision++;
            foreach (Agent agent in _agents) { ReleaseRoute(agent); agent.Goal = -1; agent.LastAccepted = -100; agent.Status = NavigationStatus.Pending; }
            for (int i = 0; i < _fields.Length; i++) { _fields[i]?.Dispose(); _fields[i] = null; }
            for (int i = 0; i < _grids.Length; i++) { _grids[i].Dispose(); _grids[i] = replacement[i]; }
            if (_stamps.Remove(ticket.Id, out Stamp previous))
            {
                previous.Projection.SetActive(false); UnityEngine.Object.Destroy(previous.Projection);
            }
            if (!ticket.Remove)
            {
                MapGeometryRect rect = ticket.Rectangle;
                var root = new GameObject("Navigation stamp " + ticket.Id) { layer = LayerMask.NameToLayer(GameplayPhysicsLayers.WorldObstacle) };
                root.transform.SetParent(ObstacleParent, false);
                root.transform.position = new Vector3((float)((rect.MinX + rect.MaxX) * .5), (float)((rect.MinY + rect.MaxY) * .5), 0);
                root.AddComponent<BoxCollider2D>().size = new Vector2((float)(rect.MaxX - rect.MinX), (float)(rect.MaxY - rect.MinY));
                _stamps.Add(ticket.Id, new Stamp { Rectangle = rect, Projection = root });
            }
            StopVelocities(); ticket.Status = NavigationStampStatus.Applied;
        }
        bool Occupied(MapGeometryRect rectangle, ActorTeam team)
        {
            IReadOnlyList<CombatActor> actors = _combat.GetActorsByTeam(team);
            for (int i = 0; i < actors.Count; i++)
            {
                CombatActor actor = actors[i];
                if (actor == null || !actor.isActiveAndEnabled || !actor.IsAlive) { continue; }
                CircleCollider2D collider = actor.GetComponent<CircleCollider2D>();
                if (collider == null) { continue; }
                Vector2 point = collider.transform.TransformPoint(collider.offset);
                double radius = collider.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x), Mathf.Abs(collider.transform.lossyScale.y)) + MapGroundProfile.PhysicsMargin;
                double dx = Math.Max(rectangle.MinX - point.x, Math.Max(0, point.x - rectangle.MaxX));
                double dy = Math.Max(rectangle.MinY - point.y, Math.Max(0, point.y - rectangle.MaxY));
                if (dx * dx + dy * dy <= radius * radius) { return true; }
            }
            return false;
        }
        void DisposeStamps()
        {
            while (_stampQueue.Count > 0) { _stampQueue.Dequeue().Status = NavigationStampStatus.Stale; }
            foreach (Stamp stamp in _stamps.Values)
            { if (stamp.Projection != null) { stamp.Projection.SetActive(false); UnityEngine.Object.Destroy(stamp.Projection); } }
            _stamps.Clear();
        }
    }
}
