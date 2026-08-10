using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The screens that build themselves have to exist in the match, and this
    /// checks the thing that was broken: the **install**, not the screen.
    ///
    /// All three of them are covered by tests that add the component by hand and
    /// then assert what it draws. Those tests pass on a build where the screen
    /// never appears at all, because none of them go anywhere near the code that
    /// decides whether it appears — which is how three screens came to be
    /// missing from every built match without a single failing test.
    ///
    /// What was wrong: <c>RuntimeInitializeOnLoadMethod(AfterSceneLoad)</c> runs
    /// once per run of the game, after the *first* scene loads. In a build that
    /// scene is the lobby, so the animal command table's guard ("is there an
    /// animal here?") refused and never ran again, and the two that did build
    /// something built it into the lobby scene, which the load that opens the
    /// match destroys.
    ///
    /// Loading the scene is the whole test. Nothing here inspects the panels.
    /// </summary>
    public sealed class MatchSceneInstallerPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        /// <summary>
        /// Leaves an empty scene behind, and this is not tidiness.
        ///
        /// A fixture that loads the town and stops there hands the next one a
        /// scene full of buildings. The next one along creates a bare player at
        /// the origin and asserts that it can walk — and at the origin of this
        /// map there is a road with geometry on it, so the character does not
        /// move and a test about swapping a model fails talking about
        /// millimetres. It cost a green run to find, and the fixture that broke
        /// it is the one that has to clean up.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene loaded = SceneManager.GetActiveScene();
            if (!loaded.name.Equals(
                    GameSceneCatalog.GetName(GameSceneId.Game),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            // Created first: unloading the only loaded scene is an error, and
            // the empty one also has to be the active scene before the town goes
            // or anything created next has nowhere to live.
            Scene empty = SceneManager.CreateScene(
                $"MatchSceneInstallerCleanup{_cleanups++}");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loaded);
        }

        private static int _cleanups;

        [UnityTest]
        public IEnumerator LoadingTheMatchInstallsTheScreensThatBuildThemselves()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            // Loaded here rather than in a helper the other tests share, because
            // the load *is* the thing under test: the installers hang off the
            // scene-loaded event, and a scene that was already open when this
            // fixture started would not raise it.
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            Assert.That(
                Object.FindFirstObjectByType<CompanionVoiceCommandTableView>(),
                Is.Not.Null,
                "The animal command table is missing from the match, so nobody "
                + "is told what they can say.");
            Assert.That(
                Object.FindFirstObjectByType<PlayerStatusBannerView>(),
                Is.Not.Null,
                "The status banner is missing, so being stuck or revealed is "
                + "never said out loud.");
            Assert.That(
                Object.FindFirstObjectByType<InkBlindOverlayView>(),
                Is.Not.Null,
                "The ink overlay is missing, so the octopus blinds nobody.");
        }

        /// <summary>
        /// And the table has to have words on it once the role is known. An
        /// installed panel at zero alpha with no rows is the same as no panel,
        /// and it is the state the table sits in until a role resolves.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCommandTableShowsEveryOrderForTheLocalRole()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            CompanionVoiceCommandTableView table =
                Object.FindFirstObjectByType<CompanionVoiceCommandTableView>();
            Assert.That(table, Is.Not.Null);
            table.Refresh();

            int expected = Companions.CompanionCommandCatalog
                .GetCommandsFor(PlayerRole.Thief).Length;
            Assert.That(
                table.RowCount,
                Is.EqualTo(expected),
                $"The table drew {table.RowCount} of the thief's {expected} "
                + "orders.");
            Assert.That(
                table.Alpha,
                Is.GreaterThan(0.9f),
                "The table is installed and invisible, which the player cannot "
                + "tell apart from missing.");
            Assert.That(
                table.ShowingRole,
                Is.EqualTo(PlayerRole.Thief),
                "The thief is being shown the dog's orders.");
        }
    }
}
