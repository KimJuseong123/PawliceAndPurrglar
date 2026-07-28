using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Animation;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Both characters have to move their legs when they move.
    ///
    /// Asserted for both roles in one test on purpose: the failure being guarded
    /// against is one of them animating and the other not, which is invisible in
    /// any test that only ever looks at one.
    ///
    /// Measured on the bones rather than on any component's own opinion. The
    /// procedural walk stands down whenever an Animator is enabled, so "the
    /// component exists and found four limbs" was true for a character that
    /// never moved a leg.
    /// </summary>
    public sealed class PlayerWalkAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator BothRolesSwingTheirLegsWhileMoving()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            foreach (PlayerRole role in
                new[] { PlayerRole.Police, PlayerRole.Thief })
            {
                PlayerRoleIdentity player = Object
                    .FindObjectsByType<PlayerRoleIdentity>(
                        FindObjectsSortMode.None)
                    .First(identity => identity.Role == role);

                var stride = player.GetComponent<CompanionLegAnimator>();
                Assert.That(
                    stride,
                    Is.Not.Null,
                    $"{role} has no procedural walk at all.");
                Assert.That(
                    stride.LegCount,
                    Is.GreaterThan(0),
                    $"{role}'s walk found no limb bones.");

                Animator animator = player
                    .GetComponentInChildren<Animator>(true);
                var guard = animator != null
                    ? animator.GetComponent<AnimatorClipGuard>()
                    : null;

                // The one that decides whether anything moves. With no authored
                // clips in the repository the Animator has to be off, or the
                // procedural walk stands down and the character slides.
                Assert.That(
                    animator == null || !animator.enabled,
                    Is.True,
                    $"{role}'s Animator is enabled with "
                    + $"{AnimatorClipGuard.CountUsableClips(animator)} "
                    + "usable clips, so the procedural walk defers to it and "
                    + $"no leg moves. Guard intervened: "
                    + $"{guard?.DisabledForMissingClips}.");

                // Now walk it and watch a real bone.
                Transform thigh = player
                    .GetComponentsInChildren<Transform>(true)
                    .First(bone =>
                        bone.name.Contains("Thigh")
                        || bone.name.Contains("UpperLeg")
                        || bone.name.Contains("Upperleg"));
                Quaternion before = thigh.localRotation;

                CharacterController controller =
                    player.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }

                // Driven at a fixed timestep rather than per frame.
                //
                // Batch-mode frames are sub-millisecond, so forty of them is
                // under a tenth of a second of walking — the gait blends in
                // from standing and had barely begun, which made a perfectly
                // good walk measure as 7° and looked like a product bug. Two
                // seconds at 5 m/s is what a player actually sees.
                //
                // Peak over the stretch, not first-versus-last: the stride is a
                // sine, so two samples can land on the same value and report a
                // still leg that is really swinging.
                const float step = 0.05f;
                const float metresPerStep = 5f * step;
                float peak = 0f;
                for (int tick = 0; tick < 40; tick++)
                {
                    player.transform.position +=
                        player.transform.forward * metresPerStep;
                    stride.Tick(step);
                    peak = Mathf.Max(
                        peak,
                        Quaternion.Angle(before, thigh.localRotation));
                }

                yield return null;

                // The bone has to belong to the skinned mesh's skeleton, or
                // rotating it moves nothing on screen.
                //
                // Membership in `bones` is what is checked, not the weights: the
                // legacy `boneWeights` array comes back empty for these meshes
                // because they use the newer per-vertex layout, so a weight
                // check reported "not skinned" for a character that animates
                // perfectly well. A check that cannot distinguish the two cases
                // is worse than no check.
                SkinnedMeshRenderer[] skins = player
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(
                    skins,
                    Is.Not.Empty,
                    $"{role} has no skinned mesh, so no bone can move it.");
                Assert.That(
                    skins.Any(skin =>
                        System.Array.IndexOf(skin.bones, thigh) >= 0),
                    Is.True,
                    $"{role}'s '{thigh.name}' is not part of any skinned "
                    + "mesh's skeleton.");

                Assert.That(
                    peak,
                    Is.GreaterThan(12f),
                    $"{role}'s '{thigh.name}' swung only {peak:0.0}° at its "
                    + "widest over 40 frames of walking. The profile asks for "
                    + "a 36° swing, so anything near zero means the leg is "
                    + "being written and then overwritten, or not written at "
                    + "all.");
            }
        }
    }
}
