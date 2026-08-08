using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "PawliceAndPurrglar/Config/Game Config Set", fileName = "GameConfigSet")]
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

        [SerializeField]
        private PetCognitionConfig petCognitionConfig;

        public MatchConfig Match => matchConfig;
        public PlayerConfig Player => playerConfig;
        public LootConfig Loot => lootConfig;
        public ArrestConfig Arrest => arrestConfig;
        public CompanionConfig Companion => companionConfig;
        public VoiceConfig Voice => voiceConfig;
        public PetCognitionConfig PetCognition => petCognitionConfig;

        public void Configure(
            MatchConfig match,
            PlayerConfig player,
            LootConfig loot,
            ArrestConfig arrest,
            CompanionConfig companion,
            VoiceConfig voice,
            PetCognitionConfig petCognition = null)
        {
            matchConfig = match;
            playerConfig = player;
            lootConfig = loot;
            arrestConfig = arrest;
            companionConfig = companion;
            voiceConfig = voice;
            petCognitionConfig = petCognition;
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
            if (petCognitionConfig != null)
            {
                petCognitionConfig.ValidateOrThrow();
            }
        }
    }
}
