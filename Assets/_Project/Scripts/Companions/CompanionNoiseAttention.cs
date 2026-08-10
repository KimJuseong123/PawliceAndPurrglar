using PawliceAndPurrglar.Gameplay.Sensing;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// Makes an animal look at a bang.
    ///
    /// The animals had exactly one reason to leave what they were doing: food
    /// put down by the other side. That made every distraction in the game a
    /// thing somebody spent an item on and aimed at a named animal. A sound is
    /// the opposite — it is not aimed at anybody, both animals hear it, and the
    /// player who made it hears it too.
    ///
    /// Deliberately weaker than food. A noise that outranked a tuna can would
    /// make the food props pointless, since a rubber chicken is cheaper than a
    /// tin and can be set off from across the street. So: shorter, and it will
    /// not interrupt an animal already at a lure.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CompanionLure))]
    public sealed class CompanionNoiseAttention : MonoBehaviour
    {
        /// <summary>
        /// How long the animal spends at the sound.
        ///
        /// Under half of a food lure's four seconds. Long enough to cross a
        /// junction and look, short enough that a bang does not remove an animal
        /// from the chase.
        /// </summary>
        public const float AttentionSeconds = 1.6f;

        [SerializeField]
        private NoiseBoard board;

        private CompanionLure _lure;
        private bool _subscribed;

        /// <summary>
        /// How many bangs this animal has actually gone to look at. Latched, so
        /// a test can tell "it ignored the sound" from "it never heard one".
        /// </summary>
        public int InvestigatedCount { get; private set; }

        public void Configure(NoiseBoard configuredBoard)
        {
            Unsubscribe();
            board = configuredBoard;
            Subscribe();
        }

        /// <summary>
        /// Decides whether this bang is worth leaving what it is doing for, and
        /// goes if so.
        ///
        /// Public so the rule can be exercised without a board and without
        /// frames.
        /// </summary>
        public bool Consider(NoiseReport report)
        {
            CompanionLure lure = ResolveLure();
            if (lure == null || !lure.HasAuthority)
            {
                return false;
            }

            // Food wins. An animal already sitting at a tin does not abandon it
            // because something banged, or the cheap prop would beat the
            // expensive one every time.
            if (lure.IsActive)
            {
                return false;
            }

            Vector3 delta = report.At - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > report.Radius * report.Radius)
            {
                return false;
            }

            if (!lure.TryLure(report.At, AttentionSeconds))
            {
                return false;
            }

            InvestigatedCount++;
            return true;
        }

        private void HandleHeard(NoiseReport report)
        {
            Consider(report);
        }

        private CompanionLure ResolveLure()
        {
            if (_lure == null)
            {
                _lure = GetComponent<CompanionLure>();
            }

            return _lure;
        }

        private void Subscribe()
        {
            if (_subscribed || board == null || !isActiveAndEnabled)
            {
                return;
            }

            board.Heard += HandleHeard;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && board != null)
            {
                board.Heard -= HandleHeard;
            }

            _subscribed = false;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // The board arrives with the match scene and this may wake first,
            // so the hook-up is retried rather than assumed. The same reason
            // the result messenger retries its evaluator.
            if (!_subscribed)
            {
                if (board == null)
                {
                    board = FindFirstObjectByType<NoiseBoard>();
                }

                Subscribe();
            }
        }
    }
}
