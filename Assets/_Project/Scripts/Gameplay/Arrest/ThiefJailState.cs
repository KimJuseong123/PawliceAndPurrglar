using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Arrest
{
    /// <summary>
    /// Holds a caught thief for a spell and then puts them back on the map.
    ///
    /// An arrest used to end the match, so nothing had to happen afterwards.
    /// Three arrests means the thief comes back twice, and the time in between
    /// is time nobody is playing — which is why it is short, and why the release
    /// point is out on the map rather than at the station door where the officer
    /// is still standing.
    ///
    /// Host side only. The client's keys reach the host by RPC and the host's
    /// motor is what refuses them, so blocking movement here blocks it for both
    /// machines; the position that results replicates like any other. A client
    /// running its own jail clock would fight the host's.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThiefJailState : MonoBehaviour
    {
        private CharacterController _controller;
        private PlayerMovementMotor _motor;
        private float _remainingSeconds;
        private Vector3 _releasePoint;
        private bool _hasReleasePoint;

        public event Action Jailed;
        public event Action Released;

        public bool IsJailed => _remainingSeconds > 0f;
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// Whether this machine decides. Defaults to true so a single-player
        /// editor scene works untouched; the network layer turns it off on the
        /// client the same way it does for interiors.
        /// </summary>
        public bool HasAuthority { get; private set; } = true;

        public void SetAuthority(bool hasAuthority)
        {
            HasAuthority = hasAuthority;
        }

        /// <summary>
        /// Puts the thief in the cell and schedules their release.
        ///
        /// Returns false when they are already in there, so an officer standing
        /// on a thief cannot extend the sentence frame by frame.
        /// </summary>
        public bool TryJail(
            float seconds,
            Vector3 cellPoint,
            Vector3 releasePoint)
        {
            if (!HasAuthority || IsJailed || seconds <= 0f)
            {
                return false;
            }

            _remainingSeconds = seconds;
            _releasePoint = releasePoint;
            _hasReleasePoint = true;
            PlaceAt(cellPoint);

            GameLogger.Info(
                GameLogCategory.Arrest,
                $"Thief jailed for {seconds:0.0}s.",
                this);
            Jailed?.Invoke();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!HasAuthority || !IsJailed)
            {
                return;
            }

            _remainingSeconds -= deltaTime;
            if (_remainingSeconds > 0f)
            {
                return;
            }

            _remainingSeconds = 0f;
            if (_hasReleasePoint)
            {
                PlaceAt(_releasePoint);
            }

            GameLogger.Info(
                GameLogCategory.Arrest,
                "Thief released from jail.",
                this);
            Released?.Invoke();
        }

        /// <summary>
        /// Ends the sentence without serving it, for a match reset.
        /// </summary>
        /// <summary>
        /// Takes the host's remaining sentence on a machine that is not serving
        /// it.
        ///
        /// The client has no authority over the jail, so its own clock never
        /// starts and the thief sat in a cell with no idea how long for. This
        /// only feeds the countdown on screen — where the thief actually is
        /// still comes from the replicated position.
        /// </summary>
        public void ApplyReplicatedRemaining(float remainingSeconds)
        {
            if (HasAuthority)
            {
                return;
            }

            bool wasJailed = IsJailed;
            _remainingSeconds = Mathf.Max(0f, remainingSeconds);
            if (!wasJailed && IsJailed)
            {
                Jailed?.Invoke();
            }
            else if (wasJailed && !IsJailed)
            {
                Released?.Invoke();
            }
        }

        public void Clear()
        {
            _remainingSeconds = 0f;
            _hasReleasePoint = false;
        }

        /// <summary>
        /// Moves the character to a point on the ground.
        ///
        /// Three things here are all lessons rather than ceremony. The capsule
        /// has to be switched off first or it overwrites the position on its own
        /// next move. The feet, not the pivot, go on the surface — the pivot
        /// sits most of a metre above the soles, so placing it on the floor
        /// buries the capsule and the controller resolves that by shoving the
        /// player downward through it. And the fall speed has to be cleared,
        /// because gravity keeps accumulating across a teleport and a few of
        /// them in a row is enough to punch through the floor between frames.
        /// </summary>
        private void PlaceAt(Vector3 groundPoint)
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            float feetToPivot =
                _controller.height * 0.5f - _controller.center.y;

            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = new Vector3(
                groundPoint.x,
                groundPoint.y + feetToPivot,
                groundPoint.z);
            _controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            if (_motor == null)
            {
                _motor = GetComponent<PlayerMovementMotor>();
            }

            _motor?.ResetVerticalVelocity();
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _motor = GetComponent<PlayerMovementMotor>();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
