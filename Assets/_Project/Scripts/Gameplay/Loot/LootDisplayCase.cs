using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Sensing;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// A glass case with something in it.
    ///
    /// Everything worth stealing has been lying loose on a plinth. That makes
    /// the jeweller's the same room as the bakery with bigger numbers on the
    /// price tags — walk in, hold the button, walk out. The design document
    /// asks for the opposite: the most valuable things in the town should cost
    /// something to reach that is not measured in metres.
    ///
    /// What a case costs is noise. Breaking the glass is quick, and the whole
    /// street hears it. There is no quiet way in, which is the point — a case
    /// you could open silently would be a slower plinth. The thief chooses
    /// between the loose valuables nobody will hear them take and the ones
    /// behind glass that pay better and announce them.
    ///
    /// Host-authoritative. The client asks by repeating its interact request
    /// exactly as it does for treasure, and this decides.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootDisplayCase : MonoBehaviour, IPlayerInteractable
    {
        /// <summary>
        /// How long the thief has to stand at the glass.
        ///
        /// Short. A case is meant to be loud rather than slow — a long wait
        /// just means the thief opens it when nobody is nearby, and then the
        /// noise costs them nothing. Being heard should be the price, and a
        /// price you can arrange to avoid is not one.
        /// </summary>
        public const float BreakSeconds = 1.1f;

        /// <summary>
        /// How far the smash carries.
        ///
        /// Further than the noise props, because this is the one sound in the
        /// game the officer cannot have caused: a rubber chicken tells you
        /// somebody is somewhere, and breaking glass tells you somebody is at
        /// the jeweller's.
        /// </summary>
        public const float SmashRadiusMeters = 30f;

        [SerializeField]
        private LootItem contents;

        [SerializeField]
        private Transform glass;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private NoiseBoard noiseBoard;

        private IMatchStateReader _matchState;
        private float _elapsed;
        private float _sinceRequest;

        public event Action<LootDisplayCase> Broken;

        public bool IsSealed { get; private set; } = true;

        /// <summary>
        /// How far through breaking it, from zero to one.
        /// </summary>
        public float Normalized =>
            IsSealed ? Mathf.Clamp01(_elapsed / BreakSeconds) : 1f;

        public LootItem Contents => contents;

        public Transform InteractionTransform => transform;

        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Loot;

        public string Prompt => IsSealed
            ? "Break the glass"
            : "Empty case";

        /// <summary>
        /// Only worth prompting while it is still shut. Once it is open the
        /// treasure inside is the thing to talk to, and leaving the case
        /// answering would have it winning the nearest-target contest against
        /// its own contents.
        /// </summary>
        public bool IsAvailable => isActiveAndEnabled && IsSealed;

        public void Configure(
            LootItem configuredContents,
            Transform configuredGlass,
            IMatchStateReader configuredMatchState,
            NoiseBoard configuredNoiseBoard = null)
        {
            contents = configuredContents;
            glass = configuredGlass;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            noiseBoard = configuredNoiseBoard;
            IsSealed = true;
            _elapsed = 0f;
            ApplySeal();
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null)
            {
                return false;
            }

            var identity = context.Player.GetComponent<PlayerRoleIdentity>();
            return Request(identity == null ? (PlayerRole?)null : identity.Role);
        }

        /// <summary>
        /// Asks to break it, and reports the one frame it gives.
        /// </summary>
        public bool Request(PlayerRole? asker)
        {
            if (!IsSealed
                || asker != PlayerRole.Thief
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return false;
            }

            _sinceRequest = 0f;
            if (_elapsed < BreakSeconds)
            {
                return false;
            }

            Break(asker.Value);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsSealed || deltaTime <= 0f)
            {
                return;
            }

            // Letting go abandons it, on the same terms as abandoning a theft.
            // Half-broken glass that stays half-broken would let a thief work
            // at a case across several safe passes and pay the noise once.
            _sinceRequest += deltaTime;
            if (_sinceRequest > 0.35f)
            {
                _elapsed = 0f;
                return;
            }

            _elapsed += deltaTime;
        }

        /// <summary>
        /// Opens it: the glass goes, the treasure becomes reachable, and
        /// everybody within thirty metres knows.
        /// </summary>
        public void Break(PlayerRole by)
        {
            if (!IsSealed)
            {
                return;
            }

            IsSealed = false;
            _elapsed = 0f;
            ApplySeal();

            ResolveNoiseBoard()?.Report(
                transform.position,
                SmashRadiusMeters,
                by);

            GameLogger.Info(
                GameLogCategory.Loot,
                $"{by} broke a display case at {transform.position}.",
                this);
            Broken?.Invoke(this);
        }

        /// <summary>
        /// Shuts it again for a fresh match.
        /// </summary>
        public void Reseal()
        {
            IsSealed = true;
            _elapsed = 0f;
            _sinceRequest = 0f;
            ApplySeal();
        }

        /// <summary>
        /// Turns the contents on or off with the glass.
        ///
        /// The treasure is disabled rather than merely un-prompted, because a
        /// disabled component is not found by the interaction scanner at all.
        /// Left active and merely refusing, it would still win the
        /// nearest-target contest against the case standing around it, and the
        /// thief would stand at the glass being offered a treasure they cannot
        /// have and no way to break in.
        /// </summary>
        private void ApplySeal()
        {
            if (contents != null)
            {
                contents.enabled = !IsSealed;
            }

            if (glass != null)
            {
                glass.gameObject.SetActive(IsSealed);
            }
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }

        private NoiseBoard ResolveNoiseBoard()
        {
            if (noiseBoard == null)
            {
                noiseBoard = FindFirstObjectByType<NoiseBoard>();
            }

            return noiseBoard;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
