using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Stars circling over a stunned player's head.
    ///
    /// Without this a stun is indistinguishable from lag or a stuck key: the
    /// character simply stops, and the player's first assumption is that the
    /// game broke rather than that they were hit. The cartoon language is the
    /// point — it says "this is temporary and it was done to you" faster than
    /// any HUD line.
    ///
    /// Reads <see cref="StunState"/> and nothing else, which is why it needs no
    /// network code: the stun is already replicated to both machines, so both
    /// see the stars over the same character at the same time.
    ///
    /// Shown on every screen on purpose. Landing a hit is most of the reward for
    /// throwing, and a thrower who cannot see the result learns nothing.
    ///
    /// Built at runtime so <c>SceneOptimizationPass</c> has nothing to bake
    /// static — a baked star would sit at the origin forever.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StunStarsView : MonoBehaviour
    {
        [SerializeField]
        private StunState stun;

        [SerializeField]
        private Material starMaterial;

        [SerializeField, Min(1)]
        private int starCount = 4;

        /// <summary>
        /// Height above the character's own origin. Clear above the head of a
        /// 1.8 m character, with room for the wider ring below it — the stars
        /// must not cut through the face as they come round the front.
        /// </summary>
        [SerializeField, Min(0.5f)]
        private float height = 2.35f;

        /// <summary>
        /// Wider than the character's own 0.45 m radius, so the ring reads as a
        /// halo around them rather than something stuck to their head. This is
        /// what makes a stun legible from the fixed camera at a distance.
        /// </summary>
        [SerializeField, Min(0.05f)]
        private float orbitRadius = 0.8f;

        [SerializeField, Min(0.05f)]
        private float starSize = 0.62f;

        [SerializeField]
        private float revolutionsPerSecond = 1.1f;

        private Transform _ring;
        private Transform[] _stars;
        private float _phase;

        public bool IsShowing { get; private set; }
        public int StarCount => Mathf.Max(1, starCount);
        public float OrbitRadius => orbitRadius;

        /// <summary>
        /// The object the stars hang from, or null before it is built.
        ///
        /// Exposed so the torch visibility rule can tell a star from the
        /// character it orbits. Asking whether a renderer has a
        /// <c>StunStarsView</c> above it does not work: this component lives on
        /// the player root, so that question is true of every renderer on the
        /// character — which silently exempted the whole thief from being hidden.
        /// </summary>
        public Transform RingRoot => _ring;

        public void Configure(
            StunState configuredStun,
            Material configuredStarMaterial)
        {
            stun = configuredStun;
            starMaterial = configuredStarMaterial;
        }

        /// <summary>
        /// A flat five-pointed star. Built rather than imported because it is ten
        /// vertices of arithmetic and an authored asset would be one more file to
        /// keep in step with nothing.
        /// </summary>
        private static Mesh BuildStarMesh(int points)
        {
            int rim = points * 2;
            var vertices = new Vector3[rim + 1];
            var triangles = new int[rim * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < rim; i++)
            {
                // Alternating long and short spokes; 0.42 is what reads as a
                // star rather than a cog or a blob.
                float radius = i % 2 == 0 ? 0.5f : 0.21f;
                float radians = Mathf.PI * 2f * i / rim
                    + Mathf.PI * 0.5f;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f);
            }

            // Wound so the front face ends up pointing at the camera once the
            // star is turned to face it.
            //
            // This was backwards, and it is why the stars were invisible for
            // their entire existence. Everything else was right — four meshes,
            // built, active, enabled, bright yellow, correctly placed above the
            // head, inside the frustum — and backface culling threw away every
            // triangle because the one visible side pointed away from the
            // camera. Measured at a dot product of -0.997, i.e. almost exactly
            // backwards.
            //
            // The rim runs counter-clockwise in the local XY plane, so the
            // second and third indices are swapped relative to the obvious
            // order. The torch wedge does not need this because it is built in
            // the XZ plane and viewed from above; the plane and the viewing
            // direction together decide the winding, which is why this cannot be
            // reasoned about once and applied everywhere.
            for (int i = 0; i < rim; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1 < rim ? i + 2 : 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Stun Star" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Build()
        {
            if (_ring != null)
            {
                return;
            }

            var ring = new GameObject("Stun Stars");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * height;
            _ring = ring.transform;

            Mesh mesh = BuildStarMesh(5);
            _stars = new Transform[Mathf.Max(1, starCount)];
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = new GameObject(
                    $"Star {i}",
                    typeof(MeshFilter),
                    typeof(MeshRenderer));
                star.transform.SetParent(_ring, false);
                float radians = Mathf.PI * 2f * i / _stars.Length;
                star.transform.localPosition = new Vector3(
                    Mathf.Cos(radians) * orbitRadius,
                    0f,
                    Mathf.Sin(radians) * orbitRadius);
                star.transform.localScale = Vector3.one * starSize;
                star.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer =
                    star.GetComponent<MeshRenderer>();
                if (starMaterial != null)
                {
                    renderer.sharedMaterial = starMaterial;
                }

                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _stars[i] = star.transform;
            }

            _ring.gameObject.SetActive(false);
        }

        private StunState ResolveStun()
        {
            if (stun == null)
            {
                stun = GetComponentInParent<StunState>();
            }

            return stun;
        }

        private void LateUpdate()
        {
            Build();
            bool showing = ResolveStun()?.IsStunned == true;
            if (showing != IsShowing)
            {
                IsShowing = showing;
                _ring.gameObject.SetActive(showing);
            }

            if (!showing)
            {
                return;
            }

            _phase += Time.deltaTime * revolutionsPerSecond * 360f;
            // The ring spins about the character's up axis; each star is turned
            // to face the fixed camera so a flat mesh never edges out of sight.
            _ring.localRotation = Quaternion.Euler(0f, _phase, 0f);
            Camera view = Camera.main;
            if (view == null || _stars == null)
            {
                return;
            }

            foreach (Transform star in _stars)
            {
                if (star != null)
                {
                    star.rotation = view.transform.rotation;
                }
            }
        }
    }
}
