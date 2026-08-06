using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Standing next to a room's treasure has to make it the thing E acts on.
    /// </summary>
    public sealed class InteriorPickupDiagnosticsPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        [UnityTest]
        public IEnumerator TheThiefCanReachAndTakeARoomsTreasure()
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

            LootSpotDraw draw = Object
                .FindObjectsByType<LootSpotDraw>(FindObjectsSortMode.None)
                .Where(candidate => candidate.PieceCount > 0)
                .OrderBy(candidate =>
                    candidate.GetComponent<HouseInterior>().InteriorId)
                .First();
            var room = draw.GetComponent<HouseInterior>();

            LootItem piece = Object
                .FindObjectsByType<LootItem>(FindObjectsSortMode.None)
                .Where(item => item.Definition != null
                    && Vector3.Distance(
                        item.transform.position,
                        room.EntryPosition) < 40f)
                .OrderBy(item => item.name)
                .FirstOrDefault();
            Assert.That(
                piece,
                Is.Not.Null,
                $"Interior {room.InteriorId} deals {draw.PieceCount} pieces and "
                + "none of them are anywhere near the room.");

            // Nothing standing over any of them. A piece under a table top is
            // drawn inside the mesh: invisible, while the prompt still appears
            // because the collider is reachable. That is exactly what a room with
            // seven marked places and nothing on show looked like.
            foreach (LootItem candidate in Object
                .FindObjectsByType<LootItem>(FindObjectsSortMode.None)
                .Where(item => item.Definition != null
                    && Vector3.Distance(
                        item.transform.position,
                        room.EntryPosition) < 40f))
            {
                Vector3 from = candidate.transform.position
                    + (Vector3.up * 0.05f);
                bool covered = Physics.Raycast(
                    from,
                    Vector3.up,
                    out RaycastHit lid,
                    1.6f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                Debug.Log(
                    $"[PICKUP-DIAG] '{candidate.name}' y {candidate.transform.position.y:0.00} "
                    + $"scale {candidate.transform.localScale.x:0.00} "
                    + $"covered {(covered ? lid.collider.name : "no")}");
                Assert.That(
                    covered,
                    Is.False,
                    $"'{candidate.name}' has '{(covered ? lid.collider.name : string.Empty)}' "
                    + "directly over it, so its model is inside the furniture.");
            }

            Debug.Log(
                $"[PICKUP-DIAG] room {room.InteriorId} entry "
                + $"{room.EntryPosition} floor {room.FloorHeight} "
                + $"piece '{piece.name}' at {piece.transform.position} "
                + $"state {piece.CurrentState} "
                + $"renderers {piece.GetComponentsInChildren<Renderer>(true).Length} "
                + $"visible {piece.PresentationRoot.gameObject.activeInHierarchy}");

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(FindObjectsSortMode.None)
                .First(identity => identity.Role == PlayerRole.Thief);
            var controller = thief.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            thief.GetComponent<PlayerInteriorState>()
                ?.SetInterior(room.InteriorId);
            thief.transform.position =
                piece.transform.position + new Vector3(0.6f, 0f, 0f);
            Physics.SyncTransforms();
            yield return null;

            var scanner = thief.GetComponent<PlayerInteractionScanner>();
            scanner.RefreshTarget();

            Debug.Log(
                "[PICKUP-DIAG] target "
                + $"{(scanner.CurrentTarget == null ? "NONE" : scanner.CurrentTarget.GetType().Name)} "
                + $"prompt '{scanner.CurrentPrompt}'");

            Assert.That(
                scanner.CurrentTarget,
                Is.SameAs(piece),
                "Standing 0.6 m from "
                + $"'{piece.name}' the thief's scanner is looking at "
                + $"{(scanner.CurrentTarget == null ? "nothing" : scanner.CurrentTarget.GetType().Name)}.");
        }
    }
}
