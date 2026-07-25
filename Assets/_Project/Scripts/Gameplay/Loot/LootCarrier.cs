using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootCarrier : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;

        public event Action<LootItem, LootItem> HeldLootChanged;

        public LootItem HeldLoot { get; private set; }
        public bool HasLoot => HeldLoot != null;

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            IMatchStateReader configuredMatchState)
        {
            identity = configuredIdentity;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        public bool TryAcquire(LootItem loot)
        {
            if (loot == null
                || identity == null
                || identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot != null
                || !loot.TryAcquire(this))
            {
                return false;
            }

            LootItem previous = HeldLoot;
            HeldLoot = loot;
            HeldLootChanged?.Invoke(previous, HeldLoot);
            return true;
        }

        private bool IsGameplayActive()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            return _matchState?.IsGameplayActive == true;
        }
    }
}
