using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Map
{
    /// <summary>
    /// MAP-003. Climbing a ladder up to a rooftop and back down.
    ///
    /// Both roles may use it, so a rooftop is never a safe zone: whatever the
    /// thief climbs, the police can climb after them.
    ///
    /// The climb runs as a timed move with the CharacterController switched off,
    /// which is what stops the controller's own collision resolution and gravity
    /// from fighting the transport and wedging the player inside the roof.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadderTraversal : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private Collider area;

        [SerializeField]
        private Transform bottomPoint;

        [SerializeField]
        private Transform topPoint;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField, Min(0.1f)]
        private float climbSeconds = 0.6f;

        /// <summary>
        /// Height at which a player counts as already on the roof, so the same
        /// key climbs up from below and comes down from above.
        /// </summary>
        [SerializeField, Min(0.5f)]
        private float topDetectionHeight = 1.5f;

        private IMatchStateReader _matchState;
        private CharacterController _climberController;
        private Transform _climber;
        private Vector3 _climbFrom;
        private Vector3 _climbTo;
        private float _climbElapsed;

        public event Action<PlayerRoleIdentity, bool> ClimbStarted;
        public event Action<PlayerRoleIdentity> ClimbFinished;

        private PlayerRoleIdentity _climberIdentity;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Traversal;
        public string Prompt => "Climb ladder";
        public bool IsAvailable =>
            isActiveAndEnabled
            && !IsClimbing
            && ResolveMatchState()?.IsGameplayActive == true;

        public bool IsClimbing => _climber != null;
        public Transform BottomPoint => bottomPoint;
        public Transform TopPoint => topPoint;

        public void Configure(
            Collider configuredArea,
            Transform configuredBottom,
            Transform configuredTop,
            IMatchStateReader matchStateReader)
        {
            area = configuredArea;
            bottomPoint = configuredBottom;
            topPoint = configuredTop;
            _matchState = matchStateReader;
            matchStateSource = matchStateReader as MonoBehaviour;
            CancelClimb();
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (area == null || bottomPoint == null || topPoint == null)
            {
                throw new InvalidOperationException(
                    $"LadderTraversal '{name}' requires an area and both "
                    + "endpoints.");
            }

            if (topPoint.position.y <= bottomPoint.position.y)
            {
                throw new InvalidOperationException(
                    $"LadderTraversal '{name}' needs its top above its "
                    + "bottom.");
            }

            if (ResolveMatchState() == null)
            {
                throw new InvalidOperationException(
                    $"LadderTraversal '{name}' requires a match state source.");
            }
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (!IsAvailable || context.Player == null)
            {
                return false;
            }

            Transform player = context.Player.transform;
            var controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                return false;
            }

            // Which way to go is decided by height, so one key does both.
            bool goingUp = player.position.y
                < bottomPoint.position.y + topDetectionHeight;
            Vector3 destination = goingUp
                ? topPoint.position
                : bottomPoint.position;

            _climberIdentity = context.Player;
            _climber = player;
            _climberController = controller;
            _climbFrom = player.position;
            _climbTo = destination;
            _climbElapsed = 0f;
            // Disabled for the duration so gravity and wall resolution cannot
            // fight the transport.
            _climberController.enabled = false;

            GameLogger.Debug(
                GameLogCategory.Player,
                $"{context.Player.Role} started climbing "
                + $"{(goingUp ? "up" : "down")} '{name}'.",
                this);
            ClimbStarted?.Invoke(context.Player, goingUp);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsClimbing)
            {
                return;
            }

            // A destroyed or disabled climber must not leave the ladder stuck.
            if (_climber == null || !_climber.gameObject.activeInHierarchy)
            {
                CancelClimb();
                return;
            }

            if (ResolveMatchState()?.IsGameplayActive != true)
            {
                FinishClimb();
                return;
            }

            _climbElapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(_climbElapsed / climbSeconds);
            // Vertical first, then across, so the player clears the parapet
            // instead of clipping through its edge.
            Vector3 position = new(
                Mathf.Lerp(
                    _climbFrom.x,
                    _climbTo.x,
                    Mathf.SmoothStep(0f, 1f, progress * progress)),
                Mathf.Lerp(_climbFrom.y, _climbTo.y, progress),
                Mathf.Lerp(
                    _climbFrom.z,
                    _climbTo.z,
                    Mathf.SmoothStep(0f, 1f, progress * progress)));
            _climber.position = position;

            if (progress >= 1f)
            {
                FinishClimb();
            }
        }

        private void FinishClimb()
        {
            PlayerRoleIdentity finished = _climberIdentity;
            if (_climber != null)
            {
                _climber.position = _climbTo;
            }

            RestoreController();
            _climber = null;
            _climberIdentity = null;
            _climbElapsed = 0f;
            if (finished != null)
            {
                ClimbFinished?.Invoke(finished);
            }
        }

        private void CancelClimb()
        {
            RestoreController();
            _climber = null;
            _climberIdentity = null;
            _climbElapsed = 0f;
        }

        private void RestoreController()
        {
            if (_climberController != null)
            {
                _climberController.enabled = true;
                _climberController = null;
            }
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            return _matchState;
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void OnDisable()
        {
            CancelClimb();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
