using PawsAndLoot.Companions;

namespace PawsAndLoot.Integration.Voice
{
    public readonly struct DogBehaviorDecision
    {
        public DogBehaviorDecision(
            CompanionCommandId actualCommand,
            string actualAction,
            string mistakeType,
            string reason,
            string targetId)
        {
            ActualCommand = actualCommand;
            ActualAction = actualAction ?? string.Empty;
            MistakeType = mistakeType ?? string.Empty;
            Reason = reason ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }

        public CompanionCommandId ActualCommand { get; }
        public string ActualAction { get; }
        public string MistakeType { get; }
        public string Reason { get; }
        public string TargetId { get; }
        public bool WasChanged => !string.IsNullOrWhiteSpace(MistakeType);
    }
}
