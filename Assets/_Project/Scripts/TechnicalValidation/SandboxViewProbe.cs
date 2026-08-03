using System.IO;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Photographs the sandbox from inside the running build and quits.
    ///
    /// The sandbox already has two ways to be looked at — the plan view from
    /// straight above and the contact sheet of the models — and both of them
    /// lied about the same change on the same afternoon. The plan view is
    /// rendered by an editor camera in an editor process with editor lighting;
    /// it answers "where is everything" very well and "what does it look like"
    /// not at all.
    ///
    /// Screen-scraping the window from outside was the other attempt, and it
    /// photographed whatever happened to be in front. The build taking its own
    /// picture cannot photograph the wrong thing.
    ///
    /// Gated on an explicit argument so a normal run is untouched:
    ///
    ///     MapSandbox.exe -sandboxShot Logs/sandbox-ingame.png -shotSeconds 4
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxViewProbe : MonoBehaviour
    {
        private const string PathArgument = "-sandboxShot";
        private const string DelayArgument = "-shotSeconds";

        /// <summary>
        /// How long to wait before the shutter, in seconds.
        ///
        /// Not zero, and not one frame. Textures stream in, the character drops
        /// the last few centimetres onto the ground, and shadows settle; a
        /// picture taken on frame one shows a town that never exists.
        /// </summary>
        private const float DefaultDelaySeconds = 4f;

        private string _destination;
        private float _remaining;
        private bool _taken;

        private void Awake()
        {
            _destination = Argument(PathArgument);
            if (string.IsNullOrEmpty(_destination))
            {
                enabled = false;
                return;
            }

            _remaining = DefaultDelaySeconds;
            string given = Argument(DelayArgument);
            if (!string.IsNullOrEmpty(given)
                && float.TryParse(given, out float seconds))
            {
                _remaining = Mathf.Max(0.1f, seconds);
            }
        }

        private void Update()
        {
            if (_taken)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            _taken = true;

            string full = Path.GetFullPath(_destination);
            string folder = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            ScreenCapture.CaptureScreenshot(full);

            // Given a moment before quitting. CaptureScreenshot is asked for at
            // the end of the frame rather than done here, so quitting in this
            // call would take the picture with it.
            Invoke(nameof(Finish), 1.5f);
        }

        private void Finish()
        {
            Debug.Log(
                $"[SANDBOX] View written to {Path.GetFullPath(_destination)}.");
            Application.Quit();
        }

        private static string Argument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(
                        args[index],
                        name,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
