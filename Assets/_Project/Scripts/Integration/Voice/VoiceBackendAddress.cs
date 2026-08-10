using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Voice
{
    /// <summary>
    /// Where the voice API actually is for this build.
    ///
    /// Two deployments serve the same build and only one of them also serves
    /// the API. On the EC2 host the page and the API share an origin; on GitHub
    /// Pages there is no API at that origin at all, and a page-origin guess
    /// sends every command to <c>https://user.github.io/api/game/…</c> — a 404
    /// that reads as "voice is broken" rather than "voice is somewhere else".
    ///
    /// So a configured absolute address wins when there is one. It is the
    /// deployer saying where the API is, which is knowledge this code cannot
    /// derive. The page origin remains the answer when nobody said — that is
    /// still right for a single-host deployment and survives a rename.
    ///
    /// <c>localhost</c> is deliberately not honoured from a browser: it is the
    /// desktop-playtest default that ships in the asset, and a page on https
    /// cannot call it. Treating it as "nothing was configured" is what keeps
    /// one config asset usable on both platforms.
    ///
    /// Resolved here rather than in <see cref="VoiceConfig"/> because a config
    /// asset is data. What is stored is what a person typed; which of several
    /// stored values applies to the machine running right now is a question
    /// about the platform, and belongs on this side of the boundary.
    /// </summary>
    public static class VoiceBackendAddress
    {
        /// <summary>
        /// The base URL to send voice traffic to, without a trailing slash.
        /// </summary>
        public static string Resolve(VoiceConfig config)
        {
            string configured = config != null
                ? config.BackendBaseUrl
                : string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
            string deployed = OriginOf(configured);
            if (!string.IsNullOrEmpty(deployed) && !IsLoopback(deployed))
            {
                GameLogger.InfoOnce(
                    GameLogCategory.Voice,
                    "voice-backend-configured",
                    $"Voice backend taken from configuration: {deployed}. The "
                    + "page's own origin is not used, so this build works from "
                    + "a host that only serves the page.");
                return Trim(configured);
            }

            string origin = OriginOf(Application.absoluteURL);
            if (!string.IsNullOrEmpty(origin))
            {
                GameLogger.InfoOnce(
                    GameLogCategory.Voice,
                    "voice-backend-origin",
                    $"Voice backend resolved to this page's origin: {origin}. "
                    + "Nothing deployable was configured.");
                return origin;
            }

            GameLogger.WarningOnce(
                GameLogCategory.Voice,
                "voice-backend-origin-missing",
                "This page reported no address, so the voice backend falls "
                + $"back to the configured '{configured}'. On https that call "
                + "will be blocked.");
#endif
            return Trim(configured);
        }

        /// <summary>
        /// Whether an origin points back at the machine running the browser.
        ///
        /// Only ever true for a developer's own playtest, and never useful from
        /// a deployed page — the player's machine is not running the API.
        /// </summary>
        public static bool IsLoopback(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return false;
            }

            string lowered = origin.ToLowerInvariant();
            return lowered.Contains("://localhost")
                || lowered.Contains("://127.0.0.1")
                || lowered.Contains("://[::1]")
                || lowered.Contains("://0.0.0.0");
        }

        /// <summary>
        /// Scheme and authority of a URL, dropping the path, query and
        /// fragment: <c>https://example.com/Build/index.html</c> becomes
        /// <c>https://example.com</c>.
        ///
        /// Returns empty for anything that is not an absolute http(s) URL. A
        /// half-parsed origin would be worse than none — it would produce a
        /// request that goes somewhere, and the somewhere would be wrong.
        /// </summary>
        public static string OriginOf(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string value = url.Trim();
            int scheme = value.IndexOf("://", System.StringComparison.Ordinal);
            if (scheme <= 0)
            {
                return string.Empty;
            }

            string protocol = value[..scheme].ToLowerInvariant();
            if (protocol != "http" && protocol != "https")
            {
                return string.Empty;
            }

            int authorityStart = scheme + 3;
            int slash = value.IndexOf('/', authorityStart);
            string authority = slash < 0
                ? value[authorityStart..]
                : value[authorityStart..slash];

            // A query or fragment can sit directly against the authority when
            // there is no path at all.
            foreach (char terminator in new[] { '?', '#' })
            {
                int cut = authority.IndexOf(terminator);
                if (cut >= 0)
                {
                    authority = authority[..cut];
                }
            }

            return authority.Length == 0
                ? string.Empty
                : $"{protocol}://{authority}";
        }

        private static string Trim(string url)
        {
            return string.IsNullOrWhiteSpace(url)
                ? string.Empty
                : url.Trim().TrimEnd('/');
        }
    }
}
