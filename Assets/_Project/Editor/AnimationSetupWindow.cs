using System;
using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Non-destructive setup surface for future authored clips. Empty profiles
    /// are reported as unconfigured and Apply does not create a controller.
    /// </summary>
    public sealed class AnimationSetupWindow : EditorWindow
    {
        private const string ProfileFolder =
            "Assets/_Project/Animation/Profiles";
        private const string ControllerFolder =
            "Assets/_Project/Animation/Controllers";

        private readonly Dictionary<string, Animator> _targets = new();
        private List<CharacterAnimationProfile> _profiles = new();
        private Vector2 _scroll;
        private bool _showDetails = true;

        [MenuItem("Pawlice and Purrglar/Animation/Open Animation Setup")]
        public static void Open()
        {
            GetWindow<AnimationSetupWindow>("Animation Setup");
        }

        [MenuItem("Pawlice and Purrglar/Animation/Validate Character Animation Profiles")]
        public static void ValidateProfiles()
        {
            List<CharacterAnimationProfile> profiles = LoadProfiles();
            int failures = 0;
            foreach (CharacterAnimationProfile profile in profiles)
            {
                List<string> errors = ValidateProfile(profile);
                if (errors.Count == 0)
                {
                    Debug.Log(
                        $"[AnimationProfile] {profile.name}: valid authored setup.",
                        profile);
                    continue;
                }

                failures++;
                Debug.LogWarning(
                    $"[AnimationProfile] {profile.name}: "
                    + string.Join("; ", errors),
                    profile);
            }

            Debug.Log(
                $"[AnimationProfile] Validation complete. Profiles={profiles.Count}; "
                + $"Invalid={failures}; No production controller was created.");
        }

        [MenuItem("Pawlice and Purrglar/Animation/Apply Valid Animation Profiles")]
        public static void ApplyValidProfiles()
        {
            ApplyProfiles(null);
        }

        private void OnEnable()
        {
            RefreshProfiles();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Drag authored clips into the Profile assets first. "
                + "The current checkout intentionally has no playable authored clips, "
                + "so Apply will make no production changes.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                {
                    RefreshProfiles();
                }

                if (GUILayout.Button("Validate", GUILayout.Width(90f)))
                {
                    ValidateProfiles();
                }

                if (GUILayout.Button("Apply Valid", GUILayout.Width(110f)))
                {
                    ApplyProfiles(_targets);
                }

                _showDetails = EditorGUILayout.ToggleLeft(
                    "Details",
                    _showDetails,
                    GUILayout.Width(80f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (CharacterAnimationProfile profile in _profiles)
            {
                DrawProfile(profile);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawProfile(CharacterAnimationProfile profile)
        {
            List<string> errors = ValidateProfile(profile);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.Update();
                EditorGUILayout.LabelField(
                    profile.name,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Character",
                    profile.Character.ToString());
                EditorGUILayout.LabelField(
                    "Rig",
                    profile.RigType.ToString());
                EditorGUILayout.ObjectField(
                    "Controller",
                    profile.AnimatorController,
                    typeof(RuntimeAnimatorController),
                    false);
                EditorGUILayout.LabelField(
                    "Runtime clip count",
                    profile.AnimatorController == null
                        ? "0"
                        : profile.AnimatorController.animationClips.Length.ToString());
                EditorGUILayout.LabelField(
                    "Animation mode",
                    errors.Count == 0 ? "Authored Animator" : "Procedural fallback");
                EditorGUILayout.LabelField(
                    "Fallback",
                    profile.AllowProceduralFallback ? "Allowed" : "Not allowed");

                if (_showDetails)
                {
                    DrawClipField(
                        "Idle",
                        serialized.FindProperty("idleClip"));
                    DrawClipField(
                        "Walk",
                        serialized.FindProperty("walkClip"));
                    DrawClipField(
                        "Run",
                        serialized.FindProperty("runClip"));
                    DrawClipField(
                        "Command",
                        serialized.FindProperty("commandClip"));
                    DrawClipField(
                        "Win",
                        serialized.FindProperty("winClip"));
                    DrawClipField(
                        "Lose",
                        serialized.FindProperty("loseClip"));
                    serialized.ApplyModifiedProperties();
                    EditorGUILayout.LabelField(
                        "Speed",
                        profile.SpeedParameter);
                    EditorGUILayout.LabelField(
                        "Thresholds",
                        $"walk={profile.WalkThreshold:0.##}, run={profile.RunThreshold:0.##}");
                    EditorGUILayout.LabelField(
                        "Transition",
                        $"{profile.TransitionDuration:0.##}s; root motion={profile.ApplyRootMotion}");
                    Animator target = _targets.TryGetValue(
                            AssetDatabase.GetAssetPath(profile),
                            out Animator selected)
                        ? selected
                        : null;
                    Animator assigned = (Animator)EditorGUILayout.ObjectField(
                        "Apply target Animator",
                        target,
                        typeof(Animator),
                        true);
                    if (assigned != target)
                    {
                        _targets[AssetDatabase.GetAssetPath(profile)] = assigned;
                    }
                }

                if (errors.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        string.Join("\n", errors),
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Valid authored setup. Apply will create/update only the "
                        + "new character-specific controller and selected target Animator.",
                        MessageType.Info);
                }
            }
        }

        private static void DrawClipField(
            string label,
            SerializedProperty property)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        public static List<CharacterAnimationProfile> LoadProfiles()
        {
            if (!AssetDatabase.IsValidFolder(ProfileFolder))
            {
                return new List<CharacterAnimationProfile>();
            }

            return AssetDatabase.FindAssets(
                    "t:CharacterAnimationProfile",
                    new[] { ProfileFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.Character)
                .ToList();
        }

        public static List<string> ValidateProfile(
            CharacterAnimationProfile profile)
        {
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("Profile is missing.");
                return errors;
            }

            if (!profile.HasRequiredLocomotionClips(out string clipReason))
            {
                errors.Add(clipReason);
            }

            foreach (AnimationClip clip in new[]
            {
                profile.IdleClip,
                profile.WalkClip,
                profile.RunClip
            })
            {
                if (clip == null)
                {
                    continue;
                }

                if (clip.length <= Mathf.Epsilon)
                {
                    errors.Add($"Clip '{clip.name}' has zero length.");
                }

                if (!clip.isLooping)
                {
                    errors.Add($"Locomotion clip '{clip.name}' is not looping.");
                }

                if (profile.RigType == CharacterAnimationRigType.Humanoid
                    && !clip.humanMotion)
                {
                    errors.Add(
                        $"Humanoid profile cannot use non-Humanoid clip '{clip.name}'.");
                }

                if (profile.RigType == CharacterAnimationRigType.Generic
                    && clip.humanMotion)
                {
                    errors.Add(
                        $"Generic profile cannot use Humanoid clip '{clip.name}'.");
                }

                if (profile.RigType == CharacterAnimationRigType.Generic
                    && !clip.humanMotion)
                {
                    ValidateGenericBindingPaths(profile, clip, errors);
                }
            }

            if (profile.ApplyRootMotion)
            {
                errors.Add("Apply Root Motion must remain false for production movement.");
            }

            if (profile.AnimatorController != null)
            {
                if (CountPlayableClips(profile.AnimatorController) < 3)
                {
                    errors.Add("Assigned controller exposes fewer than 3 playable clips.");
                }

                if (profile.AnimatorController is AnimatorController controller)
                {
                    ValidateStateMotion(
                        controller,
                        CharacterAnimatorParameters.IdleState,
                        profile.IdleClip,
                        errors);
                    ValidateStateMotion(
                        controller,
                        CharacterAnimatorParameters.WalkState,
                        profile.WalkClip,
                        errors);
                    ValidateStateMotion(
                        controller,
                        CharacterAnimatorParameters.RunState,
                        profile.RunClip,
                        errors);
                }
            }

            return errors.Distinct().ToList();
        }

        private static void ApplyProfiles(
            Dictionary<string, Animator> targetAnimators)
        {
            List<CharacterAnimationProfile> profiles = LoadProfiles();
            if (profiles.Count == 0)
            {
                Debug.LogWarning("[AnimationProfile] No Profile assets found.");
                return;
            }

            Dictionary<CharacterAnimationProfile, List<string>> failures =
                profiles.ToDictionary(profile => profile, ValidateProfile);
            List<CharacterAnimationProfile> validProfiles = failures
                .Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .ToList();
            if (validProfiles.Count == 0)
            {
                Debug.LogWarning("[AnimationProfile] Apply cancelled. No valid Profile exists.");
                return;
            }

            foreach (KeyValuePair<CharacterAnimationProfile, List<string>> failure
                     in failures.Where(pair => pair.Value.Count > 0))
            {
                Debug.LogWarning(
                    $"[AnimationProfile] Skipping {failure.Key.name}: "
                    + string.Join("; ", failure.Value),
                    failure.Key);
            }

            EnsureControllerFolder();
            CharacterAnimationProfile humanReference = validProfiles.FirstOrDefault(
                profile => profile.RigType == CharacterAnimationRigType.Humanoid);
            bool shareHuman = humanReference != null
                && validProfiles.Where(profile =>
                        profile.RigType == CharacterAnimationRigType.Humanoid)
                    .All(profile => SameLocomotionClips(profile, humanReference));

            foreach (CharacterAnimationProfile profile in validProfiles)
            {
                string fileName = profile.RigType == CharacterAnimationRigType.Humanoid
                    ? shareHuman
                        ? "HumanLocomotion.controller"
                        : profile.Character + "Locomotion.controller"
                    : profile.Character + "Locomotion.controller";
                string path = ControllerFolder + "/" + fileName;
                AnimatorController controller = BuildController(path, profile);
                AssignController(profile, controller);

                if (targetAnimators == null
                    || !targetAnimators.TryGetValue(
                        AssetDatabase.GetAssetPath(profile),
                        out Animator target)
                    || target == null)
                {
                    continue;
                }

                Undo.RecordObject(target, "Apply Character Animation Profile");
                target.runtimeAnimatorController = controller;
                target.applyRootMotion = false;
                EditorUtility.SetDirty(target);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AnimationProfile] Applied valid profiles. "
                + "Legacy CharacterLocomotion.controller was not modified.");
        }

        private static AnimatorController BuildController(
            string path,
            CharacterAnimationProfile profile)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            AddParameter(controller, CharacterAnimatorParameters.Speed,
                AnimatorControllerParameterType.Float);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = GetOrAddState(
                machine,
                CharacterAnimatorParameters.IdleState,
                profile.IdleClip);
            AnimatorState walk = GetOrAddState(
                machine,
                CharacterAnimatorParameters.WalkState,
                profile.WalkClip);
            AnimatorState run = GetOrAddState(
                machine,
                CharacterAnimatorParameters.RunState,
                profile.RunClip);
            machine.defaultState = idle;
            AddSpeedTransition(idle, walk, true, profile.WalkThreshold,
                profile.TransitionDuration);
            AddSpeedTransition(walk, idle, false, profile.WalkThreshold,
                profile.TransitionDuration);
            AddSpeedTransition(walk, run, true, profile.RunThreshold,
                profile.TransitionDuration);
            AddSpeedTransition(run, walk, false, profile.RunThreshold,
                profile.TransitionDuration);

            AddOptionalState(controller, machine, profile.CommandClip,
                CharacterAnimatorParameters.CommandState,
                CharacterAnimatorParameters.Command,
                AnimatorControllerParameterType.Trigger,
                AnimatorConditionMode.If,
                idle,
                true);
            AddOptionalState(controller, machine, profile.WinClip,
                CharacterAnimatorParameters.WinState,
                CharacterAnimatorParameters.Win,
                AnimatorControllerParameterType.Bool,
                AnimatorConditionMode.If,
                idle,
                false);
            AddOptionalState(controller, machine, profile.LoseClip,
                CharacterAnimatorParameters.LoseState,
                CharacterAnimatorParameters.Lose,
                AnimatorControllerParameterType.Bool,
                AnimatorConditionMode.If,
                idle,
                false);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddOptionalState(
            AnimatorController controller,
            AnimatorStateMachine machine,
            AnimationClip clip,
            string stateName,
            string parameterName,
            AnimatorControllerParameterType parameterType,
            AnimatorConditionMode conditionMode,
            AnimatorState returnState,
            bool trigger)
        {
            if (clip == null)
            {
                return;
            }

            AddParameter(controller, parameterName, parameterType);
            AnimatorState state = GetOrAddState(machine, stateName, clip);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.12f;
            enter.AddCondition(conditionMode, 0f, parameterName);
            AnimatorStateTransition exit = state.AddTransition(returnState);
            exit.hasExitTime = true;
            exit.exitTime = trigger ? 0.85f : 0.95f;
            exit.duration = 0.12f;
        }

        private static AnimatorState GetOrAddState(
            AnimatorStateMachine machine,
            string name,
            AnimationClip clip)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name)
                {
                    child.state.motion = clip;
                    return child.state;
                }
            }

            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            return state;
        }

        private static void AddSpeedTransition(
            AnimatorState from,
            AnimatorState to,
            bool greater,
            float threshold,
            float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(
                greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                threshold,
                CharacterAnimatorParameters.Speed);
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(parameter => parameter.name == name))
            {
                return;
            }

            controller.AddParameter(name, type);
        }

        private static void AssignController(
            CharacterAnimationProfile profile,
            RuntimeAnimatorController controller)
        {
            Undo.RecordObject(profile, "Assign Character Animation Controller");
            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("animatorController").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static bool SameLocomotionClips(
            CharacterAnimationProfile left,
            CharacterAnimationProfile right)
        {
            return left.IdleClip == right.IdleClip
                && left.WalkClip == right.WalkClip
                && left.RunClip == right.RunClip;
        }

        private static void EnsureControllerFolder()
        {
            if (AssetDatabase.IsValidFolder(ControllerFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets/_Project/Animation", "Controllers");
        }

        private static int CountPlayableClips(RuntimeAnimatorController controller)
        {
            if (controller == null || controller.animationClips == null)
            {
                return 0;
            }

            return controller.animationClips.Count(clip =>
                clip != null && clip.length > Mathf.Epsilon);
        }

        private static void ValidateStateMotion(
            AnimatorController controller,
            string stateName,
            AnimationClip expected,
            List<string> errors)
        {
            AnimatorState state = FindState(controller, stateName);
            if (state == null)
            {
                errors.Add($"Controller is missing state '{stateName}'.");
                return;
            }

            if (state.motion == null)
            {
                errors.Add($"State '{stateName}' has no Motion.");
                return;
            }

            if (expected != null && state.motion != expected)
            {
                errors.Add(
                    $"State '{stateName}' does not use Profile clip '{expected.name}'.");
            }

            if (state.motion is AnimationClip clip
                && clip.length <= Mathf.Epsilon)
            {
                errors.Add($"State '{stateName}' uses a zero-length Motion.");
            }
        }

        private static void ValidateGenericBindingPaths(
            CharacterAnimationProfile profile,
            AnimationClip clip,
            List<string> errors)
        {
            string modelPath =
                $"Assets/_Project/Art/Characters/{profile.Character.ToString().ToLowerInvariant()}.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                errors.Add($"Generic binding target is missing: {modelPath}.");
                return;
            }

            var validPaths = new HashSet<string>();
            foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
            {
                validPaths.Add(
                    AnimationUtility.CalculateTransformPath(
                        transform,
                        model.transform));
            }

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (!validPaths.Contains(binding.path))
                {
                    errors.Add(
                        $"Generic clip '{clip.name}' binds missing path '{binding.path}'.");
                    break;
                }
            }
        }

        private static AnimatorState FindState(
            AnimatorController controller,
            string stateName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorState found = FindState(
                    layer.stateMachine,
                    stateName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static AnimatorState FindState(
            AnimatorStateMachine machine,
            string stateName)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state;
                }
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = FindState(child.stateMachine, stateName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void RefreshProfiles()
        {
            _profiles = LoadProfiles();
            Repaint();
        }
    }
}
