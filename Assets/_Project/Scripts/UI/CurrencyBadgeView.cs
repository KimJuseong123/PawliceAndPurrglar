using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// A coin and a number: how much money the local player has.
    ///
    /// Two of these exist and they show the same figure — one in the bag's header
    /// and one on the screen the whole match. That is deliberate. Money is the
    /// thief's win condition, and a total that can only be read by opening a
    /// screen makes the player open the screen to answer "am I close yet?" —
    /// which is the one question they ask while being chased.
    ///
    /// Which one it is comes from a serialized enum rather than from a name
    /// lookup, and the controller finds them by asking for the component rather
    /// than by being handed them. An editor script's assignments to a
    /// <c>List</c> do not survive the prefab being written (<c>ISSUE-031</c>),
    /// so the presenter does its own finding at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyBadgeView : MonoBehaviour
    {
        public enum Placement
        {
            /// <summary>Always on screen, next to the role panel.</summary>
            Screen = 0,

            /// <summary>In the bag window's header.</summary>
            Bag = 1,

            /// <summary>In the merchant window's header.</summary>
            Merchant = 2
        }

        [SerializeField] private Placement placement = Placement.Screen;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Image coinIcon;

        public Placement Where => placement;

        public void Configure(
            Placement configuredPlacement,
            TMP_Text configuredAmountLabel,
            Image configuredCoinIcon)
        {
            placement = configuredPlacement;
            amountLabel = configuredAmountLabel;
            coinIcon = configuredCoinIcon;
        }

        public void Bind(int amount)
        {
            if (amountLabel != null)
            {
                // Grouped, because the figure runs to four digits and "1850"
                // takes a beat to read as one thousand eight hundred while
                // somebody is chasing you.
                amountLabel.text = Mathf.Max(0, amount).ToString("N0");
            }

            if (coinIcon != null)
            {
                coinIcon.enabled = coinIcon.sprite != null;
            }
        }
    }
}
