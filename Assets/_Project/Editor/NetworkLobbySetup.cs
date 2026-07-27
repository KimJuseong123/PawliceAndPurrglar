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
        /// <summary>
        /// Room rows in the lobby. A LAN playtest has one or two hosts; four is
        /// headroom, not a target.
        /// </summary>
        private const int RoomSlotCount = 4;

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

            // NET-008. On the NetworkManager object so it survives the scene
            // change: rematch is pressed on the result screen, and a scene
            // NetworkObject would already be gone by then (ISSUE-016).
            manager.gameObject
                .AddComponent<NetworkRematchCoordinator>()
                .Configure(manager);

            // NET-009. One handler for the whole session, in the one place that
            // outlives every scene load.
            manager.gameObject
                .AddComponent<NetworkDisconnectHandler>()
                .Configure(manager, session);

            // Announces this host on the LAN and lists the ones it hears, so the
            // two players can meet without reading an IP to each other.
            manager.gameObject
                .AddComponent<LanRoomDirectory>()
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
                new Vector2(760f, 540f),
                new Vector2(0f, -170f));
            panel.gameObject.AddComponent<Image>().color = Panel;

            // The room list sits at the top because it is the path most players
            // will take. Typing an IP stays below it, unchanged, for networks
            // that drop broadcast traffic.
            Text roomListLabel = CreateLabel(
                "Room List Label",
                panel,
                new Vector2(0f, 236f),
                new Vector2(720f, 26f),
                17,
                TextAnchor.MiddleCenter);
            roomListLabel.color = new Color(0.75f, 0.9f, 1f);

            var roomButtons = new Button[RoomSlotCount];
            var roomLabels = new Text[RoomSlotCount];
            for (int index = 0; index < RoomSlotCount; index++)
            {
                Button roomButton = CreateButton(
                    $"Room Slot {index}",
                    panel,
                    new Vector2(0f, 202f - index * 38f),
                    new Vector2(700f, 34f),
                    string.Empty,
                    JoinColor);
                roomButtons[index] = roomButton;
                roomLabels[index] =
                    roomButton.GetComponentInChildren<Text>();
                // Hidden until a room is found, so an empty LAN shows nothing
                // rather than four blank buttons.
                roomButton.gameObject.SetActive(false);
            }

            Text myAddress = CreateLabel(
                "My Address",
                panel,
                new Vector2(0f, 34f),
                new Vector2(720f, 30f),
                20,
                TextAnchor.MiddleCenter);
            myAddress.color = new Color(0.75f, 0.9f, 1f);

            Text status = CreateLabel(
                "Status",
                panel,
                new Vector2(0f, 4f),
                new Vector2(720f, 28f),
                18,
                TextAnchor.MiddleCenter);
            status.color = new Color(0.82f, 0.85f, 0.9f);

            InputField joinAddress = CreateField(
                "Join Address",
                panel,
                new Vector2(-150f, -34f),
                new Vector2(300f, 34f),
                "접속할 IP");
            InputField port = CreateField(
                "Port",
                panel,
                new Vector2(170f, -34f),
                new Vector2(140f, 34f),
                "포트");

            Button host = CreateButton(
                "Host Button",
                panel,
                new Vector2(-230f, -78f),
                new Vector2(200f, 36f),
                "방 만들기 (호스트)",
                HostColor);
            Button join = CreateButton(
                "Join Button",
                panel,
                new Vector2(0f, -78f),
                new Vector2(200f, 36f),
                "이 IP로 접속",
                JoinColor);
            Button leave = CreateButton(
                "Leave Button",
                panel,
                new Vector2(230f, -78f),
                new Vector2(200f, 36f),
                "세션 종료",
                LeaveColor);

            Text role = CreateLabel(
                "Role",
                panel,
                new Vector2(0f, -122f),
                new Vector2(720f, 30f),
                20,
                TextAnchor.MiddleCenter);
            role.color = new Color(1f, 0.92f, 0.72f);

            Button swap = CreateButton(
                "Swap Role Button",
                panel,
                new Vector2(-115f, -166f),
                new Vector2(220f, 36f),
                "역할 바꾸기",
                SwapColor);
            Button start = CreateButton(
                "Start Match Button",
                panel,
                new Vector2(115f, -166f),
                new Vector2(220f, 36f),
                "경기 시작",
                StartColor);

            Text hint = CreateLabel(
                "Hint",
                panel,
                new Vector2(0f, -212f),
                new Vector2(720f, 26f),
                15,
                TextAnchor.MiddleCenter);
            hint.color = new Color(0.6f, 0.65f, 0.72f);
            hint.text =
                "한 명이 방 만들기 · 같은 PC면 127.0.0.1 · 경기 시작은 호스트만";

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
            presenter.ConfigureRoomList(
                UnityEngine.Object.FindFirstObjectByType<
                    LanRoomDirectory>(),
                roomListLabel,
                roomButtons,
                roomLabels);

            // No onClick.AddListener here on purpose. From an editor script it
            // registers a non-persistent listener that is discarded when the
            // scene is saved, so the built lobby's buttons did nothing and no
            // session could ever start. The presenter binds them in OnEnable
            // instead, which survives into the build.
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
