using PawsAndLoot.Core;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the direct-IP lobby into the Bootstrap scene.
    ///
    /// Host authority over direct IP (DEC-027), so the screen has to show this
    /// machine's address and take the partner's. No Relay, no discovery service
    /// beyond the LAN advert.
    ///
    /// The interface itself is a prefab. This used to build it inline: an
    /// authored mockup stretched across the screen with transparent buttons
    /// pinned over it at fixed pixel offsets, which only lined up at the
    /// mockup's own 4:3.
    /// </summary>
    internal static class NetworkLobbySetup
    {
        private const string NetworkPrefabsListPath =
            "Assets/DefaultNetworkPrefabs.asset";

        private const string LobbyRootName = "LobbyCanvas";

        /// <summary>
        /// The scene's older title canvas. Its dark backdrop and the two accent
        /// bars are what showed through as black margins beside the lobby, so
        /// it is switched off rather than drawn behind an opaque lobby.
        /// </summary>
        private const string LegacyInterfaceRootName = "Scene UI";

        private static readonly Color LobbyBackdrop =
            new(0.949f, 0.898f, 0.855f, 1f);

        public static void Build(GameSceneId sceneId)
        {
            if (sceneId != GameSceneId.Bootstrap)
            {
                return;
            }

            GameObject boardPrefab = CreateRoleBoardPrefab();
            NetworkManager manager = CreateNetworkManager(boardPrefab);
            NetworkSessionController session =
                manager.gameObject
                    .AddComponent<NetworkSessionController>();
            session.Configure(manager, boardPrefab);

            // Sits there doing nothing unless the build was launched with
            // -dedicatedServer. On the same object as the session it drives, so
            // it cannot end up in a scene without one.
            manager.gameObject.AddComponent<DedicatedServerLauncher>();

            // Routes match scene loads through NGO while a session runs, so
            // both machines resolve the same in-scene NetworkObjects.
            NetworkSceneCoordinator coordinator =
                manager.gameObject
                    .AddComponent<NetworkSceneCoordinator>();
            coordinator.Configure(manager);

            // NET-008. On the NetworkManager object so it survives the scene
            // change: rematch is pressed on the result screen, and a scene
            // NetworkObject would already be gone by then (ISSUE-016).
            manager.gameObject
                .AddComponent<NetworkRematchCoordinator>()
                .Configure(manager);

            // The verdict travels the same road, and for the same reason. It is
            // decided in the match scene at the moment that scene starts
            // unloading, so anything living there is being destroyed as the
            // value changes — a NetworkVariable on it never reaches the client.
            manager.gameObject
                .AddComponent<NetworkMatchResultMessenger>();

            // NET-009. One handler for the whole session, in the one place that
            // outlives every scene load.
            manager.gameObject
                .AddComponent<NetworkDisconnectHandler>()
                .Configure(manager, session);

            // Announces this host on the LAN and lists the ones it hears, so the
            // two players can meet without reading an IP to each other.
            LanRoomDirectory directory =
                manager.gameObject.AddComponent<LanRoomDirectory>();
            directory.Configure(session);

            // Netcode keeps a NetworkManager alive across scene loads whether or
            // not a session ever started, so returning to the lobby used to
            // leave two of them running with the stale one still holding the
            // static singleton. Everything this lobby spawned then resolved to
            // that one and reported that its owner was not listening.
            manager.gameObject
                .AddComponent<LobbySessionReset>()
                .Configure(session);

            // NET-008 verification. On the persistent object because the press
            // happens on the result screen and the restart lands in the match
            // scene; no single scene sees both ends.
            manager.gameObject.AddComponent<
                PawsAndLoot.TechnicalValidation.NetworkRematchProbe>();

            // Command-line driven verification, inert without -netLobby.
            var probeObject = new GameObject("Network Lobby Probe");
            probeObject.AddComponent<
                PawsAndLoot.TechnicalValidation.NetworkLobbyProbe>();

            InstallInterface(session, directory);
            DeactivateLegacyInterface();
            RecolourCamera();
        }

        [MenuItem("Paws & Loot/Setup/Rebuild Bootstrap Lobby")]
        public static void RebuildBootstrapLobby()
        {
            string path = GameSceneCatalog.GetPath(GameSceneId.Bootstrap);
            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);
            DestroyRoot(scene, LobbyRootName);
            // The name the inline builder used. Removed too, so rebuilding an
            // older scene does not leave two lobbies stacked on each other.
            DestroyRoot(scene, "Lobby UI");
            DestroyRoot(scene, "NetworkManager");
            DestroyRoot(scene, "Network Lobby Probe");
            Build(GameSceneId.Bootstrap);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new System.InvalidOperationException(
                    $"Failed to save scene: {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Bootstrap lobby rebuilt.");
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    return;
                }
            }
        }

        private const string RoleBoardPrefabPath =
            "Assets/_Project/Prefabs/Network/NetworkRoleBoard.prefab";

        /// <summary>
        /// The role board has to be a registered prefab rather than a scene
        /// object, because with scene management disabled NGO asks the client to
        /// build in-scene NetworkObjects out of the prefab list and drops the
        /// connection when the lookup fails.
        /// </summary>
        private static GameObject CreateRoleBoardPrefab()
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    RoleBoardPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            string directory = "Assets/_Project/Prefabs/Network";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetFullPath(directory));
                AssetDatabase.Refresh();
            }

            var source = new GameObject("NetworkRoleBoard");
            source.AddComponent<NetworkObject>();
            source.AddComponent<NetworkRoleBoard>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                source,
                RoleBoardPrefabPath);
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static NetworkManager CreateNetworkManager(
            GameObject roleBoardPrefab)
        {
            var managerObject = new GameObject("NetworkManager");
            UnityTransport transport =
                managerObject.AddComponent<UnityTransport>();
            NetworkManager manager =
                managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = null,
                TickRate = 30,
                // Approval must be identical on both sides: NGO hashes the
                // config and drops a client whose flags differ, so flipping
                // this only on the host silently disconnected every join.
                // The host installs the callback, the client just carries the
                // same flag.
                ConnectionApproval = true,
                // NET-003 and NET-004 keep the players, loot and arrest objects
                // in the scene, so the server has to drive scene loads for
                // clients to resolve them at all.
                EnableSceneManagement = true,
                ForceSamePrefabs = true,
                EnableNetworkLogs = true
            };

            NetworkPrefabsList prefabList =
                AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                    NetworkPrefabsListPath);
            if (prefabList == null)
            {
                prefabList =
                    ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(
                    prefabList,
                    NetworkPrefabsListPath);
            }

            if (roleBoardPrefab != null
                && !prefabList.Contains(roleBoardPrefab))
            {
                prefabList.Add(
                    new NetworkPrefab { Prefab = roleBoardPrefab });
            }

            EditorUtility.SetDirty(prefabList);
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(
                prefabList);

            transport.SetConnectionData(
                LocalAddressProvider.LoopbackAddress,
                NetworkSessionController.DefaultPort);
            return manager;
        }

        /// <summary>
        /// Drops the lobby prefab into the scene and hands it the two scene
        /// services it cannot reference from an asset.
        /// </summary>
        private static void InstallInterface(
            NetworkSessionController session,
            LanRoomDirectory directory)
        {
            GameObject prefab = LoadLobbyPrefab();
            var instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new System.InvalidOperationException(
                    $"Could not instantiate the lobby prefab: "
                    + $"{LobbyCanvasBuilder.PrefabPath}");
            }

            instance.name = LobbyRootName;

            var presenter = instance.GetComponent<NetworkLobbyPresenter>();
            if (presenter == null)
            {
                throw new System.InvalidOperationException(
                    "The lobby prefab has no NetworkLobbyPresenter. Rebuild it "
                    + "with 'Paws & Loot/UI/Rebuild Lobby Canvas Prefab'.");
            }

            presenter.ConfigureSession(session, directory);
            // Without this the scene references written above are not recorded
            // as prefab instance overrides and are gone on the next load.
            PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
            EditorUtility.SetDirty(instance);
        }

        private static GameObject LoadLobbyPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                LobbyCanvasBuilder.PrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            // Generated on demand so a fresh clone can rebuild the scenes in one
            // step rather than failing on a missing asset.
            LobbyCanvasBuilder.Rebuild();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                LobbyCanvasBuilder.PrefabPath);
            if (prefab == null)
            {
                throw new System.IO.FileNotFoundException(
                    "The lobby prefab is missing and could not be generated.",
                    LobbyCanvasBuilder.PrefabPath);
            }

            return prefab;
        }

        /// <summary>
        /// Switches off the scene's older title canvas.
        ///
        /// Left on, its dark backdrop and the two accent bars filled everything
        /// the lobby did not cover, which is where the black margins in the
        /// built lobby came from. It stays in the scene rather than being
        /// deleted because the scene contract requires a canvas and a
        /// navigation button to the match, and that check counts inactive
        /// objects.
        /// </summary>
        private static void DeactivateLegacyInterface()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == LegacyInterfaceRootName && root.activeSelf)
                {
                    root.SetActive(false);
                    EditorUtility.SetDirty(root);
                    return;
                }
            }
        }

        /// <summary>
        /// The camera clears to the lobby's own ivory. The lobby draws an opaque
        /// backdrop over the whole canvas anyway, but a mismatched clear colour
        /// shows for a frame on load and in any editor view that is not playing.
        /// </summary>
        private static void RecolourCamera()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                {
                    continue;
                }

                camera.backgroundColor = LobbyBackdrop;
                EditorUtility.SetDirty(camera);
                return;
            }
        }
    }
}
