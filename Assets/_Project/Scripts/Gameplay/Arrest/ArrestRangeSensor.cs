using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Arrest
{
    public sealed class ArrestRangeSensor : MonoBehaviour
    {
        private const int MaxSightHits = 32;

        [SerializeField]
        private PlayerRoleIdentity police;

        [SerializeField]
        private PlayerRoleIdentity thief;

        [SerializeField]
        private ArrestConfig arrestConfig;

        [SerializeField]
        private LayerMask obstacleLayers = Physics.AllLayers;

        private readonly RaycastHit[] sightHits =
            new RaycastHit[MaxSightHits];

        public event Action<PlayerRoleIdentity> TargetEntered;
        public event Action<PlayerRoleIdentity> TargetExited;

        public PlayerRoleIdentity Police => police;
        public PlayerRoleIdentity Thief => thief;
        public bool IsTargetDetected { get; private set; }
        public float DistanceToTarget =>
            police != null && thief != null
                ? Vector3.Distance(
                    police.transform.position,
                    thief.transform.position)
                : float.PositiveInfinity;

        public void Configure(
            PlayerRoleIdentity configuredPolice,
            PlayerRoleIdentity configuredThief,
            ArrestConfig configuredArrestConfig,
            LayerMask configuredObstacleLayers)
        {
            police = configuredPolice;
            thief = configuredThief;
            arrestConfig = configuredArrestConfig;
            obstacleLayers = configuredObstacleLayers;
            ValidateOrThrow();
            Evaluate();
        }

        public void Evaluate()
        {
            bool detected = HasValidTarget()
                && DistanceToTarget <= arrestConfig.ArrestDistance
                && HasLineOfSight();
            SetDetection(detected);
        }

        public void ValidateOrThrow()
        {
            if (police == null
                || thief == null
                || arrestConfig == null)
            {
                throw new InvalidOperationException(
                    $"ArrestRangeSensor '{name}' has missing references.");
            }

            if (police == thief)
            {
                throw new InvalidOperationException(
                    "ArrestRangeSensor cannot target its own player.");
            }

            if (police.Role != PlayerRole.Police
                || thief.Role != PlayerRole.Thief)
            {
                throw new InvalidOperationException(
                    "ArrestRangeSensor requires one Police and one Thief.");
            }

            arrestConfig.ValidateOrThrow();
        }

        private bool HasValidTarget()
        {
            return police != null
                && thief != null
                && police != thief
                && police.enabled
                && thief.enabled
                && police.gameObject.activeInHierarchy
                && thief.gameObject.activeInHierarchy
                && police.Role == PlayerRole.Police
                && thief.Role == PlayerRole.Thief;
        }

        private bool HasLineOfSight()
        {
            Vector3 origin = GetSightPoint(police);
            Vector3 target = GetSightPoint(thief);
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(
                origin,
                direction / distance,
                sightHits,
                distance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider hit = sightHits[index].collider;
                if (hit == null
                    || IsOwnedBy(hit.transform, police.transform)
                    || IsOwnedBy(hit.transform, thief.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void SetDetection(bool detected)
        {
            if (IsTargetDetected == detected)
            {
                return;
            }

            IsTargetDetected = detected;
            if (detected)
            {
                TargetEntered?.Invoke(thief);
            }
            else
            {
                TargetExited?.Invoke(thief);
            }
        }

        private static Vector3 GetSightPoint(
            PlayerRoleIdentity identity)
        {
            Collider collider = identity.GetComponent<Collider>();
            return collider != null
                ? collider.bounds.center
                : identity.transform.position + Vector3.up;
        }

        private static bool IsOwnedBy(
            Transform candidate,
            Transform owner)
        {
            return candidate == owner || candidate.IsChildOf(owner);
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void Update()
        {
            Evaluate();
        }

        private void OnDisable()
        {
            SetDetection(false);
        }
    }
}
