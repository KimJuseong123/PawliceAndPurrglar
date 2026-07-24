using UnityEngine;

namespace CatCops
{
    public sealed class CatCopsProjectile : MonoBehaviour
    {
        public float Speed = 11f;
        public float LifeTime = 1.2f;
        public float HitRadius = 0.85f;
        public CatCopsActorRole TargetRole = CatCopsActorRole.Thief;

        private CatCopsPrototypeController _controller;
        private Vector3 _direction;
        private float _age;
        private string _itemId;

        public void Launch(Vector3 direction, CatCopsPrototypeController controller, CatCopsActorRole targetRole, string itemId)
        {
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            _controller = controller;
            TargetRole = targetRole;
            _itemId = itemId;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            transform.position += _direction * Speed * dt;
            transform.Rotate(Vector3.right, 720f * dt, Space.Self);

            CatCopsActor target = _controller != null ? _controller.GetActor(TargetRole) : null;
            if (target != null && Vector3.Distance(transform.position, target.transform.position) <= HitRadius)
            {
                _controller.ResolveProjectileHit(target, _itemId);
                Destroy(gameObject);
                return;
            }

            if (_age >= LifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
}
