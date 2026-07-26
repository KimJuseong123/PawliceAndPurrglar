using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    public sealed class CharacterTechnicalPreview : MonoBehaviour
    {
        [Serializable]
        public struct ClipInfo
        {
            public string Name;
            public float Duration;
        }

        [SerializeField]
        private string characterId;

        [SerializeField]
        private string sourceAssetPath;

        [SerializeField]
        private string importedAssetPath;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private bool movementDriven;

        [SerializeField]
        private string defaultAnimationName;

        [SerializeField]
        private ClipInfo[] clips = Array.Empty<ClipInfo>();

        [SerializeField, Min(90f)]
        private float turnSpeedDegrees = 540f;

        private Vector3 _lastPosition;
        private bool _hasIsMovingParameter;

        public string CharacterId => characterId;
        public string SourceAssetPath => sourceAssetPath;
        public string ImportedAssetPath => importedAssetPath;
        public Transform VisualRoot => visualRoot;
        public Animator Animator => animator;
        public bool MovementDriven => movementDriven;
        public string DefaultAnimationName => defaultAnimationName;
        public IReadOnlyList<ClipInfo> Clips => clips;

        public void Initialize(
            string assignedCharacterId,
            string assignedSourceAssetPath,
            string assignedImportedAssetPath,
            Transform assignedVisualRoot,
            Animator assignedAnimator,
            bool assignedMovementDriven,
            string assignedDefaultAnimationName,
            IReadOnlyList<AnimationClip> assignedClips)
        {
            characterId = assignedCharacterId;
            sourceAssetPath = assignedSourceAssetPath;
            importedAssetPath = assignedImportedAssetPath;
            visualRoot = assignedVisualRoot;
            animator = assignedAnimator;
            movementDriven = assignedMovementDriven;
            defaultAnimationName = assignedDefaultAnimationName ?? string.Empty;
            clips = assignedClips == null
                ? Array.Empty<ClipInfo>()
                : assignedClips.Select(
                        clip => new ClipInfo
                        {
                            Name = clip.name,
                            Duration = clip.length
                        })
                    .ToArray();
            CacheAnimatorParameters();
        }

        public CharacterRuntimeInfo CaptureRuntimeInfo()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Bounds? worldBounds = null;
            foreach (Renderer renderer in renderers)
            {
                worldBounds = worldBounds.HasValue
                    ? Encapsulate(worldBounds.Value, renderer.bounds)
                    : renderer.bounds;
            }

            var result = new CharacterRuntimeInfo
            {
                characterId = characterId,
                sourceAssetPath = sourceAssetPath,
                importedAssetPath = importedAssetPath,
                hasAnimator = animator != null,
                movementDriven = movementDriven,
                defaultAnimationName = defaultAnimationName,
                clipNames = clips.Select(clip => clip.Name).ToArray(),
                clipDurations = clips.Select(clip => clip.Duration).ToArray(),
                clipCount = clips.Length,
                hasSkinnedMesh = skinnedRenderers.Length > 0,
                skinnedMeshCount = skinnedRenderers.Length,
                rendererCount = renderers.Length,
                boneCount = CountUniqueBones(skinnedRenderers),
                hasIdle = ContainsClipAlias("idle"),
                hasWalk = ContainsClipAlias("walk"),
                hasRun = ContainsClipAlias("run"),
                hasIsMovingParameter = _hasIsMovingParameter,
                worldPosition = transform.position,
                visualForward = visualRoot == null ? Vector3.forward : visualRoot.forward
            };

            if (worldBounds.HasValue)
            {
                Bounds bounds = worldBounds.Value;
                result.boundsCenter = bounds.center;
                result.boundsSize = bounds.size;
                result.groundOffset = bounds.min.y - transform.position.y;
            }

            return result;
        }

        private void Awake()
        {
            _lastPosition = transform.position;
            CacheAnimatorParameters();
        }

        private void Update()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            if (delta.sqrMagnitude <= 0.000001f)
            {
                if (_hasIsMovingParameter && animator != null)
                {
                    animator.SetBool("IsMoving", false);
                }

                return;
            }

            if (_hasIsMovingParameter && animator != null)
            {
                animator.SetBool("IsMoving", true);
            }

            if (visualRoot == null)
            {
                return;
            }

            Vector3 planar = new(delta.x, 0f, delta.z);
            if (planar.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(
                visualRoot.rotation,
                targetRotation,
                turnSpeedDegrees * Time.deltaTime);
        }

        private bool ContainsClipAlias(string alias)
        {
            return clips.Any(
                clip => clip.Name.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void CacheAnimatorParameters()
        {
            _hasIsMovingParameter = animator != null
                && animator.parameters.Any(
                    parameter => parameter.type == AnimatorControllerParameterType.Bool
                        && parameter.name == "IsMoving");
        }

        private static Bounds Encapsulate(Bounds left, Bounds right)
        {
            left.Encapsulate(right);
            return left;
        }

        private static int CountUniqueBones(IEnumerable<SkinnedMeshRenderer> skinnedRenderers)
        {
            var seen = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
            {
                foreach (Transform bone in renderer.bones)
                {
                    if (bone != null)
                    {
                        seen.Add(bone);
                    }
                }
            }

            return seen.Count;
        }

        [Serializable]
        public struct CharacterRuntimeInfo
        {
            public string characterId;
            public string sourceAssetPath;
            public string importedAssetPath;
            public bool hasAnimator;
            public bool movementDriven;
            public string defaultAnimationName;
            public string[] clipNames;
            public float[] clipDurations;
            public int clipCount;
            public bool hasSkinnedMesh;
            public int skinnedMeshCount;
            public int rendererCount;
            public int boneCount;
            public bool hasIdle;
            public bool hasWalk;
            public bool hasRun;
            public bool hasIsMovingParameter;
            public Vector3 worldPosition;
            public Vector3 visualForward;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            public float groundOffset;
        }
    }
}
