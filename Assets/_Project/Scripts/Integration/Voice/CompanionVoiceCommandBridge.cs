using System;
using System.Collections.Generic;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    [Serializable]
    internal sealed class VoiceWorldContextWire
    {
        public string[] allowedIntents;
        public VoiceVisibleTarget[] visibleTargets;
        public string petType;
        public string ownerRole;
        public int commandSequence;
        public string gameSessionSeed;
    }

    [DisallowMultipleComponent]
    public sealed class CompanionVoiceCommandBridge : MonoBehaviour
    {
        [SerializeField] private CompanionCommandDispatcher dispatcher;
        [SerializeField] private VoiceBackendSocketClient backendSocket;
        [SerializeField] private CompanionTargetRegistry targetRegistry;
        [SerializeField] private PetCognitionConfig cognitionConfig;

        private readonly Dictionary<PlayerRole, int> sequences = new();
        private PetCognitionResolver resolver;
        private bool socketConnected;

        public void Configure(
            CompanionCommandDispatcher configuredDispatcher,
            VoiceBackendSocketClient configuredSocket,
            CompanionTargetRegistry configuredRegistry,
            PetCognitionConfig configuredConfig)
        {
            dispatcher = configuredDispatcher;
            backendSocket = configuredSocket;
            targetRegistry = configuredRegistry;
            cognitionConfig = configuredConfig;
            resolver = new PetCognitionResolver(cognitionConfig);
        }

        private void Awake()
        {
            resolver ??= new PetCognitionResolver(cognitionConfig);
        }

        private void Start()
        {
            if (backendSocket == null || !GameConfigService.IsInitialized)
            {
                return;
            }

            VoiceConfig voice = GameConfigService.Current.Voice;
            if (!voice.VoiceInputEnabled)
            {
                return;
            }

            ConfigureBackend(voice);
            if (string.IsNullOrWhiteSpace(VoiceCapabilityStore.Token)
                && (Unity.Netcode.NetworkManager.Singleton == null
                    || !Unity.Netcode.NetworkManager.Singleton.IsListening))
            {
                VoiceSessionCapabilityClient capabilityClient =
                    GetComponent<VoiceSessionCapabilityClient>();
                if (capabilityClient == null)
                {
                    capabilityClient = gameObject.AddComponent<
                        VoiceSessionCapabilityClient>();
                }

                capabilityClient.Configure(voice);
                capabilityClient.RegisterOffline();
            }
        }

        private void OnEnable()
        {
            VoiceCapabilityStore.Changed += HandleCapabilityChanged;
            if (backendSocket != null)
            {
                backendSocket.EventReceived += HandleBackendEvent;
            }

            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                link.VoiceCommandMetadataReceived += HandleMetadata;
                link.VoiceCommandEventReceived += HandleNetworkEvent;
            }
        }

        private void OnDisable()
        {
            VoiceCapabilityStore.Changed -= HandleCapabilityChanged;
            if (backendSocket != null)
            {
                backendSocket.EventReceived -= HandleBackendEvent;
            }

            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                link.VoiceCommandMetadataReceived -= HandleMetadata;
                link.VoiceCommandEventReceived -= HandleNetworkEvent;
            }
        }

        public void SubmitOfflineVoiceContext(
            string clientCommandId,
            string petId)
        {
            if (!IsServer
                || backendSocket == null
                || string.IsNullOrWhiteSpace(clientCommandId))
            {
                return;
            }

            NetworkPlayerLink link = FindLink(petId);
            if (link == null)
            {
                return;
            }

            RegisterContext(link.Role, link.transform, clientCommandId);
        }

        private void HandleMetadata(
            ulong senderClientId,
            string clientCommandId,
            string petId)
        {
            if (!IsServer || backendSocket == null) return;
            NetworkPlayerLink link = FindLink(petId);
            if (link == null || link.OwnerClientId != senderClientId) return;

            RegisterContext(link.Role, link.transform, clientCommandId);
        }

        private void RegisterContext(
            PlayerRole role,
            Transform owner,
            string clientCommandId)
        {
            if (!IsServer || backendSocket == null) return;

            sequences.TryGetValue(role, out int sequence);
            sequences[role] = sequence + 1;
            targetRegistry?.RebuildFromScene();
            var context = new VoiceWorldContextWire
            {
                allowedIntents = AllowedIntents(role),
                visibleTargets = targetRegistry == null
                    ? Array.Empty<VoiceVisibleTarget>()
                    : targetRegistry.BuildSnapshot(18f, owner.position),
                petType = role == PlayerRole.Police ? "DOG" : "CAT",
                ownerRole = role == PlayerRole.Police ? "POLICE" : "THIEF",
                commandSequence = sequence,
                gameSessionSeed = string.IsNullOrWhiteSpace(
                    VoiceCapabilityStore.SessionId)
                    ? "host-session"
                    : VoiceCapabilityStore.SessionId
            };
            backendSocket.RegisterVoiceContext(
                clientCommandId,
                JsonUtility.ToJson(context));
        }

        private void HandleCapabilityChanged()
        {
            if (!GameConfigService.IsInitialized
                || backendSocket == null
                || !GameConfigService.Current.Voice.VoiceInputEnabled)
            {
                return;
            }

            ConfigureBackend(GameConfigService.Current.Voice);
        }

        private void ConfigureBackend(VoiceConfig voice)
        {
            backendSocket.Configure(
                voice.BackendBaseUrl,
                VoiceCapabilityStore.SessionId,
                VoiceCapabilityStore.Token);
            if (!socketConnected
                && !string.IsNullOrWhiteSpace(VoiceCapabilityStore.Token))
            {
                backendSocket.Connect();
                socketConnected = true;
            }
        }

        private void HandleBackendEvent(VoiceBackendEvent backendEvent)
        {
            if (!IsServer || backendEvent == null) return;
            NetworkPlayerLink link = FindLink(backendEvent.petId);
            if (link == null) return;

            if (backendEvent.type == "VOICE_COMMAND_TRANSCRIBED")
            {
                FindVoiceInput(backendEvent.petId)?.ApplyServerTranscript(
                    backendEvent.payload?.transcript);
                link.PublishVoiceCommandEvent(
                    backendEvent.commandId,
                    backendEvent.type,
                    backendEvent.payload?.transcript ?? string.Empty,
                    string.Empty,
                    (int)PetCommandResultType.Correct,
                    (int)PetReactionType.Listen,
                    (int)CompanionCommandId.None);
                return;
            }

            if (backendEvent.type != "VOICE_INTENT_CANDIDATES_READY"
                || backendEvent.payload?.classification == null)
            {
                return;
            }

            VoiceIntentClassificationResult classification =
                backendEvent.payload.classification;
            PlayerRole role = link.Role;
            var context = new PetCognitionContext
            {
                petKind = CompanionCommandCatalog.GetCompanionKind(role),
                classification = classification,
                commandSequence = backendEvent.payload.commandSequence
            };
            int seed = StableSeed(
                backendEvent.commandId,
                context.commandSequence,
                context.petKind);
            PetDecision decision = resolver.Resolve(context, seed);
            FindVoiceInput(backendEvent.petId)?.ApplyServerDecision(
                decision.selectedCommandId != CompanionCommandId.None);
            Transform target = null;
            if (!string.IsNullOrWhiteSpace(decision.selectedTargetId))
            {
                targetRegistry?.TryResolve(
                    decision.selectedTargetId,
                    out target);
            }

            if (decision.selectedCommandId != CompanionCommandId.None)
            {
                var request = new CompanionCommandRequest(
                    decision.selectedCommandId,
                    role,
                    context.petKind,
                    CompanionCommandInputSource.Voice,
                    Time.time,
                    target,
                    target == null ? (Vector3?)null : target.position,
                    backendEvent.commandId,
                    decision.selectedTargetId);
                dispatcher.TryDispatch(
                    request,
                    out CompanionCommandRejection rejection);
                if (rejection != CompanionCommandRejection.None)
                {
                    decision.resultType = PetCommandResultType.Failed;
                    decision.reasonCode = rejection.ToString();
                }
            }

            link.PublishVoiceCommandEvent(
                backendEvent.commandId,
                "PET_COMMAND_INTERPRETED",
                classification.normalizedText,
                decision.selectedTargetId,
                (int)decision.resultType,
                (int)decision.reaction,
                (int)decision.selectedCommandId);
        }

        private void HandleNetworkEvent(
            string commandId,
            string eventType,
            string transcript,
            string targetId,
            int resultType,
            int reaction,
            int action)
        {
            // Presentation subscribers can attach to NetworkPlayerLink. This
            // method intentionally does not mutate gameplay on clients.
        }

        private NetworkPlayerLink FindLink(string petId)
        {
            bool dog = string.Equals(petId, "dog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "dog-1", StringComparison.OrdinalIgnoreCase);
            PlayerRole role = dog ? PlayerRole.Police : PlayerRole.Thief;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                if (link.Role == role) return link;
            }
            return null;
        }

        private static VoiceCommandInput FindVoiceInput(string petId)
        {
            bool dog = string.Equals(petId, "dog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "dog-1", StringComparison.OrdinalIgnoreCase);
            CompanionKind kind = dog ? CompanionKind.Dog : CompanionKind.Cat;
            foreach (VoiceCommandInput input in
                FindObjectsByType<VoiceCommandInput>(FindObjectsSortMode.None))
            {
                if ((kind == CompanionKind.Dog && input.name.Contains("Police"))
                    || (kind == CompanionKind.Cat && input.name.Contains("Thief")))
                {
                    return input;
                }
            }
            return null;
        }

        private static string[] AllowedIntents(PlayerRole role)
        {
            return role == PlayerRole.Police
                ? new[] { "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL", "SEARCH_AREA", "CHASE_TARGET", "GUARD_AREA", "INSPECT_TARGET" }
                : new[] { "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL", "SEARCH_AREA", "FETCH_OBJECT", "DISTRACT_TARGET", "INSPECT_TARGET" };
        }

        private static int StableSeed(
            string commandId,
            int sequence,
            CompanionKind kind)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(commandId))
                {
                    foreach (char character in commandId)
                    {
                        hash = hash * 31 + character;
                    }
                }
                hash = hash * 31 + sequence;
                hash = hash * 31 + (int)kind;
                return hash;
            }
        }

        private static bool IsServer =>
            Unity.Netcode.NetworkManager.Singleton == null
            || Unity.Netcode.NetworkManager.Singleton.IsServer;
    }
}
