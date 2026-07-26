using System;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// CAT-005. Lets the cat actually pick loot up and bring it to its owner.
    ///
    /// The cat is a courier, never a merchant. It can carry and hand over, but
    /// selling stays a player action, so the thief keeps the decisive role.
    /// Loot in transit is reserved by the thief's own carrier, which is what
    /// stops the police from grabbing it out from under the cat and keeps the
    /// existing one-item limit honest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionLootCourier : MonoBehaviour
    {
        [SerializeField]
        private Transform carryPoint;

        [SerializeField]
        private LootCarrier ownerCarrier;

        [SerializeField, Min(0.2f)]
        private float pickupRange = 1.3f;

        [SerializeField, Min(0.2f)]
        private float deliverRange = 1.8f;

        public event Action<LootItem> LootPickedUp;
        public event Action<LootItem> LootDelivered;

        public LootItem CarriedLoot { get; private set; }
        public bool HasLoot => CarriedLoot != null;

        public void Configure(
            Transform configuredCarryPoint,
            LootCarrier configuredOwnerCarrier)
        {
            carryPoint = configuredCarryPoint;
            ownerCarrier = configuredOwnerCarrier;
            CarriedLoot = null;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (carryPoint == null)
            {
                throw new InvalidOperationException(
                    $"CompanionLootCourier '{name}' requires a carry point.");
            }

            if (ownerCarrier == null)
            {
                throw new InvalidOperationException(
                    $"CompanionLootCourier '{name}' requires the owner's "
                    + "LootCarrier.");
            }
        }

        /// <summary>
        /// Picks up loot the cat has reached. The item moves into the thief's
        /// carrier so all the existing acquisition rules apply, then its
        /// presentation is re-parented onto the cat for the trip home.
        /// </summary>
        public bool TryPickUp(LootItem loot)
        {
            if (HasLoot
                || loot == null
                || ownerCarrier == null
                || ownerCarrier.HasLoot
                || !loot.IsAvailable)
            {
                return false;
            }

            if (PlanarDistance(
                    transform.position,
                    loot.transform.position) > pickupRange)
            {
                return false;
            }

            // Acquiring through the owner's carrier keeps the one-item limit,
            // the state machine and the movement penalty in one place.
            if (!ownerCarrier.TryAcquire(loot))
            {
                return false;
            }

            CarriedLoot = loot;
            AttachPresentation(loot, carryPoint);
            GameLogger.Debug(
                GameLogCategory.Companion,
                $"Cat picked up '{loot.name}' for its owner.",
                this);
            LootPickedUp?.Invoke(loot);
            return true;
        }

        /// <summary>
        /// Hands the item over once the cat is close enough to its owner. The
        /// carrier already owns it, so this only moves the visual back to the
        /// player's carry point.
        /// </summary>
        public bool TryDeliver()
        {
            if (!HasLoot || ownerCarrier == null)
            {
                return false;
            }

            Transform owner = ownerCarrier.transform;
            if (PlanarDistance(transform.position, owner.position)
                > deliverRange)
            {
                return false;
            }

            LootItem delivered = CarriedLoot;
            CarriedLoot = null;
            AttachPresentation(delivered, ownerCarrier.CarryPoint);
            GameLogger.Debug(
                GameLogCategory.Companion,
                $"Cat delivered '{delivered.name}' to its owner.",
                this);
            LootDelivered?.Invoke(delivered);
            return true;
        }

        /// <summary>
        /// Drops the escort if the cat is disabled mid-trip. The owner keeps
        /// the item because their carrier never released it, so nothing is lost.
        /// </summary>
        public void AbandonEscort()
        {
            if (!HasLoot)
            {
                return;
            }

            LootItem abandoned = CarriedLoot;
            CarriedLoot = null;
            if (ownerCarrier != null && ownerCarrier.HeldLoot == abandoned)
            {
                AttachPresentation(abandoned, ownerCarrier.CarryPoint);
            }
        }

        private static void AttachPresentation(
            LootItem loot,
            Transform target)
        {
            if (loot == null
                || loot.PresentationRoot == null
                || target == null)
            {
                return;
            }

            loot.PresentationRoot.SetParent(target, false);
            loot.PresentationRoot.localPosition = Vector3.zero;
            loot.PresentationRoot.localRotation = Quaternion.identity;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
