using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Maps/World Map Configuration")]
    public sealed class WorldMapConfiguration : ScriptableObject
    {
        [SerializeField, LabelText("默认地图")] MapDefinition _startingMap;
        [SerializeField, LabelText("默认入口 ID")] string _startingEntryId = "new_game";
        [SerializeField, LabelText("地表表现资源")] AssetReferenceT<TerrainPresentationDefinition> _terrainPresentation;
        [SerializeField, LabelText("对象表现目录")] MapObjectPresentationCatalog _objects;

        public MapDefinition StartingMap => _startingMap;
        public string StartingEntryId => _startingEntryId;
        public AssetReferenceT<TerrainPresentationDefinition> TerrainPresentation => _terrainPresentation;
        public MapObjectPresentationCatalog Objects => _objects;

        public void Validate()
        {
            if (_startingMap == null || !ContentId.IsValidSegment(_startingEntryId) || _objects == null
                || _terrainPresentation == null || !_terrainPresentation.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException("世界地图配置缺少默认地图、入口或表现资源");
            }
            _objects.Validate();
        }
    }
}
