using System;
using PawliceAndPurrglar.Companions;

namespace PawliceAndPurrglar.Integration.Voice
{
    public readonly struct DogBehaviorContext
    {
        public DogBehaviorContext(
            string[] nearbyDistractions,
            bool urgent,
            bool longCommand,
            int seed)
        {
            NearbyDistractions = nearbyDistractions ?? Array.Empty<string>();
            Urgent = urgent;
            LongCommand = longCommand;
            Seed = seed;
        }

        public string[] NearbyDistractions { get; }
        public bool Urgent { get; }
        public bool LongCommand { get; }
        public int Seed { get; }
    }

    public sealed class DogBehaviorFilter
    {
        private readonly DogBehaviorProfile profile;

        public DogBehaviorFilter(DogBehaviorProfile configuredProfile = null)
        {
            profile = configuredProfile;
        }

        public DogBehaviorDecision Decide(
            CompanionCommandId understoodCommand,
            string targetId,
            float confidence,
            DogBehaviorContext context)
        {
            if (understoodCommand == CompanionCommandId.None)
            {
                return new DogBehaviorDecision(
                    CompanionCommandId.None,
                    "NONE",
                    "MISUNDERSTOOD",
                    "NO_VALID_COMMAND",
                    targetId);
            }

            if (profile == null || context.NearbyDistractions.Length == 0)
            {
                return new DogBehaviorDecision(
                    understoodCommand,
                    CompanionCommandCatalog.GetDisplayName(understoodCommand),
                    string.Empty,
                    "",
                    targetId);
            }

            float distractionChance = profile.DistractionChance;
            if (context.Urgent)
            {
                distractionChance *= profile.UrgentCommandReduction;
            }

            var random = new Random(context.Seed);
            string distraction = context.NearbyDistractions[0];
            if (random.NextDouble() < distractionChance)
            {
                return new DogBehaviorDecision(
                    understoodCommand == CompanionCommandId.Track
                        ? CompanionCommandId.Search
                        : understoodCommand,
                    "GO_TO_" + distraction.ToUpperInvariant(),
                    "DISTRACTED",
                    "NEARBY_" + distraction.ToUpperInvariant(),
                    targetId);
            }

            if (context.LongCommand
                && random.NextDouble() < profile.LongCommandPartialChance
                && understoodCommand == CompanionCommandId.Track)
            {
                return new DogBehaviorDecision(
                    CompanionCommandId.Search,
                    "SEARCH",
                    "PARTIAL_MEMORY",
                    "LONG_COMMAND",
                    targetId);
            }

            return new DogBehaviorDecision(
                understoodCommand,
                CompanionCommandCatalog.GetDisplayName(understoodCommand),
                string.Empty,
                "",
                targetId);
        }
    }
}
