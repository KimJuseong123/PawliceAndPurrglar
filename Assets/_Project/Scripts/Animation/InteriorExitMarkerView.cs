using UnityEngine;

namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// Marks the way out of a room, on the floor.
    ///
    /// A room's exit is a trigger you walk into and nothing else. From inside it
    /// is a patch of floor identical to the rest of the floor, in a room whose
    /// walls are a single scanned mesh with no door drawn on the inside face —
    /// so the only way to leave was to remember which wall you came through and
    /// walk at it. That is a memory test, not a chase.
    ///
    /// A ring on the ground plus something bobbing above it, because the two
    /// answer different questions. The ring says *where*, exactly, and stays
    /// readable when the player is standing on it. The bob is what catches the
    /// eye from across the room, where a flat ring on a flat floor is a few
    /// pixels seen almost edge-on.
    ///
    /// <b>Built in code, and the winding is the whole risk.</b> A flat ring lying
    /// on the ground is seen from above, so its faces have to point up; get that
    /// backwards and back-face culling throws every triangle away while the
    /// component reports itself perfectly healthy. That is exactly how the stun
    /// stars stayed invisible for their entire existence. The ring here is wound
    /// the way <c>NoisePingView</c>'s is — the one in this project already proven
    /// to draw — and the bobbing part is a Unity primitive, which cannot be wound
    /// wrongly.
    ///
    /// Built rather than placed for a second reason: <c>SceneOptimizationPass</c>
    /// bakes scene renderers static, and a baked marker would sit at the origin
    /// while the room it belongs to is somewhere else.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorExitMarkerView : MonoBehaviour
    {
        private const int Segments = 40;

        /// <summary>
        /// How wide the ring is. Comfortably bigger than a character so it reads
        /// as a place to stand rather than a thing to pick up.
        /// </summary>
        [SerializeField, Min(0.3f)]
        private float radius = 1.15f;

        /// <summary>
        /// How much of the radius is ring rather than hole. A thin outline
        /// disappears at this camera distance; a filled disc reads as a hole in
        /// the floor.
        /// </summary>
        [SerializeField, Range(0.05f, 0.9f)]
        private float thickness = 0.28f;

        /// <summary>
        /// Clear of the floor. Coplanar geometry z-fights, and the fighting is
        /// worse than the marker is useful.
        /// </summary>
        [SerializeField, Min(0.005f)]
        private float lift = 0.04f;

        [SerializeField]
        private Material markerMaterial;

        private Transform _bob;
        private float _phase;

        public bool HasRing { get; private set; }

        public void Configure(Material configuredMaterial)
        {
            markerMaterial = configuredMaterial;
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            if (HasRing)
            {
                return;
            }

            HasRing = true;

            var ring = new GameObject(
                "Exit Ring",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, lift, 0f);
            ring.transform.localScale = Vector3.one * radius;
            ring.GetComponent<MeshFilter>().sharedMesh = BuildRing(thickness);
            Paint(ring.GetComponent<MeshRenderer>());

            // The bobbing part. A cube stood on its corner reads as an arrowhead
            // pointing down without needing a mesh of its own — and a primitive
            // cannot be wound backwards, which is the failure this file is most
            // exposed to.
            GameObject bob = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bob.name = "Exit Pointer";
            Destroy(bob.GetComponent<Collider>());
            bob.transform.SetParent(transform, false);
            bob.transform.localScale = Vector3.one * 0.28f;
            bob.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Paint(bob.GetComponent<MeshRenderer>());
            _bob = bob.transform;
        }

        private void Paint(MeshRenderer renderer)
        {
            if (markerMaterial != null)
            {
                renderer.sharedMaterial = markerMaterial;
            }

            // Never casts or receives. It is a sign, and a sign that throws a
            // shadow onto the floor it is drawn on reads as an object lying
            // there.
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Update()
        {
            if (_bob == null)
            {
                return;
            }

            _phase += Time.deltaTime;

            // Unscaled height rather than a lerp between two numbers, so the
            // motion reads the same whatever the room was scaled to.
            float height = 1.05f + Mathf.Sin(_phase * 2.2f) * 0.14f;
            _bob.localPosition = new Vector3(0f, height, 0f);
            _bob.localRotation = Quaternion.Euler(45f, _phase * 45f, 45f);
        }

        /// <summary>
        /// A flat annulus in the XZ plane, faces up.
        ///
        /// The winding is copied from <c>NoisePingView</c> deliberately: it is
        /// the ring in this project that is known to draw. Anticlockwise seen
        /// from below is clockwise seen from above, which is what a face
        /// pointing up needs. Reversed, the ring exists and is never drawn.
        /// </summary>
        private static Mesh BuildRing(float thickness)
        {
            var vertices = new Vector3[Segments * 2];
            var normals = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            float inner = Mathf.Max(0f, 1f - thickness);

            for (int step = 0; step < Segments; step++)
            {
                float angle = step / (float)Segments * Mathf.PI * 2f;
                var direction = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                vertices[step * 2] = direction * inner;
                vertices[step * 2 + 1] = direction;
                normals[step * 2] = Vector3.up;
                normals[step * 2 + 1] = Vector3.up;
            }

            for (int step = 0; step < Segments; step++)
            {
                int a = step * 2;
                int b = step * 2 + 1;
                int c = (step + 1) % Segments * 2;
                int d = (step + 1) % Segments * 2 + 1;

                triangles[step * 6] = a;
                triangles[step * 6 + 1] = b;
                triangles[step * 6 + 2] = d;
                triangles[step * 6 + 3] = a;
                triangles[step * 6 + 4] = d;
                triangles[step * 6 + 5] = c;
            }

            var mesh = new Mesh
            {
                name = "ExitRing",
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
