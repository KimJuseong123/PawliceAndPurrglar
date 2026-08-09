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
        public const float BreakSeconds = 2f;

        /// <summary>
        /// Whether the last opening was silent. Read by tests and by anything
        /// reporting what happened, so "quiet" is a fact about the case rather
        /// than something inferred from the absence of a noise event.
        /// </summary>
        public bool OpenedQuietly { get; private set; }

        /// <summary>
        /// How far the smash carries.
        ///
        /// Further than the noise props, because this is the one sound in the
        /// game the officer cannot have caused: a rubber chicken tells you
        /// somebody is somewhere, and breaking glass tells you somebody is at
        /// the jeweller's.
        /// </summary>
        public const float SmashRadiusMeters = 30f;

        /// <summary>
        /// How long the thief has to stand at the glass with the key.
        ///
        /// Longer than the smash, and that is the trade in one number: the loud
        /// way is quick and tells the officer where you are, the quiet way is
        /// slow and tells him nothing. Quiet and fast would make the glass
        /// pointless, and quiet and slower still would make the key pointless.
        /// </summary>
        public const float UnlockSeconds = 2.8f;

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

        private bool _openedWithKey;

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
            ? (_openedWithKey ? "Unlock the case" : "Break the glass")
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
            return Request(
                identity == null ? (PlayerRole?)null : identity.Role,
                context.Player.GetComponent<DisplayCaseKeyHolder>());
        }

        /// <summary>
        /// Asks to break it, and reports the one frame it gives.
        /// </summary>
        public bool Request(PlayerRole? asker)
        {
            return Request(asker, null);
        }

        /// <summary>
        /// Asks to open it, with or without a key.
        ///
        /// The key is read here rather than checked by the caller, because
        /// whether this case is about to be quiet decides how long it takes and
        /// the prompt has to say so before the thief commits.
        /// </summary>
        public bool Request(PlayerRole? asker, DisplayCaseKeyHolder key)
        {
            if (!IsSealed
                || asker != PlayerRole.Thief
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return false;
            }

            _sinceRequest = 0f;
            _openedWithKey = key != null && key.HasKey;
            if (_elapsed < (_openedWithKey ? UnlockSeconds : BreakSeconds))
            {
                return false;
            }

            // Spent at the moment it works, not when the thief walks up. A key
            // consumed by an abandoned attempt would be lost to a passing dog.
            bool quiet = _openedWithKey && key.TrySpend();
            Open(asker.Value, quiet);
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
                _openedWithKey = false;
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
            Open(by, false);
        }

        /// <summary>
        /// Opens it. Loudly, unless it was unlocked.
        ///
        /// The same event either way: the glass goes and the treasure becomes
        /// reachable, because what the case guards is the piece and not the
        /// noise. Only the report to the noise board is conditional — a key
        /// turning is not a sound the officer can hear across the town, and if
        /// it were there would be no reason to fetch one.
        /// </summary>
        public void Open(PlayerRole by, bool quiet)
        {
            if (!IsSealed)
            {
                return;
            }

            IsSealed = false;
            _elapsed = 0f;
            OpenedQuietly = quiet;
            ApplySeal();

            if (!quiet)
            {
                ResolveNoiseBoard()?.Report(
                    transform.position,
                    SmashRadiusMeters,
                    by);

                // And the shop's own alarm, because breaking in is the thing a
                // jeweller's alarm is for. The noise board is heard by animals
                // and drawn as a ring; the alarm is what tells the officer which
                // shop, for long enough to run there.
                FindFirstObjectByType<LootAlarm>()?.Raise(
                    transform.position,
                    ResolveIdentity(by));
            }

            GameLogger.Info(
                GameLogCategory.Loot,
                quiet
                    ? $"{by} unlocked a display case at {transform.position}."
                    : $"{by} broke a display case at {transform.position}.",
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
            _openedWithKey = false;
            OpenedQuietly = false;
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

        /// <summary>
        /// The character of a role, for the alarm to name whoever set it off.
        /// Null is fine — the alarm falls back to revealing the thief.
        /// </summary>
        private static PawsAndLoot.Gameplay.Players.PlayerRoleIdentity
            ResolveIdentity(PlayerRole role)
        {
            foreach (PawsAndLoot.Gameplay.Players.PlayerRoleIdentity candidate in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Players.PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role == role)
                {
                    return candidate;
                }
            }

            return null;
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
