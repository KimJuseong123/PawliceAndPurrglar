namespace PawliceAndPurrglar.Gameplay.Loot
{
    /// <summary>
    /// How much of the thief's hands and speed a piece of treasure costs.
    ///
    /// Everything used to weigh the same. A fountain pen and a gold bar were
    /// both "carrying loot", both cost the same fixed slice of speed, and the
    /// choice of what to steal was therefore only ever a choice about price.
    /// The whole point of a room full of different things is that some of them
    /// are worth the risk of being slow and some are not.
    ///
    /// Four steps rather than a number per item, because the thief has to be
    /// able to feel the difference the moment they pick something up, and a
    /// continuum of forty-seven multipliers reads as one blurry multiplier.
    /// The names come from
    /// <c>docs/17_게임_아이템_사용처_정리.md</c>.
    /// </summary>
    public enum LootCarryType
    {
        /// <summary>
        /// Goes in a pocket. Costs nothing at all — a watch or a pen should be
        /// a clean getaway, and paying even a little for it would make the
        /// small valuables pointless.
        /// </summary>
        Pocket = 0,

        /// <summary>
        /// Carried in one hand. A small cost.
        /// </summary>
        OneHand = 1,

        /// <summary>
        /// Both hands. Costs enough that an officer already on your heels will
        /// close the distance.
        /// </summary>
        TwoHand = 2,

        /// <summary>
        /// Too big to carry properly. Slow enough that taking it is a decision
        /// about the whole match rather than about one room.
        /// </summary>
        Bulky = 3
    }

    public static class LootCarryRules
    {
        /// <summary>
        /// What fraction of full speed the thief keeps.
        ///
        /// The old flat penalty was a single multiplier in
        /// <see cref="PawliceAndPurrglar.Config.PlayerConfig"/>; one hand keeps that
        /// value so nothing that already existed changes pace, and the other
        /// three are placed around it.
        ///
        /// Bulky is 0.6 rather than something slower because the thief still
        /// has to be able to reach the merchant. Below about that, carrying the
        /// crown stops being a gamble and becomes a promise to be caught, and
        /// nobody picks it up twice.
        /// </summary>
        public static float SpeedMultiplier(
            LootCarryType carryType,
            float oneHandMultiplier)
        {
            return carryType switch
            {
                LootCarryType.Pocket => 1f,
                LootCarryType.TwoHand => oneHandMultiplier * 0.85f,
                LootCarryType.Bulky => 0.6f,
                _ => oneHandMultiplier
            };
        }

        /// <summary>
        /// How long the thief has to stand still to take it.
        ///
        /// Taking something was instant, so the only cost of stealing was
        /// getting there. These seconds are the moment an officer rounding the
        /// corner can actually use — the thief is committed, in the open, and
        /// visibly busy.
        ///
        /// Scaled with size for the obvious reason: a ring comes off a cushion
        /// and a gold bar does not.
        /// </summary>
        public static float PickupSeconds(LootCarryType carryType)
        {
            return carryType switch
            {
                LootCarryType.Pocket => 0.4f,
                LootCarryType.OneHand => 0.9f,
                LootCarryType.TwoHand => 1.6f,
                LootCarryType.Bulky => 2.4f,
                _ => 0.9f
            };
        }

        /// <summary>
        /// How far the sound of dropping it carries.
        ///
        /// Derived from the carry type rather than written per piece, because
        /// it is the same fact seen from another side: what makes a gold bar
        /// slow to run with is what makes it loud when it hits the pavement. A
        /// watch in a pocket makes no sound worth hearing and gets nothing.
        ///
        /// This is what gives the officer a reason to chase noise rather than
        /// only sightlines, and it gives the thief a reason to think before
        /// dropping the heavy thing to run — the drop that saves them is the
        /// drop that says where they are.
        /// </summary>
        public static float DropNoiseRadius(LootCarryType carryType)
        {
            return carryType switch
            {
                LootCarryType.Pocket => 0f,
                LootCarryType.OneHand => 9f,
                LootCarryType.TwoHand => 17f,
                LootCarryType.Bulky => 26f,
                _ => 9f
            };
        }

        public static string DisplayName(LootCarryType carryType)
        {
            return carryType switch
            {
                LootCarryType.Pocket => "주머니",
                LootCarryType.OneHand => "한 손",
                LootCarryType.TwoHand => "양손",
                LootCarryType.Bulky => "짐짝",
                _ => "한 손"
            };
        }
    }
}
