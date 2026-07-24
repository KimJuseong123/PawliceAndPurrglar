using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Game Config Set", fileName = "GameConfigSet")]
    public sealed class GameConfigSet : GameConfigAsset
    {
        [Header("Required Config Assets")]
        [SerializeField]
        private MatchConfig matchConfig;

        [SerializeField]
        private PlayerConfig playerConfig;

        [SerializeField]
        private LootConfig lootConfig;

        [SerializeField]
        private ArrestConfig arrestConfig;

        [SerializeField]
        private CompanionConfig companionConfig;

        [SerializeField]
        private VoiceConfig voiceConfig;

        public MatchConfig Match => matchConfig;
        public PlayerConfig Player => playerConfig;
        public LootConfig Loot => lootConfig;
        public ArrestConfig Arrest => arrestConfig;
        public CompanionConfig Companion => companionConfig;
        public VoiceConfig Voice => voiceConfig;

        public void Configure(
            MatchConfig match,
            PlayerConfig player,
            LootConfig loot,
            ArrestConfig arrest,
            CompanionConfig companion,
            VoiceConfig voice)
        {
            matchConfig = match;
            playerConfig = player;
            lootConfig = loot;
            arrestConfig = arrest;
            companionConfig = companion;
            voiceConfig = voice;
        }

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequireAssigned(this, matchConfig, nameof(matchConfig));
            GameConfigValidation.RequireAssigned(this, playerConfig, nameof(playerConfig));
            GameConfigValidation.RequireAssigned(this, lootConfig, nameof(lootConfig));
            GameConfigValidation.RequireAssigned(this, arrestConfig, nameof(arrestConfig));
            GameConfigValidation.RequireAssigned(this, companionConfig, nameof(companionConfig));
            GameConfigValidation.RequireAssigned(this, voiceConfig, nameof(voiceConfig));

            matchConfig.ValidateOrThrow();
            playerConfig.ValidateOrThrow();
            lootConfig.ValidateOrThrow();
            arrestConfig.ValidateOrThrow();
            companionConfig.ValidateOrThrow();
            voiceConfig.ValidateOrThrow();
        }
    }
}
