using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// Puts the thief at one of the outskirt corners when the match starts.
    ///
    /// The corners were already drawn from on release, and starting from a
    /// fixed spot undid half of what that is for: the officer knew where the
    /// first ten seconds would be spent even if they did not know where the
    /// next thirty would.
    ///
    /// Drawn on the host and nowhere else. The thief's position is replicated
    /// host to client, so the host choosing and the client following is the
    /// whole mechanism — two machines each drawing would put the thief in two
    /// places, which is the failure this project has met from four different
    /// directions now.
    ///
    /// Once per match, on the frame play begins. Doing it on the frame the
    /// scene loads would be too early — the role board has not handed out roles
    /// yet, so nobody knows whether this machine decides.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThiefStartSpawn : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private ThiefSpawnPoints spawnPoints;

        [SerializeField]
        private PlayerInteriorState interiorState;

        private IMatchStateReader _matchState;
        private bool _placed;

        public bool HasPlaced => _placed;

        public void Configure(
            IMatchStateReader configuredMatchState,
            ThiefSpawnPoints configuredSpawnPoints,
            PlayerInteriorState configuredInteriorState)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            spawnPoints = configuredSpawnPoints;
            interiorState = configuredInteriorState;
        }

        private void Awake()
        {
            interiorState ??= GetComponent<PlayerInteriorState>();
        }

        private void Update()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            if (_matchState == null || spawnPoints == null)
            {
                return;
            }

            if (_matchState.CurrentState != MatchState.Playing)
            {
                // Rearmed for the next match, so a rematch draws again rather
                // than reusing whichever corner the last one happened to pick.
                _placed = false;
                return;
            }

            if (_placed)
            {
                return;
            }

            _placed = true;

            // Only the machine that decides where this character is.
            if (interiorState != null && !interiorState.HasAuthority)
            {
                return;
            }

            Vector3 corner = spawnPoints.Draw(transform.position);
            InteriorTravel.Place(gameObject, corner, null);

            GameLogger.Info(
                GameLogCategory.Player,
                $"Thief starts at {corner}.",
                this);
        }
    }
}
