using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Input
{
    /// <summary>
    /// COMP-002 input half. Turns the number keys 1..4 into
    /// <see cref="CompanionCommandRequest"/> values and hands them to the
    /// dispatcher.
    ///
    /// This is the temporary stand-in for voice, not a voice feature. It knows
    /// nothing about companion state and cannot change it; everything goes
    /// through the dispatcher, which is what lets voice replace this class
    /// later without touching the rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionCommandKeyboardInput : MonoBehaviour
    {
        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private PlayerRoleIdentity issuer;

        [SerializeField]
        private Transform commandTargetSource;

        [SerializeField]
        private bool isLocallyControlled = true;

        [SerializeField, Min(1f)]
        private float commandCastDistance = 8f;

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        public int LastNumberKey { get; private set; }
        public CompanionCommandRejection LastRejection { get; private set; }

        public void Configure(
            CompanionCommandDispatcher configuredDispatcher,
            PlayerRoleIdentity configuredIssuer,
            Transform configuredTargetSource,
            bool locallyControlled)
        {
            dispatcher = configuredDispatcher;
            issuer = configuredIssuer;
            commandTargetSource = configuredTargetSource;
            isLocallyControlled = locallyControlled;
            LastNumberKey = 0;
            LastRejection = CompanionCommandRejection.None;
        }

        /// <summary>
        /// Issues one command. Separated from Update so tests can drive it
        /// without synthesising key presses.
        /// </summary>
        public bool TryIssue(int numberKey, float issuedAtSeconds)
        {
            if (dispatcher == null || issuer == null)
            {
                return false;
            }

            LastNumberKey = numberKey;
            Vector3 targetPosition = ResolveTargetPosition();
            bool dispatched = dispatcher.TryDispatchNumberKey(
                issuer.Role,
                numberKey,
                CompanionCommandInputSource.Keyboard,
                issuedAtSeconds,
                null,
                targetPosition,
                out CompanionCommandRejection rejection);
            LastRejection = rejection;
            return dispatched;
        }

        /// <summary>
        /// Commands that need a place use the point ahead of the issuing
        /// player, which keeps the prototype playable without a cursor.
        /// </summary>
        private Vector3 ResolveTargetPosition()
        {
            Transform source = commandTargetSource != null
                ? commandTargetSource
                : issuer.transform;
            Vector3 ahead = source.position
                + source.forward * commandCastDistance;
            ahead.y = source.position.y;
            return ahead;
        }

        private void Update()
        {
            if (!isLocallyControlled || Keyboard.current == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                TryIssue(1, Time.time);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                TryIssue(2, Time.time);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                TryIssue(3, Time.time);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                TryIssue(4, Time.time);
            }
        }
    }
}
