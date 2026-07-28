using PawsAndLoot.Gameplay.Items;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Draws a placed prop, and flashes a sensor light when it trips.
    ///
    /// Placed props had no visual at all — the coordinator created a bare
    /// GameObject with a trigger and nothing to look at. Nobody had noticed
    /// because nothing placeable was obtainable yet, so the first banana anybody
    /// ever put down would have been an invisible one. A trap the thief cannot
    /// see is not a trap, it is bad luck.
    ///
    /// Greybox stand-ins, built here rather than authored, because these are
    /// prototype props whose final art is not decided. Shapes are chosen to be
    /// told apart from directly overhead, which is the only angle this game is
    /// seen from: the banana is a flat yellow sliver, the glue trap a flat dark
    /// square, the sensor light a small post with a lamp on top.
    ///
    /// Presentation only. The prop is still whatever <c>PlacedTrap</c> says it
    /// is, and removing this component would leave the rules untouched and the
    /// street empty.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedTrapView : MonoBehaviour
    {
        [SerializeField]
        private ThrowableKind kind = ThrowableKind.Banana;

        [SerializeField]
        private Material material;

        /// <summary>
        /// How long the lamp stays lit after tripping. Matches the reveal, so the
        /// light is on for exactly as long as the thief is exposed — the flash is
        /// the explanation for it.
        /// </summary>
        [SerializeField, Min(0.1f)]
        private float flashSeconds = ThrowableCatalog.RevealSeconds;

        private Light _lamp;
        private float _flashUntil;
        private bool _built;

        public bool IsFlashing => Time.time < _flashUntil;

        public void Configure(
            ThrowableKind configuredKind,
            Material configuredMaterial)
        {
            kind = configuredKind;
            material = configuredMaterial;
        }

        /// <summary>
        /// Lights the lamp. Called on every machine, because the thief has to see
        /// what they set off as much as the officer does — being revealed without
        /// knowing it would read as the game leaking your position for no reason.
        /// </summary>
        public void Flash()
        {
            _flashUntil = Time.time + flashSeconds;
        }

        private GameObject AddPart(
            PrimitiveType shape,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            // No collider: the trap's own trigger radius decides everything, and
            // a solid prop in the road would block the runner it is meant to
            // catch — and stop thrown rocks besides.
            Destroy(part.GetComponent<Collider>());
            if (material != null)
            {
                part.GetComponent<Renderer>().sharedMaterial = material;
            }

            part.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            return part;
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            switch (kind)
            {
                case ThrowableKind.SensorLight:
                    AddPart(
                        PrimitiveType.Cylinder,
                        new Vector3(0f, 0.35f, 0f),
                        new Vector3(0.12f, 0.35f, 0.12f));
                    GameObject head = AddPart(
                        PrimitiveType.Cube,
                        new Vector3(0f, 0.78f, 0f),
                        new Vector3(0.38f, 0.2f, 0.26f));
                    var lampObject = new GameObject("Lamp", typeof(Light));
                    lampObject.transform.SetParent(
                        head.transform,
                        false);
                    _lamp = lampObject.GetComponent<Light>();
                    _lamp.type = LightType.Point;
                    _lamp.range = 9f;
                    _lamp.intensity = 5.5f;
                    _lamp.color = new Color(1f, 0.95f, 0.75f);
                    _lamp.shadows = LightShadows.None;
                    _lamp.enabled = false;
                    break;

                case ThrowableKind.GlueTrap:
                    // Flat and wide, so it reads as something on the ground
                    // rather than an object to walk around.
                    AddPart(
                        PrimitiveType.Cube,
                        new Vector3(0f, 0.03f, 0f),
                        new Vector3(1.1f, 0.06f, 1.1f));
                    break;

                default:
                    AddPart(
                        PrimitiveType.Capsule,
                        new Vector3(0f, 0.08f, 0f),
                        new Vector3(0.22f, 0.16f, 0.22f))
                        .transform.localRotation =
                        Quaternion.Euler(0f, 0f, 90f);
                    break;
            }
        }

        private void LateUpdate()
        {
            Build();
            if (_lamp != null)
            {
                _lamp.enabled = IsFlashing;
            }
        }
    }
}
