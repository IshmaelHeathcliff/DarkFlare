using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DarkFlare
{
    // 所有者显式 Dispose；这是静态资源投影，不驱动角色或持有游戏状态。
    public sealed class MapGeometryProjection : IDisposable
    {
        public GameObject Root { get; }
        public CompositeCollider2D Collider { get; }
        public MapGeometrySnapshot Geometry { get; }
        bool _disposed;
        const int PathsPerCollider = 128;
        public const int MaxProjectionRectangles = 32768;

        public MapGeometryProjection(MapGeometrySnapshot geometry, Scene scene)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            if (!scene.IsValid() || !scene.isLoaded) { throw new ArgumentException("需要已加载的目标场景", nameof(scene)); }
            if (geometry.Rectangles.Count > MaxProjectionRectangles) { throw new MapGeometryCapacityException("碰撞投影矩形超出容量 " + MaxProjectionRectangles); }
            double extent = Math.Max(Math.Max(Math.Abs(geometry.Bounds.MinX), Math.Abs(geometry.Bounds.MaxX)),
                Math.Max(Math.Abs(geometry.Bounds.MinY), Math.Abs(geometry.Bounds.MaxY)));
            if (extent * Math.Pow(2, -23) > Math.Min(0.001, geometry.CellSize / 512))
            { throw new ArgumentOutOfRangeException(nameof(geometry), "世界坐标的 float 精度不足以投影像素边界；请平移原点或增大格宽"); }
            int layer = LayerMask.NameToLayer(GameplayPhysicsLayers.WorldObstacle);
            if (layer < 0) { throw new InvalidOperationException("缺少 WorldObstacle 物理层"); }
            Root = new GameObject("Map geometry") { hideFlags = HideFlags.DontSave, layer = layer };
            try
            {
                SceneManager.MoveGameObjectToScene(Root, scene);
                Root.SetActive(false);
                Root.transform.position = new Vector3((float)geometry.Bounds.MinX, (float)geometry.Bounds.MinY, 0);
                var body = Root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                Collider = Root.AddComponent<CompositeCollider2D>();
                Collider.generationType = CompositeCollider2D.GenerationType.Manual;
                Collider.geometryType = CompositeCollider2D.GeometryType.Polygons;
                Collider.vertexDistance = (float)Math.Min(0.0001, geometry.CellSize / 1024);
                Collider.offsetDistance = 0;
                Collider.edgeRadius = 0;
            }
            catch { Dispose(); throw; }
        }

        public async UniTask PopulateAsync(CancellationToken token = default, bool activate = true)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapGeometryProjection)); }
            if (Root.GetComponents<PolygonCollider2D>().Length != 0) { throw new InvalidOperationException("投影只允许填充一次"); }
            try
            {
                int total = Geometry.Rectangles.Count + 4;
                for (int start = 0; start < total; start += PathsPerCollider)
                {
                    token.ThrowIfCancellationRequested();
                    var polygon = Root.AddComponent<PolygonCollider2D>();
                    polygon.compositeOperation = Collider2D.CompositeOperation.Merge;
                    polygon.pathCount = Math.Min(PathsPerCollider, total - start);
                    for (int i = 0; i < polygon.pathCount; i++)
                    {
                        int index = start + i;
                        MapGeometryRect rect = index < Geometry.Rectangles.Count ? Geometry.Rectangles[index] : Boundary(index - Geometry.Rectangles.Count);
                        float minX = (float)(rect.MinX - Geometry.Bounds.MinX), maxX = (float)(rect.MaxX - Geometry.Bounds.MinX);
                        float minY = (float)(rect.MinY - Geometry.Bounds.MinY), maxY = (float)(rect.MaxY - Geometry.Bounds.MinY);
                        polygon.SetPath(i, new[] { new Vector2(minX, minY), new Vector2(maxX, minY), new Vector2(maxX, maxY), new Vector2(minX, maxY) });
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                token.ThrowIfCancellationRequested();
                // 候选可以在禁用状态下构建；只有提交者能够启用物理投影。
                Root.SetActive(activate);
                Collider.GenerateGeometry();
                if (Collider.errorState != ColliderErrorState2D.None) { throw new InvalidOperationException("碰撞轮廓生成失败：" + Collider.errorState); }
                token.ThrowIfCancellationRequested();
            }
            catch { Dispose(); throw; }
        }

        MapGeometryRect Boundary(int index)
        {
            MapGeometryRect b = Geometry.Bounds;
            double thickness = Math.Max(1, Geometry.CellSize);
            return index switch
            {
                0 => new MapGeometryRect(b.MinX - thickness, b.MinY - thickness, b.MinX, b.MaxY + thickness),
                1 => new MapGeometryRect(b.MaxX, b.MinY - thickness, b.MaxX + thickness, b.MaxY + thickness),
                2 => new MapGeometryRect(b.MinX, b.MinY - thickness, b.MaxX, b.MinY),
                _ => new MapGeometryRect(b.MinX, b.MaxY, b.MaxX, b.MaxY + thickness)
            };
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            if (Root == null) { return; }
            Root.SetActive(false);
            if (Application.isPlaying) { Object.Destroy(Root); }
            else { Object.DestroyImmediate(Root); }
        }
    }
}
