using System;
using PawsAndLoot.Gameplay.Loot;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class LootCarryMovementPenalty : MonoBehaviour
    {
        [SerializeField]
        private LootCarrier carrier;

        [SerializeField]
        private PlayerMovementMotor movementMotor;

        private bool _subscribed;

        public bool IsApplied =>
            movementMotor != null
            && movementMotor.IsLootCarryPenaltyActive;

        public void Configure(
            LootCarrier configuredCarrier,
            PlayerMovementMotor configuredMovementMotor)
        {
            Unsubscribe();
            carrier = configuredCarrier
                ? configuredCarrier
                : throw new ArgumentNullException(
                    nameof(configuredCarrier));
            movementMotor = configuredMovementMotor
                ? configuredMovementMotor
                : throw new ArgumentNullException(
                    nameof(configuredMovementMotor));
            SubscribeAndSync();
        }

        private void OnEnable()
        {
            SubscribeAndSync();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (movementMotor != null)
            {
                movementMotor.SetLootCarryPenalty(false);
            }
        }

        private void HandleHeldLootChanged(
            LootItem previous,
            LootItem current)
        {
            SyncFromBag();
        }

        private void HandleCarriedLootChanged()
        {
            SyncFromBag();
        }

        /// <summary>
        /// Reads the whole bag rather than the piece in hand.
        ///
        /// The bag holds several pieces now, and the hands hold the last one
        /// taken. Weighing the hands would let a thief carry two gold bars at
        /// ring speed by picking the ring up last, which is the one order every
        /// player would find.
        /// </summary>
        private void SyncFromBag()
        {
            if (carrier == null || movementMotor == null)
            {
                return;
            }

            movementMotor.SetLootCarryPenalty(
                carrier.HasLoot,
                carrier.HeaviestCarryType);
        }

        private void SubscribeAndSync()
        {
            if (!isActiveAndEnabled
                || carrier == null
                || movementMotor == null)
            {
                return;
            }

            if (!_subscribed)
            {
                carrier.HeldLootChanged += HandleHeldLootChanged;
                carrier.CarriedLootChanged += HandleCarriedLootChanged;
                _subscribed = true;
            }

            SyncFromBag();
        }

        private void Unsubscribe()
        {
            if (_subscribed && carrier != null)
            {
                carrier.HeldLootChanged -= HandleHeldLootChanged;
                carrier.CarriedLootChanged -= HandleCarriedLootChanged;
            }

            _subscribed = false;
        }
    }
}
