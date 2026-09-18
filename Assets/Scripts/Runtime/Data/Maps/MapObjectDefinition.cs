using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Flags]
    public enum MapObjectCapabilities
    {
        [LabelText("无")] None = 0,
        [LabelText("障碍")] Obstacle = 1,
        [LabelText("装饰")] Decoration = 2,
        [LabelText("交互")] Interaction = 4
    }

    public enum MapInteractionKind
    {
        [LabelText("无")] None,
        [LabelText("商人")] Trader,
        [LabelText("打造台")] Crafting,
        [LabelText("地图出口")] Portal
    }

    [ContentDefinition(ContentNamespaces.MapObject)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Maps/Map Object Definition")]
    public sealed class MapObjectDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")] string _id;
        [SerializeField, LabelText("对象能力")] MapObjectCapabilities _capabilities = MapObjectCapabilities.Decoration;
        [SerializeField, LabelText("障碍尺寸（世界单位）")] Vector2 _footprint = Vector2.one;
        [SerializeField, LabelText("交互类型")] MapInteractionKind _interactionKind;
        [SerializeField, LabelText("外观变体 ID")] string[] _appearanceVariants = Array.Empty<string>();

        public string Id => _id;
        public MapObjectCapabilities Capabilities => _capabilities;
        public Vector2 Footprint => _footprint;
        public MapInteractionKind InteractionKind => _interactionKind;
        public IReadOnlyList<string> AppearanceVariants => Array.AsReadOnly(_appearanceVariants ?? Array.Empty<string>());
    }
}
