using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Switches an Animator off when its controller has no usable clips, so a
    /// character without animation still stands up straight.
    ///
    /// The locomotion clips come from TopDownEngine, a paid asset whose licence
    /// forbids redistribution, so it is not in this repository. In a checkout
    /// without it the controller's clip references are dangling, and a humanoid
    /// Animator with nothing to play retargets the rig to a rest pose that does
    /// not match the one the scene was built against.
    ///
    /// That mismatch is the bug this prevents. Model height and foot placement
    /// are baked at scene-build time from the bind pose
    /// (<c>PlaceholderModelLibrary.NormaliseHeight</c>), so a different runtime
    /// pose sank the characters into the ground: the game looked broken rather
    /// than merely unanimated.
    ///
    /// With the Animator off the mesh keeps its bind pose, which is exactly the
    /// pose those offsets were measured from. Nothing about gameplay changes:
    /// movement, collision and the rules never read the Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorClipGuard : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        /// <summary>
        /// True when the guard had to intervene. Exposed so a probe or test can
        /// tell "no animation available" apart from "animation is playing".
        /// </summary>
        public bool DisabledForMissingClips { get; private set; }

        public void Configure(Animator configuredAnimator)
        {
            animator = configuredAnimator;
        }

        /// <summary>
        /// Counts clips the controller can actually play. A missing asset
        /// reference deserialises to null rather than disappearing, so the array
        /// length alone does not answer this.
        /// </summary>
        public static int CountUsableClips(Animator candidate)
        {
            RuntimeAnimatorController controller =
                candidate == null
                    ? null
                    : candidate.runtimeAnimatorController;
            if (controller == null)
            {
                return 0;
            }

            AnimationClip[] clips = controller.animationClips;
            if (clips == null)
            {
                return 0;
            }

            int usable = 0;
            foreach (AnimationClip clip in clips)
            {
                if (clip != null)
                {
                    usable++;
                }
            }

            return usable;
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null || CountUsableClips(animator) > 0)
            {
                return;
            }

            animator.enabled = false;
            DisabledForMissingClips = true;
            // One shared message rather than one per character: GameLogger
            // suppresses repeats of the same key, so this reads once.
            GameLogger.Warning(
                GameLogCategory.Player,
                "No animation clips are available, so Animators are switched "
                + "off and characters hold their bind pose. Install "
                + "TopDownEngine locally, or wait for MODEL-002 to supply the "
                + "characters' own clips.",
                this);
        }
    }
}
