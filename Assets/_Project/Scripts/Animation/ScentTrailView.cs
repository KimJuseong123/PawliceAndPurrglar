using System.Collections.Generic;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Draws the thief's scent trail as footprints on the ground.
    ///
    /// The trail has been recorded and followed by the dog since DOG-003 while
    /// being completely invisible: the tracking worked and the police could not
    /// see any of it, so the command felt like it did nothing. This is that data,
    /// drawn.
    ///
    /// Only while the dog is actually tracking, and only on the police's screen.
    /// A permanently visible trail would replace the chase with a map reading
    /// exercise, and showing it to the thief would tell them exactly what they
    /// are leaking.
    ///
    /// Presentation only. Nothing here moves the dog or the thief, and the marks
    /// have no colliders — removing this component changes nothing but what is
    /// on screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScentTrailView : MonoBehaviour
    {
        [SerializeField]
        private ThiefScentTrail trail;

        [SerializeField]
        private CompanionAgent dog;

        [SerializeField]
        private PlayerRoleIdentity viewer;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Material markMaterial;

        /// <summary>
        /// Marks stay up briefly after the dog stops tracking, so the police can
        /// glance at the trail instead of having to read it mid-sprint.
        /// </summary>
        [SerializeField, Min(0f)]
        private float lingerSeconds = 2.5f;

        [SerializeField, Min(1)]
        private int maximumMarks = 24;

        private readonly List<Transform> _marks = new();
        private IMatchStateReader _matchState;
        private float _visibleUntil;

        public int VisibleMarkCount { get; private set; }

        public void Configure(
            ThiefScentTrail configuredTrail,
            CompanionAgent configuredDog,
            PlayerRoleIdentity configuredViewer,
            IMatchStateReader configuredMatchState,
            Material configuredMarkMaterial)
        {
            trail = configuredTrail;
            dog = configuredDog;
            viewer = configuredViewer;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            markMaterial = configuredMarkMaterial;
        }

        /// <summary>
        /// Finds the trail and the dog at runtime.
        ///
        /// Both are created by the scene builder in a different function, and the
        /// view is only meaningful on one machine anyway, so looking them up here
        /// keeps the builder from threading references across the whole file.
        /// </summary>
        private void ResolveSources()
        {
            if (trail == null)
            {
                trail = FindFirstObjectByType<ThiefScentTrail>();
            }

            if (dog != null)
            {
                return;
            }

            foreach (CompanionAgent candidate in
                FindObjectsByType<CompanionAgent>(
                    FindObjectsSortMode.None))
            {
                if (candidate.CompanionKind == CompanionKind.Dog)
                {
                    dog = candidate;
                    return;
                }
            }
        }

        /// <summary>
        /// True while the dog is carrying out a track order, or shortly after.
        /// </summary>
        private bool ShouldShow()
        {
            if (trail == null || dog == null)
            {
                return false;
            }

            if (dog.IsBusyWithCommand
                && dog.ActiveCommand == CompanionCommandId.Track)
            {
                _visibleUntil = Time.time + lingerSeconds;
                return true;
            }

            return Time.time < _visibleUntil;
        }

        /// <summary>
        /// The police's own screen only. Visibility is per-screen, so it is
        /// gated on the local role the same way the torch cone is.
        /// </summary>
        private bool ViewerIsLocal()
        {
            if (viewer == null)
            {
                return false;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == viewer.Role;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.ActiveRole == viewer.Role;
        }

        private Transform EnsureMark(int index)
        {
            while (_marks.Count <= index)
            {
                var mark = GameObject.CreatePrimitive(
                    PrimitiveType.Quad);
                mark.name = $"Scent Mark {_marks.Count}";
                // No collider: a footprint must never block anybody or be
                // picked up by the interaction scanner.
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.transform.SetParent(transform, false);
                // Flat on the ground, lifted a hair to avoid z-fighting the road.
                mark.transform.rotation =
                    Quaternion.Euler(90f, 0f, 0f);
                mark.transform.localScale =
                    new Vector3(0.42f, 0.42f, 1f);
                if (markMaterial != null)
                {
                    mark.GetComponent<Renderer>().sharedMaterial =
                        markMaterial;
                }

                mark.GetComponent<Renderer>().shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                mark.SetActive(false);
                _marks.Add(mark.transform);
            }

            return _marks[index];
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _marks.Count; i++)
            {
                if (_marks[i] != null
                    && _marks[i].gameObject.activeSelf)
                {
                    _marks[i].gameObject.SetActive(false);
                }
            }
        }

        private void LateUpdate()
        {
            ResolveSources();
            if (!ViewerIsLocal()
                || ResolveMatchState()?.IsGameplayActive != true
                || !ShouldShow())
            {
                HideFrom(0);
                VisibleMarkCount = 0;
                return;
            }

            IReadOnlyList<ThiefScentTrail.ScentPoint> points =
                trail.Points;

            // Newest last, and the newest end is the useful one, so the tail of
            // the list is what gets drawn when there are more points than marks.
            int take = Mathf.Min(points.Count, maximumMarks);
            int first = points.Count - take;
            for (int i = 0; i < take; i++)
            {
                ThiefScentTrail.ScentPoint point = points[first + i];
                Transform mark = EnsureMark(i);
                Vector3 spot = point.Position;
                spot.y = 0.03f;
                mark.position = spot;
                if (!mark.gameObject.activeSelf)
                {
                    mark.gameObject.SetActive(true);
                }
            }

            HideFrom(take);
            VisibleMarkCount = take;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }

        private void OnDisable()
        {
            HideFrom(0);
            VisibleMarkCount = 0;
        }
    }
}
