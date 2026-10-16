using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class SkillBoardEffectProjection
    {
        public IReadOnlyList<ModifierInstance> Global { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<ModifierInstance>> Flames { get; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<FirewoodPortReference, int>> FlameSteps { get; }

        internal SkillBoardEffectProjection(List<ModifierInstance> global,
            Dictionary<string, IReadOnlyList<ModifierInstance>> flames,
            Dictionary<string, IReadOnlyDictionary<FirewoodPortReference, int>> steps)
        {
            Global = global.AsReadOnly();
            Flames = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<ModifierInstance>>(flames);
            FlameSteps = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyDictionary<FirewoodPortReference, int>>(steps);
        }
    }

    public static class SkillBoardEffects
    {
        public const string ActorSource = "skill_board";

        public static SkillBoardEffectProjection Resolve(SkillBoardState board, PlayerGrowthState growth)
        {
            var graph = new Dictionary<FirewoodPortReference, HashSet<FirewoodPortReference>>();
            var global = new List<ModifierInstance>();
            var sources = new List<(FirewoodPortReference Node, int Radius, ModifierInstance[] Modifiers)>();
            foreach (InstalledFirewood wood in board.Firewoods)
            {
                string id = wood.Item.InstanceId;
                if (!growth.IsActive(id)) { continue; }
                foreach (FirewoodNode node in wood.Placement.Layout.Nodes)
                {
                    if (node.Kind == FirewoodNodeKind.Crack || growth.IsActive(id, node.Id))
                    {
                        AddNode(new FirewoodPortReference(id, node.Id));
                    }
                }
                if (wood.Placement.Layout.Size == FirewoodSize.Large)
                {
                    AddNode(new FirewoodPortReference(id, SkillBoardGeometry.CenterNodeId));
                }
                foreach (FirewoodEdge edge in wood.Placement.Layout.Edges)
                {
                    Connect(new FirewoodPortReference(id, edge.First), new FirewoodPortReference(id, edge.Second));
                }
                foreach (SkillBoardTalentState talent in wood.Item.SkillBoardState.Talents)
                {
                    if (!growth.IsActive(id, talent.NodeId)) { continue; }
                    ModifierInstance[] modifiers = WithOrigin(talent.Modifiers, ModifierOriginKind.Talent, id + ":" + talent.NodeId,
                        wood.Item.BaseDefinition.LocalizedName.Message);
                    if (talent.IsGlobal) { global.AddRange(modifiers); }
                    else { sources.Add((new FirewoodPortReference(id, talent.NodeId), talent.Radius, modifiers)); }
                }
            }
            foreach (FirewoodConnection connection in board.Connections) { Connect(connection.First, connection.Second); }
            foreach (SkillBoardEmbedding embedding in board.Embeddings)
            {
                if (embedding.Item.BaseDefinition.ItemType == ItemType.Ember && growth.IsActive(embedding.Slot.PlacementId))
                {
                    sources.Add((embedding.Slot, embedding.Item.SkillBoardState.EmberRadius,
                        WithOrigin(embedding.Item.CollectModifiers(), ModifierOriginKind.Ember, embedding.Item.InstanceId,
                            embedding.Item.BaseDefinition.LocalizedName.Message)));
                }
            }
            var flames = new Dictionary<string, IReadOnlyList<ModifierInstance>>(StringComparer.Ordinal);
            var steps = new Dictionary<string, IReadOnlyDictionary<FirewoodPortReference, int>>(StringComparer.Ordinal);
            foreach (SkillBoardEmbedding flame in board.Embeddings)
            {
                if (flame.Item.BaseDefinition.ItemType != ItemType.Flame || !growth.IsActive(flame.Slot.PlacementId)) { continue; }
                var modifiers = new List<ModifierInstance>(WithOrigin(flame.Item.CollectModifiers(), ModifierOriginKind.Flame, flame.Item.InstanceId,
                    flame.Item.BaseDefinition.LocalizedName.Message));
                Dictionary<FirewoodPortReference, int> distances = Distances(flame.Slot);
                steps.Add(flame.Item.InstanceId, new System.Collections.ObjectModel.ReadOnlyDictionary<FirewoodPortReference, int>(distances));
                foreach (var source in sources)
                {
                    if (distances.TryGetValue(source.Node, out int distance) && distance <= source.Radius)
                    {
                        modifiers.AddRange(source.Modifiers);
                    }
                }
                flames.Add(flame.Item.InstanceId, modifiers.AsReadOnly());
            }
            return new SkillBoardEffectProjection(global, flames, steps);

            void AddNode(FirewoodPortReference node) { graph.TryAdd(node, new HashSet<FirewoodPortReference>()); }
            void Connect(FirewoodPortReference first, FirewoodPortReference second)
            {
                if (!graph.ContainsKey(first) || !graph.ContainsKey(second)) { return; }
                graph[first].Add(second);
                graph[second].Add(first);
            }
            Dictionary<FirewoodPortReference, int> Distances(FirewoodPortReference start)
            {
                var result = new Dictionary<FirewoodPortReference, int>();
                if (!graph.ContainsKey(start)) { return result; }
                var queue = new Queue<FirewoodPortReference>();
                result.Add(start, 0);
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    FirewoodPortReference node = queue.Dequeue();
                    foreach (FirewoodPortReference next in graph[node])
                    {
                        if (result.TryAdd(next, result[node] + 1)) { queue.Enqueue(next); }
                    }
                }
                return result;
            }
        }

        static ModifierInstance[] WithOrigin(IEnumerable<ModifierInstance> modifiers, ModifierOriginKind kind, string id, LocalizedMessage name)
        {
            return modifiers.Select(m => m.WithOrigin(new ModifierOrigin(kind, id, sourceName: name))).ToArray();
        }

        internal static CombatActor.ActorEffectPreparation Prepare(CombatActor actor, SkillBoardState board, PlayerGrowthState growth)
        {
            var replacements = new Dictionary<string, IEnumerable<ModifierInstance>>
            {
                [ActorSource] = Resolve(board, growth).Global,
                [PlayerGrowthSystem.AttributeSource] = growth.CreateModifiers()
            };
            return actor.PrepareEffects(replacements, actor.StatusTags, actor.BlockedActions);
        }
    }
}
