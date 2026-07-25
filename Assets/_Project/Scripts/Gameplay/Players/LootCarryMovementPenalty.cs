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
            movementMotor.SetLootCarryPenalty(current != null);
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
                _subscribed = true;
            }

            movementMotor.SetLootCarryPenalty(carrier.HasLoot);
        }

        private void Unsubscribe()
        {
            if (_subscribed && carrier != null)
            {
                carrier.HeldLootChanged -= HandleHeldLootChanged;
            }

            _subscribed = false;
        }
    }
}
