using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Camera
{
    /// <summary>
    /// A free look-around view, used only while the local player is indoors.
    ///
    /// The town is seen from one fixed angle on purpose: both players read the same
    /// streets the same way, and nobody gains an advantage by turning the camera.
    /// A room is the opposite case — it has four walls, and a fixed overhead angle
    /// would put two of them between the camera and the player. So the camera turns
    /// indoors and only indoors.
    ///
    /// The cursor is hidden and locked while it is active. That is what allows a low
    /// sensitivity: with the pointer confined to the window, a slow turn runs out of
    /// screen and stops, and the player has to swipe repeatedly. Locked, the mouse
    /// reports movement forever.
    ///
    /// Two consequences are handled deliberately rather than left as surprises.
    /// Aiming has no cursor to read, so <c>ToolUseInput</c> switches to throwing
    /// along the camera's own facing while the cursor is locked — indoors you throw
    /// where you are looking, which is what a third-person view implies anyway.
    /// And <b>Escape</b> releases the cursor: without it the pointer is trapped, and
    /// with two windows open on one machine for testing there would be no way to
    /// reach the other one.
    ///
    /// Per screen and presentation only. Nothing in the simulation reads the camera,
    /// so the two players can be looking at completely different things.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorOrbitCamera : MonoBehaviour
    {
        [SerializeField]
        private TopDownFollowCamera townCamera;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        /// <summary>
        /// Close and steep enough to stay inside the room.
        ///
        /// Nine metres at 34 degrees put the camera 6.2 m above the player, which is
        /// over the top of a wall — every neighbouring room was visible at once.
        /// Read by the sightline test together with <see cref="Distance"/> and the
        /// pitch cap, since the three of them and the wall height are one decision.
        /// </summary>
        [SerializeField, Min(1f)]
        private float distance = 6.5f;

        [SerializeField, Range(5f, 80f)]
        private float pitchDegrees = 26f;

        /// <summary>
        /// Low, which is the whole reason the cursor is locked. Unlocked, a slow
        /// turn runs out of screen; locked, it can be as slow as it likes.
        /// </summary>
        [SerializeField, Min(0.01f)]
        private float degreesPerPixel = 0.11f;

        /// <summary>
        /// How far the view may tilt. Clamped at both ends: past the low limit the
        /// camera slides into the floor, and past the high one it looks over the
        /// wall into the neighbouring rooms (ISSUE-035).
        ///
        /// The high limit is not a taste decision, it is arithmetic. The camera sits
        /// <c>lookOffset.y + distance·sin(pitch)</c> above the player, and the rooms
        /// are the house model at 2.2x, so their 2.55 m walls stand 5.61 m tall.
        /// At 38 degrees the camera is 5.20 m up and stays inside; at the 62 it used
        /// to allow it would be 6.94 m and looking over the top. The upper bound and
        /// the room scale have to move together, which
        /// <c>InteriorSightlinePlayModeTests</c> is there to enforce.
        /// </summary>
        [SerializeField]
        private Vector2 pitchLimits = new(14f, 28f);

        [SerializeField, Min(0.01f)]
        private float smoothTimeSeconds = 0.08f;

        [SerializeField]
        private Vector3 lookOffset = new(0f, 1.2f, 0f);

        private Transform _followed;
        private PlayerInteriorState _state;
        private float _yaw;
        private Vector3 _velocity;
        private bool _active;
        private bool _released;

        public bool IsActive => _active;
        public float Yaw => _yaw;
        public float Pitch => pitchDegrees;

        /// <summary>
        /// Exposed so a test can work out how high the camera can ever get, rather
        /// than being told the answer.
        /// </summary>
        public float Distance => distance;
        public float MaxPitch => pitchLimits.y;
        public Vector3 LookOffset => lookOffset;

        /// <summary>
        /// True while the cursor is deliberately free — Escape was pressed. Exposed
        /// so a test can assert that the trap has a way out.
        /// </summary>
        public bool IsCursorReleased => _released;

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
        /// Resolved every frame until found, because the role each machine controls
        /// is handed out by the host after the lobby — binding this at scene-build
        /// time would watch the wrong character.
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

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked
                ? CursorLockMode.Locked
                : CursorLockMode.None;
            Cursor.visible = !locked;
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

            // Leaving always frees the cursor, whatever state it was in. A pointer
            // still trapped after stepping into the street would be a bug nobody
            // could work around.
            _released = false;
            SetCursorLocked(active);

            if (!active || _followed == null)
            {
                ClearRoomBounds();
                return;
            }

            AdoptRoomOf(_followed);

            // Start behind the player rather than at whatever yaw was left over
            // from the last visit, so walking in never begins facing a wall.
            _yaw = _followed.eulerAngles.y;
            transform.position = DesiredPosition();
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// How far inside the walls the camera is kept, in metres.
        ///
        /// The rooms are one welded mesh, so a wall between the camera and the
        /// player cannot be singled out and faded — there is nothing to single
        /// out. Keeping the camera inside the room instead means no outer wall
        /// is ever between the two, which is the same result by a different
        /// road and costs nothing.
        ///
        /// Half a metre in, so the near plane does not clip through.
        /// </summary>
        private const float WallStandoff = 0.5f;

        private Bounds _room;
        private bool _hasRoom;

        /// <summary>
        /// Tells the camera which room it is in, so it can stay inside it.
        ///
        /// Given rather than found: the room is a box the generator already
        /// measured, and asking the camera to work it out from colliders every
        /// frame would be a second opinion about the same thing.
        /// </summary>
        public void SetRoomBounds(Bounds room)
        {
            _room = room;
            _hasRoom = true;
        }

        public void ClearRoomBounds()
        {
            _hasRoom = false;
        }

        /// <summary>
        /// Works out which room the player is standing in and keeps to it.
        ///
        /// By floor area rather than by an id, so nothing has to be threaded
        /// through: the rooms are laid out well apart from each other off the
        /// map, and a point is inside exactly one of them.
        /// </summary>
        private void AdoptRoomOf(Transform player)
        {
            foreach (Gameplay.Interiors.HouseInterior room in
                FindObjectsByType<Gameplay.Interiors.HouseInterior>(
                    FindObjectsSortMode.None))
            {
                Vector2 half = room.FloorHalfExtents;
                Vector3 centre = room.transform.position;
                if (Mathf.Abs(player.position.x - centre.x) > half.x
                    || Mathf.Abs(player.position.z - centre.z) > half.y)
                {
                    continue;
                }

                SetRoomBounds(new Bounds(
                    new Vector3(centre.x, player.position.y, centre.z),
                    new Vector3(half.x * 2f, 1f, half.y * 2f)));
                return;
            }

            ClearRoomBounds();
        }

        private Vector3 DesiredPosition()
        {
            Quaternion rotation = Quaternion.Euler(
                pitchDegrees,
                _yaw,
                0f);
            Vector3 wanted = _followed.position
                + lookOffset
                - rotation * Vector3.forward * distance;

            if (!_hasRoom)
            {
                return wanted;
            }

            // Pulled back inside the walls. Height is left alone: the camera is
            // meant to be above the wall tops looking down, and the wall height
            // is chosen to keep it under the ceiling that is not there.
            return new Vector3(
                Mathf.Clamp(
                    wanted.x,
                    _room.min.x + WallStandoff,
                    _room.max.x - WallStandoff),
                wanted.y,
                Mathf.Clamp(
                    wanted.z,
                    _room.min.z + WallStandoff,
                    _room.max.z - WallStandoff));
        }

        /// <summary>
        /// Escape frees the pointer; a click takes it back.
        ///
        /// Both halves matter. Without the release the cursor is trapped in the
        /// window, which on a machine running two copies for testing means the
        /// other one cannot be reached. Without the re-capture the player has no
        /// way back into looking around except by leaving the house.
        /// </summary>
        private void UpdateCursorLock(Mouse mouse)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && keyboard.escapeKey.wasPressedThisFrame
                && !_released)
            {
                _released = true;
                SetCursorLocked(false);
                return;
            }

            if (_released
                && mouse != null
                && mouse.leftButton.wasPressedThisFrame)
            {
                _released = false;
                SetCursorLocked(true);
            }
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
            UpdateCursorLock(mouse);

            // Only while the cursor is actually captured. Turning the view with a
            // free pointer would move the camera every time somebody reached for a
            // window.
            if (mouse != null && !_released)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * degreesPerPixel;
                // Mouse up raises the view, which means a shallower angle.
                pitchDegrees = Mathf.Clamp(
                    pitchDegrees - delta.y * degreesPerPixel,
                    pitchLimits.x,
                    pitchLimits.y);
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

        private void OnDisable()
        {
            // Never leave the pointer captured because this was switched off.
            if (_active)
            {
                SetCursorLocked(false);
            }
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
