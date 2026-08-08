using PawsAndLoot.Logging;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Tells the two machines, in one sentence, that they are running different
    /// builds.
    ///
    /// <b>What goes wrong without this.</b> Every <c>NetworkObject</c> placed in
    /// the match scene carries a <c>GlobalObjectIdHash</c>, and that hash is a
    /// property of the saved scene file. `Game.unity` is regenerated from
    /// `GreyboxMapSetup` whenever the map changes, and regenerating it gives all
    /// 130 of them **new hashes**. Two builds made from different commits
    /// therefore disagree about the identity of every replicated object in the
    /// game.
    ///
    /// NGO's report of that is per-object and unreadable:
    ///
    /// <code>
    /// [Netcode] NetworkPrefab hash was not found! In-Scene placed NetworkObject
    ///           soft synchronization failure for Hash: 2322046117!
    /// [Netcode] [GlobalObjectIdHash=2322046117] Failed to spawn NetworkObject!
    /// NullReferenceException: Object reference not set to an instance of an object
    /// </code>
    ///
    /// Nothing in there says "different build". What the player experiences is
    /// that they cannot move — the failed object is `Police Player`, which
    /// carries the <c>NetworkPlayerLink</c> that movement input travels through —
    /// while the bag still opens, because the bag is local UI. That combination
    /// reads as a movement bug, and it is not one.
    ///
    /// <b>Why a plain MonoBehaviour.</b> A <c>NetworkBehaviour</c> here would be
    /// an in-scene <c>NetworkObject</c> itself, so it would fail to spawn for
    /// exactly the reason it exists to report. Named messages live on the
    /// <c>NetworkManager</c> and need no spawned object, so they still get
    /// through when everything else has not.
    ///
    /// <b>What it cannot do.</b> It reports; it cannot repair. Mismatched builds
    /// have no correct behaviour available — the fix is to put the same build on
    /// both machines. The value here is entirely that the message says so.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSceneFingerprint : MonoBehaviour
    {
        public const string MessageName = "PawsAndLoot.SceneFingerprint";

        /// <summary>
        /// Room for the version string. Written as a length-prefixed string, so
        /// the buffer has to allow for the longest one worth sending.
        /// </summary>
        private const int MessageBytes = 256;

        [SerializeField]
        private NetworkManager networkManager;

        private bool _registered;
        private bool _sent;
        private bool _reported;

        /// <summary>
        /// What the host said its build was, once it has said it. Empty until
        /// then. Exposed so a probe can record it next to the local one.
        /// </summary>
        public string RemoteBuild { get; private set; } = string.Empty;

        /// <summary>
        /// True once the two have been compared and found different. Latched,
        /// because the scene it describes does not recover.
        /// </summary>
        public bool Mismatched { get; private set; }

        public void Configure(NetworkManager manager)
        {
            networkManager = manager;
        }

        /// <summary>
        /// What identifies this build.
        ///
        /// The git commit, stamped into <c>PlayerSettings.bundleVersion</c> by
        /// <c>PlaytestBuild</c> at build time and read back here as
        /// <c>Application.version</c>.
        ///
        /// This replaced an attempt to fingerprint the scene's
        /// <c>GlobalObjectIdHash</c> values directly, which does not compile:
        /// that field is <c>internal</c> to the Netcode package. Comparing the
        /// commit is the better question anyway — "are we the same build" is
        /// what actually has to be true, and the scene hashes are only one of
        /// the things that go wrong when it is not.
        ///
        /// A build made outside <c>PlaytestBuild</c> (the editor, or Build
        /// Settings by hand) carries whatever version was there, so two such
        /// builds compare equal and this says nothing. That is a reported
        /// limitation rather than a hidden one: see <see cref="IsStamped"/>.
        /// </summary>
        public static string LocalBuild => Application.version;

        /// <summary>
        /// Whether this build carries a commit stamp at all. An unstamped build
        /// cannot be compared usefully, and saying so is better than comparing
        /// two placeholders and reporting agreement.
        /// </summary>
        public static bool IsStamped => Application.version.Contains("+");

        private void Update()
        {
            NetworkManager manager = ResolveManager();
            if (manager == null || !manager.IsListening)
            {
                return;
            }

            EnsureRegistered(manager);

            // The host announces, once, as soon as somebody is listening. Sent
            // on every new connection rather than only the first, because the
            // client that needs to hear it is whichever one just arrived.
            if (manager.IsServer
                && !_sent
                && manager.ConnectedClientsIds.Count > 0)
            {
                _sent = true;
                Announce(manager);
            }
        }

        private void Announce(NetworkManager manager)
        {
            using var writer = new FastBufferWriter(
                MessageBytes,
                Allocator.Temp);
            writer.WriteValueSafe(LocalBuild);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                MessageName,
                writer);
        }

        private void HandleFingerprint(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out string hostBuild);
            RemoteBuild = hostBuild;

            if (_reported || hostBuild == LocalBuild)
            {
                return;
            }

            _reported = true;

            if (!IsStamped || !hostBuild.Contains("+"))
            {
                // Said rather than skipped. An unstamped build is the case where
                // this check is blind, and a blind check that stays quiet is
                // indistinguishable from a passing one.
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "빌드 버전이 다르지만 한쪽이 커밋 도장이 없는 빌드입니다 "
                    + $"(이 기계 '{LocalBuild}', 호스트 '{hostBuild}'). "
                    + "에디터나 손으로 만든 빌드는 비교할 수 없습니다.",
                    this);
                return;
            }

            Mismatched = true;

            // One error with the two commits in it, rather than a hundred and
            // thirty from NGO with neither.
            GameLogger.Error(
                GameLogCategory.Network,
                "이 빌드와 호스트의 빌드가 다릅니다. 경기 씬의 복제 오브젝트를 "
                + "서로 다른 것으로 취급하므로 캐릭터가 움직이지 않습니다 "
                + "(가방처럼 로컬 UI만 동작합니다).\n"
                + $"  이 기계: {LocalBuild}\n"
                + $"  호스트:  {hostBuild}\n"
                + "  Game.unity를 재생성하면 in-scene NetworkObject 해시가 전부 "
                + "바뀝니다. 두 기계에 같은 빌드를 쓰세요.",
                this);
        }

        private NetworkManager ResolveManager()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            return networkManager;
        }

        private void EnsureRegistered(NetworkManager manager)
        {
            if (_registered || manager.CustomMessagingManager == null)
            {
                return;
            }

            _registered = true;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                MessageName,
                HandleFingerprint);
        }
    }
}
