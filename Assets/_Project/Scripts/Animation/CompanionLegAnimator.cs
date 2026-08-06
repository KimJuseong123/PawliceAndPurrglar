using System.Collections.Generic;
using PawsAndLoot.Logging;
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

        /// <summary>
        /// The jump pose, in degrees. Large on purpose — the ask was for
        /// something comical, and a tasteful jump on a chibi model reads as a
        /// hitch in the walk. The signs are per-limb, so the two sides splay
        /// outward instead of both swinging the same way.
        /// </summary>
        private const float JumpUpperDegrees = 54f;
        private const float JumpLowerDegrees = -38f;
        private enum LegSide
        {
            Left,
            Right
        }

        /// <summary>
        /// A quadruped walks its four limbs a quarter cycle apart; a biped
        /// swings each arm against the opposite leg. Set by the caller, not
        /// guessed from the rig: the cat is a four-legged animal on a biped
        /// skeleton, so the bone names do not tell you how it should move.
        /// </summary>
        public enum GaitMode
        {
            Quadruped = 0,
            Biped = 1
        }

        private sealed class Leg
        {
            public Transform Upper;
            public Transform Lower;
            public Quaternion UpperRest;
            public Quaternion LowerRest;
            public float PhaseOffset;

            /// <summary>
            /// Fraction of the profile's amplitude this limb uses.
            ///
            /// Arms are not legs. A walking person's arms swing a fraction of
            /// what their legs do, and giving them the same amplitude produced
            /// the "휘적휘적" flail — worst on the thief, whose legs barely
            /// deform, so the arms were the only motion on screen and they were
            /// swinging as hard as legs.
            /// </summary>
            public float Amplitude = 1f;

            /// <summary>
            /// Local axis that swings this limb forwards and backwards, and the
            /// sign that makes a positive angle swing forward.
            ///
            /// Measured per bone rather than assumed. The rigs disagree: on the
            /// dog a rotation about X splays the leg sideways and Z is the
            /// stride, while on the cat and the players it is the other way
            /// round. One hardcoded axis is why the dog looked like it was
            /// paddling rather than walking.
            /// </summary>
            public Vector3 UpperAxis;
            public Vector3 LowerAxis;
            public float UpperSign;
            public float LowerSign;

            /// <summary>
            /// Whether this limb is an arm on a biped, which is the only thing
            /// that flops up and down as well as swinging fore and aft.
            /// </summary>
            public bool IsArm;

            /// <summary>
            /// Local axis that raises and lowers this limb, and the sign that
            /// makes a positive angle lift it.
            ///
            /// A second measured axis rather than a guess for the same reason the
            /// stride axis is measured: the two player rigs and the animals do not
            /// agree about which local axis is which, and a hardcoded one turns
            /// the flop into a twist that barely shows.
            /// </summary>
            public Vector3 LiftAxis;
            public float LiftSign;

            /// <summary>
            /// How much of the profile's knee bend this limb's lower joint uses.
            ///
            /// Split out from <see cref="Amplitude"/>, which used to be squared to
            /// get this. Squaring tied the elbow to the shoulder, so raising the
            /// shoulder to make the arms visible raised the elbow much faster —
            /// and a forearm folding as far as a knee is the single most
            /// flail-like part of the whole thing.
            /// </summary>
            public float ElbowShare = 1f;
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

        /// <summary>
        /// How far the head turns side to side across a stride. Small: it is a
        /// weight shift, not the animal looking around.
        /// </summary>
        [SerializeField, Min(0f)]
        private float headSwayDegrees = 9f;

        /// <summary>
        /// How much of the leg amplitude a biped's arms use.
        ///
        /// A walking person's arms travel roughly a third of what their legs do.
        /// At parity the upper body dominated and read as jitter rather than
        /// stride, which is exactly what "눈이 아프다" described.
        ///
        /// Raised from 0.34 to 0.62 — about 15° of shoulder rather than 8°.
        /// A third is anatomically right and read as **nothing moving**, because
        /// these arms start spread out from the body: eight degrees of swing on a
        /// limb that is already held wide is lost next to the pose it is swinging
        /// from. Not back to parity, which is the flail this number was lowered
        /// to escape.
        /// </summary>
        [SerializeField, Range(0.05f, 1f)]
        private float armAmplitude = 0.62f;

        /// <summary>
        /// How far a biped's arms rise and fall across a stride, in degrees.
        ///
        /// The fore-and-aft swing above was the only thing these arms did, and on
        /// a model that holds them spread out from the body it read as **arms
        /// pinned in a pose** rather than as arms moving: the swing happens in the
        /// plane you are looking along, so almost none of it reaches the screen.
        /// Lifting them is the motion a top-down camera can actually see, and it
        /// is what makes the walk look authored rather than switched on.
        ///
        /// Deliberately not folded into <see cref="armAmplitude"/>. That number
        /// controls the stride and was lowered once already to escape a flail;
        /// raising it to get visible arms would bring the flail back.
        /// </summary>
        [SerializeField, Min(0f)]
        private float armLiftDegrees = 14f;

        /// <summary>
        /// A second, faster ripple on top of the lift, as a share of it.
        ///
        /// One sine is a metronome. The half-amplitude second harmonic is what
        /// turns "up, down, up, down" into the loose flop that was asked for,
        /// because the limb no longer spends the same time going each way.
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float armFlopRipple = 0.45f;

        /// <summary>
        /// How far the arms wave while off the ground, in degrees.
        ///
        /// The jump was a fixed star pose held for its whole half second — every
        /// limb thrown out and then nothing until landing. Waving the arms through
        /// it costs nothing and is the difference between a pose and a jump.
        /// </summary>
        [SerializeField, Min(0f)]
        private float armJumpFlapDegrees = 26f;

        /// <summary>Waves per second while airborne.</summary>
        [SerializeField, Min(0.1f)]
        private float armJumpFlapsPerSecond = 3.2f;

        private readonly List<Leg> _legs = new();
        private Transform _head;
        private Quaternion _headRest;
        private Vector3 _headYawAxis = Vector3.up;
        private float _headYawSign = 1f;
        private Vector3 _lastPosition;
        private float _externalSpeed;
        private int _externalSpeedFrame = int.MinValue;
        private float _phase;
        private float _movingBlend;
        private float _airborneBlend;
        private float _airborneTarget;
        private int _airborneFrame = -10;
        private float _flapPhase;

        public int LegCount => _legs.Count;
        public float MovingBlend => _movingBlend;

        /// <summary>
        /// Whether this component found limbs to drive. The body animator uses
        /// it to decide whether there is a gait to follow at all.
        /// </summary>
        public bool HasGait => _legs.Count > 0;

        /// <summary>
        /// Position within the stride, 0 to 1. Zero is the moment a leg plants,
        /// because <see cref="EvaluateStride"/> starts each leg's stance there.
        /// </summary>
        public float GaitCycle =>
            Mathf.Repeat(_phase / (Mathf.PI * 2f), 1f);

        /// <summary>
        /// Footfalls in one stride. The four limbs land a quarter cycle apart;
        /// a biped's two legs land half a cycle apart.
        ///
        /// This is what the body bob has to match. A bob on its own timer drifts
        /// against the feet and reads as bouncing rather than walking, which is
        /// exactly how the animals looked.
        /// </summary>
        public int FootfallsPerCycle =>
            gait == GaitMode.Biped ? 2 : 4;

        [SerializeField]
        private GaitMode gait = GaitMode.Quadruped;

        /// <summary>
        /// When set, the limbs are only driven while this Animator is switched
        /// off. Clips win when they exist; this is the fallback that keeps the
        /// characters walking in a checkout with no animation assets.
        /// </summary>
        [SerializeField]
        private Animator deferToAnimator;

        public void Configure(
            Transform configuredSkeletonRoot,
            GaitMode configuredGait = GaitMode.Quadruped,
            Animator configuredDeferTo = null)
        {
            skeletonRoot = configuredSkeletonRoot;
            gait = configuredGait;
            deferToAnimator = configuredDeferTo;

            if (gait == GaitMode.Quadruped)
            {
                // A short-legged dog covers ground with a visibly longer,
                // slower swing than a person, and that is most of what makes
                // the walk read as a waddle rather than a trot. The player
                // values are left alone: a human mincing along at this stride
                // would look wrong.
                swingDegrees = 36f;
                kneeBendDegrees = 42f;
                stridesPerSecond = 1.55f;
            }

            _legs.Clear();
            _phase = 0f;
            _movingBlend = 0f;
            _lastPosition = transform.position;
            CollectLegs();
            CollectHead();
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

                MeasureSwingAxis(
                    bone,
                    out Vector3 upperAxis,
                    out float upperSign);
                Vector3 lowerAxis = upperAxis;
                float lowerSign = upperSign;
                if (lower != null)
                {
                    MeasureSwingAxis(lower, out lowerAxis, out lowerSign);
                }

                // On a biped the front limbs are arms. On a quadruped they
                // are front legs and carry weight like the back ones.
                bool isArm = gait == GaitMode.Biped && isFront;
                Vector3 liftAxis = upperAxis;
                float liftSign = 1f;
                if (isArm)
                {
                    MeasureLiftAxis(
                        bone,
                        upperAxis,
                        out liftAxis,
                        out liftSign);
                }

                _legs.Add(new Leg
                {
                    Upper = bone,
                    Lower = lower,
                    UpperRest = bone.localRotation,
                    LowerRest = lower != null
                        ? lower.localRotation
                        : Quaternion.identity,
                    UpperAxis = upperAxis,
                    LowerAxis = lowerAxis,
                    UpperSign = upperSign,
                    LowerSign = lowerSign,
                    LiftAxis = liftAxis,
                    LiftSign = liftSign,
                    IsArm = isArm,
                    PhaseOffset = ResolveGaitPhase(side, isFront),
                    Amplitude = isArm ? armAmplitude : 1f,
                    ElbowShare = isArm ? 0.45f : 1f
                });
            }
        }

        /// <summary>
        /// Finds the head and works out which of its local axes turns it left
        /// and right.
        ///
        /// Twist helpers are skipped and the shallowest match wins, so a neck
        /// chain resolves to the head itself rather than a helper partway up it.
        /// </summary>
        private void CollectHead()
        {
            _head = null;
            if (skeletonRoot == null || headSwayDegrees <= 0f)
            {
                return;
            }

            int bestDepth = int.MaxValue;
            foreach (Transform bone in
                skeletonRoot.GetComponentsInChildren<Transform>(true))
            {
                if (IsTwistHelper(bone.name)
                    || bone.name.IndexOf(
                        "head",
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int depth = 0;
                for (Transform p = bone;
                    p != skeletonRoot && p != null;
                    p = p.parent)
                {
                    depth++;
                }

                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    _head = bone;
                }
            }

            if (_head == null)
            {
                return;
            }

            _headRest = _head.localRotation;
            MeasureYawAxis(_head, out _headYawAxis, out _headYawSign);
        }

        /// <summary>
        /// Picks the local axis whose rotation turns a bone about world up.
        ///
        /// Measured rather than assumed, for the same reason the limb swing is:
        /// the dog and the cat are on different skeletons and neither agrees
        /// with the other about which axis is which. Comparing the rotation the
        /// bone actually undergoes against world up answers it for any rig.
        /// </summary>
        private static void MeasureYawAxis(
            Transform bone,
            out Vector3 axis,
            out float sign)
        {
            axis = Vector3.up;
            sign = 1f;

            Quaternion rest = bone.localRotation;
            Quaternion before = bone.rotation;
            float best = 0f;

            foreach (Vector3 candidate in
                new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                bone.localRotation =
                    rest * Quaternion.AngleAxis(25f, candidate);
                Quaternion change =
                    bone.rotation * Quaternion.Inverse(before);
                bone.localRotation = rest;

                change.ToAngleAxis(
                    out float angle,
                    out Vector3 worldAxis);
                if (angle > 180f)
                {
                    angle -= 360f;
                }

                // How much of the resulting turn is about world up.
                float yaw = Vector3.Dot(
                    worldAxis.normalized,
                    Vector3.up) * angle;
                if (Mathf.Abs(yaw) <= Mathf.Abs(best))
                {
                    continue;
                }

                best = yaw;
                axis = candidate;
            }

            sign = best < 0f ? -1f : 1f;
        }

        /// <summary>
        /// Finds the local axis that swings a limb along the character's facing,
        /// and the sign that makes a positive angle swing it forward.
        ///
        /// Done by trying each axis and measuring where the limb's tip actually
        /// goes, because the authored rigs disagree about which axis is which
        /// and guessing wrong makes a leg splay sideways instead of stride. The
        /// bone is put straight back, so this leaves no trace in the scene.
        /// </summary>
        private void MeasureSwingAxis(
            Transform bone,
            out Vector3 axis,
            out float sign)
        {
            axis = Vector3.right;
            sign = 1f;

            Transform tip = FindTip(bone);
            if (tip == null)
            {
                return;
            }

            Quaternion rest = bone.localRotation;
            Vector3 forward = transform.forward;
            Vector3 restTip = tip.position;
            float best = 0f;

            foreach (Vector3 candidate in
                new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                bone.localRotation =
                    rest * Quaternion.AngleAxis(25f, candidate);
                float along = Vector3.Dot(
                    tip.position - restTip,
                    forward);
                bone.localRotation = rest;

                if (Mathf.Abs(along) <= Mathf.Abs(best))
                {
                    continue;
                }

                best = along;
                axis = candidate;
            }

            // A negative reading means this axis swings the limb backwards, so
            // the whole gait is mirrored for that bone rather than left to run
            // out of step with the others.
            sign = best < 0f ? -1f : 1f;
        }

        /// <summary>
        /// Finds the local axis that raises and lowers a limb, and the sign that
        /// makes a positive angle raise it.
        ///
        /// Measured the same way the stride axis is — turn the bone, watch where
        /// its tip goes — but against world up instead of the character's facing,
        /// and with the stride axis excluded. Excluding it matters: on these rigs
        /// one axis carries a little of both, and letting the flop reuse it would
        /// make the arms swing harder rather than lift, which is the motion the
        /// camera already cannot see.
        /// </summary>
        private static void MeasureLiftAxis(
            Transform bone,
            Vector3 swingAxis,
            out Vector3 axis,
            out float sign)
        {
            axis = Vector3.up;
            sign = 1f;

            Transform tip = FindTip(bone);
            if (tip == null)
            {
                return;
            }

            Quaternion rest = bone.localRotation;
            Vector3 restTip = tip.position;
            float best = 0f;

            foreach (Vector3 candidate in
                new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (candidate == swingAxis)
                {
                    continue;
                }

                bone.localRotation =
                    rest * Quaternion.AngleAxis(25f, candidate);
                float rise = tip.position.y - restTip.y;
                bone.localRotation = rest;

                if (Mathf.Abs(rise) <= Mathf.Abs(best))
                {
                    continue;
                }

                best = rise;
                axis = candidate;
            }

            sign = best < 0f ? -1f : 1f;
        }

        /// <summary>
        /// Deepest descendant of a bone, used as the limb's tip when measuring
        /// which way it swings. Twist helpers are skipped: they barely move.
        /// </summary>
        private static Transform FindTip(Transform bone)
        {
            Transform best = null;
            int bestDepth = 0;
            foreach (Transform candidate in
                bone.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == bone || IsTwistHelper(candidate.name))
                {
                    continue;
                }

                int depth = 0;
                for (Transform p = candidate;
                    p != bone && p != null;
                    p = p.parent)
                {
                    depth++;
                }

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Quadruped: a lateral sequence walk, the gait dogs and cats actually
        /// use at walking speed — rear-left, front-left, rear-right,
        /// front-right, each a quarter cycle apart.
        ///
        /// Biped: legs half a cycle apart, and each arm opposite the leg on the
        /// same side. That contralateral swing is most of what makes a walk read
        /// as a walk rather than a shuffle.
        /// </summary>
        private float ResolveGaitPhase(LegSide side, bool isFront)
        {
            float half = Mathf.PI;
            if (gait == GaitMode.Biped)
            {
                bool leftish = side == LegSide.Left;
                if (isFront)
                {
                    // Arms: opposite the leg on their own side.
                    return leftish ? half : 0f;
                }

                return leftish ? 0f : half;
            }

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

        /// <summary>
        /// Speed handed in by whoever knows it, in metres per second.
        ///
        /// Has to be set every frame it applies. Going stale on purpose means a
        /// character that stops being remote-driven — a rematch, a host
        /// migration — falls back to measuring itself without anybody having to
        /// remember to clear this.
        /// </summary>
        public void SetExternalSpeed(float metresPerSecond)
        {
            _externalSpeed = Mathf.Max(0f, metresPerSecond);
            _externalSpeedFrame = Time.frameCount;
        }

        /// <summary>
        /// Off the ground, so the limbs stop walking and start flailing.
        ///
        /// Told rather than worked out, for the same reason the speed is: on a client
        /// the character is moved by writing its position, so it has no grounded
        /// state of its own and its vertical motion arrives in lumps.
        /// </summary>
        public void SetAirborne(bool airborne)
        {
            _airborneTarget = airborne ? 1f : 0f;
            _airborneFrame = Time.frameCount;
        }

        /// <summary>
        /// The motor to ask when nobody has told this animator anything.
        ///
        /// A single reference, set by the scene builder, so a character works with
        /// or without a session: in the editor scene there is no link publishing
        /// anything, and without this the jump would be invisible in exactly the
        /// place it is easiest to test. Null on the animals, which do not jump.
        ///
        /// The network call wins while it is fresh, because a replicated character's
        /// own controller has no useful grounded state — its position is written,
        /// not walked.
        /// </summary>
        [SerializeField]
        private PawsAndLoot.Gameplay.Players.PlayerMovementMotor
            airborneSource;

        public void ConfigureAirborneSource(
            PawsAndLoot.Gameplay.Players.PlayerMovementMotor motor)
        {
            airborneSource = motor;
        }

        /// <summary>
        /// How far into the jump pose the limbs are. Exposed so a test can assert
        /// that a jump looks different from a walk, which is the entire ask —
        /// "우스꽝스럽게" is not something a number can check, but "not the same
        /// pose as standing" is.
        /// </summary>
        public float AirborneBlend => _airborneBlend;

        private bool HasFreshExternalSpeed =>
            _externalSpeedFrame >= Time.frameCount - 1;

        public void Tick(float deltaTime)
        {
            if (_legs.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            // Authored clips beat procedural motion. Writing bones on top of a
            // running Animator would fight it every frame.
            if (deferToAnimator != null && deferToAnimator.enabled)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 delta = current - _lastPosition;
            delta.y = 0f;
            _lastPosition = current;

            // How fast this character is moving, told rather than measured when
            // somebody knows better.
            //
            // Measuring the transform is right for anything this machine
            // simulates, and wrong for a character whose position arrives over
            // the network. A replicated position is corrected toward its target
            // and then sits still until the next packet, so most frames measure
            // zero and the gait blend keeps being pulled back down: on a client
            // both characters swung 2.5° while the host showed 24°, and the body
            // settle was the only motion left. That is precisely the "vibrating
            // in place" that was reported.
            float speed = HasFreshExternalSpeed
                ? _externalSpeed
                : delta.magnitude / deltaTime;
            float target = speed < 0.15f ? 0f : 1f;
            _movingBlend = Mathf.MoveTowards(
                _movingBlend,
                target,
                blendPerSecond * deltaTime);

            // Faster than the walk blend. A jump is over in under half a second, so
            // easing into the pose at walking speed would mean landing before the
            // legs had finished leaving the ground.
            float airborneWanted = _airborneFrame >= Time.frameCount - 1
                ? _airborneTarget
                : (airborneSource != null && airborneSource.IsAirborne
                    ? 1f
                    : 0f);
            _airborneBlend = Mathf.MoveTowards(
                _airborneBlend,
                airborneWanted,
                8f * deltaTime);

            if (_movingBlend > 0.001f)
            {
                _phase += deltaTime * stridesPerSecond * Mathf.PI * 2f;
            }

            // Its own clock, because the jump wave has nothing to do with the
            // stride: a jump taken from a standstill has no stride to ride on and
            // would hold the star pose perfectly still.
            _flapPhase = Mathf.Repeat(
                _flapPhase + deltaTime * armJumpFlapsPerSecond,
                1f);

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

                // A star jump, deliberately silly: every limb thrown out at once
                // and the lower joints kicked the other way. The stride is faded
                // out underneath rather than switched off, so a jump taken at a run
                // does not snap.
                float hipAngle = hipDegrees * _movingBlend * leg.UpperSign
                    * leg.Amplitude;
                float kneeAngle = kneeDegrees * _movingBlend * leg.LowerSign
                    * leg.Amplitude * leg.ElbowShare;

                // The arms' own motion, and the only part of it the camera can
                // see. Everything else here happens in the plane the player is
                // looking along.
                float liftAngle = leg.IsArm
                    ? EvaluateArmLift(cycle) * _movingBlend * leg.LiftSign
                    : 0f;

                if (_airborneBlend > 0.001f)
                {
                    hipAngle = Mathf.Lerp(
                        hipAngle,
                        JumpUpperDegrees * leg.UpperSign * leg.Amplitude,
                        _airborneBlend);
                    kneeAngle = Mathf.Lerp(
                        kneeAngle,
                        JumpLowerDegrees * leg.LowerSign * leg.Amplitude,
                        _airborneBlend);
                    if (leg.IsArm)
                    {
                        // Waved rather than held. The star pose was a still frame
                        // for the whole half second the jump lasts, and a still
                        // frame is what "대충 만든 느낌" describes.
                        liftAngle = Mathf.Lerp(
                            liftAngle,
                            Mathf.Sin(_flapPhase * Mathf.PI * 2f)
                                * armJumpFlapDegrees
                                * leg.LiftSign,
                            _airborneBlend);
                    }
                }

                Quaternion upper = leg.UpperRest
                    * Quaternion.AngleAxis(hipAngle, leg.UpperAxis);
                if (leg.IsArm && Mathf.Abs(liftAngle) > 0.001f)
                {
                    upper *= Quaternion.AngleAxis(liftAngle, leg.LiftAxis);
                }

                leg.Upper.localRotation = upper;
                if (leg.Lower != null)
                {
                    // The elbow gets even less than the shoulder. A forearm
                    // bending as far as a knee is the single most flail-like
                    // part of the whole thing.
                    leg.Lower.localRotation = leg.LowerRest
                        * Quaternion.AngleAxis(kneeAngle, leg.LowerAxis);
                }
            }

            ApplyHeadSway();
        }

        /// <summary>
        /// Swings the head once per stride, not once per footfall.
        ///
        /// The body rises and falls with every paw that lands; the head leads
        /// the weight shift, which happens once per full cycle. Running them at
        /// the same rate makes the animal look like it is shaking its head
        /// rather than walking.
        /// </summary>
        private void ApplyHeadSway()
        {
            if (_head == null)
            {
                return;
            }

            float sway = Mathf.Sin(_phase)
                * headSwayDegrees
                * _movingBlend
                * _headYawSign;
            _head.localRotation = _headRest
                * Quaternion.AngleAxis(sway, _headYawAxis);
        }

        /// <summary>
        /// How high an arm is held at this point in its own cycle, in degrees.
        ///
        /// Two sines rather than one. A single sine is a metronome — the arm
        /// spends exactly as long going up as coming down, which reads as a
        /// mechanism. The half-rate ripple breaks that symmetry and is the whole
        /// difference between a swing and a flop.
        ///
        /// Peaks a quarter cycle after the limb plants, so the arm is highest as
        /// it passes the body rather than at the ends of its swing.
        /// </summary>
        private float EvaluateArmLift(float cycle)
        {
            float radians = cycle * Mathf.PI * 2f;
            return (Mathf.Sin(radians)
                    + (armFlopRipple * Mathf.Sin(radians * 2f)))
                * armLiftDegrees
                / (1f + armFlopRipple);
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

        /// <summary>
        /// Collects the limbs again when the scene loads.
        ///
        /// <see cref="Configure"/> runs in the editor while the scene is built,
        /// but the collected list is plain runtime state and is not serialised,
        /// so a built player started with nothing to drive and this component
        /// silently did nothing at all. Only the body hop was ever visible,
        /// which is why the animals looked like they were bouncing instead of
        /// walking.
        ///
        /// The serialised fields — the skeleton root, the gait and the Animator
        /// to defer to — do survive, so re-collecting here is enough.
        /// </summary>
        private void Awake()
        {
            _lastPosition = transform.position;
            if (_legs.Count > 0 || skeletonRoot == null)
            {
                return;
            }

            CollectLegs();
            CollectHead();
            GameLogger.Debug(
                GameLogCategory.Companion,
                $"'{name}' drives {_legs.Count} limbs "
                + $"as a {gait}.",
                this);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
