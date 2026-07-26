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

        private static readonly string[] WalkClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/"
            + "LoftTie@Walking.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@Walking.fbx"
        };

        private static readonly string[] CommandClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@StandingMeleeKick.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/"
            + "LoftTie@Jump.fbx"
        };

        private static readonly string[] WinClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@StandingIdle2.fbx"
        };

        private static readonly string[] LoseClipCandidates =
        {
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/"
            + "LoftSuit@CrouchingIdle.fbx"
        };

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

            AnimationClip walk = FindHumanoidClip(WalkClipCandidates);
            AnimationClip command =
                FindHumanoidClip(CommandClipCandidates);
            AnimationClip win = FindHumanoidClip(WinClipCandidates);
            AnimationClip lose = FindHumanoidClip(LoseClipCandidates);

            var controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    ControllerPath);
            controller.AddParameter(
                CharacterAnimatorParameters.Speed,
                AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState(
                CharacterAnimatorParameters.IdleState);
            idleState.motion = idle != null ? idle : run;
            idleState.speed = idle != null ? 1f : 0f;
            machine.defaultState = idleState;

            AnimatorState runState = machine.AddState(
                CharacterAnimatorParameters.RunState);
            runState.motion = run;

            // Walk only exists as its own state when a walk clip is available,
            // otherwise speed blends straight from idle into run.
            AnimatorState walkState = null;
            if (walk != null)
            {
                walkState = machine.AddState(
                    CharacterAnimatorParameters.WalkState);
                walkState.motion = walk;
                AddSpeedTransition(
                    idleState,
                    walkState,
                    AnimatorConditionMode.Greater,
                    CharacterAnimatorParameters.WalkThreshold);
                AddSpeedTransition(
                    walkState,
                    idleState,
                    AnimatorConditionMode.Less,
                    CharacterAnimatorParameters.WalkThreshold);
                AddSpeedTransition(
                    walkState,
                    runState,
                    AnimatorConditionMode.Greater,
                    CharacterAnimatorParameters.RunThreshold);
                AddSpeedTransition(
                    runState,
                    walkState,
                    AnimatorConditionMode.Less,
                    CharacterAnimatorParameters.RunThreshold);
            }
            else
            {
                AddSpeedTransition(
                    idleState,
                    runState,
                    AnimatorConditionMode.Greater,
                    CharacterAnimatorParameters.WalkThreshold);
                AddSpeedTransition(
                    runState,
                    idleState,
                    AnimatorConditionMode.Less,
                    CharacterAnimatorParameters.WalkThreshold);
            }

            AnimatorState locomotionReturn = walkState ?? idleState;

            if (command != null)
            {
                controller.AddParameter(
                    CharacterAnimatorParameters.Command,
                    AnimatorControllerParameterType.Trigger);
                AnimatorState commandState = machine.AddState(
                    CharacterAnimatorParameters.CommandState);
                commandState.motion = command;
                AnimatorStateTransition enter =
                    machine.AddAnyStateTransition(commandState);
                enter.hasExitTime = false;
                enter.duration = 0.06f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    CharacterAnimatorParameters.Command);
                AnimatorStateTransition exit =
                    commandState.AddTransition(locomotionReturn);
                exit.hasExitTime = true;
                exit.exitTime = 0.85f;
                exit.duration = 0.12f;
            }

            AddResultState(
                controller,
                machine,
                win,
                CharacterAnimatorParameters.WinState,
                CharacterAnimatorParameters.Win);
            AddResultState(
                controller,
                machine,
                lose,
                CharacterAnimatorParameters.LoseState,
                CharacterAnimatorParameters.Lose);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Animation] Locomotion controller built. "
                + $"idle={Describe(idle)} walk={Describe(walk)} "
                + $"run={Describe(run)} command={Describe(command)} "
                + $"win={Describe(win)} lose={Describe(lose)}");
        }

        private static string Describe(AnimationClip clip)
        {
            return clip == null ? "none" : clip.name;
        }

        private static void AddSpeedTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            float threshold)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(
                mode,
                threshold,
                CharacterAnimatorParameters.Speed);
        }

        /// <summary>
        /// Win and Lose are terminal poses: entered from anywhere by a bool and
        /// never left, because the match is over.
        /// </summary>
        private static void AddResultState(
            AnimatorController controller,
            AnimatorStateMachine machine,
            AnimationClip clip,
            string stateName,
            string parameterName)
        {
            if (clip == null)
            {
                return;
            }

            controller.AddParameter(
                parameterName,
                AnimatorControllerParameterType.Bool);
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            AnimatorStateTransition enter =
                machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.2f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(
                AnimatorConditionMode.If,
                0f,
                parameterName);
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
