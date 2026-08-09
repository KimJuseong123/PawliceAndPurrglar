namespace PawsAndLoot.Audio
{
    /// <summary>
    /// AUDIO-001. The complete sound vocabulary.
    ///
    /// Rules raise these by name and never touch an AudioSource, so a missing
    /// clip, a muted mixer or a removed audio system cannot change what
    /// happens in a match.
    /// </summary>
    public enum GameSoundId
    {
        None = 0,
        CommandSucceeded = 1,
        CommandFailed = 2,
        LootAcquired = 3,
        LootSold = 4,
        ArrestStarted = 5,
        ArrestCompleted = 6,
        DogBark = 7,
        CatMeow = 8,
        Victory = 9,
        Defeat = 10,

        // C-4. Doors and interiors.
        //
        // One id for going in, coming out and the leaf swinging. The three were
        // separate rows on the acquisition sheet and they are the same event to
        // a player: a door happened. Splitting them would have meant three
        // recordings that have to sound like the same door.
        DoorOpen = 11,

        // C-4. The bag opening and closing.
        InventoryToggle = 12,

        // C-5. Leaving the ground.
        Jump = 13,

        // C-6. A sale at the supply counter.
        PurchaseMade = 14,

        // C-7. The pre-match countdown.
        //
        // Named Tick after the sheet, raised once. The clip that arrived is a
        // whole three-second countdown rather than a single beep, and one per
        // second would have been three copies of it playing over each other.
        CountdownTick = 15,

        // C-8. Which role the lobby handed this player.
        //
        // Two ids rather than one with a role argument: they are two different
        // recordings, and the bank maps ids to clips.
        RoleAssignedPolice = 16,
        RoleAssignedThief = 17,

        // C-8. The other player arriving and leaving.
        //
        // Share their recordings with the voice capture pair below — a rising
        // beep for a connection and a falling one for a loss is the same
        // vocabulary. Kept as their own ids so the call sites read as what they
        // are and the mix can separate them later.
        PeerJoined = 18,
        PeerLeft = 19,

        // C-9. The raccoon merchant.
        RaccoonChitter = 20,

        // C-11. Voice capture.
        VoiceRecordStart = 21,
        VoiceRecordStop = 22,
        VoiceRecognizeFail = 23,
        VoiceModelReady = 24
    }
}
