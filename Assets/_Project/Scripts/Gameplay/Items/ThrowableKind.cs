using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;

namespace PawliceAndPurrglar.Gameplay.Items
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
        DogTreat = 5,

        /// <summary>
        /// Nobody's. Placed on the ground; squawks when anybody walks near and
        /// does nothing else at all.
        ///
        /// The only prop that belongs to neither side, because it is the only
        /// one that does not favour anybody: a sound pulls attention to a place
        /// regardless of who is standing there, and both players want that for
        /// opposite reasons. The officer wants a doorway that tells; the thief
        /// wants a corridor that lies.
        /// </summary>
        RubberChicken = 6,

        /// <summary>
        /// Thief. Placed on the ground; goes off by itself after a few seconds
        /// with a bang everyone hears.
        ///
        /// The only prop on a fuse rather than a tripwire, which is the whole
        /// point of it — a trap waits for somebody to make a mistake, and a
        /// firework happens whether or not anybody obliges. That is what makes
        /// it worth planning around and what makes it cost the thief their own
        /// position when they set it.
        /// </summary>
        Firework = 7,

        /// <summary>
        /// Thief. Thrown at a player; sticks to their face.
        ///
        /// Deliberately not a second rock. The rock takes time away, and
        /// another prop that also took time away would be the same prop with a
        /// different model. This takes sight instead — the victim keeps every
        /// bit of their speed and loses the ability to see where they are
        /// spending it.
        ///
        /// That makes it the only prop whose worth depends on where it lands.
        /// Blinded in an open square you shrug and keep running; blinded at a
        /// junction you have to guess. Nothing else in the set pays for timing
        /// that way, and it is rare because it is the strongest thing the thief
        /// can do to somebody who is already looking at them.
        /// </summary>
        FrozenOctopus = 8
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
        Lure = 2,

        /// <summary>
        /// Makes a sound where it stands and nothing else.
        ///
        /// Nobody is stopped, nobody is slowed, nobody loses anything. It goes
        /// on the noise board and the animals and the screen do what they like
        /// with it — which means it works on the player who set it off too, and
        /// that is not a flaw in it.
        /// </summary>
        Noise = 3,

        /// <summary>
        /// Covers their eyes and leaves their legs alone.
        ///
        /// The one effect that costs information rather than time. A victim who
        /// cannot see keeps every bit of their speed, which is why it is worth
        /// having alongside a stun instead of being folded into one.
        /// </summary>
        Blind = 4
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
        /// How long an animal stays with the food.
        ///
        /// Four seconds is a corner and a half at a run. Long enough that
        /// losing the dog matters, short enough that the officer is not simply
        /// without a dog for the rest of the chase — the props buy a moment,
        /// they do not remove a character.
        /// </summary>
        public const float LureSeconds = 4f;

        /// <summary>
        /// How far a bang carries.
        ///
        /// Deliberately larger than the torch's 17 m reach: hearing something
        /// should reach further than seeing it, or a noise prop is only ever a
        /// worse torch. Large enough to cross a block, small enough that half
        /// the town does not turn round.
        /// </summary>
        public const float NoiseRadiusMeters = 22f;

        /// <summary>
        /// How long a firework sits before it goes off.
        ///
        /// Long enough to walk away from and short enough to plan around. Under
        /// about two seconds the thief cannot be anywhere else when it fires,
        /// which is the only reason to place one.
        /// </summary>
        public const float FireworkFuseSeconds = 2.5f;

        /// <summary>
        /// How long the octopus stays on somebody's face.
        ///
        /// Longer than a stun, because it costs nothing but sight and a short
        /// blindness is one you can stand still through. Short enough that it
        /// is a corner, not a round.
        /// </summary>
        public const float BlindSeconds = 2.2f;

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
            return kind is ThrowableKind.Rock or ThrowableKind.FrozenOctopus
                ? ThrowableUse.Thrown
                : ThrowableUse.Placed;
        }

        /// <summary>
        /// Which side a prop belongs to. The pickup points enforce it, and it is
        /// stated here so a shop or a drop cannot disagree with the map.
        /// </summary>
        public static PawliceAndPurrglar.Gameplay.Players.PlayerRole? GetOwner(
            ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Thief,
                ThrowableKind.DogTreat =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Thief,
                ThrowableKind.TunaCan =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Police,
                ThrowableKind.GlueTrap =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Police,
                ThrowableKind.SensorLight =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Police,
                ThrowableKind.Firework =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Thief,
                ThrowableKind.FrozenOctopus =>
                    PawliceAndPurrglar.Gameplay.Players.PlayerRole.Thief,
                // A rock in the street is nobody's, and neither is a rubber
                // chicken on a supermarket shelf.
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
                ThrowableKind.RubberChicken => TrapEffect.Noise,
                ThrowableKind.Firework => TrapEffect.Noise,
                ThrowableKind.FrozenOctopus => TrapEffect.Blind,
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
                // Noise does nothing to a body. Being startled is not being
                // held, and a squawk that also stopped you would quietly be the
                // best trap in the game.
                ThrowableKind.RubberChicken => 0f,
                ThrowableKind.Firework => 0f,
                // It does not hold anybody. That is the rock's job, and giving
                // it away twice would leave the thief with two of the same
                // prop.
                ThrowableKind.FrozenOctopus => 0f,
                _ => RockStunSeconds
            };
        }

        /// <summary>
        /// What the stun should look like. Presentation only — the duration and
        /// the re-stun guard are the same for all three.
        ///
        /// A banana and a rock both take a second of control and were both being
        /// drawn as four stars over the head, which is the drawing for "somebody
        /// hit you". Nobody is hit by a banana; they stand on it and go over, and
        /// the joke only lands if the screen shows the fall. The glue trap is the
        /// third case: you are upright, awake, and your feet will not come off
        /// the floor.
        ///
        /// Anything new falls to <see cref="StunCause.Impact"/>, which is the
        /// safe default — it is what the stars already meant.
        /// </summary>
        public static StunCause GetStunCause(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana => StunCause.Slip,
                ThrowableKind.GlueTrap => StunCause.Stuck,
                _ => StunCause.Impact
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
            return kind switch
            {
                ThrowableKind.SensorLight => 3.2f,
                // Wider than something underfoot. A chicken you have to tread
                // on exactly is a chicken that never squawks, and the point of
                // it is to notice somebody passing rather than to catch them.
                ThrowableKind.RubberChicken => 2.4f,
                _ => 0.85f
            };
        }

        /// <summary>
        /// How long a prop waits before going off on its own, or zero if it
        /// waits to be trodden on instead.
        ///
        /// Asked of every prop rather than of the firework specifically, so the
        /// trap loop has one question to ask and adding a second timed prop does
        /// not add a branch to it.
        /// </summary>
        public static float GetFuseSeconds(ThrowableKind kind)
        {
            return kind == ThrowableKind.Firework
                ? FireworkFuseSeconds
                : 0f;
        }

        /// <summary>
        /// How long this prop blinds whoever it lands on, or zero if it does
        /// not.
        /// </summary>
        public static float GetBlindSeconds(ThrowableKind kind)
        {
            return kind == ThrowableKind.FrozenOctopus ? BlindSeconds : 0f;
        }

        /// <summary>
        /// How far this prop's noise carries, or zero if it makes none.
        /// </summary>
        public static float GetNoiseRadius(ThrowableKind kind)
        {
            return GetEffect(kind) == TrapEffect.Noise
                ? NoiseRadiusMeters
                : 0f;
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

        public static bool CanUseInQuickSlot(ThrowableKind kind)
        {
            return kind is ThrowableKind.Rock
                or ThrowableKind.Banana
                or ThrowableKind.GlueTrap
                or ThrowableKind.SensorLight
                or ThrowableKind.TunaCan
                or ThrowableKind.DogTreat
                or ThrowableKind.RubberChicken
                or ThrowableKind.Firework
                or ThrowableKind.FrozenOctopus;
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
                ThrowableKind.RubberChicken => "고무닭",
                ThrowableKind.Firework => "폭죽",
                ThrowableKind.FrozenOctopus => "냉동 문어",
                _ => "돌"
            };
        }

        /// <summary>
        /// What kind of thing this is, for the hover tooltip.
        ///
        /// Composed from the two facts that already exist — who owns it
        /// (<see cref="GetOwner"/>) and how it is used (<see cref="GetUse"/>) —
        /// rather than written out per kind, so a prop cannot be labelled the
        /// officer's here and the thief's at the pickup point.
        ///
        /// The six results are held as static strings rather than built on
        /// demand: the HUD rebinds twenty-five cells every frame, and a tooltip
        /// that allocated a string per cell per frame would be a per-frame cost
        /// for a panel nobody is looking at.
        /// </summary>
        public static string GetCategoryLabel(ThrowableKind kind)
        {
            bool thrown = GetUse(kind) == ThrowableUse.Thrown;
            return GetOwner(kind) switch
            {
                PlayerRole.Police => thrown
                    ? PoliceThrownCategory
                    : PolicePlacedCategory,
                PlayerRole.Thief => thrown
                    ? ThiefThrownCategory
                    : ThiefPlacedCategory,
                _ => thrown ? SharedThrownCategory : SharedPlacedCategory
            };
        }

        /// <summary>
        /// One line on what the prop does, for the hover tooltip.
        ///
        /// The seconds and the metres are interpolated from the constants above
        /// once, at type load, so the sentence cannot say three seconds while
        /// <see cref="GlueHoldSeconds"/> says four. Written here beside the
        /// durations rather than in the UI for the same reason
        /// <see cref="GetDisplayName"/> is: a list of prop descriptions living
        /// in a view is a list that drifts out of step with the enum.
        /// </summary>
        public static string GetShortDescription(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Banana => BananaDescription,
                ThrowableKind.GlueTrap => GlueTrapDescription,
                ThrowableKind.SensorLight => SensorLightDescription,
                ThrowableKind.TunaCan => TunaCanDescription,
                ThrowableKind.DogTreat => DogTreatDescription,
                ThrowableKind.RubberChicken => RubberChickenDescription,
                ThrowableKind.Firework => FireworkDescription,
                ThrowableKind.FrozenOctopus => FrozenOctopusDescription,
                _ => RockDescription
            };
        }

        /// <summary>
        /// How to use it, in the player's own controls.
        ///
        /// Both bindings, because <c>ToolUseInput</c> accepts both and a hint
        /// that named only the mouse would be wrong for the hand already on the
        /// keyboard. Thrown and placed props say different words for the same
        /// press: "throw" and "put down" are what the player is doing, and a
        /// single "use" would describe neither.
        /// </summary>
        public static string GetUsageHint(ThrowableKind kind)
        {
            return GetUse(kind) == ThrowableUse.Thrown
                ? ThrownUsageHint
                : PlacedUsageHint;
        }

        private const string SharedThrownCategory = "공용 · 투척 아이템";
        private const string SharedPlacedCategory = "공용 · 설치 아이템";
        private const string PoliceThrownCategory = "경찰 전용 · 투척 아이템";
        private const string PolicePlacedCategory = "경찰 전용 · 설치 아이템";
        private const string ThiefThrownCategory = "도둑 전용 · 투척 아이템";
        private const string ThiefPlacedCategory = "도둑 전용 · 설치 아이템";

        private const string ThrownUsageHint = "좌클릭 · F : 던지기";
        private const string PlacedUsageHint = "좌클릭 · F : 설치";

        private static readonly string RockDescription =
            $"맞은 상대를 {RockStunSeconds:0.#}초 동안 기절시킵니다.";
        private static readonly string BananaDescription =
            $"바닥에 두면 밟은 상대가 {BananaSlipSeconds:0.#}초 동안 미끄러집니다.";
        private static readonly string GlueTrapDescription =
            $"밟은 상대를 {GlueHoldSeconds:0.#}초 동안 붙잡아 둡니다.";
        private static readonly string SensorLightDescription =
            $"지나가는 상대를 {RevealSeconds:0.#}초 동안 환하게 드러냅니다.";
        private static readonly string TunaCanDescription =
            $"도둑의 고양이를 {LureSeconds:0.#}초 동안 불러 세웁니다.";
        private static readonly string DogTreatDescription =
            $"경찰의 강아지를 {LureSeconds:0.#}초 동안 불러 세웁니다.";
        private static readonly string RubberChickenDescription =
            $"누가 지나가면 {NoiseRadiusMeters:0}m 밖까지 들리는 소리를 냅니다.";
        private static readonly string FireworkDescription =
            $"{FireworkFuseSeconds:0.#}초 뒤에 스스로 터져 큰 소리를 냅니다.";
        private static readonly string FrozenOctopusDescription =
            $"맞은 상대의 시야를 {BlindSeconds:0.#}초 동안 가립니다.";

        /// <summary>
        /// Model stem under <c>Assets/_Project/Art/Props</c>, or null for a prop
        /// whose art has not been made.
        ///
        /// Every kind used to answer either "rock" or "can", so eight of the
        /// nine props wore a tuna can and the placed ones were greybox on top of
        /// that. A banana, a rubber chicken and a firework are three different
        /// promises to the player and they all looked like lunch.
        ///
        /// Null rather than a stand-in for the two that have no art. Borrowing
        /// somebody else's model is worse than a grey shape: a grey shape reads
        /// as unfinished and a wrong model reads as a lie.
        /// </summary>
        public static string GetModelStem(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "throwable_rock",
                ThrowableKind.Banana => "throwable_banana",
                ThrowableKind.TunaCan => "throwable_tuna_can",
                ThrowableKind.DogTreat => "throwable_bone",
                ThrowableKind.RubberChicken => "throwable_rubber_chicken",
                ThrowableKind.Firework => "throwable_firework",
                ThrowableKind.FrozenOctopus => "throwable_octopus",

                // The officer's two placed props. These were `null` for months,
                // which meant `PlacedTrapView` drew primitives — so putting one
                // down left a plain blue box on the road and the player could
                // not tell what they had placed, or that they had placed
                // anything. The art existed the whole time, in `ArtSource` under
                // the same names the HUD icons already used.
                ThrowableKind.GlueTrap => "trap_glue",
                ThrowableKind.SensorLight => "trap_sensor_light",
                _ => null
            };
        }

        /// <summary>
        /// How big the prop should be in the world, in metres, along
        /// whichever of its three dimensions is longest.
        ///
        /// Needed because the models do not arrive at a usable size and there is
        /// no reason they should — they are generated, and one came in at 23 cm
        /// while the next filled a room. The importer scales each one until it
        /// measures this wide, so the number here is the size the player sees
        /// rather than a multiplier against whatever the file happened to hold.
        ///
        /// The longest dimension rather than the footprint. Normalising the
        /// footprint sounds right for props read from overhead and is wrong for
        /// anything tall: the rubber chicken is a standing bird, so fixing its
        /// width at 38 cm made it well over a metre high — a toy the size of a
        /// child, standing in the road.
        ///
        /// Read at the camera distance this game is played from: a prop on the
        /// ground has to be told apart from directly overhead, and everything
        /// under about a quarter of a metre becomes a speck.
        /// </summary>
        public static float GetModelSize(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.FrozenOctopus => 0.50f,
                ThrowableKind.Firework => 0.42f,
                ThrowableKind.RubberChicken => 0.38f,
                ThrowableKind.Banana => 0.36f,
                ThrowableKind.DogTreat => 0.32f,

                // The officer's two, sized for what they have to say from the
                // fixed camera. The pouch reads as something dropped on the
                // ground and the lantern has to be seen standing on a dark
                // street, so it is the tallest of the set.
                ThrowableKind.SensorLight => 0.60f,
                ThrowableKind.GlueTrap => 0.45f,
                _ => 0.30f
            };
        }
    }
}
