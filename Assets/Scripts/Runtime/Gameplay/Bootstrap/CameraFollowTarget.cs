using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraFollowTarget : MonoBehaviour
    {
        [SerializeField]
        Transform _target;

        [SerializeField]
        [Min(0f)]
        float _smoothTime = 0.08f;

        [SerializeField]
        Vector3 _offset = new Vector3(0f, 0f, -10f);

        [SerializeField]
        Collider2D _worldBounds;

        [SerializeField]
        [Min(0f)]
        float _boundsPadding = 0.05f;

        Vector3 _velocity;
        Camera _camera;
        Bounds? _mapBounds;

        public void SetMapBounds(MapGeometryRect bounds)
        {
            _mapBounds = new Bounds(new Vector3((float)((bounds.MinX + bounds.MaxX) / 2), (float)((bounds.MinY + bounds.MaxY) / 2), 0),
                new Vector3((float)(bounds.MaxX - bounds.MinX), (float)(bounds.MaxY - bounds.MinY), 0));
            _velocity = Vector3.zero;
        }

        public Transform Target => _target;

        public Collider2D WorldBounds => _worldBounds;

        public void SetTarget(Transform target)
        {
            if (_target == target)
            {
                return;
            }

            _target = target;
            _velocity = Vector3.zero;
        }

        public void SetWorldBounds(Collider2D worldBounds)
        {
            _worldBounds = worldBounds;
            _velocity = Vector3.zero;
        }

        public Vector3 ClampToWorldBounds(Vector3 position)
        {
            if (_worldBounds == null && !_mapBounds.HasValue)
            {
                return position;
            }

            EnsureComponents();
            Bounds bounds = _mapBounds ?? _worldBounds.bounds;

            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            {
                return position;
            }

            float halfHeight = _camera.orthographic ? _camera.orthographicSize : 0f;
            float halfWidth = halfHeight * _camera.aspect;
            position.x = ClampAxis(
                position.x,
                bounds.min.x + halfWidth + _boundsPadding,
                bounds.max.x - halfWidth - _boundsPadding,
                bounds.center.x);
            position.y = ClampAxis(
                position.y,
                bounds.min.y + halfHeight + _boundsPadding,
                bounds.max.y - halfHeight - _boundsPadding,
                bounds.center.y);
            return position;
        }

        void Awake()
        {
            EnsureComponents();
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 targetPosition = ClampToWorldBounds(_target.position + _offset);
            Vector3 nextPosition = _smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime)
                : targetPosition;
            transform.position = ClampToWorldBounds(nextPosition);
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
        }

        static float ClampAxis(float value, float minimum, float maximum, float fallback)
        {
            if (minimum > maximum)
            {
                return fallback;
            }

            return Mathf.Clamp(value, minimum, maximum);
        }
    }
}
