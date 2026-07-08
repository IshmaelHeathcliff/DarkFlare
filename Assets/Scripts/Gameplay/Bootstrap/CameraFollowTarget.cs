using UnityEngine;

namespace DarkFlare
{
    public class CameraFollowTarget : MonoBehaviour
    {
        [SerializeField]
        Transform _target;

        [SerializeField]
        float _smoothTime = 0.08f;

        [SerializeField]
        Vector3 _offset = new Vector3(0f, 0f, -10f);

        Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 targetPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
        }
    }
}
