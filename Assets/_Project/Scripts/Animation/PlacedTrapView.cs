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

        /// <summary>
        /// Who placed it. Only used to decide who may see a covert prop.
        /// </summary>
        [SerializeField]
        private PawsAndLoot.Gameplay.Players.PlayerRole placedBy =
            PawsAndLoot.Gameplay.Players.PlayerRole.Police;

        private Light _lamp;
        private float _flashUntil;
        private bool _built;
        private bool _hiddenFromOpponent;

        public bool IsFlashing => Time.time < _flashUntil;

        /// <summary>
        /// A sensor light is only drawn on its owner's screen.
        ///
        /// The whole point of a detector is that the other side walks into it
        /// without knowing. A visible one is just a worse trap — it warns the
        /// person it is meant to catch, and there is nothing they can do with the
        /// warning except walk around it, which makes it useless.
        ///
        /// The things underfoot are the opposite: a banana or a glue patch you
        /// cannot see is bad luck rather than a trap, so those stay visible to
        /// everybody.
        /// </summary>
        public bool IsCovert =>
            ThrowableCatalog.GetEffect(kind) == TrapEffect.Reveal;

        public void Configure(
            ThrowableKind configuredKind,
            Material configuredMaterial,
            PawsAndLoot.Gameplay.Players.PlayerRole configuredPlacedBy =
                PawsAndLoot.Gameplay.Players.PlayerRole.Police)
        {
            kind = configuredKind;
            material = configuredMaterial;
            placedBy = configuredPlacedBy;
        }

        /// <summary>
        /// True on the machine playing the side that placed this prop.
        ///
        /// Read every frame rather than cached: the role is handed out by the host
        /// after the lobby, and a cached answer taken during the scene load would
        /// be whatever the default was.
        /// </summary>
        private bool ViewerOwnsThis()
        {
            PawsAndLoot.Gameplay.Players.PlayerRole? assigned =
                PawsAndLoot.Gameplay.Players.LocalPlayerRoleSelector
                    .OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == placedBy;
            }

            PawsAndLoot.Gameplay.Players.LocalPlayerRoleSelector selector =
                FindFirstObjectByType<
                    PawsAndLoot.Gameplay.Players
                        .LocalPlayerRoleSelector>();
            return selector != null && selector.ActiveRole == placedBy;
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

            // The authored prop, when it exists. Only the officer's glue trap
            // and sensor light have no model, and the sensor light needs its
            // lamp either way, so both keep going through the shapes below.
            if (kind != ThrowableKind.SensorLight
                && ThrowableModelLibrary.TryInstantiate(kind, transform) != null)
            {
                return;
            }

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

            // A covert prop is hidden from the other side, lamp included. The
            // thief must not be told they are about to walk into a sensor, and
            // must not see the flash that gives their position away either.
            bool hide = IsCovert && !ViewerOwnsThis();
            if (hide != _hiddenFromOpponent)
            {
                _hiddenFromOpponent = hide;
                foreach (Renderer renderer in
                    GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = !hide;
                }
            }

            if (_lamp != null)
            {
                _lamp.enabled = IsFlashing && !hide;
            }
        }
    }
}
