using System;
using PawsAndLoot.Companions;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Temporary movement animation for the animals.
    ///
    /// The authored dog and cat are quadrupeds with no animation clips, and
    /// humanoid clips cannot retarget onto them, so movement is faked with a
    /// hop and a lean instead. This is a placeholder, not authored animation;
    /// it is replaced when MODEL-005 and MODEL-006 ship real clips.
    ///
    /// Only the visual child is offset. The agent's own transform and its
    /// CharacterController are never touched, so collision stays exact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionProceduralAnimator : MonoBehaviour
    {
        [SerializeField]
        private CompanionAgent agent;

        [SerializeField]
        private Transform visual;

        // Once the legs carry the stride, the body only needs a slight vertical
        // settle. The earlier large hop is what made the animals look like they
        // were bouncing rather than walking.
        //
        // Dropped again after the legs started swinging on the right axis: the
        // dog's limbs had been splaying sideways, so the hop was the only motion
        // that read at all and it had been left doing work it should not.
        [SerializeField, Min(0f)]
        private float hopHeight = 0.065f;

        /// <summary>
        /// Legs to take the rhythm from. Optional: without it the body falls
        /// back to a free-running hop.
        /// </summary>
        [SerializeField]
        private CompanionLegAnimator gaitSource;

        /// <summary>
        /// Twice the stride rate, because a walking animal's body dips once per
        /// footfall rather than once per stride.
        /// </summary>
        [SerializeField, Min(0.1f)]
        private float hopsPerSecond = 3.8f;

        [SerializeField, Min(0f)]
        private float leanDegrees = 2f;

        /// <summary>
        /// How far the body rolls toward the supporting side, once per stride.
        ///
        /// This is the part that reads as a waddle. A short-legged dog shifts
        /// its weight sideways over each planted pair, and without it the walk
        /// looks like a machine moving four sticks.
        /// </summary>
        [SerializeField, Min(0f)]
        private float rollDegrees = 4f;

        private Vector3 _visualRestPosition;
        private Vector3 _lastPosition;
        private float _phase;
        private float _movingBlend;
        private float _externalSpeed;
        private int _externalSpeedFrame = int.MinValue;

        public float MovingBlend => _movingBlend;

        /// <summary>
        /// The speed to bob at, sent by whoever is moving this body.
        ///
        /// The legs were given this and the body was not, so on a client the
        /// two disagreed about whether the animal was walking. A replicated
        /// position arrives in a lump and then sits still until the next
        /// packet, so measuring it frame to frame reads "running, stopped,
        /// stopped, running" — and this blend chases that, so the hop's height
        /// changes every frame. Not a bob and not a hop: a vibration, and from
        /// the thief's own screen the cat is close enough to fill it.
        ///
        /// The measurement is still right for anything moved by its own motor,
        /// which is the host and every offline test, so it stays as the answer
        /// when nobody has said otherwise.
        /// </summary>
        public void SetExternalSpeed(float metresPerSecond)
        {
            _externalSpeed = Mathf.Max(0f, metresPerSecond);
            _externalSpeedFrame = Time.frameCount;
        }

        private bool HasFreshExternalSpeed =>
            _externalSpeedFrame >= Time.frameCount - 1;

        /// <summary>
        /// <paramref name="configuredAgent"/> may be null. It is only consulted
        /// to stand down while a companion is inactive, so the players — who use
        /// this for the same body settle and have no agent — pass nothing.
        ///
        /// The tuning arguments default to the animal values, so passing them is
        /// how a two-legged runner gets a taller bob without retuning the dog.
        /// </summary>
        public void Configure(
            CompanionAgent configuredAgent,
            Transform configuredVisual,
            CompanionLegAnimator configuredGaitSource = null,
            float configuredHopHeight = -1f,
            float configuredRollDegrees = -1f)
        {
            agent = configuredAgent;
            visual = configuredVisual;
            gaitSource = configuredGaitSource;
            if (configuredHopHeight >= 0f)
            {
                hopHeight = configuredHopHeight;
            }

            if (configuredRollDegrees >= 0f)
            {
                rollDegrees = configuredRollDegrees;
            }

            ValidateOrThrow();
            _visualRestPosition = visual.localPosition;
            _lastPosition = transform.position;
            _phase = 0f;
            _movingBlend = 0f;
        }

        public void ValidateOrThrow()
        {
            if (visual == null)
            {
                throw new InvalidOperationException(
                    $"CompanionProceduralAnimator '{name}' requires a visual "
                    + "transform.");
            }
        }

        public void Tick(float deltaTime)
        {
            if (visual == null || deltaTime <= 0f)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 delta = current - _lastPosition;
            delta.y = 0f;
            _lastPosition = current;

            float speed = HasFreshExternalSpeed
                ? _externalSpeed
                : delta.magnitude / deltaTime;
            bool disabled = agent != null && !agent.IsActive;
            float target = disabled || speed < 0.15f ? 0f : 1f;
            _movingBlend = Mathf.MoveTowards(
                _movingBlend,
                target,
                8f * deltaTime);

            if (_movingBlend <= 0.001f)
            {
                visual.localPosition = _visualRestPosition;
                visual.localRotation = Quaternion.identity;
                return;
            }

            visual.localPosition = _visualRestPosition
                + new Vector3(0f, EvaluateRise(deltaTime), 0f);
            visual.localRotation = Quaternion.Euler(
                -leanDegrees * _movingBlend,
                0f,
                EvaluateRoll());
        }

        /// <summary>
        /// How far the body sits above its rest height this frame.
        ///
        /// Tied to the legs when there are legs to tie it to. A walking animal
        /// is lowest as a paw lands and rises over the planted limb in between,
        /// and it is that alternation — not the vertical motion by itself —
        /// that reads as walking. Bobbing on an independent timer drifts against
        /// the footfalls and just looks like bouncing, which is what this used
        /// to do.
        ///
        /// Falls back to the old free-running hop when no gait is available, so
        /// a rig with no recognisable legs still shows some life.
        /// </summary>
        private float EvaluateRise(float deltaTime)
        {
            if (gaitSource != null && gaitSource.HasGait)
            {
                float footfalls =
                    Mathf.Max(1, gaitSource.FootfallsPerCycle);
                // Zero at each footfall, one midway between them.
                float rise = (1f - Mathf.Cos(
                    gaitSource.GaitCycle
                    * footfalls
                    * Mathf.PI * 2f)) * 0.5f;
                return rise * hopHeight * _movingBlend;
            }

            _phase += deltaTime * hopsPerSecond * Mathf.PI * 2f;
            return Mathf.Abs(Mathf.Sin(_phase))
                * hopHeight * _movingBlend;
        }

        /// <summary>
        /// Sideways body roll, once per stride so it matches the head rather
        /// than the twice-as-fast footfalls.
        /// </summary>
        private float EvaluateRoll()
        {
            if (rollDegrees <= 0f
                || gaitSource == null
                || !gaitSource.HasGait)
            {
                return 0f;
            }

            return Mathf.Sin(gaitSource.GaitCycle * Mathf.PI * 2f)
                * rollDegrees
                * _movingBlend;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
