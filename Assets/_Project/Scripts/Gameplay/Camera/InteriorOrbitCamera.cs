using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Camera
{
    /// <summary>
    /// A free third-person view, used only while the local player is indoors.
    ///
    /// The town is seen from one fixed angle on purpose: both players read the
    /// same streets the same way, and nobody can gain an advantage by turning the
    /// camera. A room is the opposite case — it has four walls, and a fixed
    /// overhead angle would put two of them between the camera and the player.
    /// So the camera turns indoors and only indoors.
    ///
    /// Orbited by dragging the right mouse button. The left button throws and the
    /// cursor aims, so free-look cannot have the bare mouse; a held button is the
    /// cheapest way to have both without a mode to remember.
    ///
    /// Takes over from <see cref="TopDownFollowCamera"/> by disabling it, rather
    /// than by both writing the transform and fighting over it.
    ///
    /// Per screen and presentation only. Nothing about the simulation reads the
    /// camera, so the two players can be looking at completely different things.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorOrbitCamera : MonoBehaviour
    {
        [SerializeField]
        private TopDownFollowCamera townCamera;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField, Min(1f)]
        private float distance = 9f;

        [SerializeField, Range(5f, 80f)]
        private float pitchDegrees = 34f;

        [SerializeField, Min(0.05f)]
        private float degreesPerPixel = 0.22f;

        [SerializeField, Min(0.01f)]
        private float smoothTimeSeconds = 0.08f;

        [SerializeField]
        private Vector3 lookOffset = new(0f, 1.2f, 0f);

        private Transform _followed;
        private PlayerInteriorState _state;
        private float _yaw;
        private Vector3 _velocity;
        private bool _active;

        public bool IsActive => _active;
        public float Yaw => _yaw;

        public void Configure(
            TopDownFollowCamera configuredTownCamera,
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            townCamera = configuredTownCamera;
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// The local player's own interior state.
        ///
        /// Resolved every frame until found, because the role each machine
        /// controls is handed out by the host after the lobby — binding this at
        /// scene-build time would watch the wrong character.
        /// </summary>
        private PlayerInteriorState ResolveLocalState()
        {
            if (_state != null)
            {
                return _state;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                if (roleSelector == null)
                {
                    return null;
                }

                assigned = roleSelector.ActiveRole;
            }

            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role != assigned.Value)
                {
                    continue;
                }

                _state = candidate.GetComponent<PlayerInteriorState>();
                _followed = candidate.transform;
                return _state;
            }

            return null;
        }

        private void SetActive(bool active)
        {
            if (_active == active)
            {
                return;
            }

            _active = active;
            if (townCamera != null)
            {
                townCamera.enabled = !active;
            }

            if (!active || _followed == null)
            {
                return;
            }

            // Start behind the player rather than at whatever yaw was left over
            // from the last visit, so walking in never begins with the camera
            // facing a wall.
            _yaw = _followed.eulerAngles.y;
            transform.position = DesiredPosition();
            _velocity = Vector3.zero;
        }

        private Vector3 DesiredPosition()
        {
            Quaternion rotation = Quaternion.Euler(
                pitchDegrees,
                _yaw,
                0f);
            return _followed.position
                + lookOffset
                - rotation * Vector3.forward * distance;
        }

        public void Tick(float deltaTime)
        {
            PlayerInteriorState state = ResolveLocalState();
            SetActive(state != null && state.IsIndoors);
            if (!_active || _followed == null || deltaTime <= 0f)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                _yaw += mouse.delta.ReadValue().x * degreesPerPixel;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                DesiredPosition(),
                ref _velocity,
                smoothTimeSeconds,
                Mathf.Infinity,
                deltaTime);
            transform.rotation = Quaternion.LookRotation(
                (_followed.position + lookOffset - transform.position)
                    .normalized,
                Vector3.up);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
