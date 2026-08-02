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
            movementMotor.SetLootCarryPenalty(
                current != null,
                WeightOf(current));
        }

        /// <summary>
        /// How heavy a piece is, defaulting to one hand.
        ///
        /// A piece with no definition is a broken piece, and refusing to slow
        /// the thief at all would make the broken case the fastest one to
        /// carry.
        /// </summary>
        private static LootCarryType WeightOf(LootItem loot)
        {
            return loot != null && loot.Definition != null
                ? loot.Definition.CarryType
                : LootCarryType.OneHand;
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

            movementMotor.SetLootCarryPenalty(
                carrier.HasLoot,
                WeightOf(carrier.HeldLoot));
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
