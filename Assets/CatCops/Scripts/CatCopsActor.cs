using UnityEngine;

namespace CatCops
{
    public enum CatCopsActorRole
    {
        Police,
        Thief,
        Dog,
        Cat,
        Merchant
    }

    public sealed class CatCopsActor : MonoBehaviour
    {
        public CatCopsActorRole Role;
        public float MoveSpeed = 4f;
        public float InteractionRadius = 1.35f;
        public Transform CarryPoint;
        public Renderer[] TintRenderers;

        [Header("Runtime")]
        public bool IsStunned;
        public bool IsConfused;
        public bool HasLoot;

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void SetStatus(bool stunned, bool confused)
        {
            IsStunned = stunned;
            IsConfused = confused;
            transform.localScale = _baseScale * (stunned ? 0.92f : 1f);
        }

        public void MoveTowards(Vector3 target, float deltaTime)
        {
            if (IsStunned)
            {
                return;
            }

            Vector3 planar = target - transform.position;
            planar.y = 0f;
            if (planar.sqrMagnitude < 0.025f)
            {
                return;
            }

            Vector3 direction = planar.normalized;
            transform.position += direction * MoveSpeed * deltaTime;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                10f * deltaTime);
        }

        public float DistanceTo(Transform other)
        {
            Vector3 a = transform.position;
            Vector3 b = other.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
