using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    public enum CharacterAnimationCharacter
    {
        Police = 0,
        Thief = 1,
        Dog = 2,
        Cat = 3,
        Raccoon = 4
    }

    public enum CharacterAnimationRigType
    {
        Humanoid = 0,
        Generic = 1
    }

    public enum CharacterAnimationMode
    {
        ProceduralFallback = 0,
        AuthoredAnimator = 1
    }

    /// <summary>
    /// Authoring data for one character family. A profile with no controller or
    /// clips is a valid "not supplied yet" profile; it deliberately selects the
    /// procedural backend and never invents an AnimationClip.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterAnimationProfile",
        menuName = "Paws & Loot/Animation/Character Animation Profile")]
    public sealed class CharacterAnimationProfile : ScriptableObject
    {
        [SerializeField]
        private CharacterAnimationCharacter character;

        [SerializeField]
        private CharacterAnimationRigType rigType;

        [SerializeField]
        private RuntimeAnimatorController animatorController;

        [SerializeField]
        private AnimationClip idleClip;

        [SerializeField]
        private AnimationClip walkClip;

        [SerializeField]
        private AnimationClip runClip;

        [SerializeField]
        private AnimationClip commandClip;

        [SerializeField]
        private AnimationClip winClip;

        [SerializeField]
        private AnimationClip loseClip;

        [SerializeField]
        private string speedParameter = CharacterAnimatorParameters.Speed;

        [SerializeField, Range(0f, 1f)]
        private float walkThreshold = CharacterAnimatorParameters.WalkThreshold;

        [SerializeField, Range(0f, 1f)]
        private float runThreshold = CharacterAnimatorParameters.RunThreshold;

        [SerializeField, Min(0f)]
        private float transitionDuration = 0.12f;

        [SerializeField]
        private bool applyRootMotion;

        [SerializeField]
        private bool allowProceduralFallback = true;

        [SerializeField]
        private string genericSkeletonSignature;

        public CharacterAnimationCharacter Character => character;
        public CharacterAnimationRigType RigType => rigType;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public AnimationClip IdleClip => idleClip;
        public AnimationClip WalkClip => walkClip;
        public AnimationClip RunClip => runClip;
        public AnimationClip CommandClip => commandClip;
        public AnimationClip WinClip => winClip;
        public AnimationClip LoseClip => loseClip;
        public string SpeedParameter => string.IsNullOrEmpty(speedParameter)
            ? CharacterAnimatorParameters.Speed
            : speedParameter;
        public float WalkThreshold => walkThreshold;
        public float RunThreshold => runThreshold;
        public float TransitionDuration => transitionDuration;
        public bool ApplyRootMotion => applyRootMotion;
        public bool AllowProceduralFallback => allowProceduralFallback;
        public string GenericSkeletonSignature => genericSkeletonSignature;

        public int AssignedClipCount
        {
            get
            {
                int count = 0;
                foreach (AnimationClip clip in EnumerateClips())
                {
                    if (clip != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public IEnumerable<AnimationClip> EnumerateClips()
        {
            yield return idleClip;
            yield return walkClip;
            yield return runClip;
            yield return commandClip;
            yield return winClip;
            yield return loseClip;
        }

        public AnimationClip GetLocomotionClip(string stateName)
        {
            switch (stateName)
            {
                case CharacterAnimatorParameters.IdleState:
                    return idleClip;
                case CharacterAnimatorParameters.WalkState:
                    return walkClip;
                case CharacterAnimatorParameters.RunState:
                    return runClip;
                case CharacterAnimatorParameters.CommandState:
                    return commandClip;
                case CharacterAnimatorParameters.WinState:
                    return winClip;
                case CharacterAnimatorParameters.LoseState:
                    return loseClip;
                default:
                    return null;
            }
        }

        public bool HasRequiredLocomotionClips(out string reason)
        {
            if (idleClip == null || walkClip == null || runClip == null)
            {
                reason = "Idle, Walk and Run authored clips are required.";
                return false;
            }

            foreach (AnimationClip clip in new[] { idleClip, walkClip, runClip })
            {
                if (clip.length <= Mathf.Epsilon)
                {
                    reason = $"Clip '{clip.name}' has no playable length.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class AnimationValidationResult
    {
        public bool IsValid;
        public string CharacterName;
        public CharacterAnimationRigType RigType;
        public string ControllerName;
        public int RuntimeClipCount;
        public bool HasIdleState;
        public bool HasWalkState;
        public bool HasRunState;
        public bool HasSpeedParameter;
        public bool HasValidAvatar;
        public string IdleClipName;
        public string WalkClipName;
        public string RunClipName;
        public readonly List<string> FailureReasons = new();

        public string PrimaryFailure => FailureReasons.Count == 0
            ? string.Empty
            : FailureReasons[0];

        public void AddFailure(string reason)
        {
            if (!string.IsNullOrEmpty(reason) && !FailureReasons.Contains(reason))
            {
                FailureReasons.Add(reason);
            }

            IsValid = false;
        }
    }
}
