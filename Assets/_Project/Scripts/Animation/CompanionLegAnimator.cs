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
            public Transform Upper;
            public Transform Lower;
            public Quaternion UpperRest;
            public Quaternion LowerRest;
            public float PhaseOffset;
        }

        // Upper joint (hip or shoulder) and the joint below it (knee or elbow).
        // A real gait needs both: the hip swings the whole leg forward while the
        // knee folds on the way through and straightens to plant.
        private static readonly string[] UpperLegPatterns =
        {
            "_Limb_1",
            "Thigh",
            "Upperarm",
            "UpLeg"
        };

        private static readonly string[] LowerLegPatterns =
        {
            "_Limb_2",
            "Calf",
            "Forearm",
            "Shin"
        };

        [SerializeField]
        private Transform skeletonRoot;

        [SerializeField, Min(0f)]
        private float swingDegrees = 24f;

        /// <summary>
        /// How far the knee folds. Larger than the hip swing because a real
        /// stride bends the knee much more than it rotates the hip.
        /// </summary>
        [SerializeField, Min(0f)]
        private float kneeBendDegrees = 34f;

        [SerializeField, Min(0.1f)]
        private float stridesPerSecond = 1.9f;

        [SerializeField, Min(0f)]
        private float blendPerSecond = 9f;

        /// <summary>
        /// Fraction of the cycle the foot spends planted. Above one half, which
        /// is what separates a walk from a trot and stops the sliding look.
        /// </summary>
        [SerializeField, Range(0.5f, 0.8f)]
        private float stanceFraction = 0.62f;

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
                Transform lower = FindLowerJoint(bone);

                _legs.Add(new Leg
                {
                    Upper = bone,
                    Lower = lower,
                    UpperRest = bone.localRotation,
                    LowerRest = lower != null
                        ? lower.localRotation
                        : Quaternion.identity,
                    PhaseOffset = ResolveGaitPhase(side, isFront)
                });
            }
        }

        /// <summary>
        /// Lateral sequence walk, the gait dogs and cats actually use at
        /// walking speed: rear-left, front-left, rear-right, front-right, each
        /// a quarter cycle apart. A two-phase diagonal trot is what made the
        /// previous version read as hopping rather than walking.
        /// </summary>
        private static float ResolveGaitPhase(LegSide side, bool isFront)
        {
            float quarter = Mathf.PI * 0.5f;
            if (side == LegSide.Left)
            {
                return isFront ? quarter : 0f;
            }

            return isFront ? quarter * 3f : quarter * 2f;
        }

        private static Transform FindLowerJoint(Transform upper)
        {
            foreach (Transform child in
                upper.GetComponentsInChildren<Transform>(true))
            {
                if (child == upper || IsTwistHelper(child.name))
                {
                    continue;
                }

                foreach (string pattern in LowerLegPatterns)
                {
                    if (child.name.IndexOf(
                            pattern,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return child;
                    }
                }
            }

            return null;
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

            foreach (Leg leg in _legs)
            {
                if (leg.Upper == null)
                {
                    continue;
                }

                // Normalised position in this leg's own cycle.
                float cycle = Mathf.Repeat(
                    (_phase + leg.PhaseOffset) / (Mathf.PI * 2f),
                    1f);
                EvaluateStride(
                    cycle,
                    out float hipDegrees,
                    out float kneeDegrees);

                leg.Upper.localRotation = leg.UpperRest
                    * Quaternion.Euler(
                        hipDegrees * _movingBlend,
                        0f,
                        0f);
                if (leg.Lower != null)
                {
                    leg.Lower.localRotation = leg.LowerRest
                        * Quaternion.Euler(
                            kneeDegrees * _movingBlend,
                            0f,
                            0f);
                }
            }
        }

        /// <summary>
        /// One stride split into stance and swing.
        ///
        /// During stance the foot is planted, so the hip rotates backwards at a
        /// steady rate and the knee stays almost straight; that constant push is
        /// what makes the ground look solid. During the shorter swing the hip
        /// snaps forward and the knee folds and unfolds so the foot clears the
        /// ground instead of dragging.
        /// </summary>
        private void EvaluateStride(
            float cycle,
            out float hipDegrees,
            out float kneeDegrees)
        {
            if (cycle < stanceFraction)
            {
                float t = cycle / stanceFraction;
                hipDegrees = Mathf.Lerp(swingDegrees, -swingDegrees, t);
                // A small dip at mid-stance carries the body weight.
                kneeDegrees =
                    Mathf.Sin(t * Mathf.PI) * kneeBendDegrees * 0.18f;
                return;
            }

            float swingT = (cycle - stanceFraction)
                / Mathf.Max(0.01f, 1f - stanceFraction);
            // SmoothStep so the leg accelerates and settles rather than
            // teleporting back to the front of the stride.
            hipDegrees = Mathf.Lerp(
                -swingDegrees,
                swingDegrees,
                Mathf.SmoothStep(0f, 1f, swingT));
            kneeDegrees = Mathf.Sin(swingT * Mathf.PI) * kneeBendDegrees;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
