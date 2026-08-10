using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// Owns the decision between authored Animator playback and procedural
    /// locomotion. It is created at runtime when an existing scene component
    /// first encounters a character Animator, so the current production scene
    /// does not need to be reserialized just to add this policy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationModeResolver : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private CharacterAnimationProfile profile;

        private bool _configured;
        private bool _reported;

        public Animator Animator => animator;
        public Transform VisualRoot => visualRoot;
        public CharacterAnimationProfile Profile => profile;
        public CharacterAnimationMode Mode { get; private set; } =
            CharacterAnimationMode.ProceduralFallback;
        public bool ProceduralFallbackEnabled =>
            Mode == CharacterAnimationMode.ProceduralFallback;
        public AnimationValidationResult Validation { get; private set; }

        /// <summary>
        /// Finds the existing PlayerRoot/companion root and adds this component
        /// only in memory. Adding it at runtime avoids touching Game.unity.
        /// </summary>
        public static CharacterAnimationModeResolver EnsureFor(
            Animator configuredAnimator,
            Transform preferredOwner = null,
            CharacterAnimationProfile configuredProfile = null)
        {
            if (configuredAnimator == null)
            {
                return null;
            }

            PlayerVisualRoot playerVisual =
                configuredAnimator.GetComponentInParent<PlayerVisualRoot>();
            Transform owner = playerVisual != null
                ? playerVisual.transform
                : preferredOwner != null
                    ? preferredOwner
                    : configuredAnimator.transform.root;

            CharacterAnimationModeResolver resolver =
                owner.GetComponent<CharacterAnimationModeResolver>();
            if (resolver == null)
            {
                resolver = owner.gameObject.AddComponent<
                    CharacterAnimationModeResolver>();
            }

            resolver.Configure(
                configuredAnimator,
                playerVisual != null
                    ? playerVisual.VisualRoot
                    : configuredAnimator.transform.parent,
                configuredProfile);
            return resolver;
        }

        public void Configure(
            Animator configuredAnimator,
            Transform configuredVisualRoot = null,
            CharacterAnimationProfile configuredProfile = null)
        {
            animator = configuredAnimator;
            visualRoot = configuredVisualRoot;
            if (configuredProfile != null)
            {
                profile = configuredProfile;
            }

            _configured = true;
            Resolve();
        }

        public void Resolve()
        {
            if (!_configured || animator == null)
            {
                return;
            }

            if (animator.runtimeAnimatorController == null
                && profile != null
                && profile.AnimatorController != null)
            {
                animator.runtimeAnimatorController = profile.AnimatorController;
            }

            animator.applyRootMotion = false;
            Validation = AnimatorClipGuard.Validate(animator, profile);
            bool valid = Validation.IsValid
                && (profile == null || !profile.ApplyRootMotion);
            Mode = valid
                ? CharacterAnimationMode.AuthoredAnimator
                : CharacterAnimationMode.ProceduralFallback;

            // Apply the decision before fallback LateUpdate runs. The fallback
            // itself also checks this state, so repeated resolver calls remain
            // harmless and cannot produce a transform fight.
            animator.enabled = valid;
            LogOnce();
        }

        public bool IsAuthoredAnimatorActive(Animator candidate)
        {
            return candidate != null
                && candidate == animator
                && Mode == CharacterAnimationMode.AuthoredAnimator
                && candidate.enabled;
        }

        private void LogOnce()
        {
            if (_reported || Validation == null)
            {
                return;
            }

            _reported = true;
            string character = profile == null
                ? ResolveCharacterName()
                : profile.Character.ToString();
            string rig = profile == null
                ? ResolveRigType().ToString()
                : profile.RigType.ToString();
            string controller = animator == null
                || animator.runtimeAnimatorController == null
                ? "None"
                : animator.runtimeAnimatorController.name;

            string message =
                "[AnimationSetup] "
                + $"Character: {character}; "
                + $"RigType: {rig}; "
                + $"Animator: {(animator == null ? "None" : animator.name)}; "
                + $"Controller: {controller}; "
                + $"RuntimeClipCount: {Validation.RuntimeClipCount}; "
                + $"IdleClip: {NameOrNone(Validation.IdleClipName)}; "
                + $"WalkClip: {NameOrNone(Validation.WalkClipName)}; "
                + $"RunClip: {NameOrNone(Validation.RunClipName)}; "
                + $"AnimatorEnabled: {Mode == CharacterAnimationMode.AuthoredAnimator}; "
                + $"ProceduralFallbackEnabled: {ProceduralFallbackEnabled}; "
                + $"Reason: {(Validation.IsValid ? "Valid authored animation setup" : Validation.PrimaryFailure)}";

            if (Validation.IsValid)
            {
                Debug.Log(message, this);
            }
            else
            {
                Debug.LogWarning(message + ". Authored clips were not generated.", this);
            }
        }

        private string ResolveCharacterName()
        {
            PlayerRoleIdentity role = GetComponentInParent<PlayerRoleIdentity>();
            if (role != null)
            {
                return role.Role.ToString();
            }

            return name;
        }

        private CharacterAnimationRigType ResolveRigType()
        {
            return animator != null
                && animator.avatar != null
                && animator.avatar.isHuman
                ? CharacterAnimationRigType.Humanoid
                : CharacterAnimationRigType.Generic;
        }

        private static string NameOrNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "None" : value;
        }
    }
}
