using PawsAndLoot.Gameplay.Players;

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
        SensorLight = 3
        ,Bone = 4
        ,TunaCan = 5
        ,RubberChicken = 6
        ,NoiseCan = 7
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
        Reveal = 1
    }

    public readonly struct ThrowableLoadoutItem
    {
        public ThrowableLoadoutItem(ThrowableKind kind, int quantity)
        {
            Kind = kind;
            Quantity = quantity;
        }

        public ThrowableKind Kind { get; }
        public int Quantity { get; }
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
        /// How far a thrown prop travels before it drops. Short on purpose:
        /// a rock that crosses the map would make the chase a shooting range.
        ///
        /// Deliberately shorter than the torch's 17 m reach, so seeing somebody
        /// is not the same as being able to hit them — the officer still has to
        /// close the distance, which is the chase.
        /// </summary>
        public const float ThrowRangeMeters = 12f;
        public const float MinimumThrowRangeMeters = 4f;

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
        public const float PoliceThrowHitRadiusBonusMeters = 0.25f;

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
            return kind == ThrowableKind.SensorLight
                ? TrapEffect.Reveal
                : TrapEffect.Hold;
        }

        public static float GetStunSeconds(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana => BananaSlipSeconds,
                ThrowableKind.GlueTrap => GlueHoldSeconds,
                // A sensor light does not slow anybody down. It only tells.
                ThrowableKind.SensorLight => 0f,
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

        public static float GetThrowHitRadius(
            PlayerRole thrower,
            PlayerRole target) => thrower == PlayerRole.Police
                ? ThrowHitRadiusMeters + PoliceThrowHitRadiusBonusMeters
                : ThrowHitRadiusMeters;

        public static bool TryGetStartingLoadout(
            PlayerRole role,
            int slot,
            out ThrowableLoadoutItem item)
        {
            item = default;
            if (slot < 0 || slot >= QuickSlotController.SlotCount)
            {
                return false;
            }

            if (role == PlayerRole.Police)
            {
                item = slot switch
                {
                    0 => new ThrowableLoadoutItem(ThrowableKind.Rock, 2),
                    1 => new ThrowableLoadoutItem(ThrowableKind.GlueTrap, 1),
                    2 => new ThrowableLoadoutItem(ThrowableKind.SensorLight, 2),
                    _ => default
                };
                return slot <= 2;
            }

            item = slot switch
            {
                0 => new ThrowableLoadoutItem(ThrowableKind.Rock, 1),
                1 => new ThrowableLoadoutItem(ThrowableKind.Banana, 2),
                _ => default
            };
            return slot <= 1;
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
