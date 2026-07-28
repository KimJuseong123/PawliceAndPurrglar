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
    /// Stun a player in the real Game scene and check the stars are actually
    /// drawable — not merely that the component thinks it is showing them.
    ///
    /// The existing test asserted <c>IsShowing</c>, which is the component's own
    /// opinion, and it passed the whole time the stars were invisible on screen.
    /// A mesh can be built, active and enabled and still draw nothing: facing
    /// away from the camera, behind it, or scaled to nothing.
    ///
    /// So this measures the things that decide whether a pixel appears.
    /// </summary>
    public sealed class StunStarsVisibilityPlayModeTests
    {
        /// <summary>
        /// The torch wedge is built by the same hand-rolled triangle fan as the
        /// stars, so it is worth asking whether it has the same defect. It is a
        /// marking on the road, so its front face has to point up.
        /// </summary>
        [UnityTest]
        public IEnumerator TorchWedgeFacesUpwards()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;
            yield return null;

            FlashlightConeView cone =
                Object.FindFirstObjectByType<FlashlightConeView>();
            Assert.That(cone, Is.Not.Null);
            Assert.That(cone.IsVisible, Is.True);

            MeshRenderer[] patches =
                cone.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(patches.Length, Is.EqualTo(2));

            foreach (MeshRenderer patch in patches)
            {
                Mesh mesh = patch.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Transform at = patch.transform;
                Vector3 a = at.TransformPoint(vertices[triangles[0]]);
                Vector3 b = at.TransformPoint(vertices[triangles[1]]);
                Vector3 c = at.TransformPoint(vertices[triangles[2]]);
                Vector3 normal =
                    Vector3.Cross(b - a, c - a).normalized;

                Assert.That(
                    Vector3.Dot(normal, Vector3.up),
                    Is.GreaterThan(0.5f),
                    $"{patch.name}'s front face points "
                    + $"{normal} instead of up, so the marking is culled "
                    + "when seen from above — which is the only angle this "
                    + "game is ever seen from.");
            }
        }

        [UnityTest]
        public IEnumerator StunStarsAreDrawableWhenAPlayerIsStunned()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            MatchRuntimeState runtime =
                Object.FindFirstObjectByType<MatchRuntimeState>();
            runtime.TryTransitionTo(MatchState.Playing);

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(identity => identity.Role == PlayerRole.Thief);
            var stun = thief.GetComponent<StunState>();
            var stars = thief.GetComponent<StunStarsView>();
            Assert.That(stun, Is.Not.Null);
            Assert.That(stars, Is.Not.Null);

            // Point the match camera at the thief the way the game does. Without
            // this the camera sits wherever the scene authored it and "outside
            // the frustum" would only mean the test never looked at the player.
            PawsAndLoot.Gameplay.Camera.TopDownFollowCamera follow =
                Object.FindFirstObjectByType<
                    PawsAndLoot.Gameplay.Camera.TopDownFollowCamera>();
            Assert.That(follow, Is.Not.Null);
            follow.SetTarget(thief.transform, true);
            follow.SnapToTarget();

            Assert.That(stun.TryApply(3f), Is.True);
            // Two frames: one for the view to build its ring, one for it to be
            // positioned and turned to face the camera.
            yield return null;
            yield return null;

            Assert.That(stars.IsShowing, Is.True);

            MeshRenderer[] starRenderers = thief
                .GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer =>
                    renderer.GetComponentInParent<StunStarsView>() != null)
                .ToArray();

            Assert.That(
                starRenderers.Length,
                Is.EqualTo(4),
                "Four stars have to exist as renderers, not just as a count "
                + "on the component.");

            foreach (MeshRenderer renderer in starRenderers)
            {
                Assert.That(
                    renderer.gameObject.activeInHierarchy,
                    Is.True,
                    $"{renderer.name} is not active.");
                Assert.That(
                    renderer.enabled,
                    Is.True,
                    $"{renderer.name} is disabled — something switched it "
                    + "off, most likely the torch visibility rule.");
                Assert.That(
                    renderer.sharedMaterial,
                    Is.Not.Null,
                    $"{renderer.name} has no material, so it draws nothing.");
                Assert.That(
                    renderer.bounds.size.magnitude,
                    Is.GreaterThan(0.05f),
                    $"{renderer.name} has collapsed to nothing.");
            }

            UnityEngine.Camera view = UnityEngine.Camera.main;
            Assert.That(view, Is.Not.Null);

            // Above the player, or the stars are inside the character.
            foreach (MeshRenderer renderer in starRenderers)
            {
                Assert.That(
                    renderer.transform.position.y,
                    Is.GreaterThan(thief.transform.position.y + 1.8f),
                    $"{renderer.name} is not above the head.");
            }

            // In front of the camera and inside its frustum. A star behind the
            // camera is enabled, active and completely invisible.
            Plane[] frustum =
                GeometryUtility.CalculateFrustumPlanes(view);
            foreach (MeshRenderer renderer in starRenderers)
            {
                Assert.That(
                    GeometryUtility.TestPlanesAABB(
                        frustum,
                        renderer.bounds),
                    Is.True,
                    $"{renderer.name} at {renderer.bounds.center} is outside "
                    + $"the frustum of the camera at "
                    + $"{view.transform.position} looking "
                    + $"{view.transform.forward}, while the thief it belongs "
                    + $"to is at {thief.transform.position}.");
            }

            // The decisive one: which way does the mesh face.
            //
            // These are single-sided triangles with backface culling on, so a
            // star turned the wrong way draws nothing at all while every other
            // check above still passes.
            foreach (MeshRenderer renderer in starRenderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Transform at = renderer.transform;
                Vector3 a = at.TransformPoint(vertices[triangles[0]]);
                Vector3 b = at.TransformPoint(vertices[triangles[1]]);
                Vector3 c = at.TransformPoint(vertices[triangles[2]]);

                // Unity treats clockwise-from-the-front as front-facing, which
                // makes this cross product the outward normal.
                Vector3 normal =
                    Vector3.Cross(b - a, c - a).normalized;
                Vector3 toCamera =
                    (view.transform.position - a).normalized;

                Assert.That(
                    Vector3.Dot(normal, toCamera),
                    Is.GreaterThan(0f),
                    $"{renderer.name}'s front face points away from the "
                    + "camera, so backface culling discards it. Facing the "
                    + "billboard at the camera is not enough — the winding "
                    + "has to agree with which side is the front.");
            }
        }
    }
}
