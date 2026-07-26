namespace PawsAndLoot.Companions
{
    /// <summary>
    /// States shared by the dog and the cat. The concrete command behaviour
    /// lives outside this enum so both animals reuse one lifecycle.
    /// </summary>
    public enum CompanionState
    {
        Idle = 0,
        Follow = 1,
        MoveToTarget = 2,
        ExecuteCommand = 3,
        ReturnToOwner = 4,
        Cooldown = 5,
        Disabled = 6
    }
}
