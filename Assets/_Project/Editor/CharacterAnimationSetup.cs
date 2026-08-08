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

        /// <summary>
        /// Where authored humanoid clips are looked for.
        ///
        /// This used to be six hard-coded paths into
        /// <c>Assets/TopDownEngine/Demos/Loft3D/...</c>. That package is a paid
        /// asset whose licence forbids redistribution, so it was never in the
        /// repository and is not installed here — which made every one of those
        /// paths a miss, and the controller that *was* committed (built once on
        /// a machine that had it) reference six clips that resolve to nothing.
        ///
        /// Searched by name in our own folder instead. Nothing is there yet;
        /// <c>MODEL-002</c> is the job that fills it, and until it does the
        /// rebuild below says so and builds no controller at all, which is the
        /// honest state rather than a controller full of holes.
        /// </summary>
        private const string ClipFolder =
            "Assets/_Project/Art/Characters/Animations";

        private static readonly string[] IdleClipNames = { "Idle" };
        private static readonly string[] RunClipNames = { "Run", "Running" };
        private static readonly string[] WalkClipNames = { "Walk", "Walking" };
        private static readonly string[] CommandClipNames = { "Command" };
        private static readonly string[] WinClipNames = { "Win" };
        private static readonly string[] LoseClipNames = { "Lose" };

        private static readonly string[] HumanoidRigTargets =
        {
            "Assets/_Project/Art/Characters/police.fbx",
            "Assets/_Project/Art/Characters/thief.fbx"
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

            AnimationClip idle = FindAuthoredClip(IdleClipNames);
            AnimationClip run = FindAuthoredClip(RunClipNames);
            if (run == null)
            {
                Debug.LogWarning(
                    "[Animation] No humanoid run clip available, so no "
                    + "locomotion controller was built. Characters stay "
                    + "static until authored clips arrive (MODEL-002).");
                return;
            }

            AnimationClip walk = FindAuthoredClip(WalkClipNames);
            AnimationClip command =
                FindAuthoredClip(CommandClipNames);
            AnimationClip win = FindAuthoredClip(WinClipNames);
            AnimationClip lose = FindAuthoredClip(LoseClipNames);

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

        /// <summary>
        /// The first humanoid clip in <see cref="ClipFolder"/> whose name
        /// contains one of these words.
        ///
        /// By name rather than by path, because the authored clips are not here
        /// yet and guessing their filenames would put this back in the business
        /// of hard-coded paths that silently miss.
        /// </summary>
        private static AnimationClip FindAuthoredClip(
            IEnumerable<string> names)
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder))
            {
                return null;
            }

            foreach (string guid in
                AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is not AnimationClip clip
                        || clip.name.StartsWith("__preview__"))
                    {
                        continue;
                    }

                    foreach (string name in names)
                    {
                        if (clip.name.IndexOf(
                                name,
                                System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return clip;
                        }
                    }
                }
            }

            return null;
        }
    }
}
