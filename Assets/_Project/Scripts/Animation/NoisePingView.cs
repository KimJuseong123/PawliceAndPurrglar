using PawliceAndPurrglar.Gameplay.Sensing;
using UnityEngine;

namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// Draws a ring on the ground where something banged.
    ///
    /// Without it the noise props are invisible to the person they are aimed
    /// at. The animal turns and walks off, and the player watching has no idea
    /// why — which reads as the animal breaking rather than as somebody setting
    /// off a firework two streets away. A sound with nothing to see is a sound
    /// that only the code knows about.
    ///
    /// A ring rather than a marker, and it grows, because the thing being shown
    /// is not a place but a place plus a reach: the edge of the ring is exactly
    /// how far the bang carried, so a player can see whether they were inside
    /// it.
    ///
    /// Built in code and wound the way the stun stars had to be wound. A flat
    /// ring lying on the ground is seen from above, so its faces point up — get
    /// that backwards and back-face culling throws every triangle away, the
    /// component reports itself perfectly healthy, and nothing is ever drawn
    /// (`ISSUE-030`).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoisePingView : MonoBehaviour
    {
        private const int Segments = 48;
        private const float ThicknessMeters = 0.55f;
        private const float HeightMeters = 0.06f;

        [SerializeField]
        private NoiseBoard board;

        [SerializeField]
        private Material ringMaterial;

        private GameObject _ring;
        private MeshRenderer _renderer;
        private float _remaining;
        private float _total;
        private float _radius;

        public bool IsShowing => _remaining > 0f;

        public void Configure(NoiseBoard configuredBoard, Material material)
        {
            board = configuredBoard;
            ringMaterial = material;
        }

        /// <summary>
        /// Where the ring currently sits and how big it is, so a test can ask
        /// what is on screen rather than whether the component thinks it is
        /// showing something.
        /// </summary>
        public float CurrentRadius =>
            _ring == null ? 0f : _ring.transform.localScale.x;

        private void OnEnable()
        {
            EnsureRing();
        }

        private void Update()
        {
            if (board == null)
            {
                board = FindFirstObjectByType<NoiseBoard>();
                if (board == null)
                {
                    return;
                }
            }

            if (board.IsRinging
                && !Mathf.Approximately(board.RemainingSeconds, _remaining))
            {
                // Restarted whenever the board's countdown jumps back up, which
                // is the one signal that a new bang was written down. Asking the
                // board for a count instead would work equally well and mean
                // this had to remember a number the board already keeps.
                if (board.RemainingSeconds > _remaining)
                {
                    Begin(board.Latest);
                }
            }

            Advance(Time.deltaTime);
        }

        private void Begin(NoiseReport report)
        {
            EnsureRing();
            _radius = Mathf.Max(0.5f, report.Radius);
            _total = Mathf.Max(0.05f, report.RingSeconds);
            _remaining = _total;
            _ring.transform.position = new Vector3(
                report.At.x,
                report.At.y + HeightMeters,
                report.At.z);
        }

        private void Advance(float deltaTime)
        {
            if (_ring == null)
            {
                return;
            }

            if (_remaining <= 0f)
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }

                return;
            }

            _remaining = Mathf.Max(0f, _remaining - deltaTime);
            float grown = 1f - _remaining / _total;
            _ring.transform.localScale =
                Vector3.one * Mathf.Lerp(1f, _radius, grown);
            if (_renderer != null)
            {
                _renderer.enabled = true;
            }
        }

        private void EnsureRing()
        {
            if (_ring != null)
            {
                return;
            }

            _ring = new GameObject("Noise Ring");
            _ring.transform.SetParent(transform, false);
            _ring.AddComponent<MeshFilter>().sharedMesh = BuildRing();
            _renderer = _ring.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = ringMaterial;
            _renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        /// <summary>
        /// A flat annulus of unit outer radius, facing straight up.
        /// </summary>
        private static Mesh BuildRing()
        {
            var vertices = new Vector3[Segments * 2];
            var normals = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            float inner = Mathf.Max(0f, 1f - ThicknessMeters);

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

                // Anticlockwise seen from below is clockwise seen from above,
                // which is the winding a face pointing up needs. Reversed, the
                // ring exists and is never drawn.
                triangles[step * 6] = a;
                triangles[step * 6 + 1] = b;
                triangles[step * 6 + 2] = d;
                triangles[step * 6 + 3] = a;
                triangles[step * 6 + 4] = d;
                triangles[step * 6 + 5] = c;
            }

            var mesh = new Mesh
            {
                name = "NoiseRing",
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
