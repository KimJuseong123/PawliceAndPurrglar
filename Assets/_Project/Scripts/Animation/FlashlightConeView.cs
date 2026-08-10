using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// Draws the torch's reach on the ground: a filled wedge plus the circle
    /// the officer sees inside regardless of facing.
    ///
    /// The lit floor alone does not tell either player where the edge is. A spot
    /// light fades out, so "am I in it?" is a guess, and guessing is fatal for
    /// the thief and infuriating for the police. This states the boundary the
    /// rule actually uses.
    ///
    /// Shown to <b>both</b> players on purpose. The thief needs it most — the
    /// whole plan of slipping around the beam only exists if the beam has a
    /// visible edge — and hiding it from them would leave the night as pure
    /// luck. The police sees their own reach, which is what makes sweeping a
    /// street a decision rather than a wiggle.
    ///
    /// Built at runtime rather than in the scene, deliberately. Anything the
    /// scene builder leaves behind gets baked <c>BatchingStatic</c> by
    /// <c>SceneOptimizationPass</c>, and a baked renderer does not move no
    /// matter what its transform says — the same trap that kept the bin lid
    /// shut. Creating the meshes here means there is nothing to bake.
    ///
    /// Presentation only. Geometry taken straight from
    /// <see cref="FlashlightCone"/>, so it cannot drift away from the rule it
    /// is illustrating.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightConeView : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity owner;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Material wedgeMaterial;

        [SerializeField]
        private Material nearMaterial;

        /// <summary>
        /// Just off the road. High enough to beat z-fighting, low enough that it
        /// still reads as painted on the ground from the fixed camera.
        /// </summary>
        [SerializeField]
        private float groundOffset = 0.06f;

        private IMatchStateReader _matchState;
        private Transform _wedge;
        private Transform _near;
        private bool _built;

        public bool IsVisible { get; private set; }

        public void Configure(
            PlayerRoleIdentity configuredOwner,
            IMatchStateReader configuredMatchState,
            Material configuredWedgeMaterial,
            Material configuredNearMaterial)
        {
            owner = configuredOwner;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            wedgeMaterial = configuredWedgeMaterial;
            nearMaterial = configuredNearMaterial;
        }

        /// <summary>
        /// A wedge spanning the rule's half-angle either side of forward, out to
        /// the rule's range. Flat in local XZ so it lies on the road when the
        /// holder is upright.
        /// </summary>
        private static Mesh BuildWedge(int segments)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;

            float span = FlashlightCone.HalfAngleDegrees * 2f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = -FlashlightCone.HalfAngleDegrees
                    + span * i / segments;
                float radians = angle * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Sin(radians) * FlashlightCone.RangeMeters,
                    0f,
                    Mathf.Cos(radians) * FlashlightCone.RangeMeters);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = "Flashlight Wedge" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildDisc(float radius, int segments)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float radians = Mathf.PI * 2f * i / segments;
                vertices[i + 1] = new Vector3(
                    Mathf.Sin(radians) * radius,
                    0f,
                    Mathf.Cos(radians) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = "Flashlight Near Disc" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Transform CreatePatch(
            string patchName,
            Mesh mesh,
            Material material)
        {
            var patch = new GameObject(
                patchName,
                typeof(MeshFilter),
                typeof(MeshRenderer));
            patch.transform.SetParent(transform, false);
            patch.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = patch.GetComponent<MeshRenderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            // A painted marking neither casts nor receives shadows; either one
            // would make it flicker as the officer runs past buildings.
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return patch.transform;
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            _wedge = CreatePatch(
                "Cone Wedge",
                BuildWedge(28),
                wedgeMaterial);
            _near = CreatePatch(
                "Near Circle",
                BuildDisc(FlashlightCone.AlwaysSeenRadius, 24),
                nearMaterial);
        }

        private void SetVisible(bool visible)
        {
            if (IsVisible == visible)
            {
                return;
            }

            IsVisible = visible;
            if (_wedge != null)
            {
                _wedge.gameObject.SetActive(visible);
            }

            if (_near != null)
            {
                _near.gameObject.SetActive(visible);
            }
        }

        private void LateUpdate()
        {
            Build();
            if (owner == null
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            // Flat on the ground under the officer, turned to their facing.
            // Written in world space because the holder pitches and rolls as
            // they walk and the marking must not tip with them.
            Vector3 spot = owner.transform.position;
            spot.y = groundOffset;
            transform.SetPositionAndRotation(
                spot,
                Quaternion.Euler(
                    0f,
                    owner.transform.eulerAngles.y,
                    0f));
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
            SetVisible(false);
        }
    }
}
