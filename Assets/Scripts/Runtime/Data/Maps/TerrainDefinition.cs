using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [ContentDefinition(ContentNamespaces.Terrain)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Maps/Terrain Definition")]
    public sealed class TerrainDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")] string _id;
        [SerializeField, LabelText("允许步行")] bool _walkable = true;
        [SerializeField, LabelText("移动代价"), MinValue(1)] int _movementCost = 1;

        public string Id => _id;
        public bool Walkable => _walkable;
        public int MovementCost => _movementCost;
    }
}
