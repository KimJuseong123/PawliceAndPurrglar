using PawsAndLoot.Config;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    /// <summary>
    /// Where the voice API actually is for this build.
    ///
    /// The configured URL is right for a desktop playtest and wrong for every
    /// deployed browser: a page served over https cannot call
    /// <c>http://localhost:3000</c>, and would not want to — the API is on the
    /// same machine that served the page, so the page's own origin is the
    /// answer and is the only answer that survives the server being renamed.
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
            string origin = OriginOf(Application.absoluteURL);
            if (!string.IsNullOrEmpty(origin))
            {
                GameLogger.InfoOnce(
                    GameLogCategory.Voice,
                    "voice-backend-origin",
                    $"Voice backend resolved to this page's origin: {origin}");
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
