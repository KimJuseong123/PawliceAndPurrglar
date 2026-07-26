using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Swings the animals' actual leg bones so they walk instead of sliding.
    ///
    /// The authored animals ship no clips. Retargeting a humanoid walk is
    /// possible on paper because Unity accepts their skeletons as Humanoid, but
    /// it would stand a quadruped up on its hind legs, so the legs are driven
    /// directly instead.
    ///
    /// Legs move in a diagonal gait: front-left with rear-right, then the
    /// opposite pair. Only bone rotations change, never the root, so collision
    /// and the CharacterController are untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionLegAnimator : MonoBehaviour
    {
        private enum LegSide
        {
            Left,
            Right
        }

        private sealed class Leg
        {
            public Transform Bone;
            public Quaternion RestRotation;
            public float PhaseOffset;
        }

        // Upper leg joints only. Driving the whole chain looks broken without
        // an IK pass, and one joint per leg already reads as a stride.
        private static readonly string[] UpperLegPatterns =
        {
            "_Limb_1",
            "Thigh",
            "Upperarm",
            "UpLeg"
        };

        [SerializeField]
        private Transform skeletonRoot;

        [SerializeField, Min(0f)]
        private float swingDegrees = 26f;

        [SerializeField, Min(0.1f)]
        private float stridesPerSecond = 2.1f;

        [SerializeField, Min(0f)]
        private float blendPerSecond = 9f;

        private readonly List<Leg> _legs = new();
        private Vector3 _lastPosition;
        private float _phase;
        private float _movingBlend;

        public int LegCount => _legs.Count;
        public float MovingBlend => _movingBlend;

        public void Configure(Transform configuredSkeletonRoot)
        {
            skeletonRoot = configuredSkeletonRoot;
            _legs.Clear();
            _phase = 0f;
            _movingBlend = 0f;
            _lastPosition = transform.position;
            CollectLegs();
        }

        /// <summary>
        /// Finds the upper leg joints and assigns each a gait phase. Bones are
        /// matched by name because the authored rigs use different conventions:
        /// the dog is a true quadruped with numbered limbs, the cat and raccoon
        /// arrived on a biped skeleton.
        /// </summary>
        private void CollectLegs()
        {
            if (skeletonRoot == null)
            {
                return;
            }

            var matched = new List<Transform>();
            foreach (Transform bone in
                skeletonRoot.GetComponentsInChildren<Transform>(true))
            {
                foreach (string pattern in UpperLegPatterns)
                {
                    if (bone.name.IndexOf(
                            pattern,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched.Add(bone);
                        break;
                    }
                }
            }

            foreach (Transform bone in matched)
            {
                if (IsTwistHelper(bone.name))
                {
                    continue;
                }

                LegSide side = ResolveSide(bone.name);
                bool isFront = ResolveIsFront(bone.name);

                // Diagonal gait: opposite corners share a phase.
                bool firstPhase = isFront
                    ? side == LegSide.Left
                    : side == LegSide.Right;
                _legs.Add(new Leg
                {
                    Bone = bone,
                    RestRotation = bone.localRotation,
                    PhaseOffset = firstPhase ? 0f : Mathf.PI
                });
            }
        }

        /// <summary>
        /// Twist helper bones exist to distribute skinning and produce visible
        /// artefacts when rotated directly.
        /// </summary>
        private static bool IsTwistHelper(string boneName)
        {
            return boneName.IndexOf(
                "Twist",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static LegSide ResolveSide(string boneName)
        {
            string lower = boneName.ToLowerInvariant();
            if (lower.Contains("right") || lower.Contains("r_"))
            {
                return LegSide.Right;
            }

            return LegSide.Left;
        }

        /// <summary>
        /// Front versus rear. Arms on a biped rig stand in for front legs, and
        /// the dog's numbered limb groups separate the pairs.
        /// </summary>
        private static bool ResolveIsFront(string boneName)
        {
            string lower = boneName.ToLowerInvariant();
            if (lower.Contains("upperarm") || lower.Contains("clavicle"))
            {
                return true;
            }

            if (lower.Contains("thigh") || lower.Contains("upleg"))
            {
                return false;
            }

            return lower.Contains("0_");
        }

        public void Tick(float deltaTime)
        {
            if (_legs.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 delta = current - _lastPosition;
            delta.y = 0f;
            _lastPosition = current;

            float speed = delta.magnitude / deltaTime;
            float target = speed < 0.15f ? 0f : 1f;
            _movingBlend = Mathf.MoveTowards(
                _movingBlend,
                target,
                blendPerSecond * deltaTime);

            if (_movingBlend > 0.001f)
            {
                _phase += deltaTime * stridesPerSecond * Mathf.PI * 2f;
            }

            float amplitude = swingDegrees * _movingBlend;
            foreach (Leg leg in _legs)
            {
                if (leg.Bone == null)
                {
                    continue;
                }

                float swing = Mathf.Sin(_phase + leg.PhaseOffset) * amplitude;
                leg.Bone.localRotation =
                    leg.RestRotation * Quaternion.Euler(swing, 0f, 0f);
            }
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
