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
        VoiceModelReady = 24,

        // C-1. Props that exist to make a noise.
        //
        // These six were the sheet's "구현이 끝났는데 소리만 없음" — the rubber
        // chicken and the firework do nothing else, so until now half of each of
        // them was missing.
        NoisePropSquawk = 25,
        NoisePropFuse = 26,
        NoisePropBang = 27,

        /// <summary>
        /// The same explosion heard from more than eight metres away.
        ///
        /// A different recording, not the near one turned down — distance is
        /// mostly the loss of the high end and the arrival of a room, and volume
        /// alone does not read as far away.
        /// </summary>
        NoiseHeardFar = 28,

        // Reacting to a noise, which is not the same as being told to do
        // something: these must not be the command-accepted bark and meow, or an
        // animal noticing a firework sounds like an animal being given an order.
        DogAlerted = 29,
        CatAlerted = 30,

        // C-2. Throwing and being hit.
        ThrowCharge = 31,
        ThrowReleased = 32,
        ThrowHitBody = 33,

        /// <summary>
        /// Seeing stars. Raised for a rock to the head and nothing else.
        ///
        /// A banana and a glue trap are also stuns, and they have their own
        /// sounds — playing this on top of those would be two sounds for one
        /// event, where the first one has already said what happened.
        /// </summary>
        Stunned = 34,

        Blinded = 35,
        TrapPlaced = 36,
        TrapSlip = 37,
        TrapSticky = 38,
        SensorTripped = 39,
        LureTaken = 40,

        // C-3. Loot and the cases it sits in.
        LootPickupStart = 41,
        GlassBreak = 42,
        CaseKeyUnlock = 43
    }
}
