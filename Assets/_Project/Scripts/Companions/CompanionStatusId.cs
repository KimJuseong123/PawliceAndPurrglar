namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Player-facing companion status. The order is intentionally not the
    /// priority; CompanionStatusPriority owns that policy in one place.
    /// </summary>
    public enum CompanionStatusId
    {
        Idle = 0,
        CommandReceived = 1,
        Tracking = 2,
        Guarding = 3,
        Chasing = 4,
        Distracted = 5,
        HeardNoise = 6,
        FoundTarget = 7,
        Attacking = 8,
        Confused = 9,
        Stunned = 10,
        SearchingHideout = 11
    }

    public static class CompanionStatusPriority
    {
        public static int Get(CompanionStatusId status)
        {
            return status switch
            {
                CompanionStatusId.Stunned => 100,
                CompanionStatusId.Attacking => 90,
                CompanionStatusId.Confused => 80,
                CompanionStatusId.FoundTarget => 70,
                CompanionStatusId.Chasing => 60,
                CompanionStatusId.Distracted => 50,
                CompanionStatusId.CommandReceived => 40,
                CompanionStatusId.Tracking => 35,
                CompanionStatusId.Guarding => 35,
                CompanionStatusId.HeardNoise => 30,
                CompanionStatusId.SearchingHideout => 25,
                _ => 0
            };
        }
    }
}
