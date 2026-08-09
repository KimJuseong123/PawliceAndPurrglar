using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Input
{
    /// <summary>
    /// COMP-002 input half. Turns a command number into a
    /// <see cref="CompanionCommandRequest"/> and hands it to the dispatcher.
    ///
    /// It knows nothing about companion state and cannot change it; everything
    /// goes through the dispatcher, which is what let voice replace the keys
    /// without touching a rule.
    ///
    /// The keys are gone. `Ctrl+1..4` was the stand-in while there was no
    /// microphone, and it was removed on 2026-08-10 — the animals are told what
    /// to do out loud now, and `CompanionCommandTableView` puts the phrases on
    /// screen. The class keeps its name because the whole scene and every test
    /// refers to it, and renaming a serialised component detaches it from
    /// `Game.unity`, which costs a scene regeneration and the
    /// `GlobalObjectIdHash` of all 130 in-scene NetworkObjects with it.
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

        // `Update` read Ctrl+1..4 here until 2026-08-10. The keys were the
        // stand-in for voice while there was no microphone, and keeping both
        // meant the on-screen table had to describe one of two ways to give the
        // same order — or describe only one and be wrong about the other.
        //
        // `TryIssue` stays. It is what the voice pipeline and the tests call,
        // and it was always the part that did the work; the key reading was a
        // caller.

        private bool CanReadLocalInput()
        {
            if (isLocallyControlled)
            {
                return true;
            }

            if (NetworkManager.Singleton?.IsListening == true
                || issuer == null)
            {
                return false;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.IsGameplayInputEnabled
                && selector.ActiveRole == issuer.Role;
        }
    }
}
