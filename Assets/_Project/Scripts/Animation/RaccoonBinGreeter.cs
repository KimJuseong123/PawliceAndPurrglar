using PawsAndLoot.Audio;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// The raccoon merchant hides in its bin, throws the lid open and pops up to
    /// wave when a player comes near.
    ///
    /// Procedural, like the companions' walk: the authored characters still ship
    /// with zero animation clips (MODEL-002), so the lid, the rise and the wave
    /// are driven by moving transforms. When real clips arrive this becomes the
    /// trigger for them rather than the animation itself.
    ///
    /// The order matters and is enforced rather than merely timed: the lid has
    /// to be most of the way open before the raccoon moves, or it rises through
    /// a closed lid. Leaving reverses it — the raccoon drops before the lid
    /// falls, so the lid never shuts on its head.
    ///
    /// Nothing here touches gameplay. The merchant has no collider of its own
    /// and the greeting cannot be failed or missed; it is a signal that says
    /// "you can sell here", which is otherwise invisible on a greybox map.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaccoonBinGreeter : MonoBehaviour
    {
        /// <summary>
        /// How far the lid has to be open before the raccoon starts rising, and
        /// how far down the raccoon has to be before the lid starts closing.
        /// </summary>
        private const float LidClearance = 0.7f;
        private const float SunkClearance = 0.15f;

        [SerializeField]
        private Transform raccoonRoot;

        /// <summary>
        /// Hinge at the bin's back rim. Rotating this opens the real lid mesh,
        /// which was reparented under it at build time.
        /// </summary>
        [SerializeField]
        private Transform lidPivot;

        [SerializeField]
        private Transform waveUpperArm;

        [SerializeField]
        private Transform waveForearm;

        [SerializeField, Min(1f)]
        private float greetRadius = 6f;

        [SerializeField]
        private float hiddenLocalY = -1.5f;

        [SerializeField]
        private float raisedLocalY = 0.25f;

        [SerializeField]
        private float lidOpenDegrees = 120f;

        [SerializeField, Min(0.1f)]
        private float riseSpeed = 2.6f;

        [SerializeField, Min(0.1f)]
        private float lidSpeed = 3.2f;

        [SerializeField, Min(0.1f)]
        private float waveCyclesPerSecond = 2.4f;

        /// <summary>
        /// Degrees the upper arm lifts. Signed, because which way is "up"
        /// depends on the bone's own axes; the left arm on this rig lifts on a
        /// negative Z.
        /// </summary>
        [SerializeField]
        private float waveLiftDegrees = -105f;

        private Renderer[] _raccoonRenderers;
        private bool _raccoonVisible = true;
        private Quaternion _upperArmRest;
        private Quaternion _forearmRest;
        private Quaternion _lidRest;
        private float _risen;
        private float _lidOpen;
        private float _wavePhase;
        private bool _capturedRest;

        public bool IsGreeting { get; private set; }
        public float RisenFraction => _risen;
        public float LidOpenFraction => _lidOpen;

        /// <summary>
        /// The transforms this component moves at runtime.
        ///
        /// Exposed so the scene optimisation pass can leave them out of static
        /// batching. A batched renderer is baked into a combined mesh and stops
        /// responding to its transform entirely, which is what silently stopped
        /// the lid from opening once the pass started running.
        /// </summary>
        public Transform RaccoonRoot => raccoonRoot;
        public Transform LidPivot => lidPivot;

        public void Configure(
            Transform configuredRoot,
            Transform configuredLidPivot,
            Transform configuredUpperArm,
            Transform configuredForearm,
            float configuredHiddenLocalY,
            float configuredRaisedLocalY,
            float configuredRadius)
        {
            raccoonRoot = configuredRoot;
            lidPivot = configuredLidPivot;
            waveUpperArm = configuredUpperArm;
            waveForearm = configuredForearm;
            hiddenLocalY = configuredHiddenLocalY;
            raisedLocalY = configuredRaisedLocalY;
            greetRadius = Mathf.Max(1f, configuredRadius);
        }

        /// <summary>
        /// Nearest player, or -1 when there is none. Distance is measured on the
        /// ground plane so standing on a rooftop directly above still counts as
        /// being at the bin.
        /// </summary>
        private float DistanceToNearestPlayer()
        {
            float nearest = -1f;
            foreach (PlayerRoleIdentity player in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (!player.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 delta = player.transform.position
                    - transform.position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (nearest < 0f || distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private void CaptureRest()
        {
            if (_capturedRest)
            {
                return;
            }

            _capturedRest = true;
            if (waveUpperArm != null)
            {
                _upperArmRest = waveUpperArm.localRotation;
            }

            if (waveForearm != null)
            {
                _forearmRest = waveForearm.localRotation;
            }

            if (lidPivot != null)
            {
                _lidRest = lidPivot.localRotation;
            }

            if (raccoonRoot != null)
            {
                Vector3 start = raccoonRoot.localPosition;
                start.y = hiddenLocalY;
                raccoonRoot.localPosition = start;
                _raccoonRenderers =
                    raccoonRoot.GetComponentsInChildren<Renderer>(true);
                SetRaccoonVisible(false);
            }
        }

        /// <summary>
        /// The merchant is not merely low in the bin, it is switched off.
        ///
        /// Sinking it out of sight relies on the bin hiding it from every
        /// angle, which a tilted camera and an open lid do not guarantee. Off
        /// is off, and it is only turned back on once the lid is open enough to
        /// cover the moment it appears.
        /// </summary>
        private void SetRaccoonVisible(bool visible)
        {
            if (_raccoonRenderers == null
                || _raccoonVisible == visible)
            {
                return;
            }

            _raccoonVisible = visible;
            foreach (Renderer renderer in _raccoonRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void Update()
        {
            CaptureRest();
            if (raccoonRoot == null)
            {
                return;
            }

            float distance = DistanceToNearestPlayer();
            bool wasGreeting = IsGreeting;
            IsGreeting = distance >= 0f && distance <= greetRadius;

            // On the edge into the radius, not while inside it. Someone standing
            // at the merchant to sell is inside this radius the whole time, and a
            // chitter per frame there is the same clip a hundred times a second.
            if (IsGreeting && !wasGreeting)
            {
                GameSoundService.Request(GameSoundId.RaccoonChitter);
            }

            // Opening leads, closing follows: the lid may only shut once the
            // raccoon is back inside.
            float lidTarget = IsGreeting || _risen > SunkClearance
                ? 1f
                : 0f;
            _lidOpen = Mathf.MoveTowards(
                _lidOpen,
                lidTarget,
                lidSpeed * Time.deltaTime);

            bool lidCoversTheReveal = _lidOpen >= LidClearance;
            float riseTarget =
                IsGreeting && lidCoversTheReveal ? 1f : 0f;
            _risen = Mathf.MoveTowards(
                _risen,
                riseTarget,
                riseSpeed * Time.deltaTime);

            // Appears with the open lid and disappears only after it is fully
            // back down, so the switch itself is never on screen.
            SetRaccoonVisible(
                lidCoversTheReveal
                && (IsGreeting || _risen > 0.01f));

            ApplyLid();
            ApplyRise();
            ApplyWave();
        }

        private void ApplyLid()
        {
            if (lidPivot == null)
            {
                return;
            }

            lidPivot.localRotation = _lidRest * Quaternion.Euler(
                lidOpenDegrees * Mathf.SmoothStep(0f, 1f, _lidOpen),
                0f,
                0f);
        }

        private void ApplyRise()
        {
            Vector3 local = raccoonRoot.localPosition;
            // Eased so it reads as a peek rather than a lift on rails.
            local.y = Mathf.Lerp(
                hiddenLocalY,
                raisedLocalY,
                Mathf.SmoothStep(0f, 1f, _risen));
            raccoonRoot.localPosition = local;
        }

        /// <summary>
        /// The arm only waves once the raccoon is most of the way out, so it
        /// never flails from inside the bin. The upper arm lifts and the forearm
        /// swings, which is what reads as a wave rather than a raised arm.
        /// </summary>
        private void ApplyWave()
        {
            float strength = Mathf.InverseLerp(0.65f, 1f, _risen);
            if (strength <= 0f)
            {
                _wavePhase = 0f;
                RestoreArms();
                return;
            }

            _wavePhase += Time.deltaTime * waveCyclesPerSecond
                * Mathf.PI * 2f;
            float swing = Mathf.Sin(_wavePhase) * 26f * strength;

            if (waveUpperArm != null)
            {
                waveUpperArm.localRotation = _upperArmRest
                    * Quaternion.Euler(
                        0f,
                        0f,
                        waveLiftDegrees * strength);
            }

            if (waveForearm != null)
            {
                waveForearm.localRotation = _forearmRest
                    * Quaternion.Euler(0f, 0f, swing);
            }
        }

        private void RestoreArms()
        {
            if (waveUpperArm != null)
            {
                waveUpperArm.localRotation = Quaternion.Slerp(
                    waveUpperArm.localRotation,
                    _upperArmRest,
                    Mathf.Clamp01(Time.deltaTime * 8f));
            }

            if (waveForearm != null)
            {
                waveForearm.localRotation = Quaternion.Slerp(
                    waveForearm.localRotation,
                    _forearmRest,
                    Mathf.Clamp01(Time.deltaTime * 8f));
            }
        }
    }
}
