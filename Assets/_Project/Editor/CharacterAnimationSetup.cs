using System.Collections.Generic;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the shared locomotion Animator controller for the authored
    /// characters.
    ///
    /// The authored FBX ship no animation clips, but their skeletons import as
    /// valid Unity Humanoid, so humanoid clips from the external demo package
    /// can be retargeted onto them. That package is optional, so every step
    /// degrades to "no controller" instead of failing the scene rebuild.
    /// </summary>
    public static class CharacterAnimationSetup
    {
        public const string ControllerPath =
            "Assets/_Project/Art/Characters/CharacterLocomotion.controller";

        private const string IdleStateName = "Idle";
        private const string RunStateName = "Run";
        private const float RunThreshold = 0.15f;

        private static readonly string[] HumanoidRigTargets =
        {
            "Assets/_Project/Art/Characters/police.fbx",
            "Assets/_Project/Art/Characters/thief.fbx"
        };

        // Preferred clip sources, in priority order.
        private static readonly string[] IdleClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/"
            + "LoftTie@Idle.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@StandingIdle.fbx"
        };

        private static readonly string[] RunClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/"
            + "LoftTie@Running.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@Running.fbx"
        };

        /// <summary>
        /// The animals must stay Generic. Unity will happily build a Humanoid
        /// Avatar for them, but retargeting a human walk would stand the dog up
        /// on its hind legs, so their legs are driven procedurally instead.
        /// </summary>
        private static readonly string[] GenericRigTargets =
        {
            "Assets/_Project/Art/Characters/dog.fbx",
            "Assets/_Project/Art/Characters/cat.fbx",
            "Assets/_Project/Art/Characters/raccoon.fbx"
        };

        [MenuItem("Paws & Loot/Setup/Rebuild Character Locomotion Animator")]
        public static void Rebuild()
        {
            EnsureHumanoidRigs();
            EnsureGenericAnimalRigs();

            AnimationClip idle = FindHumanoidClip(IdleClipCandidates);
            AnimationClip run = FindHumanoidClip(RunClipCandidates);
            if (run == null)
            {
                Debug.LogWarning(
                    "[Animation] No humanoid run clip available, so no "
                    + "locomotion controller was built. Characters stay "
                    + "static until authored clips arrive (MODEL-002).");
                return;
            }

            var controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    ControllerPath);
            controller.AddParameter(
                PlayerLocomotionAnimator.SpeedParameter,
                AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState(IdleStateName);
            idleState.motion = idle != null ? idle : run;
            idleState.speed = idle != null ? 1f : 0f;
            AnimatorState runState = machine.AddState(RunStateName);
            runState.motion = run;
            machine.defaultState = idleState;

            AnimatorStateTransition toRun =
                idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.1f;
            toRun.AddCondition(
                AnimatorConditionMode.Greater,
                RunThreshold,
                PlayerLocomotionAnimator.SpeedParameter);

            AnimatorStateTransition toIdle =
                runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(
                AnimatorConditionMode.Less,
                RunThreshold,
                PlayerLocomotionAnimator.SpeedParameter);

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Animation] Locomotion controller built. "
                + $"idle={(idle == null ? "none (run frozen)" : idle.name)} "
                + $"run={run.name}");
        }

        /// <summary>
        /// Authored characters import as Generic by default. Humanoid is
        /// required for clip retargeting, so it is forced once and reverted if
        /// Unity cannot build a valid Avatar.
        /// </summary>
        public static void EnsureHumanoidRigs()
        {
            foreach (string path in HumanoidRigTargets)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null
                    || importer.animationType
                        == ModelImporterAnimationType.Human)
                {
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
                if (!HasValidAvatar(path))
                {
                    importer.animationType =
                        ModelImporterAnimationType.Generic;
                    importer.SaveAndReimport();
                    Debug.LogWarning(
                        $"[Animation] {path} produced no valid Humanoid "
                        + "Avatar, reverted to Generic.");
                }
            }
        }

        public static void EnsureGenericAnimalRigs()
        {
            foreach (string path in GenericRigTargets)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null
                    || importer.animationType
                        == ModelImporterAnimationType.Generic)
                {
                    continue;
                }

                importer.animationType =
                    ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
                Debug.Log(
                    $"[Animation] {path} forced back to Generic so its legs "
                    + "can be driven directly.");
            }
        }

        public static AnimatorController LoadController()
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath);
        }

        private static bool HasValidAvatar(string path)
        {
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is Avatar avatar)
                {
                    return avatar.isValid && avatar.isHuman;
                }
            }

            return false;
        }

        private static AnimationClip FindHumanoidClip(
            IEnumerable<string> candidatePaths)
        {
            foreach (string path in candidatePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                if (importer.animationType
                    != ModelImporterAnimationType.Human)
                {
                    importer.animationType =
                        ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                }

                foreach (Object sub in
                    AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is AnimationClip clip
                        && !clip.name.StartsWith("__preview__"))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }
    }
}
