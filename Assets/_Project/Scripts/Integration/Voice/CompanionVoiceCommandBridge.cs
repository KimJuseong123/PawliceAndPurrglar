using System;
using System.Collections.Generic;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.Logging;
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

        /// <summary>
        /// Command ids the host has already answered.
        ///
        /// One command can reach the host twice: the backend pushes it down the
        /// host's event socket *and* the player who spoke forwards the same
        /// answer over NGO. Both carry the server's own command id, so one entry
        /// here is enough to keep the obedience roll and the dispatch happening
        /// once. Rolling twice would be worse than harmless — the roll decides
        /// whether the animal obeys, and the second one is allowed to disagree.
        ///
        /// Bounded because a long match issues a command every thirty seconds
        /// and nothing ever removes one.
        /// </summary>
        private readonly HashSet<string> handledEvents = new();
        private readonly Queue<string> handledOrder = new();
        private const int HandledEventMemory = 64;

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
                link.VoiceCommandResultReceived += HandleNetworkResult;
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
                link.VoiceCommandResultReceived -= HandleNetworkResult;
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
                VoiceBackendAddress.Resolve(voice),
                VoiceCapabilityStore.SessionId,
                VoiceCapabilityStore.Token);
            if (!socketConnected
                && !string.IsNullOrWhiteSpace(VoiceCapabilityStore.Token))
            {
                backendSocket.Connect();
                socketConnected = true;
            }
        }

        /// <summary>
        /// Entry point for the synchronous HTTP path, which carries the same
        /// information the socket would have pushed.
        ///
        /// Deliberately reuses <see cref="HandleBackendEvent"/> rather than
        /// repeating the resolve-and-dispatch below: the obedience roll, the seed,
        /// the dispatch and the replication are the interesting part, and two
        /// copies of them would eventually disagree about whether the animal
        /// obeyed. Only the delivery differs, so only the delivery is new.
        /// </summary>
        public void ApplyBackendResult(
            string commandId,
            string petId,
            string transcript,
            VoiceIntentClassificationResult classification,
            int commandSequence)
        {
            if (string.IsNullOrWhiteSpace(petId))
            {
                return;
            }

            // A guest reached the backend on its own and is holding the answer.
            // It cannot roll the obedience dice or move an animal — the host
            // owns both — so the answer is forwarded instead of applied.
            //
            // This used to fall straight into `HandleBackendEvent`, whose first
            // line returns off the host **without a word**. The guest's command
            // therefore ended here every single time: the animal never heard it
            // and the guest's feed sat on "음성 명령 처리 중" until the match
            // ended, because the state machine is only ever advanced by a
            // result that never came (`ISSUE-075`).
            if (!IsServer)
            {
                ForwardResultToHost(commandId, petId, transcript, classification);
                return;
            }

            if (!string.IsNullOrWhiteSpace(transcript))
            {
                HandleBackendEvent(new VoiceBackendEvent
                {
                    type = "VOICE_COMMAND_TRANSCRIBED",
                    commandId = commandId,
                    petId = petId,
                    payload = new VoiceBackendEventPayload
                    {
                        transcript = transcript
                    }
                });
            }

            if (classification == null)
            {
                return;
            }

            HandleBackendEvent(new VoiceBackendEvent
            {
                type = "VOICE_INTENT_CANDIDATES_READY",
                commandId = commandId,
                petId = petId,
                payload = new VoiceBackendEventPayload
                {
                    classification = classification,
                    commandSequence = commandSequence
                }
            });
        }

        /// <summary>
        /// Sends a guest's answer to the host over its own player link.
        ///
        /// Its own link, not whichever one matches the pet: <c>SendTo.Server</c>
        /// carries <c>OwnerClientId</c>, and sending through somebody else's
        /// object would hand the host a sender it cannot check the request
        /// against.
        /// </summary>
        private void ForwardResultToHost(
            string commandId,
            string petId,
            string transcript,
            VoiceIntentClassificationResult classification)
        {
            CompanionKind expected = IsDog(petId)
                ? CompanionKind.Dog
                : CompanionKind.Cat;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                if (!link.IsOwner
                    || CompanionCommandCatalog.GetCompanionKind(link.Role)
                        != expected)
                {
                    continue;
                }

                link.SubmitVoiceCommandResultRpc(
                    commandId ?? string.Empty,
                    petId,
                    transcript ?? string.Empty,
                    classification == null
                        ? string.Empty
                        : JsonUtility.ToJson(classification));
                GameLogger.Info(
                    GameLogCategory.Voice,
                    $"Guest forwarded voice command {commandId} for {petId} to "
                    + "the host for the obedience roll.",
                    this);
                return;
            }

            // Said out loud rather than dropped. Without a link the answer has
            // nowhere to go, and the only symptom is a feed that never leaves
            // "처리 중" — which reads as a microphone or a server fault.
            GameLogger.Warning(
                GameLogCategory.Voice,
                $"Voice command {commandId} for {petId} was understood but this "
                + "machine owns no player link to send it to the host with, so "
                + "the animal will not hear it.",
                this);
            FindVoiceInput(petId)?.ApplyServerFailure("VOICE_HOST_LINK_MISSING");
        }

        /// <summary>
        /// Host side of <see cref="ForwardResultToHost"/>.
        /// </summary>
        private void HandleNetworkResult(
            ulong senderClientId,
            string commandId,
            string petId,
            string transcript,
            string classificationJson)
        {
            if (!IsServer) return;

            NetworkPlayerLink link = FindLink(petId);
            if (link == null || link.OwnerClientId != senderClientId)
            {
                GameLogger.Warning(
                    GameLogCategory.Voice,
                    $"Client {senderClientId} sent a voice result for {petId}, "
                    + "which is not the animal it owns. Refused.",
                    this);
                return;
            }

            VoiceIntentClassificationResult classification = null;
            if (!string.IsNullOrWhiteSpace(classificationJson))
            {
                try
                {
                    classification =
                        JsonUtility.FromJson<VoiceIntentClassificationResult>(
                            classificationJson);
                }
                catch (Exception exception)
                {
                    GameLogger.Warning(
                        GameLogCategory.Voice,
                        $"Voice classification from client {senderClientId} "
                        + "could not be read: " + exception.Message,
                        this);
                }
            }

            ApplyBackendResult(
                commandId,
                petId,
                transcript,
                classification,
                0);
        }

        private void HandleBackendEvent(VoiceBackendEvent backendEvent)
        {
            if (backendEvent == null) return;
            if (!IsServer)
            {
                // Only the host is registered on the backend's socket, so this
                // is the guest's own rejected registration answering back. Named
                // rather than swallowed: a dropped event used to be
                // indistinguishable from one that never arrived.
                GameLogger.Debug(
                    GameLogCategory.Voice,
                    $"Ignored backend event '{backendEvent.type}' off the host.",
                    this);
                return;
            }

            NetworkPlayerLink link = FindLink(backendEvent.petId);
            if (link == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Voice,
                    $"Backend event '{backendEvent.type}' names pet "
                    + $"'{backendEvent.petId}', which no player link matches.",
                    this);
                return;
            }

            if (!TryClaim(backendEvent.commandId, backendEvent.type))
            {
                // The socket and the guest's forward carry the same command.
                GameLogger.Debug(
                    GameLogCategory.Voice,
                    $"Voice command {backendEvent.commandId} was already "
                    + $"answered; the second '{backendEvent.type}' is ignored.",
                    this);
                return;
            }

            if (backendEvent.type == "VOICE_COMMAND_TRANSCRIBED")
            {
                FindVoiceInput(backendEvent.petId)?.ApplyServerTranscript(
                    backendEvent.payload?.transcript);
                link.PublishVoiceCommandEvent(
                    backendEvent.petId,
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
                backendEvent.petId,
                backendEvent.commandId,
                "PET_COMMAND_INTERPRETED",
                classification.normalizedText,
                decision.selectedTargetId,
                (int)decision.resultType,
                (int)decision.reaction,
                (int)decision.selectedCommandId);
        }

        /// <summary>
        /// The host's answer, arriving at the machine that spoke.
        ///
        /// Presentation only — no roll, no dispatch, no target resolution. What
        /// it does do is move the local state machine off
        /// <c>Transcribing</c>, which nothing else on a guest ever did: the
        /// broadcast landed in an empty method, so the guest's feed showed
        /// "음성 명령 처리 중" for the rest of the match no matter what the
        /// animal actually did (`ISSUE-075`).
        ///
        /// The host skips this. It applied the same result directly, and
        /// applying it twice would restart the cooldown.
        /// </summary>
        private void HandleNetworkEvent(
            string petId,
            string commandId,
            string eventType,
            string transcript,
            string targetId,
            int resultType,
            int reaction,
            int action)
        {
            if (IsServer) return;

            VoiceCommandInput input = FindVoiceInput(petId);
            if (input == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Voice,
                    $"Voice event '{eventType}' arrived for pet '{petId}' with "
                    + "no matching input on this machine, so nothing on screen "
                    + "will change.",
                    this);
                return;
            }

            if (eventType == "VOICE_COMMAND_TRANSCRIBED")
            {
                input.ApplyServerTranscript(transcript);
                return;
            }

            if (eventType != "PET_COMMAND_INTERPRETED")
            {
                return;
            }

            var command = (CompanionCommandId)action;
            input.ApplyServerDecision(
                command != CompanionCommandId.None,
                new VoiceCommandResult
                {
                    transcript = transcript,
                    interpretedCommand = IntentNameOf(command),
                    targetId = targetId,
                    deliberatelyMisunderstood =
                        (PetCommandResultType)resultType
                            == PetCommandResultType.Misunderstood,
                    animalFeedback = ((PetReactionType)reaction).ToString(),
                    requestId = commandId
                });
        }

        /// <summary>
        /// The name the feed's mapper knows this command by.
        ///
        /// Not <c>ToString</c>: the mapper's vocabulary is the server's intent
        /// list, where two of these carry an underscore. Left to the enum name,
        /// `FollowOwner` and `ReturnOwner` would map back to nothing and the
        /// guest would be told "NO COMMAND" for an order the animal obeyed.
        /// </summary>
        public static string IntentNameOf(CompanionCommandId command)
        {
            return command switch
            {
                CompanionCommandId.None => string.Empty,
                CompanionCommandId.FollowOwner => "FOLLOW_OWNER",
                CompanionCommandId.ReturnOwner => "RETURN_OWNER",
                _ => command.ToString().ToUpperInvariant()
            };
        }

        /// <summary>
        /// Whether this is the first time the host has seen this command event.
        /// </summary>
        private bool TryClaim(string commandId, string eventType)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                // Nothing to compare against, so it cannot be a repeat. Better
                // to answer twice than to drop the only copy.
                return true;
            }

            string key = commandId + "|" + eventType;
            if (!handledEvents.Add(key))
            {
                return false;
            }

            handledOrder.Enqueue(key);
            while (handledOrder.Count > HandledEventMemory)
            {
                handledEvents.Remove(handledOrder.Dequeue());
            }

            return true;
        }

        private static bool IsDog(string petId)
        {
            return string.Equals(petId, "dog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    petId,
                    "dog-1",
                    StringComparison.OrdinalIgnoreCase);
        }

        private NetworkPlayerLink FindLink(string petId)
        {
            bool dog = IsDog(petId);
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
            CompanionKind kind = IsDog(petId)
                ? CompanionKind.Dog
                : CompanionKind.Cat;
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
            // `BARK` and `HIDE` were missing here as well as from
            // `FromIntent`, so the fourth number-key command of each animal had
            // no voice route. The server clamps candidates to this list
            // (`validateCandidates`), which means an intent absent here is
            // dropped without a word in any log.
            return role == PlayerRole.Police
                ? new[] { "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL", "SEARCH_AREA", "CHASE_TARGET", "GUARD_AREA", "INSPECT_TARGET", "BARK" }
                : new[] { "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL", "SEARCH_AREA", "FETCH_OBJECT", "DISTRACT_TARGET", "INSPECT_TARGET", "HIDE" };
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
