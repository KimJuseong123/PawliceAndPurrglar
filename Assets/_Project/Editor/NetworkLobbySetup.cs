using PawsAndLoot.Core;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the direct-IP lobby into the Bootstrap scene.
    ///
    /// Host authority over direct IP (DEC-027), so the screen has to show this
    /// machine's address and take the partner's. No Relay, no discovery.
    /// </summary>
    internal static class NetworkLobbySetup
    {
        private const string NetworkPrefabsListPath =
            "Assets/DefaultNetworkPrefabs.asset";

        private static readonly Color Panel =
            new(0.06f, 0.08f, 0.12f, 0.96f);
        private static readonly Color Field =
            new(0.14f, 0.17f, 0.22f, 1f);
        private static readonly Color HostColor =
            new(0.08f, 0.28f, 0.62f, 1f);
        private static readonly Color JoinColor =
            new(0.13f, 0.42f, 0.29f, 1f);
        private static readonly Color SwapColor =
            new(0.42f, 0.29f, 0.55f, 1f);
        private static readonly Color StartColor =
            new(0.72f, 0.5f, 0.12f, 1f);
        private static readonly Color LeaveColor =
            new(0.4f, 0.16f, 0.16f, 1f);

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

            // Routes match scene loads through NGO while a session runs, so
            // both machines resolve the same in-scene NetworkObjects.
            NetworkSceneCoordinator coordinator =
                manager.gameObject
                    .AddComponent<NetworkSceneCoordinator>();
            coordinator.Configure(manager);

            // Command-line driven verification, inert without -netLobby.
            var probeObject = new GameObject("Network Lobby Probe");
            probeObject.AddComponent<
                PawsAndLoot.TechnicalValidation.NetworkLobbyProbe>();

            BuildInterface(session);
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

        private static void BuildInterface(
            NetworkSessionController session)
        {
            var canvasObject = new GameObject(
                "Lobby UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the existing flow panel.
            canvas.sortingOrder = 10;
            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = CreateRect(
                "Lobby Panel",
                canvasObject.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(760f, 330f),
                new Vector2(0f, -300f));
            panel.gameObject.AddComponent<Image>().color = Panel;

            Text myAddress = CreateLabel(
                "My Address",
                panel,
                new Vector2(0f, 118f),
                new Vector2(720f, 30f),
                20,
                TextAnchor.MiddleCenter);
            myAddress.color = new Color(0.75f, 0.9f, 1f);

            Text status = CreateLabel(
                "Status",
                panel,
                new Vector2(0f, 86f),
                new Vector2(720f, 28f),
                18,
                TextAnchor.MiddleCenter);
            status.color = new Color(0.82f, 0.85f, 0.9f);

            InputField joinAddress = CreateField(
                "Join Address",
                panel,
                new Vector2(-150f, 44f),
                new Vector2(300f, 34f),
                "접속할 IP");
            InputField port = CreateField(
                "Port",
                panel,
                new Vector2(170f, 44f),
                new Vector2(140f, 34f),
                "포트");

            Button host = CreateButton(
                "Host Button",
                panel,
                new Vector2(-230f, 2f),
                new Vector2(200f, 36f),
                "호스트로 시작",
                HostColor);
            Button join = CreateButton(
                "Join Button",
                panel,
                new Vector2(0f, 2f),
                new Vector2(200f, 36f),
                "이 IP로 접속",
                JoinColor);
            Button leave = CreateButton(
                "Leave Button",
                panel,
                new Vector2(230f, 2f),
                new Vector2(200f, 36f),
                "세션 종료",
                LeaveColor);

            Text role = CreateLabel(
                "Role",
                panel,
                new Vector2(0f, -42f),
                new Vector2(720f, 30f),
                20,
                TextAnchor.MiddleCenter);
            role.color = new Color(1f, 0.92f, 0.72f);

            Button swap = CreateButton(
                "Swap Role Button",
                panel,
                new Vector2(-115f, -86f),
                new Vector2(220f, 36f),
                "역할 바꾸기",
                SwapColor);
            Button start = CreateButton(
                "Start Match Button",
                panel,
                new Vector2(115f, -86f),
                new Vector2(220f, 36f),
                "경기 시작",
                StartColor);

            Text hint = CreateLabel(
                "Hint",
                panel,
                new Vector2(0f, -126f),
                new Vector2(720f, 26f),
                15,
                TextAnchor.MiddleCenter);
            hint.color = new Color(0.6f, 0.65f, 0.72f);
            hint.text =
                "같은 PC에서 둘을 띄우려면 127.0.0.1 · 경기 시작은 호스트만";

            NetworkLobbyPresenter presenter =
                panel.gameObject.AddComponent<NetworkLobbyPresenter>();
            presenter.Configure(
                session,
                myAddress,
                status,
                role,
                joinAddress,
                port,
                host,
                join,
                swap,
                start,
                leave);

            host.onClick.AddListener(presenter.OnHostPressed);
            join.onClick.AddListener(presenter.OnJoinPressed);
            swap.onClick.AddListener(presenter.OnSwapRolePressed);
            start.onClick.AddListener(presenter.OnStartMatchPressed);
            leave.onClick.AddListener(presenter.OnLeavePressed);
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 position)
        {
            var rect = new GameObject(
                name,
                typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Text CreateLabel(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static InputField CreateField(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            string placeholder)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                size,
                position);
            rect.gameObject.AddComponent<Image>().color = Field;

            Text text = CreateLabel(
                "Text",
                rect,
                Vector2.zero,
                size - new Vector2(16f, 8f),
                18,
                TextAnchor.MiddleLeft);
            text.supportRichText = false;

            Text hint = CreateLabel(
                "Placeholder",
                rect,
                Vector2.zero,
                size - new Vector2(16f, 8f),
                18,
                TextAnchor.MiddleLeft);
            hint.text = placeholder;
            hint.color = new Color(0.55f, 0.6f, 0.68f);

            InputField field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = rect.GetComponent<Image>();
            return field;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            string caption,
            Color color)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateLabel(
                "Label",
                rect,
                Vector2.zero,
                size,
                17,
                TextAnchor.MiddleCenter);
            label.text = caption;
            return button;
        }
    }
}
