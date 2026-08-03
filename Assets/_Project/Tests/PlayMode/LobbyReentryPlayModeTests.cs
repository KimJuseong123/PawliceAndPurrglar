using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Integration.Network;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Returning to the lobby after a match and starting a second one.
    ///
    /// This failed for a reason nothing in the project guarded against.
    /// <c>NetworkManager.OnEnable</c> calls <c>DontDestroyOnLoad</c> on itself
    /// unconditionally — not only once a session starts — and only claims
    /// <c>Singleton</c> when it is still null. Loading Bootstrap a second time
    /// therefore leaves two managers alive, with the stale one still the
    /// singleton.
    ///
    /// The lobby then starts its host on the fresh manager, but
    /// <c>NetworkObject.NetworkManager</c> falls back to the singleton when no
    /// owner is set, so spawning the role board reported
    /// "NetworkManagerOwner is not listening" and roles were never assigned.
    ///
    /// The scene reload is simulated rather than performed: what matters is a
    /// second manager existing while the first is the singleton, and building
    /// that directly keeps the case fast and free of scene-load ordering.
    /// </summary>
    public sealed class LobbyReentryPlayModeTests
    {
        /// <summary>
        /// Above the default so a leftover session from another case cannot make
        /// this one look like a bind failure.
        /// </summary>
        private const string Port = "7987";

        private GameObject _stale;
        private GameObject _fresh;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject held in new[] { _stale, _fresh })
            {
                if (held == null)
                {
                    continue;
                }

                var manager = held.GetComponent<NetworkManager>();
                if (manager != null && manager.IsListening)
                {
                    manager.Shutdown();
                }

                Object.DestroyImmediate(held);
            }

            _stale = null;
            _fresh = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturningToTheLobbyLeavesOneManagerOwningTheSingleton()
        {
            NetworkSessionController stale = CreateSession("Stale NetworkManager");
            _stale = stale.gameObject;
            yield return null;

            Assert.That(
                stale.TryStartHost(Port),
                Is.True,
                "The first host could not start, so the case cannot say "
                + "anything about the second.");
            yield return null;

            // The Bootstrap scene is loaded again. Its own manager awakes while
            // the previous one is still alive in DontDestroyOnLoad.
            NetworkSessionController fresh = CreateSession("Fresh NetworkManager");
            _fresh = fresh.gameObject;
            yield return null;

            Assert.That(
                Object.FindObjectsByType<NetworkManager>(
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(2),
                "Two managers is the situation under test.");

            // The defect itself, asserted before it is repaired: the manager the
            // new lobby does not use is the one every NetworkObject will resolve
            // to. Without this the case could pass for the wrong reason.
            Assert.That(
                NetworkManager.Singleton,
                Is.EqualTo(stale.GetComponent<NetworkManager>()),
                "The stale manager was expected to still own the singleton; if "
                + "Netcode stops doing that, this case no longer reproduces "
                + "anything and should be revisited.");

            LobbySessionReset.Run(fresh);
            // A frame for the destroyed manager's OnDisable and OnDestroy, which
            // is where NGO releases the singleton and the scene bridge handlers
            // are cleared.
            yield return null;
            yield return null;

            Assert.That(
                Object.FindObjectsByType<NetworkManager>(
                    FindObjectsSortMode.None)
                    .Count(manager => manager != null),
                Is.EqualTo(1),
                "The stale manager was left alive.");
            Assert.That(
                NetworkManager.Singleton,
                Is.EqualTo(fresh.GetComponent<NetworkManager>()),
                "The lobby's own manager must own the singleton, or every "
                + "NetworkObject it spawns resolves to the stale one and "
                + "reports that its owner is not listening.");
            Assert.That(
                fresh.Mode,
                Is.EqualTo(NetworkSessionController.SessionMode.Offline),
                "The fresh lobby must start offline.");
        }

        /// <summary>
        /// The point of the whole thing: a second match can be hosted.
        /// </summary>
        [UnityTest]
        public IEnumerator SecondHostStartSucceedsAfterReturningToTheLobby()
        {
            NetworkSessionController stale = CreateSession("Stale NetworkManager");
            _stale = stale.gameObject;
            yield return null;
            Assert.That(stale.TryStartHost(Port), Is.True);
            yield return null;

            NetworkSessionController fresh = CreateSession("Fresh NetworkManager");
            _fresh = fresh.gameObject;
            yield return null;

            LobbySessionReset.Run(fresh);
            yield return null;
            yield return null;

            Assert.That(
                fresh.TryStartHost(Port),
                Is.True,
                $"The second host failed: {fresh.LastStatus}");
            yield return null;

            Assert.That(
                fresh.GetComponent<NetworkManager>().IsListening,
                Is.True);
            Assert.That(
                fresh.Mode,
                Is.EqualTo(NetworkSessionController.SessionMode.Host));
        }

        private static NetworkSessionController CreateSession(string name)
        {
            var host = new GameObject(name);
            UnityTransport transport = host.AddComponent<UnityTransport>();
            NetworkManager manager = host.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = null,
                TickRate = 30,
                EnableSceneManagement = false,
                ForceSamePrefabs = false
            };
            transport.SetConnectionData(
                LocalAddressProvider.LoopbackAddress,
                ushort.Parse(Port));

            NetworkSessionController session =
                host.AddComponent<NetworkSessionController>();
            session.Configure(manager);
            return session;
        }
    }
}
