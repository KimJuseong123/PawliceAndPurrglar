using PawsAndLoot.Config;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerInteractionScanner : MonoBehaviour
    {
        private const int MaxNearbyColliders = 32;

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
            float nearestDistance = float.PositiveInfinity;

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
                    || !candidate.IsAvailable
                    || !identity.CanInteract(candidate.InteractionType))
                {
                    continue;
                }

                float distance = (
                    candidate.InteractionTransform.position
                    - transform.position).sqrMagnitude;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                currentTargetComponent = component;
            }
        }

        public bool TryInteractCurrent()
        {
            IPlayerInteractable target = CurrentTarget;
            if (target == null
                || !IsGameplayActive()
                || !target.IsAvailable
                || !identity.CanInteract(target.InteractionType))
            {
                return false;
            }

            bool succeeded = target.TryInteract(
                new PlayerInteractionContext(identity));
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
