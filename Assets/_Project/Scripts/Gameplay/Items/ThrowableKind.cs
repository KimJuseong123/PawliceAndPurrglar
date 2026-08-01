using PawsAndLoot.Companions;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// The prop kinds a player can pick up and use.
    ///
    /// Deliberately few, and each one has to teach a different lesson. The rock
    /// punishes being seen, the banana punishes running a straight line, the glue
    /// trap punishes running the same route twice, and the sensor light punishes
    /// running past the same corner twice. Anything that does none of those would
    /// just be more inventory.
    ///
    /// Two per side, and they are not mirrored: the thief's props buy seconds,
    /// the police's props buy information and position. A police banana would be
    /// a worse rock, so neither side gets the other's.
    /// </summary>
    public enum ThrowableKind
    {
        /// <summary>
        /// Thrown at a player. Stuns on contact.
        /// </summary>
        Rock = 0,

        /// <summary>
        /// Placed on the ground. Slips whoever runs over it.
        /// </summary>
        Banana = 1,

        /// <summary>
        /// Police. Placed on the ground; holds the thief in place long enough to
        /// be caught up with. A sticky mouse trap, because a bear trap on a
        /// cartoon street would be the only cruel object in the town.
        /// </summary>
        GlueTrap = 2,

        /// <summary>
        /// Police. Placed on the ground; lights the thief up when they pass. The
        /// corridor sensor light every apartment block has — at night a light is
        /// worth more than an alarm, because the officer already cannot see.
        /// </summary>
        SensorLight = 3,

        /// <summary>
        /// Police. Placed on the ground; the thief's cat goes to it and stays.
        /// The cat scouts and steals, so pulling it away denies information —
        /// which is what the officer's props are for.
        /// </summary>
        TunaCan = 4,

        /// <summary>
        /// Thief. Placed on the ground; the police's dog goes to it and stays.
        /// The dog tracks, so pulling it away buys seconds — which is what the
        /// thief's props are for.
        /// </summary>
        DogTreat = 5
    }

    /// <summary>
    /// How each kind is used. Thrown props travel and hit; placed props wait.
    /// </summary>
    public enum ThrowableUse
    {
        Thrown = 0,
        Placed = 1
    }

    /// <summary>
    /// What a placed prop does to whoever sets it off.
    ///
    /// Separate from the kind so the network layer applies an effect rather than
    /// switching on every prop it has ever heard of.
    /// </summary>
    public enum TrapEffect
    {
        /// <summary>
        /// Holds them still. Duration comes from the kind.
        /// </summary>
        Hold = 0,

        /// <summary>
        /// Makes them visible for a while and does not slow them at all. The
        /// thief keeps running — they just do it in the open.
        /// </summary>
        Reveal = 1,

        /// <summary>
        /// Pulls the opponent's animal to the spot and keeps it there.
        ///
        /// Applies to the animal, never to a player, and it is the only effect
        /// that fires on being placed rather than on being trodden on: a smell
        /// that only works if the dog happens to step on it is not a lure.
        /// </summary>
        Lure = 2
    }

    public static class ThrowableCatalog
    {
        /// <summary>
        /// Durations come from <c>docs/03_GAME_RULES.md</c> section 12: a strong
        /// stun is 1.0~1.5s and a banana slip is 1.0s. They live here rather
        /// than in a config asset for now because the whole item layer is a
        /// prototype; they move to <c>Settings/Configs</c> once the set settles.
        /// </summary>
        public const float RockStunSeconds = 1.2f;
        public const float BananaSlipSeconds = 1.0f;

        /// <summary>
        /// Three seconds, down from the five first sketched. Five is long enough
        /// that a well-placed trap is an arrest rather than a chance at one, and
        /// the thief spends it watching.
        /// </summary>
        public const float GlueHoldSeconds = 3f;

        /// <summary>
        /// How long a tripped sensor keeps the thief visible. Long enough for the
        /// officer to turn and look, short enough that being seen once is not the
        /// end of the run.
        /// </summary>
        public const float RevealSeconds = 2.5f;

        /// <summary>
        /// How long an animal stays with the food.
        ///
        /// Four seconds is a corner and a half at a run. Long enough that
        /// losing the dog matters, short enough that the officer is not simply
        /// without a dog for the rest of the chase — the props buy a moment,
        /// they do not remove a character.
        /// </summary>
        public const float LureSeconds = 4f;

        /// <summary>
        /// How far a thrown prop travels before it drops. Short on purpose:
        /// a rock that crosses the map would make the chase a shooting range.
        ///
        /// Deliberately shorter than the torch's 17 m reach, so seeing somebody
        /// is not the same as being able to hit them — the officer still has to
        /// close the distance, which is the chase.
        /// </summary>
        public const float ThrowRangeMeters = 12f;

        /// <summary>
        /// Visual diameter of a thrown prop. The corridor is derived from it, so
        /// what the player watches fly is the same size as the thing that decides
        /// the hit.
        /// </summary>
        public const float PropDiameterMeters = 0.34f;

        /// <summary>
        /// Half-width of the corridor a throw sweeps, measured against the rig
        /// rather than picked.
        ///
        /// The brief was "the rock's edge grazing the victim's edge should
        /// count". A player capsule is 0.45 m in radius, so edges touching is
        /// 0.45 + one prop radius = 0.62 m. Two more prop widths of slack on top
        /// of that is what "generously" comes to.
        ///
        /// Worth stating plainly: this was never why throws were missing. The
        /// corridor was already wider than edge-to-edge, and the real cause was
        /// the throw stopping dead on invisible trigger volumes before it got
        /// anywhere near the target.
        /// </summary>
        public const float ThrowHitRadiusMeters =
            0.45f + PropDiameterMeters * 2.5f;

        public static ThrowableUse GetUse(ThrowableKind kind)
        {
            return kind == ThrowableKind.Rock
                ? ThrowableUse.Thrown
                : ThrowableUse.Placed;
        }

        /// <summary>
        /// Which side a prop belongs to. The pickup points enforce it, and it is
        /// stated here so a shop or a drop cannot disagree with the map.
        /// </summary>
        public static PawsAndLoot.Gameplay.Players.PlayerRole? GetOwner(
            ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana =>
                    PawsAndLoot.Gameplay.Players.PlayerRole.Thief,
                ThrowableKind.DogTreat =>
                    PawsAndLoot.Gameplay.Players.PlayerRole.Thief,
                ThrowableKind.TunaCan =>
                    PawsAndLoot.Gameplay.Players.PlayerRole.Police,
                ThrowableKind.GlueTrap =>
                    PawsAndLoot.Gameplay.Players.PlayerRole.Police,
                ThrowableKind.SensorLight =>
                    PawsAndLoot.Gameplay.Players.PlayerRole.Police,
                // A rock in the street is nobody's.
                _ => null
            };
        }

        public static TrapEffect GetEffect(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.SensorLight => TrapEffect.Reveal,
                ThrowableKind.TunaCan => TrapEffect.Lure,
                ThrowableKind.DogTreat => TrapEffect.Lure,
                _ => TrapEffect.Hold
            };
        }

        /// <summary>
        /// Which animal a lure calls. Null for everything that is not one.
        /// </summary>
        public static CompanionKind? GetLuredCompanion(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.TunaCan => CompanionKind.Cat,
                ThrowableKind.DogTreat => CompanionKind.Dog,
                _ => null
            };
        }

        public static float GetStunSeconds(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana => BananaSlipSeconds,
                ThrowableKind.GlueTrap => GlueHoldSeconds,
                // A sensor light does not slow anybody down. It only tells.
                ThrowableKind.SensorLight => 0f,
                // Food does nothing to a person. Stepping over a tuna can is
                // stepping over a tuna can.
                ThrowableKind.TunaCan => 0f,
                ThrowableKind.DogTreat => 0f,
                _ => RockStunSeconds
            };
        }

        /// <summary>
        /// How close somebody has to pass to set a placed prop off.
        ///
        /// The sensor reaches further than the things underfoot, because it is a
        /// detector rather than something you step in — and a detector you have
        /// to tread on exactly would never fire.
        /// </summary>
        public static float GetTriggerRadius(ThrowableKind kind)
        {
            return kind == ThrowableKind.SensorLight ? 3.2f : 0.85f;
        }

        /// <summary>
        /// Name shown in the HUD. Here so the UI does not grow its own list that
        /// drifts out of step with the enum.
        /// </summary>
        public static string GetDisplayName(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana => "바나나",
                ThrowableKind.GlueTrap => "끈끈이",
                ThrowableKind.SensorLight => "센서등",
                ThrowableKind.TunaCan => "참치캔",
                ThrowableKind.DogTreat => "개껌",
                _ => "돌"
            };
        }

        /// <summary>
        /// Model stem under <c>Assets/_Project/Art/Props</c>. The placed props
        /// have no authored models yet and borrow the can until they arrive;
        /// <c>PlacedTrapView</c> draws a greybox stand-in either way.
        /// </summary>
        public static string GetModelStem(ThrowableKind kind)
        {
            return kind == ThrowableKind.Rock
                ? "throwable_rock"
                : "throwable_can";
        }
    }
}
