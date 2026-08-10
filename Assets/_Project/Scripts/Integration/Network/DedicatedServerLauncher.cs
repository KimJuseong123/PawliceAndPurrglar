using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// Starts the session as a server with nobody playing on it.
    ///
    /// A host is a player who happens to be authoritative. A server is not a
    /// player at all: it owns the simulation, both characters are driven by
    /// clients, and there is no screen. That is what a machine in a data centre
    /// has to be, and it is the one shape the game could not take.
    ///
    /// Off unless asked for by <c>-dedicatedServer</c>. A build that decided for
    /// itself would turn every double-click into a headless server, and the
    /// thing the player sees is a window that never opens.
    ///
    /// Deliberately separate from the probes. They also start sessions from the
    /// command line, and they do it to measure something and quit; this starts
    /// one to keep it running. Sharing an argument between "run a test" and "be
    /// the server" is how a deploy ends up quitting after sixty seconds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DedicatedServerLauncher : MonoBehaviour
    {
        public const string ServerArgument = "-dedicatedServer";
        public const string PortArgument = "-netPort";

        [SerializeField]
        private NetworkSessionController session;

        public static bool IsRequested => FindArgument(ServerArgument) != null;

        public bool Started { get; private set; }

        private void Start()
        {
            if (!IsRequested)
            {
                return;
            }

            session ??= FindFirstObjectByType<NetworkSessionController>();
            if (session == null)
            {
                GameLogger.Error(
                    GameLogCategory.Network,
                    "-dedicatedServer was given and there is no session "
                    + "controller in this scene, so nothing can be started.",
                    this);
                return;
            }

            string port = FindArgument(PortArgument)
                ?? NetworkSessionController.DefaultPort.ToString();

            // A server with no screen still renders every frame unless told not
            // to, which on a rented machine is money spent drawing to nobody.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            Started = session.TryStartServer(port);
            GameLogger.Info(
                GameLogCategory.Network,
                Started
                    ? $"Dedicated server listening on port {port}."
                    : "Dedicated server failed to start.",
                this);
        }

        /// <summary>
        /// The value after a named argument, or null when it is absent. Returns
        /// an empty string for a flag given with nothing after it, so "present"
        /// and "absent" stay different answers.
        /// </summary>
        private static string FindArgument(string name)
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        name,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return index + 1 < arguments.Length
                    && !arguments[index + 1].StartsWith('-')
                        ? arguments[index + 1]
                        : string.Empty;
            }

            return null;
        }
    }
}
