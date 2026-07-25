using UnityEngine;

namespace PawsAndLoot.Gameplay.Camera
{
    public sealed class TopDownFollowCamera : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private Vector3 offset = new(0f, 16f, -14f);

        [SerializeField, Min(0.01f)]
        private float smoothTimeSeconds = 0.12f;

        [SerializeField]
        private Vector3 lookOffset = new(0f, 1f, 0f);

        private Vector3 _velocity;

        public Transform Target => target;

        public void Configure(
            Transform followTarget,
            Vector3 followOffset,
            float smoothTime)
        {
            target = followTarget;
            offset = followOffset;
            smoothTimeSeconds = Mathf.Max(0.01f, smoothTime);
            SnapToTarget();
        }

        public void SetTarget(Transform followTarget, bool snap)
        {
            target = followTarget;
            if (snap)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset);
            _velocity = Vector3.zero;
        }

        public void Tick(float deltaTime)
        {
            if (target == null || deltaTime <= 0f)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                target.position + offset,
                ref _velocity,
                smoothTimeSeconds,
                Mathf.Infinity,
                deltaTime);
            transform.LookAt(target.position + lookOffset);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
