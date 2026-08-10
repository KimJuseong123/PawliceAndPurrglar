using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Ink across the screen while the frozen octopus is stuck to your face.
    ///
    /// `BlindedState` has worked since `ITEM-008` and **said nothing** — the prop
    /// took 2.2 seconds of sight and the screen never changed, so the only way to
    /// know you had been hit was to notice you were guessing. A prop whose entire
    /// value is "you cannot see" needs the not-seeing to be the visible part.
    ///
    /// The splatter is generated rather than authored: no octopus-ink sprite
    /// exists, a texture built here needs no artist and no import settings, and
    /// it works in a WebGL build unchanged. Real art can replace
    /// <see cref="CreateSplatter"/> without touching anything else.
    ///
    /// Deliberately **not** total. A fully black screen reads as a bug or a scene
    /// load, and a blinded player who cannot tell they are still alive stops
    /// playing rather than guessing — which is the interesting part. Gaps near
    /// the edges leave enough peripheral vision to keep running.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkBlindOverlayView : MonoBehaviour
    {
        private const int TextureSize = 256;

        /// <summary>
        /// How quickly the ink arrives. Fast enough to feel like an impact, slow
        /// enough that the frame it lands on is not simply a cut.
        /// </summary>
        private const float SplashSeconds = 0.12f;

        /// <summary>
        /// The ink thins over the last stretch instead of vanishing, so sight
        /// returns as a warning rather than a surprise.
        /// </summary>
        private const float FadeSeconds = 0.6f;

        private BlindedState _blinded;
        private Image _image;
        private CanvasGroup _group;
        private float _peakAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<InkBlindOverlayView>() != null)
            {
                return;
            }

            new GameObject("Ink Blind Overlay")
                .AddComponent<InkBlindOverlayView>();
        }

        private void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the HUD. Being blinded outranks knowing how much money you
            // have, and ink behind the inventory panel would be a curiosity
            // rather than a handicap.
            canvas.sortingOrder = 500;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Never eats a click. The prop takes sight, not control — a player
            // who cannot see but can still act is the whole design.
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var child = new GameObject("Ink");
            child.transform.SetParent(transform, false);
            _image = child.AddComponent<Image>();
            _image.raycastTarget = false;
            _image.sprite = CreateSplatter();
            _image.color = new Color(0.04f, 0.03f, 0.07f, 1f);

            RectTransform rect = _image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (_blinded == null)
            {
                _blinded = ResolveLocalBlindedState();
                if (_blinded == null)
                {
                    return;
                }
            }

            if (!_blinded.IsBlinded)
            {
                if (_group.alpha > 0f)
                {
                    _group.alpha = Mathf.MoveTowards(
                        _group.alpha,
                        0f,
                        Time.unscaledDeltaTime / FadeSeconds);
                }

                _peakAlpha = 0f;
                return;
            }

            // A fresh hit re-rolls the splatter so two octopuses in a row do not
            // land in exactly the same shape — the second would read as the
            // first still being there.
            if (_peakAlpha <= 0f)
            {
                _image.rectTransform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Random.Range(0, 4) * 90f);
                _image.rectTransform.localScale = new Vector3(
                    Random.value < 0.5f ? -1f : 1f,
                    1f,
                    1f);
                _peakAlpha = 0.94f;
            }

            float remaining = _blinded.RemainingSeconds;
            float target = remaining < FadeSeconds
                ? Mathf.Lerp(0f, _peakAlpha, remaining / FadeSeconds)
                : _peakAlpha;

            _group.alpha = Mathf.MoveTowards(
                _group.alpha,
                target,
                Time.unscaledDeltaTime / SplashSeconds);
        }

        /// <summary>
        /// The blinded state belonging to the role this machine plays.
        ///
        /// Both role objects exist on both machines, so taking the first one
        /// found would put ink on the screen of whoever was *not* hit — the same
        /// mistake the match-end sting made by following the winner instead of
        /// the viewer.
        /// </summary>
        private BlindedState ResolveLocalBlindedState()
        {
            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            if (selector == null)
            {
                return null;
            }

            foreach (BlindedState candidate in
                FindObjectsByType<BlindedState>(FindObjectsSortMode.None))
            {
                PlayerRoleIdentity identity =
                    candidate.GetComponent<PlayerRoleIdentity>();
                if (identity != null && identity.Role == selector.ActiveRole)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// A splatter built from overlapping blobs.
        ///
        /// Blobs rather than one shape because ink that hits a face spreads
        /// unevenly, and a single soft ellipse reads as a vignette — a lighting
        /// effect, not something thrown at you. The centre is covered hardest and
        /// the corners are left thinnest, which is also where a player most needs
        /// a sliver of vision to keep moving.
        /// </summary>
        private static Sprite CreateSplatter()
        {
            var texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false);
            var pixels = new Color32[TextureSize * TextureSize];

            // Fixed seed: the shape is re-used with rotation and mirroring for
            // variety, and a texture that differed per session would make a bug
            // report impossible to reproduce.
            var random = new System.Random(20260807);
            const int blobCount = 26;
            var centres = new Vector2[blobCount];
            var radii = new float[blobCount];
            for (int i = 0; i < blobCount; i++)
            {
                double angle = random.NextDouble() * System.Math.PI * 2.0;
                double spread = System.Math.Pow(random.NextDouble(), 0.6) * 0.46;
                centres[i] = new Vector2(
                    0.5f + (float)(System.Math.Cos(angle) * spread),
                    0.5f + (float)(System.Math.Sin(angle) * spread));
                radii[i] = 0.10f + (float)random.NextDouble() * 0.22f;
            }

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var point = new Vector2(
                        (x + 0.5f) / TextureSize,
                        (y + 0.5f) / TextureSize);

                    float coverage = 0f;
                    for (int i = 0; i < blobCount; i++)
                    {
                        float distance = Vector2.Distance(point, centres[i]);
                        // Soft falloff so blob edges melt together instead of
                        // showing as circles.
                        coverage = Mathf.Max(
                            coverage,
                            Mathf.SmoothStep(1f, 0f, distance / radii[i]));
                    }

                    pixels[y * TextureSize + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f));
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
