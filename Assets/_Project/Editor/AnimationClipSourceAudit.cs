using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Non-destructive report for the authored locomotion animation sources.
    ///
    /// The project intentionally does not contain TopDown Engine's paid demo
    /// assets. This audit makes that dependency visible without importing,
    /// reimporting, creating, or saving any asset.
    /// </summary>
    public static class AnimationClipSourceAudit
    {
        public const string ControllerPath =
            "Assets/_Project/Art/Characters/CharacterLocomotion.controller";

        private const string ProjectCharacterRoot =
            "Assets/_Project/Art/Characters";

        private static readonly BindingDefinition[] Definitions =
        {
            new BindingDefinition(
                "Idle",
                "4154cd260b34a4c408b7abaf7ddadc97",
                true,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Idle.fbx",
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@StandingIdle.fbx"),
            new BindingDefinition(
                "Walk",
                "27cca24148160794ea268c6d64671e5b",
                true,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Walking.fbx",
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@Walking.fbx"),
            new BindingDefinition(
                "Run",
                "70c18e0752b220f439894f5c0b0fe381",
                true,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Running.fbx",
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@Running.fbx"),
            new BindingDefinition(
                "Command",
                "dca26005bd5b52e499d280f68bfe24ea",
                false,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@StandingMeleeKick.fbx",
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Jump.fbx"),
            new BindingDefinition(
                "Win",
                "1f6a9fbf200d8684e83ba2a8237e83b9",
                false,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@StandingIdle2.fbx"),
            new BindingDefinition(
                "Lose",
                "9cd03327ea1e2e146adce20d40d05a49",
                false,
                "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@CrouchingIdle.fbx")
        };

        private static readonly string[] ModelPaths =
        {
            ProjectCharacterRoot + "/police.fbx",
            ProjectCharacterRoot + "/thief.fbx",
            ProjectCharacterRoot + "/dog.fbx",
            ProjectCharacterRoot + "/cat.fbx",
            ProjectCharacterRoot + "/raccoon.fbx"
        };

        public sealed class BindingDefinition
        {
            public BindingDefinition(
                string stateName,
                string motionGuid,
                bool requiredForLocomotion,
                params string[] candidatePaths)
            {
                StateName = stateName;
                MotionGuid = motionGuid;
                RequiredForLocomotion = requiredForLocomotion;
                CandidatePaths = candidatePaths;
            }

            public string StateName { get; }
            public string MotionGuid { get; }
            public bool RequiredForLocomotion { get; }
            public string[] CandidatePaths { get; }
        }

        public sealed class BindingReport
        {
            public string StateName;
            public string ExpectedMotionGuid;
            public string SerializedMotionGuid;
            public AnimationClip CurrentMotion;
            public string CurrentAssetPath;
            public string GuidAssetPath;
            public bool MetaExists;
            public string[] CandidatePaths;
            public string[] CandidateClips;
        }

        public sealed class ModelReport
        {
            public string AssetPath;
            public ModelImporterAnimationType RigType;
            public bool ImportAnimation;
            public int ImporterClipCount;
            public int EmbeddedClipCount;
            public string Avatar;
            public bool AvatarValid;
            public bool AvatarHuman;
            public string RootMotionBone;
        }

        [MenuItem("Paws & Loot/Animation/Audit Clip Sources")]
        public static void Audit()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("[AnimationSourceAudit] Begin");
            report.AppendLine(
                "This audit is read-only; no Controller, FBX, meta, scene, "
                + "or prefab is written.");

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);
            Dictionary<string, string> serializedGuids =
                ReadSerializedMotionGuids();
            foreach (BindingReport binding in CollectControllerBindings())
            {
                string serialized = serializedGuids.TryGetValue(
                        binding.StateName,
                        out string guid)
                    ? guid
                    : "<not found>";
                binding.SerializedMotionGuid = serialized;

                report.AppendLine(
                    $"State={binding.StateName}; "
                    + $"Motion GUID={binding.ExpectedMotionGuid}; "
                    + $"YAML GUID={serialized}; "
                    + $"Current Motion={Describe(binding.CurrentMotion)}; "
                    + $"Current Asset={DescribePath(binding.CurrentAssetPath)}; "
                    + $"GUID Asset={DescribePath(binding.GuidAssetPath)}; "
                    + $"Meta={binding.MetaExists}; "
                    + $"Candidates={string.Join(", ", binding.CandidatePaths)}; "
                    + $"Imported Clips={string.Join(", ", binding.CandidateClips)}");
            }

            report.AppendLine(
                controller == null
                    ? "Controller: MISSING"
                    : $"Controller: {controller.name}; "
                      + $"Runtime clips={controller.animationClips.Length}");

            foreach (ModelReport model in CollectModelReports())
            {
                report.AppendLine(
                    $"FBX={model.AssetPath}; Rig={model.RigType}; "
                    + $"importAnimation={model.ImportAnimation}; "
                    + $"clipAnimations={model.ImporterClipCount}; "
                    + $"embeddedClips={model.EmbeddedClipCount}; "
                    + $"Avatar={model.Avatar}; "
                    + $"AvatarValid={model.AvatarValid}; "
                    + $"AvatarHuman={model.AvatarHuman}; "
                      + $"RootMotionBone={model.RootMotionBone}");
            }

            report.AppendLine(
                "Runtime gate with authored police Avatar: "
                + DescribeRuntimeGate(controller));

            report.AppendLine(
                "TopDownEngine source is required for human clips when the "
                + "candidate paths above are absent. No replacement animation "
                + "was generated.");
            report.AppendLine("[AnimationSourceAudit] End");
            Debug.Log(report.ToString());
        }

        public static BindingDefinition[] GetExpectedBindings()
        {
            return Definitions.ToArray();
        }

        public static List<BindingReport> CollectControllerBindings()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);
            Dictionary<string, AnimationClip> motions =
                FindStateMotions(controller);
            List<BindingReport> reports = new List<BindingReport>();

            foreach (BindingDefinition definition in Definitions)
            {
                AnimationClip currentMotion = motions.TryGetValue(
                        definition.StateName,
                        out AnimationClip motion)
                    ? motion
                    : null;
                string currentPath = currentMotion == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(currentMotion);
                string guidPath = AssetDatabase.GUIDToAssetPath(
                    definition.MotionGuid);
                reports.Add(new BindingReport
                {
                    StateName = definition.StateName,
                    ExpectedMotionGuid = definition.MotionGuid,
                    CurrentMotion = currentMotion,
                    CurrentAssetPath = currentPath,
                    GuidAssetPath = guidPath,
                    MetaExists = !string.IsNullOrEmpty(guidPath)
                        && File.Exists(guidPath + ".meta"),
                    CandidatePaths = definition.CandidatePaths,
                    CandidateClips = FindCandidateClips(
                        definition.CandidatePaths)
                });
            }

            return reports;
        }

        public static List<ModelReport> CollectModelReports()
        {
            List<ModelReport> reports = new List<ModelReport>();
            foreach (string path in ModelPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path)
                    as ModelImporter;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);
                Avatar avatar = FindAvatar(model, path);
                int embeddedClips = FindClips(path).Count;
                reports.Add(new ModelReport
                {
                    AssetPath = path,
                    RigType = importer == null
                        ? ModelImporterAnimationType.None
                        : importer.animationType,
                    ImportAnimation = importer != null
                        && importer.importAnimation,
                    ImporterClipCount = importer == null
                        ? 0
                        : importer.clipAnimations.Length,
                    EmbeddedClipCount = embeddedClips,
                    Avatar = avatar == null ? "<none>" : avatar.name,
                    AvatarValid = avatar != null && avatar.isValid,
                    AvatarHuman = avatar != null && avatar.isHuman,
                    RootMotionBone = ReadMotionNode(importer)
                });
            }

            return reports;
        }

        private static Dictionary<string, AnimationClip> FindStateMotions(
            AnimatorController controller)
        {
            Dictionary<string, AnimationClip> result =
                new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            if (controller == null)
            {
                return result;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectStateMotions(layer.stateMachine, result);
            }

            return result;
        }

        private static void CollectStateMotions(
            AnimatorStateMachine machine,
            Dictionary<string, AnimationClip> result)
        {
            if (machine == null)
            {
                return;
            }

            foreach (ChildAnimatorState child in machine.states)
            {
                result[child.state.name] = child.state.motion as AnimationClip;
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                CollectStateMotions(child.stateMachine, result);
            }
        }

        private static Dictionary<string, string> ReadSerializedMotionGuids()
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(ControllerPath))
            {
                return result;
            }

            string text = File.ReadAllText(ControllerPath);
            string[] blocks = text.Split(
                new[] { "--- !u!" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string block in blocks)
            {
                System.Text.RegularExpressions.Match name = Regex.Match(
                    block,
                    @"(?m)^  m_Name: (?<name>[^\r\n]+)$");
                System.Text.RegularExpressions.Match motion = Regex.Match(
                    block,
                    @"m_Motion: \{fileID: \d+, guid: (?<guid>[0-9a-f]+), type: \d+\}");
                if (!name.Success || !motion.Success)
                {
                    continue;
                }

                string stateName = name.Groups["name"].Value.Trim();
                if (Definitions.Any(definition =>
                        definition.StateName == stateName))
                {
                    result[stateName] = motion.Groups["guid"].Value;
                }
            }

            return result;
        }

        private static string[] FindCandidateClips(string[] paths)
        {
            List<string> clips = new List<string>();
            foreach (string path in paths)
            {
                foreach (AnimationClip clip in FindClips(path))
                {
                    clips.Add($"{path}:{clip.name}:{clip.length:0.###}s");
                }
            }

            return clips.ToArray();
        }

        private static List<AnimationClip> FindClips(string path)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            if (AssetImporter.GetAtPath(path) == null)
            {
                return clips;
            }

            foreach (UnityEngine.Object asset in
                AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip
                    && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static Avatar FindAvatar(GameObject model, string path)
        {
            Animator animator = model == null
                ? null
                : model.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null)
            {
                return animator.avatar;
            }

            foreach (UnityEngine.Object asset in
                AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static string ReadMotionNode(ModelImporter importer)
        {
            if (importer == null)
            {
                return "<none>";
            }

            // Unity 6 does not expose the old rootMotionBoneName property on
            // every ModelImporter version. Read the equivalent motion node
            // when the installed editor exposes it, without making the audit
            // depend on a version-specific API.
            PropertyInfo property = typeof(ModelImporter).GetProperty(
                "motionNodeName",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(string))
            {
                return property.GetValue(importer) as string ?? "<empty>";
            }

            return "<not exposed by this Unity importer API>";
        }

        private static string DescribeRuntimeGate(
            AnimatorController controller)
        {
            GameObject police = AssetDatabase.LoadAssetAtPath<GameObject>(
                ProjectCharacterRoot + "/police.fbx");
            Animator sourceAnimator = police == null
                ? null
                : police.GetComponentInChildren<Animator>(true);
            if (controller == null || sourceAnimator == null)
            {
                return "FALLBACK; authored controller or police Animator missing";
            }

            GameObject probe = new GameObject("AnimationSourceAuditProbe");
            try
            {
                Animator animator = probe.AddComponent<Animator>();
                animator.avatar = sourceAnimator.avatar;
                animator.runtimeAnimatorController = controller;
                bool ready = AnimatorClipGuard.HasUsableLocomotion(
                    animator,
                    out string reason);
                return ready
                    ? "READY"
                    : $"FALLBACK; reason={reason}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static string Describe(AnimationClip clip)
        {
            return clip == null
                ? "<unresolved>"
                : $"{clip.name} ({clip.length:0.###}s, loop={clip.isLooping})";
        }

        private static string DescribePath(string path)
        {
            return string.IsNullOrEmpty(path) ? "<none>" : path;
        }
    }
}
