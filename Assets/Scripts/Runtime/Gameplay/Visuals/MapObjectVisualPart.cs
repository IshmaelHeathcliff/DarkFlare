using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkFlare
{
    [DisallowMultipleComponent, RequireComponent(typeof(WorldSortParticipant))]
    public sealed class MapObjectVisualPart : MonoBehaviour
    {
        [SerializeField, LabelText("稳定分段 ID")] string _partId = "body";
        public string PartId => _partId;

        public static MapObjectVisualPart[] ValidatePrefab(GameObject prefab)
        {
            if (prefab == null) { throw new InvalidOperationException("表现 Prefab 缺失"); }
            MapObjectVisualPart[] parts = prefab.GetComponentsInChildren<MapObjectVisualPart>(true);
            if (parts.Length == 0) { throw new InvalidOperationException(prefab.name + "：没有表现分段"); }
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var participants = new HashSet<WorldSortParticipant>();
            foreach (MapObjectVisualPart part in parts)
            {
                WorldSortParticipant p = part.GetComponent<WorldSortParticipant>();
                if (string.IsNullOrWhiteSpace(part.PartId) || !identities.Add(part.PartId) || p == null || !p.ExplicitRegistration
                    || p.SortAnchor == null || !p.SortAnchor.IsChildOf(part.transform) || !p.HasValidAnchor || !p.enabled
                    || p.SortingGroup == null || !p.SortingGroup.enabled)
                { throw new InvalidOperationException(prefab.name + "：分段身份、显式注册或锚点无效"); }
                Transform parent = part.transform.parent;
                while (parent != null && parent.IsChildOf(prefab.transform))
                {
                    if (parent.GetComponent<SortingGroup>() != null) { throw new InvalidOperationException(prefab.name + "：禁止嵌套 SortingGroup"); }
                    parent = parent.parent;
                }
                participants.Add(p);
            }
            if (prefab.GetComponentsInChildren<SortingGroup>(true).Length != parts.Length
                || prefab.GetComponentsInChildren<WorldSortParticipant>(true).Length != parts.Length)
            { throw new InvalidOperationException(prefab.name + "：未声明的排序单元"); }
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not SpriteRenderer sprite || sprite.sprite == null || sprite.sharedMaterial == null
                    || renderer.sortingLayerName != "WorldObject" || !participants.Contains(renderer.GetComponentInParent<WorldSortParticipant>(true)))
                { throw new InvalidOperationException(prefab.name + "：Renderer 没有唯一分段归属"); }
            }
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Collider2D || component is Rigidbody2D || component is Collider || component is Rigidbody
                    || (component is MonoBehaviour && component is not MapObjectVisualPart && component is not WorldSortParticipant))
                { throw new InvalidOperationException(prefab.name + "：表现资源包含缺失脚本或玩法 / 物理组件"); }
            }
            return parts;
        }
    }
}
