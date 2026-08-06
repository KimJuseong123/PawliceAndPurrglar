using PawsAndLoot.Config;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerInteractionScanner : MonoBehaviour
    {
        /// <summary>
        /// How many colliders one scan may consider.
        ///
        /// OverlapSphereNonAlloc fills the buffer and stops — it does not say
        /// it ran out, and which colliders make the cut is arbitrary. Thirty-two
        /// was enough on an empty greybox and is not enough beside the
        /// raccoon's pitch, where a bin, a sale zone, five shelves, a boundary
        /// and a doorway all sit inside one interaction radius. The symptom is
        /// one particular pickup that cannot be picked up, which reads as that
        /// pickup being broken.
        /// </summary>
        private const int MaxNearbyColliders = 128;

        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private PlayerConfig playerConfig;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private readonly Collider[] nearbyColliders =
            new Collider[MaxNearbyColliders];
        private IMatchStateReader matchState;
        private MonoBehaviour currentTargetComponent;

        /// <summary>
        /// Whether the thing in range is answered by a screen rather than by the
        /// key itself.
        ///
        /// The raccoon's pitch and the cat's bag. It matters because the interact
        /// key is forwarded to the host and run there: a key press that both opens
        /// a screen and performs the action would sell the piece in the thief's
        /// hands the instant they asked to *look* at the shop, and the cat's bag
        /// opened **on the officer's monitor** because the officer was hosting.
        ///
        /// Read only by the two local input paths. The host's own
        /// <see cref="TryInteractCurrent"/> is deliberately not gated on it: the
        /// sale itself still belongs to the zone, and the tests and the
        /// two-process probe drive it directly.
        /// </summary>
        public bool CurrentTargetIsAnsweredByAScreen =>
            CurrentTarget is IScreenAnsweredInteractable;

        public IPlayerInteractable CurrentTarget =>
            currentTargetComponent != null
                ? currentTargetComponent as IPlayerInteractable
                : null;
        public bool HasTarget => CurrentTarget != null;
        public string CurrentPrompt =>
            CurrentTarget != null ? CurrentTarget.Prompt : string.Empty;

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            PlayerConfig configuredPlayerConfig,
            IMatchStateReader configuredMatchState)
        {
            identity = configuredIdentity;
            playerConfig = configuredPlayerConfig;
            matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        public void RefreshTarget()
        {
            RefreshTarget(ContextInteractionKey.E);
        }

        public void RefreshTarget(ContextInteractionKey key)
        {
            currentTargetComponent = null;
            if (identity == null
                || playerConfig == null
                || !IsGameplayActive())
            {
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                playerConfig.InteractionRange,
                nearbyColliders,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            if (count >= MaxNearbyColliders)
            {
                // Said out loud rather than truncated in silence. If this ever
                // fires, something within arm's reach is invisible to the
                // player and no other symptom will tell them why.
                PawsAndLoot.Logging.GameLogger.Warning(
                    PawsAndLoot.Logging.GameLogCategory.Player,
                    $"Interaction scan filled its {MaxNearbyColliders} slot "
                    + "buffer, so something nearby was not considered.",
                    this);
            }

            float nearestDistance = float.PositiveInfinity;
            int highestPriority = int.MinValue;

            for (int index = 0; index < count; index++)
            {
                Collider nearby = nearbyColliders[index];
                if (nearby == null || nearby.transform.IsChildOf(transform))
                {
                    continue;
                }

                MonoBehaviour component =
                    FindInteractableComponent(nearby);
                if (component is not IPlayerInteractable candidate
                    || !InteractionResolver.IsValid(candidate, identity, key))
                {
                    continue;
                }

                float distance = (
                    candidate.InteractionTransform.position
                    - transform.position).sqrMagnitude;
                int priority = candidate is IInteractionPriority prioritized
                    ? prioritized.InteractionPriority
                    : 0;
                if (priority < highestPriority
                    || (priority == highestPriority
                        && distance >= nearestDistance))
                {
                    continue;
                }

                highestPriority = priority;
                nearestDistance = distance;
                currentTargetComponent = component;
            }
        }

        public bool TryInteractCurrent()
        {
            IPlayerInteractable target = CurrentTarget;
            if (target == null
                || !IsGameplayActive()
                || !InteractionResolver.IsValid(
                    target,
                    identity,
                    ContextInteractionKey.E))
            {
                return false;
            }

            bool succeeded = InteractionResolver.TryExecute(
                target,
                new PlayerInteractionContext(identity),
                ContextInteractionKey.E);
            RefreshTarget();
            return succeeded;
        }

        private void Update()
        {
            RefreshTarget();
        }

        private bool IsGameplayActive()
        {
            if (matchState == null && matchStateSource != null)
            {
                matchState = matchStateSource as IMatchStateReader;
            }

            return matchState?.IsGameplayActive == true;
        }

        private static MonoBehaviour FindInteractableComponent(
            Collider source)
        {
            MonoBehaviour[] components =
                source.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                if (component is IPlayerInteractable)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
