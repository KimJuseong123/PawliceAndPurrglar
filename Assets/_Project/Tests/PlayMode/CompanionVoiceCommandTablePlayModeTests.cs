using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The table of things to say to the animal.
    ///
    /// It replaced the panel listing `CTRL + 1..4`, which described keys that no
    /// longer exist. What matters about the replacement is not that it draws —
    /// the panel it replaced drew perfectly — but that it draws *this* player's
    /// vocabulary. A thief shown the dog's orders would try "짖어", get silence,
    /// and have no way to find out why: an unrecognised phrase and a phrase for
    /// the wrong animal fail identically.
    /// </summary>
    public sealed class CompanionVoiceCommandTablePlayModeTests
    {
        private GameObject _table;

        [TearDown]
        public void TearDown()
        {
            if (_table != null)
            {
                Object.DestroyImmediate(_table);
            }

            // Static and outlives the scene, so a role left set here decides
            // the role for whatever test runs next (`ISSUE-054`).
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        [UnityTest]
        public IEnumerator TheThiefIsShownTheCatsOrdersAndNotTheDogs()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            _table = new GameObject("Table");
            CompanionVoiceCommandTableView view =
                _table.AddComponent<CompanionVoiceCommandTableView>();
            yield return null;

            view.Refresh();

            Assert.That(view.ShowingRole, Is.EqualTo(PlayerRole.Thief));
            Assert.That(
                view.Alpha,
                Is.GreaterThan(0.9f),
                "A table drawn at alpha zero is the failure this file exists "
                + "for.");

            string all = string.Join("\n", view.Lines);
            Assert.That(all, Does.Contain("고양이"));
            Assert.That(all, Does.Contain("숨기"), "숨기 is the cat's.");
            Assert.That(all, Does.Contain("유인"));
            Assert.That(
                all,
                Does.Not.Contain("짖기"),
                "짖기 is the dog's and does nothing for a cat.");
            Assert.That(all, Does.Not.Contain("추적"));

            // The five either animal obeys are on both tables, and they are the
            // only ones that resolve with the speech model unreachable.
            Assert.That(all, Does.Contain("멈추기"));
            Assert.That(all, Does.Contain("돌아오기"));
        }

        [UnityTest]
        public IEnumerator SwappingSidesRedrawsTheTableRatherThanAddingToIt()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            _table = new GameObject("Table");
            CompanionVoiceCommandTableView view =
                _table.AddComponent<CompanionVoiceCommandTableView>();
            yield return null;

            view.Refresh();
            int thiefRows = view.RowCount;
            Assert.That(thiefRows, Is.EqualTo(9));

            // A rematch can hand this machine the other side.
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            view.Refresh();

            Assert.That(view.ShowingRole, Is.EqualTo(PlayerRole.Police));
            Assert.That(
                view.RowCount,
                Is.EqualTo(thiefRows),
                "The rows have to be replaced, not appended to. Both tables at "
                + "once is the failure that reads as the table simply being "
                + "wrong.");

            string all = string.Join("\n", view.Lines);
            Assert.That(all, Does.Contain("짖기"));
            Assert.That(all, Does.Not.Contain("숨기"));
        }

        /// <summary>
        /// Every row prints something in both columns.
        ///
        /// A command whose phrase came back empty would draw a name with blank
        /// space beside it, which reads as "this one takes no words" rather
        /// than as a missing entry.
        /// </summary>
        [Test]
        public void EveryListedCommandHasAKoreanNameAndSomethingToSay()
        {
            foreach (PlayerRole role in
                new[] { PlayerRole.Police, PlayerRole.Thief })
            {
                CompanionCommandId[] commands =
                    CompanionCommandCatalog.GetCommandsFor(role);
                Assert.That(commands, Has.Length.EqualTo(9));
                Assert.That(
                    commands.Distinct().Count(),
                    Is.EqualTo(commands.Length),
                    "A repeated row is a row the player reads twice.");

                foreach (CompanionCommandId command in commands)
                {
                    Assert.That(
                        CompanionCommandCatalog.GetKoreanName(command),
                        Is.Not.Empty.And.Not.EqualTo("없음"),
                        command.ToString());
                    Assert.That(
                        CompanionCommandCatalog.GetSpokenExamples(command),
                        Is.Not.Empty,
                        command.ToString());

                    // And it has to be a command this role may actually give.
                    // Printing one the dispatcher will refuse is worse than
                    // printing nothing.
                    Assert.That(
                        CompanionCommandCatalog.BelongsTo(command, role),
                        Is.True,
                        $"{command} is on the {role} table but not theirs.");
                }
            }
        }
    }
}
