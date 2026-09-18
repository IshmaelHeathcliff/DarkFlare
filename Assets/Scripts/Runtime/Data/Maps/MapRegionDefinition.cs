using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public enum MapRegionKind
    {
        [LabelText("命名区域")] Named,
        [LabelText("安全区")] Safe,
        [LabelText("刷怪区")] Spawn
    }

    [ContentDefinition(ContentNamespaces.MapRegion)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Maps/Map Region Definition")]
    public sealed class MapRegionDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")] string _id;
        [SerializeField, LabelText("区域语义")] MapRegionKind _kind;

        public string Id => _id;
        public MapRegionKind Kind => _kind;
    }
}
