using System.Runtime.InteropServices;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Puts a string on the player's clipboard.
    ///
    /// Two implementations, because there are two clipboards. On the desktop
    /// <c>GUIUtility.systemCopyBuffer</c> is the operating system's; in a
    /// browser it is a buffer inside the Unity player that the page never sees,
    /// so the copy has to be done in JavaScript. Calling the desktop one in a
    /// browser fails silently and completely — the player presses 코드 복사, the
    /// lobby says it worked, and nothing is on the clipboard.
    /// </summary>
    public static class ClipboardBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int PawliceAndPurrglarCopyText(string text);
#endif

        /// <summary>
        /// Must be called from inside the click that asked for it. Browsers
        /// refuse a clipboard write that is not attributable to a user gesture,
        /// and they refuse it without raising anything.
        /// </summary>
        public static bool Copy(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return PawliceAndPurrglarCopyText(text) != 0;
#else
            try
            {
                GUIUtility.systemCopyBuffer = text;
                return true;
            }
            catch (System.Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Network,
                    exception,
                    "The clipboard refused the invite code.");
                return false;
            }
#endif
        }
    }
}
