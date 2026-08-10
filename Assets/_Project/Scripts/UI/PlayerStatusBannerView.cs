using System;
using PawliceAndPurrglar.Gameplay.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// What is happening to the player at *this* screen, right now.
    ///
    /// Four states that were all implemented and all silent:
    ///
    /// - **끈끈이** took three seconds of movement and drew the rock's four
    ///   stars, so it read as "you were hit and it is going on a long time".
    /// - **센서등** takes nothing away — it only makes you visible, and the
    ///   entire consequence happens on the *other* player's screen. A thief
    ///   walked through one, saw no change, and was arrested for reasons the
    ///   game never showed them.
    /// - **경보 질주** gives the officer 1.25× for twelve seconds. Twelve
    ///   seconds is long enough to end before you notice it started.
    /// - **은신** hides the thief's own character, which is the one case with
    ///   *some* feedback — but the camera keeps following a body that is no
    ///   longer drawn, and "am I hidden or did I break something" is the wrong
    ///   question to leave a player holding.
    ///
    /// Screen-space rather than over the head: all four are facts about you, and
    /// the two that stop you moving have to be legible precisely when the camera
    /// is sitting on a character who is not moving.
    ///
    /// One banner, one state at a time, in the order below. Two stacked banners
    /// read as one broken banner.
    ///
    /// Installs itself, like <see cref="InkBlindOverlayView"/>. An overlay that
    /// has to be added to a prefab is an overlay that goes missing from
    /// whichever of the two HUD paths nobody remembered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatusBannerView : MonoBehaviour
    {
        /// <summary>
        /// What the banner can say, most urgent first. The order is the
        /// priority: being unable to move outranks being visible, and both
        /// outrank a bonus.
        /// </summary>
        public enum Status
        {
            Stuck = 0,
            Revealed = 1,
            Sprinting = 2,
            Hiding = 3
        }

        /// <summary>
        /// How quickly the banner arrives and leaves. Short — the effects are
        /// 2.5 to 12 seconds, and a half-second fade at each end would spend a
        /// third of the shortest one not saying anything.
        /// </summary>
        private const float FadeSeconds = 0.18f;

        /// <summary>
        /// How hard the edge of the screen is tinted. Deliberately an edge and
        /// not a wash: unlike the octopus, none of these takes your sight, and
        /// dimming the middle would take it anyway.
        /// </summary>
        private const float EdgeAlpha = 0.85f;

        /// <summary>
        /// How often the scene is searched for the components this reads.
        ///
        /// It used to be every frame, twice — two <c>FindObjectsByType</c> calls
        /// per frame on a build whose first target is WebGL, to watch two
        /// objects that are created once per match and then never replaced.
        /// Cached with this as the retry interval, so a scene that has not
        /// spawned its players yet costs two searches a second instead of a
        /// hundred and twenty.
        /// </summary>
        private const float RescanSeconds = 0.5f;

        private const int TextureSize = 128;

        private static readonly Color GlueColor = new(0.93f, 0.68f, 0.16f, 1f);
        private static readonly Color SensorColor = new(1f, 0.27f, 0.30f, 1f);
        private static readonly Color SprintColor = new(0.38f, 0.78f, 1f, 1f);
        private static readonly Color HideColor = new(0.55f, 0.85f, 0.55f, 1f);

        private CanvasGroup _group;
        private Image _edge;
        private TMP_Text _label;

        private bool _hasCache;
        private PlayerRole _cachedRole;
        private float _nextRescan;
        private StunState _stun;
        private PlayerMovementMotor _motor;
        private ThiefHidingState _hiding;
        private FlashlightVisibility[] _watchers =
            Array.Empty<FlashlightVisibility>();

        /// <summary>
        /// What the banner is showing, or null for nothing. Exposed so a test
        /// can assert the screen rather than the component's opinion of itself —
        /// which is what passed for the whole life of the stun stars while every
        /// triangle was being culled.
        /// </summary>
        public Status? Showing { get; private set; }

        public float Alpha => _group != null ? _group.alpha : 0f;

        public string LabelText => _label != null ? _label.text : string.Empty;

        /// <summary>
        /// Registered per match scene, not installed once at startup. The
        /// callback fires after the **first** scene loads, so anything built
        /// here belonged to the lobby and was destroyed by the load that opened
        /// the match — see <see cref="MatchSceneInstaller"/>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            MatchSceneInstaller.Register(Install);
        }

        private static void Install()
        {
            if (FindFirstObjectByType<PlayerStatusBannerView>() != null)
            {
                return;
            }

            new GameObject("Player Status Banner")
                .AddComponent<PlayerStatusBannerView>();
        }

        private void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the HUD, below the ink. Being stuck matters more than the
            // money counter and less than not being able to see at all.
            canvas.sortingOrder = 480;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var edgeObject = new GameObject("Edge");
            edgeObject.transform.SetParent(transform, false);
            _edge = edgeObject.AddComponent<Image>();
            _edge.raycastTarget = false;
            _edge.sprite = CreateEdgeSprite();
            RectTransform edgeRect = _edge.rectTransform;
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = Vector2.zero;
            edgeRect.offsetMax = Vector2.zero;

            var labelObject = new GameObject("Status");
            labelObject.transform.SetParent(transform, false);
            _label = labelObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font =
                Resources.Load<TMP_FontAsset>("PawliceAndPurrglarDefaultFont");
            if (font != null)
            {
                _label.font = font;
                _label.fontSharedMaterial = font.material;
            }

            _label.fontSize = 34f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;

            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 148f);

            // 1.45x the type size, because TMP draws nothing at all — not an
            // ellipsis, nothing — when the rect is shorter than one line.
            labelRect.sizeDelta = new Vector2(760f, 56f);
        }

        private void Update()
        {
            Refresh(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Split out so a test can step it without waiting on frames.
        /// </summary>
        public void Refresh(float deltaTime)
        {
            Showing = Resolve(out float remaining);

            if (Showing == null)
            {
                _group.alpha = Mathf.MoveTowards(
                    _group.alpha,
                    0f,
                    deltaTime / FadeSeconds);
                return;
            }

            Color tint = TintFor(Showing.Value);
            _edge.color = new Color(tint.r, tint.g, tint.b, EdgeAlpha);
            _label.color = tint;
            _label.text = TextFor(Showing.Value, remaining);

            _group.alpha = Mathf.MoveTowards(
                _group.alpha,
                1f,
                deltaTime / FadeSeconds);
        }

        private static Color TintFor(Status status)
        {
            return status switch
            {
                Status.Stuck => GlueColor,
                Status.Revealed => SensorColor,
                Status.Sprinting => SprintColor,
                _ => HideColor
            };
        }

        /// <summary>
        /// The number is there so the player can decide whether to keep pressing
        /// keys or accept it and plan the next three seconds. Hiding has no
        /// countdown because it has no duration — it ends when you leave.
        /// </summary>
        private static string TextFor(Status status, float remaining)
        {
            return status switch
            {
                Status.Stuck => $"끈끈이에 붙었다   {remaining:0.0}초",
                Status.Revealed => $"센서등에 발각됐다   {remaining:0.0}초",
                Status.Sprinting => $"경보 질주   {remaining:0.0}초",
                _ => "숨는 중   E로 나가기"
            };
        }

        /// <summary>
        /// Which of the four applies to the player at this screen, if any.
        /// </summary>
        private Status? Resolve(out float remaining)
        {
            remaining = 0f;
            if (!TryResolveLocalRole(out PlayerRole role))
            {
                return null;
            }

            EnsureCache(role);

            if (_stun != null
                && _stun.IsStunned
                && _stun.Cause == StunCause.Stuck)
            {
                remaining = _stun.RemainingSeconds;
                return Status.Stuck;
            }

            // Being revealed is recorded on the watcher, not on the person it
            // happens to — so the question is "is somebody who is not me
            // currently seeing me regardless of their torch", and the component
            // that knows is the other role's.
            foreach (FlashlightVisibility watcher in _watchers)
            {
                if (watcher != null
                    && watcher.ViewerRole != role
                    && watcher.IsRevealed)
                {
                    remaining = watcher.RevealRemainingSeconds;
                    return Status.Revealed;
                }
            }

            if (_motor != null && _motor.BoostRemainingSeconds > 0f)
            {
                remaining = _motor.BoostRemainingSeconds;
                return Status.Sprinting;
            }

            if (_hiding != null && _hiding.IsHiding)
            {
                return Status.Hiding;
            }

            return null;
        }

        /// <summary>
        /// Finds the local player's components once and keeps them.
        ///
        /// Re-searched only when the role changes, when something it holds has
        /// been destroyed, or on the retry interval when it found nothing —
        /// which covers the two cases that matter: the players not existing yet
        /// at scene load, and a rematch replacing them.
        ///
        /// <c>_hiding</c> is deliberately not a liveness probe. Only the thief
        /// has one, so treating null as "cache is stale" would make the
        /// officer's screen re-search the scene forever.
        /// </summary>
        private void EnsureCache(PlayerRole role)
        {
            bool roleChanged = !_hasCache || _cachedRole != role;
            bool broken = _stun == null || _motor == null;

            if (!broken)
            {
                foreach (FlashlightVisibility watcher in _watchers)
                {
                    if (watcher == null)
                    {
                        broken = true;
                        break;
                    }
                }
            }

            if (!roleChanged && !broken)
            {
                return;
            }

            // A changed role is re-read at once; only the "found nothing yet"
            // retry is throttled. Throttling both would leave the banner
            // watching the wrong character for up to half a second after the
            // lobby hands out roles, which is a long time in a chase.
            if (!roleChanged && Time.unscaledTime < _nextRescan)
            {
                return;
            }

            _nextRescan = Time.unscaledTime + RescanSeconds;
            _hasCache = true;
            _cachedRole = role;
            _stun = null;
            _motor = null;
            _hiding = null;

            // Both role objects exist on both machines, so taking the first one
            // found would banner the player who was not caught.
            foreach (PlayerRoleIdentity identity in
                FindObjectsByType<PlayerRoleIdentity>(FindObjectsSortMode.None))
            {
                if (identity.Role != role)
                {
                    continue;
                }

                // Each field filled independently, and no early exit.
                //
                // Stopping at the first object of this role that had *any* of
                // them was wrong wherever a scene holds more than one — a
                // half-populated leftover matched first, the loop broke, and the
                // banner then watched a character with no stun on it and
                // reported nothing for the whole match. Costing a full pass over
                // a handful of identities twice a second is the cheaper
                // mistake.
                _stun ??= identity.GetComponent<StunState>();
                _motor ??= identity.GetComponent<PlayerMovementMotor>();
                _hiding ??= identity.GetComponent<ThiefHidingState>();
            }

            _watchers = FindObjectsByType<FlashlightVisibility>(
                FindObjectsSortMode.None);
        }

        /// <summary>
        /// Which role this machine plays, asked the same way the sensor alert
        /// line asks it.
        ///
        /// The lobby's assignment first, because the server decided it and a
        /// scene selector that has not caught up would put the banner on the
        /// wrong screen for the first frames of a match — which is exactly when
        /// a trap is most likely to go off.
        /// </summary>
        private static bool TryResolveLocalRole(out PlayerRole role)
        {
            return LocalPlayerRoleSelector.TryResolveLocalRole(out role);
        }

        /// <summary>
        /// A frame: opaque at the border, clear in the middle.
        ///
        /// Generated for the same reason the ink is — no sprite exists, an
        /// import needs settings and a meta file, and this works unchanged in a
        /// WebGL build. Squared falloff so the tint hugs the edge instead of
        /// creeping toward the centre of the screen, where it would read as a
        /// colour grade rather than a warning.
        /// </summary>
        private static Sprite CreateEdgeSprite()
        {
            var texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false);
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize;
                    float v = (y + 0.5f) / TextureSize;
                    float toEdge = Mathf.Min(
                        Mathf.Min(u, 1f - u),
                        Mathf.Min(v, 1f - v));

                    // 0 at the border, 1 by a fifth of the way in.
                    float inward = Mathf.Clamp01(toEdge / 0.2f);
                    float coverage = 1f - (inward * inward);

                    pixels[(y * TextureSize) + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f));
        }
    }
}
