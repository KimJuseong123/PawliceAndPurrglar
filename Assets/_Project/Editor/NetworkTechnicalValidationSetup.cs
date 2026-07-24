using System;
using System.IO;
using PawsAndLoot.Core;
using PawsAndLoot.TechnicalValidation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    public static class NetworkTechnicalValidationSetup
    {
        public const string PlayerPrefabPath =
            "Assets/_Project/Prefabs/TechnicalValidation/NetworkPlayer.prefab";
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawsAndLootNetworkTech.exe";

        private const string PlayerMaterialPath =
            "Assets/_Project/Materials/TechnicalValidation/NetworkPlayer.mat";
        private const string NetworkPrefabsListPath =
            "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Paws & Loot/Technical Validation/Create NET-001 Scene")]
        public static void CreateScene()
        {
            GameObject playerPrefab = CreatePlayerPrefab();
            NetworkPrefabsList prefabList =
                PrepareNetworkPrefabsList(playerPrefab);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor();
            NetworkManager manager =
                CreateNetworkManager(playerPrefab, prefabList);

            var probeObject = new GameObject("NET-001 Connection Probe");
            NetworkConnectionProbe probe =
                probeObject.AddComponent<NetworkConnectionProbe>();
            probe.NetworkManager = manager;

            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.NetworkTechnicalTest);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save network validation scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
        }

        [MenuItem("Paws & Loot/Technical Validation/Validate NET-001 Scene")]
        public static void ValidateScene()
        {
            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.NetworkTechnicalTest);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"Network validation scene is missing: {scenePath}",
                    scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            NetworkManager manager = FindInScene<NetworkManager>(scene);
            NetworkConnectionProbe probe =
                FindInScene<NetworkConnectionProbe>(scene);
            UnityTransport transport = manager == null
                ? null
                : manager.GetComponent<UnityTransport>();
            NetworkPrefabsList prefabList =
                manager?.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 1
                    ? manager.NetworkConfig.Prefabs.NetworkPrefabsLists[0]
                    : null;

            if (scene.name != "NetworkTechnicalTest"
                || Camera.main == null
                || manager == null
                || probe == null
                || transport == null
                || manager.NetworkConfig.PlayerPrefab == null
                || prefabList == null
                || !prefabList.Contains(manager.NetworkConfig.PlayerPrefab)
                || manager.NetworkConfig.EnableSceneManagement)
            {
                throw new InvalidOperationException(
                    "NET-001 scene is missing its manager, transport, player prefab, or probe.");
            }
        }

        [MenuItem("Paws & Loot/Technical Validation/Build Windows NET-001")]
        public static void BuildWindows()
        {
            CreateScene();

            string absoluteBuildPath = Path.GetFullPath(WindowsBuildPath);
            string buildDirectory = Path.GetDirectoryName(absoluteBuildPath);
            if (string.IsNullOrWhiteSpace(buildDirectory))
            {
                throw new InvalidOperationException(
                    $"Could not determine build directory for '{absoluteBuildPath}'.");
            }

            Directory.CreateDirectory(buildDirectory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        GameSceneCatalog.GetPath(GameSceneId.NetworkTechnicalTest)
                    },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"NET-001 Windows build failed with result {report.summary.result} " +
                    $"and {report.summary.totalErrors} errors.");
            }
        }

        [MenuItem("Paws & Loot/Technical Validation/Build Windows NET-002")]
        public static void BuildWindowsRoleValidation()
        {
            BuildWindows();
        }

        private static GameObject CreatePlayerPrefab()
        {
            Material material = LoadOrCreateMaterial();
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "NetworkPlayer";
            player.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
            player.GetComponent<Renderer>().sharedMaterial = material;
            player.AddComponent<NetworkObject>();
            player.AddComponent<TechnicalNetworkPlayer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                player,
                PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Could not save network player prefab: {PlayerPrefabPath}");
            }

            return prefab;
        }

        private static NetworkManager CreateNetworkManager(
            GameObject playerPrefab,
            NetworkPrefabsList prefabList)
        {
            var managerObject = new GameObject("NetworkManager");
            UnityTransport transport =
                managerObject.AddComponent<UnityTransport>();
            NetworkManager manager =
                managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                TickRate = 30,
                ConnectionApproval = false,
                EnableSceneManagement = false,
                ForceSamePrefabs = true,
                EnableNetworkLogs = true
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            return manager;
        }

        private static NetworkPrefabsList PrepareNetworkPrefabsList(
            GameObject playerPrefab)
        {
            NetworkPrefabsList prefabList =
                AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                    NetworkPrefabsListPath);
            if (prefabList == null)
            {
                prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(prefabList, NetworkPrefabsListPath);
            }

            if (!prefabList.Contains(playerPrefab))
            {
                prefabList.Add(new NetworkPrefab { Prefab = playerPrefab });
            }

            EditorUtility.SetDirty(prefabList);
            return prefabList;
        }

        private static Material LoadOrCreateMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "URP Lit shader is unavailable for NET-001.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PlayerMaterialPath);
            }

            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateCamera()
        {
            var cameraObject =
                new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 7f, -8f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.6f, 0f));

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            camera.fieldOfView = 48f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(14f, 0.3f, 10f);
            floor.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/TechnicalValidation/Floor.mat");
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
