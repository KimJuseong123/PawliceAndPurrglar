using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Says how much of the room the player is standing in is still there.
    ///
    /// Written for the officer, who cannot pick any of it up. Without this a room
    /// with nothing in it and a room the thief emptied ten seconds ago look
    /// exactly the same from the doorway — and the difference is the only thing
    /// the officer would want to know, because one says "search elsewhere" and the
    /// other says "they came through here".
    ///
    /// Shown to the thief too. They can see the shelves themselves, so it tells
    /// them nothing they could not work out, and a line that appears for one role
    /// only is a line somebody has to remember to keep working for that role.
    ///
    /// Builds its own label rather than being handed one, like
    /// <see cref="InteriorEscapePresenter"/>: a list filled by an editor script
    /// does not survive being saved, and this project has lost three playtests to
    /// that (<c>ISSUE-031</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorLootTallyPresenter : MonoBehaviour
    {
        private Text _label;
        private PlayerInteriorState _interiorState;
        private PlayerRole _role = PlayerRole.Police;

        public string CurrentText => _label != null ? _label.text : string.Empty;

        public bool IsShowing =>
            _label != null && _label.gameObject.activeSelf;

        private void OnEnable()
        {
            EnsureLabel();
        }

        private void EnsureLabel()
        {
            if (_label != null)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var host = new GameObject("Interior Loot Tally");
            host.transform.SetParent(canvas.transform, false);

            var rect = host.AddComponent<RectTransform>();

            // Top centre, under the catch tally, where the match-wide numbers
            // already are. It is a fact about the place rather than about the
            // player, so it does not belong next to their own bag.
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -168f);
            rect.sizeDelta = new Vector2(520f, 40f);

            _label = host.AddComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontSize = 22;
            _label.color = new Color(1f, 0.9f, 0.55f);
            _label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            _label.raycastTarget = false;
            host.SetActive(false);
        }

        /// <summary>
        /// Which character's whereabouts this screen follows.
        ///
        /// Re-asked while it is null rather than once at startup: the role this
        /// machine controls is handed out by the host after the lobby, so at
        /// startup there is nobody to ask.
        /// </summary>
        private void ResolveInteriorState()
        {
            if (_interiorState != null)
            {
                return;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            if (selector == null)
            {
                return;
            }

            _role = selector.ActiveRole;
            foreach (PlayerInteriorState candidate in
                FindObjectsByType<PlayerInteriorState>(
                    FindObjectsSortMode.None))
            {
                PlayerRoleIdentity identity =
                    candidate.GetComponent<PlayerRoleIdentity>();
                if (identity != null && identity.Role == _role)
                {
                    _interiorState = candidate;
                    return;
                }
            }
        }

        /// <summary>
        /// The draw belonging to the room this player is inside.
        ///
        /// Matched on the interior's own id rather than on proximity. The rooms
        /// are built far away from the town and from each other, so distance would
        /// work today and stop working the moment two of them are built close
        /// together — and it would fail by reporting the wrong room's shelves,
        /// which is worse than reporting none.
        /// </summary>
        private LootSpotDraw FindDrawForInterior(int interiorId)
        {
            // Held between frames, and this is not a micro-optimisation.
            //
            // The sweep below walks every object in the scene, and the scene
            // holds the whole town plus nineteen rooms. It used to run **every
            // frame the player was indoors** — the one place in the game where
            // frame time is already spent on a room being looked at closely —
            // which is exactly the "it gets slow when I go into the supermarket"
            // report, and exactly what CLAUDE.md says not to do.
            //
            // Keyed by the room's id so walking out of one shop and into another
            // still re-resolves, and re-swept when the held reference has gone
            // (a Unity null check, so a room destroyed with its scene reads as
            // gone rather than as a live reference to nothing).
            bool sameRoom = _cachedDrawInteriorId == interiorId;
            if (sameRoom && (_cachedDraw != null || _cachedDrawAbsent))
            {
                return _cachedDraw;
            }

            _cachedDrawInteriorId = interiorId;
            _cachedDraw = null;
            foreach (HouseInterior room in
                FindObjectsByType<HouseInterior>(FindObjectsSortMode.None))
            {
                if (room.InteriorId == interiorId)
                {
                    _cachedDraw = room.GetComponent<LootSpotDraw>();
                    break;
                }
            }

            _cachedDrawAbsent = _cachedDraw == null;
            return _cachedDraw;
        }

        private LootSpotDraw _cachedDraw;

        /// <summary>
        /// Which room <see cref="_cachedDraw"/> belongs to.
        /// </summary>
        private int _cachedDrawInteriorId = int.MinValue;

        /// <summary>
        /// Whether that room was swept and genuinely has no draw.
        ///
        /// Needed because "no draw" is an answer worth remembering: without it a
        /// room with unmarked shelves — the jail, or any room the loot pass has
        /// not reached — would fail the cache check on every frame and sweep the
        /// scene again, which is the cost this exists to remove.
        /// </summary>
        private bool _cachedDrawAbsent;

        private void Update()
        {
            EnsureLabel();
            if (_label == null)
            {
                return;
            }

            ResolveInteriorState();
            LootSpotDraw draw =
                _interiorState != null
                && _interiorState.IsIndoors
                && _interiorState.CurrentInteriorId != HouseInterior.JailId
                    ? FindDrawForInterior(_interiorState.CurrentInteriorId)
                    : null;

            // A room with no draw at all is a room with no shelves marked, and
            // saying "0 남음" there would tell the officer the thief had emptied
            // a room that never held anything.
            bool visible = draw != null && draw.PieceCount > 0;
            if (_label.gameObject.activeSelf != visible)
            {
                _label.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            int remaining = draw.RemainingCount;
            _label.text = remaining > 0
                ? $"이 건물 물건 {remaining} / {draw.PieceCount} 남음"
                : $"이 건물 물건 0 / {draw.PieceCount} — 이미 털렸다";
            _label.color = remaining > 0
                ? new Color(1f, 0.9f, 0.55f)
                : new Color(1f, 0.55f, 0.5f);
        }
    }
}
