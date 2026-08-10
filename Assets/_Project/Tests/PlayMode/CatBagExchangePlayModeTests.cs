using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// Clicking a cell has to move the thing in it.
    ///
    /// Driven through the cells' own buttons rather than by calling the handler,
    /// because the handler was never the part in doubt: what a player reports as
    /// "it does not move" is a click that never reached a listener. Invoking
    /// <c>onClick</c> is as close to the pointer as a headless test can get.
    /// </summary>
    public sealed class CatBagExchangePlayModeTests
    {
        /// <summary>
        /// Shuts what this test opened.
        ///
        /// The panels are left open otherwise, and "a panel is open" is a static
        /// flag that outlives the test — the next one runs with gameplay input
        /// suppressed and fails somewhere unrelated. Play Mode failures that
        /// arrive in a clump are pollution far more often than they are seven
        /// separate faults (`ISSUE-054`).
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            var hud = Object.FindFirstObjectByType<RoleAwareHudController>();
            if (hud != null)
            {
                hud.SetInventoryOpen(false);
            }

            // And the legacy HUD singleton the match scene installs. It is
            // marked DontDestroyOnLoad, so loading the match scene here left one
            // alive for whatever ran next — and the next test builds two of its
            // own and asserts which one wins. A survivor makes it lose to a HUD
            // it never created.
            foreach (CommonHudPresenter stray in Object
                .FindObjectsByType<CommonHudPresenter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(stray.gameObject);
            }

            PawliceAndPurrglar.Input.GameplayInputRouter
                .SetGameplayInputSuppressed(false);
            PawliceAndPurrglar.Input.GameplayInputRouter.SetToolUseSuppressed(false);
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        [UnityTest]
        public IEnumerator ClickingMovesPropsBothWaysBetweenTheBagAndTheCat()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;
            yield return null;

            var hud = Object.FindFirstObjectByType<RoleAwareHudController>();
            Assert.That(hud, Is.Not.Null, "No HUD in the match scene.");

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(FindObjectsSortMode.None)
                .First(identity => identity.Role == PlayerRole.Thief);
            var carrier = thief.GetComponent<ToolCarrier>();
            Assert.That(carrier, Is.Not.Null);

            CatInventoryInteractable.InstallMissingInteractables();
            var catBag =
                Object.FindFirstObjectByType<CatInventoryInteractable>();
            Assert.That(
                catBag,
                Is.Not.Null,
                "The cat has no bag component at all.");
            Assert.That(
                catBag.SlotCount,
                Is.EqualTo(4),
                "The cat's bag is not two by two.");

            // Something to move. The thief's starting loadout is whatever the
            // catalogue says, and a test that depended on it would break the day
            // somebody rebalanced the loadout.
            Assert.That(
                carrier.TryStore(ThrowableKind.Rock, 1),
                Is.True,
                "Could not put a rock in the thief's quick slots.");

            hud.OpenContainerExchange(catBag, carrier);
            yield return null;

            Assert.That(
                hud.IsInventoryOpen,
                Is.True,
                "Opening the cat's bag left the thief's own bag shut, so there "
                + "is nowhere to move things from.");

            int slot = FirstOccupiedSlot(carrier);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            carrier.TryGetSlot(slot, out ThrowableKind moving);

            Button bagCell = FindCell(hud, "Inventory Slot", slot + 1);
            Assert.That(
                bagCell,
                Is.Not.Null,
                $"No clickable cell {slot + 1} in the thief's bag.");
            bagCell.onClick.Invoke();
            yield return null;

            Assert.That(
                CountInContainer(catBag, moving),
                Is.EqualTo(1),
                $"Clicking bag cell {slot + 1} did not put the {moving} in the "
                + "cat's bag.");

            // And back again.
            int catSlot = FirstOccupiedSlot(catBag);
            Assert.That(catSlot, Is.GreaterThanOrEqualTo(0));
            Button catCell = FindCell(hud, "Cat Bag Slot", catSlot + 1);
            Assert.That(
                catCell,
                Is.Not.Null,
                $"No clickable cell {catSlot + 1} in the cat's bag.");
            catCell.onClick.Invoke();
            yield return null;

            Assert.That(
                CountInContainer(catBag, moving),
                Is.EqualTo(0),
                "Clicking the cat's cell did not hand the prop back.");
            Assert.That(
                CountInContainer(carrier, moving),
                Is.GreaterThan(0),
                "The prop left the cat's bag and did not arrive in the thief's.");
        }

        private static int FirstOccupiedSlot(ISlotContainer container)
        {
            for (int index = 0; index < container.SlotCount; index++)
            {
                if (container.TryGetSlot(index, out _))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CountInContainer(
            ISlotContainer container,
            ThrowableKind kind)
        {
            int held = 0;
            for (int index = 0; index < container.SlotCount; index++)
            {
                if (container.TryGetSlot(index, out ThrowableKind found)
                    && found == kind)
                {
                    held += container.GetSlotQuantity(index);
                }
            }

            return held;
        }

        private static Button FindCell(
            RoleAwareHudController hud,
            string prefix,
            int number)
        {
            return hud.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button =>
                    button.name == $"{prefix} {number}");
        }
    }
}
