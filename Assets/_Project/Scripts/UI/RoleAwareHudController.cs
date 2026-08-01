using System;
using System.Text;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
using PawsAndLoot.Integration.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Shared police/thief HUD controller. Role-specific content is supplied
    /// as view data; the hierarchy and visual states stay identical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleAwareHudController : MonoBehaviour
    {
        private const int QuickSlotCount = 4;

        [SerializeField] private TMP_Text matchTimer;
        [SerializeField] private RoleStatusPanelView roleStatus;
        [SerializeField] private QuickSlotView[] quickSlots = new QuickSlotView[QuickSlotCount];
        [SerializeField] private AnimalCommandShortcutView[] animalCommands = new AnimalCommandShortcutView[QuickSlotCount];
        [SerializeField] private MicrophoneStatusView microphone;
        [SerializeField] private TMP_Text voiceFeedback;
        [SerializeField] private VoiceCommandFeedView voiceFeed;
        [SerializeField] private ContextInteractionPromptView contextPrompt;
        [SerializeField] private InventorySlotView[] inventorySlots = Array.Empty<InventorySlotView>();
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Button bagButton;
        [SerializeField] private Button voiceButton;
        [SerializeField] private MinimapHudController minimap;

        private ToolCarrier carrier;
        private VoiceCommandInput voice;
        private PlayerInteractionScanner scanner;
        private bool inventoryOpen;
        private bool buttonListenersBound;

        public bool IsInventoryOpen => inventoryOpen;
        public bool IsPlayerBound => carrier != null;
        public string VoiceProviderLabel => voice == null
            ? "Pending"
            : voice.VoiceProviderName;

        public void Configure(
            TMP_Text configuredMatchTimer,
            RoleStatusPanelView configuredRoleStatus,
            QuickSlotView[] configuredQuickSlots,
            AnimalCommandShortcutView[] configuredAnimalCommands,
            MicrophoneStatusView configuredMicrophone,
            TMP_Text configuredVoiceFeedback,
            VoiceCommandFeedView configuredVoiceFeed,
            ContextInteractionPromptView configuredContextPrompt,
            InventorySlotView[] configuredInventorySlots,
            GameObject configuredInventoryPanel,
            Button configuredBagButton,
            Button configuredVoiceButton,
            MinimapHudController configuredMinimap)
        {
            matchTimer = configuredMatchTimer;
            roleStatus = configuredRoleStatus;
            quickSlots = configuredQuickSlots ?? Array.Empty<QuickSlotView>();
            animalCommands = configuredAnimalCommands ?? Array.Empty<AnimalCommandShortcutView>();
            microphone = configuredMicrophone;
            voiceFeedback = configuredVoiceFeedback;
            voiceFeed = configuredVoiceFeed;
            contextPrompt = configuredContextPrompt;
            inventorySlots = configuredInventorySlots ?? Array.Empty<InventorySlotView>();
            inventoryPanel = configuredInventoryPanel;
            bagButton = configuredBagButton;
            voiceButton = configuredVoiceButton;
            minimap = configuredMinimap;
            BindButtonListeners();
        }

        private void OnEnable()
        {
            GameplayInputRouter.InventoryTogglePressed += ToggleInventory;
            GameplayInputRouter.EscapePressed += HandleEscape;
            GameplayInputRouter.BindingDisplayChanged += BindBindingLabels;
            BindButtonListeners();
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            GameplayInputRouter.InventoryTogglePressed -= ToggleInventory;
            GameplayInputRouter.EscapePressed -= HandleEscape;
            GameplayInputRouter.BindingDisplayChanged -= BindBindingLabels;
            if (buttonListenersBound)
            {
                bagButton?.onClick.RemoveListener(ToggleInventory);
                voiceButton?.onClick.RemoveListener(ToggleVoiceCapture);
                buttonListenersBound = false;
            }
            GameplayInputRouter.SetGameplayInputSuppressed(false);
        }

        private void BindButtonListeners()
        {
            if (!isActiveAndEnabled || buttonListenersBound)
            {
                return;
            }

            bagButton?.onClick.AddListener(ToggleInventory);
            voiceButton?.onClick.AddListener(ToggleVoiceCapture);
            buttonListenersBound = bagButton != null || voiceButton != null;
        }

        private void Update()
        {
            ResolveSources();
            BindRoleStatus();
            BindBindingLabels();
            BindQuickSlots();
            BindMicrophone();
            BindContextPrompt();
        }

        public void SetMatchTime(float seconds)
        {
            if (matchTimer == null) return;
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            matchTimer.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        public void ToggleInventory()
        {
            SetInventoryOpen(!inventoryOpen);
        }

        public void SetInventoryOpen(bool open)
        {
            inventoryOpen = open;
            GameplayInputRouter.SetGameplayInputSuppressed(open);
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(open);
            }
        }

        private void ToggleVoiceCapture()
        {
            ResolveSources();
            if (voice == null)
            {
                return;
            }

            if (voice.State == VoiceCommandInputState.Recording)
            {
                voice.StopListening();
            }
            else
            {
                voice.StartListening();
            }
        }

        private void HandleEscape()
        {
            if (inventoryOpen)
            {
                SetInventoryOpen(false);
            }
        }

        private void ResolveSources()
        {
            PlayerRole role = ResolveRole();
            if (carrier == null)
            {
                foreach (ToolCarrier candidate in
                    FindObjectsByType<ToolCarrier>(FindObjectsSortMode.None))
                {
                    if (candidate.Role == role)
                    {
                        carrier = candidate;
                        break;
                    }
                }
            }

            if (scanner == null)
            {
                foreach (PlayerInteractionScanner candidate in
                    FindObjectsByType<PlayerInteractionScanner>(FindObjectsSortMode.None))
                {
                    PlayerRoleIdentity identity =
                        candidate.GetComponent<PlayerRoleIdentity>();
                    if (identity == null || identity.Role == role)
                    {
                        scanner = candidate;
                        if (identity != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (voice == null)
            {
                foreach (VoiceCommandInput candidate in
                    FindObjectsByType<VoiceCommandInput>(FindObjectsSortMode.None))
                {
                    bool isDog = string.Equals(
                        candidate.PetId,
                        "dog",
                        StringComparison.OrdinalIgnoreCase);
                    if ((role == PlayerRole.Police) == isDog)
                    {
                        voice = candidate;
                        break;
                    }
                }
            }
        }

        private PlayerRole ResolveRole()
        {
            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                ? selector.ActiveRole
                : carrier != null ? carrier.Role : PlayerRole.Police;
        }

        private void BindRoleStatus()
        {
            if (roleStatus == null) return;
            PlayerRole role = ResolveRole();
            HudRole hudRole = role == PlayerRole.Thief ? HudRole.Thief : HudRole.Police;
            roleStatus.Bind(new RoleStatusPanelViewModel(
                hudRole,
                role == PlayerRole.Thief ? "THIEF" : "POLICE",
                role == PlayerRole.Thief ? "Steal the target loot" : "Protect your animal",
                "ACTIVE"));
        }

        private void BindQuickSlots()
        {
            for (int index = 0; index < quickSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool hasItem = carrier != null
                    && carrier.TryGetSlot(index, out kind);
                bool selected = carrier != null && carrier.SelectedSlot == index;
                QuickSlotViewModel model = new(
                    GameplayInputRouter.GetQuickSlotLabel(index),
                    null,
                    hasItem ? 1 : 0,
                    selected,
                    !hasItem,
                    0f,
                    hasItem ? GetItemGlyph(kind) : string.Empty);
                quickSlots[index]?.Bind(model);

                if (index < inventorySlots.Length)
                {
                    inventorySlots[index]?.Bind(new InventorySlotViewModel(
                        GameplayInputRouter.GetQuickSlotLabel(index),
                        null,
                        hasItem ? 1 : 0,
                        selected,
                        !hasItem,
                        hasItem ? GetItemGlyph(kind) : string.Empty));
                }
            }
        }

        private void BindBindingLabels()
        {
            PlayerRole role = ResolveRole();
            for (int index = 0; index < animalCommands.Length; index++)
            {
                CompanionCommandId command =
                    CompanionCommandCatalog.FromDebugNumberKey(role, index + 1);
                animalCommands[index]?.Bind(new AnimalCommandShortcutViewModel(
                    "SHIFT +",
                    GameplayInputRouter.GetAnimalCommandLabel(index + 1).Replace("SHIFT + ", string.Empty),
                    CompanionCommandCatalog.GetDisplayName(command),
                    false));
            }
        }

        private void BindMicrophone()
        {
            if (microphone == null) return;
            if (voice == null)
            {
                microphone.Bind(new MicrophoneStatusViewModel(
                    "VOICE UNAVAILABLE",
                    false,
                    false,
                    true,
                    0f,
                    0f));
                if (voiceFeedback != null)
                {
                    voiceFeedback.text = "VOICE SERVICE UNAVAILABLE";
                }
                return;
            }

            bool recording = voice.State == VoiceCommandInputState.Recording;
            bool processing = voice.State == VoiceCommandInputState.PermissionRequested
                || voice.State == VoiceCommandInputState.Uploading
                || voice.State == VoiceCommandInputState.Transcribing
                || voice.State == VoiceCommandInputState.Interpreting;
            bool permissionFailed = voice.State == VoiceCommandInputState.Failed;
            float maximum = Mathf.Max(0.1f, voice.MaximumRecordingSeconds);
            string stateLabel = voice.State switch
            {
                VoiceCommandInputState.PermissionRequested => "PERMISSION",
                VoiceCommandInputState.Recording => "RECORDING",
                VoiceCommandInputState.Uploading => "UPLOADING",
                VoiceCommandInputState.Transcribing => "TRANSCRIBING",
                VoiceCommandInputState.Interpreting => "INTERPRETING",
                VoiceCommandInputState.Completed => "COMPLETED",
                VoiceCommandInputState.Failed => "FAILED",
                _ => "READY"
            };
            microphone.Bind(new MicrophoneStatusViewModel(
                permissionFailed ? "ERROR" : stateLabel,
                recording,
                processing,
                permissionFailed,
                voice.ListeningElapsedSeconds / maximum,
                voice.CooldownRemainingSeconds,
                voice.PostCommandCooldownSeconds));

            if (voiceFeedback != null)
            {
                var feedback = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(voice.LastTranscript))
                {
                    feedback.Append("YOU: ").Append(voice.LastTranscript);
                }

                if (voice.LastResult != null)
                {
                    if (feedback.Length > 0) feedback.Append("\n");
                    feedback.Append("ANIMAL: ")
                        .Append(string.IsNullOrWhiteSpace(
                            voice.LastResult.interpretedCommand)
                            ? "UNKNOWN"
                            : voice.LastResult.interpretedCommand);
                    if (!string.IsNullOrWhiteSpace(voice.LastResult.animalFeedback))
                    {
                        feedback.Append(" — ")
                            .Append(voice.LastResult.animalFeedback);
                    }
                }

                if (!string.IsNullOrWhiteSpace(voice.LastError))
                {
                    if (feedback.Length > 0) feedback.Append("\n");
                    feedback.Append("ERROR: ").Append(voice.LastError);
                }

                voiceFeedback.text = feedback.ToString();
            }

            voiceFeed?.Bind(voice);
            if (voiceButton != null)
            {
                bool available = voice.State != VoiceCommandInputState.Uploading
                    && voice.State != VoiceCommandInputState.Transcribing
                    && voice.State != VoiceCommandInputState.Interpreting
                    && voice.CooldownRemainingSeconds <= 0f;
                voiceButton.interactable = available;
            }
        }

        private static string GetItemGlyph(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "R",
                ThrowableKind.Banana => "B",
                ThrowableKind.GlueTrap => "G",
                ThrowableKind.SensorLight => "S",
                ThrowableKind.Bone => "BN",
                ThrowableKind.TunaCan => "T",
                ThrowableKind.RubberChicken => "C",
                ThrowableKind.NoiseCan => "N",
                _ => "?"
            };
        }

        private void BindContextPrompt()
        {
            if (contextPrompt == null) return;
            bool visible = scanner != null && scanner.HasTarget;
            contextPrompt.Bind(new ContextInteractionPromptViewModel(
                visible,
                $"[{GameplayInputRouter.InteractionBindingLabel}]",
                visible ? scanner.CurrentPrompt : string.Empty,
                false,
                0f));
        }
    }

    public static class HudRuntimeInstaller
    {
        private static bool startedFromBootstrap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // The player starts in Bootstrap and enters Game later. The
            // RuntimeInitialize callback only covers the initial scene, so
            // keep listening for subsequent scene transitions as well.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            startedFromBootstrap |= string.Equals(
                SceneManager.GetActiveScene().name,
                "Bootstrap",
                StringComparison.OrdinalIgnoreCase);
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!string.Equals(scene.name, "Game", StringComparison.OrdinalIgnoreCase)
                || UnityEngine.Object.FindFirstObjectByType<RoleAwareHudController>() != null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>("HudCanvas");
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                instance.transform.localScale = Vector3.one;
                SuppressLegacyPresentation();
                ReportStartup(scene, instance, "Resources/HudCanvas.prefab");
                return;
            }

            GameObject built = BuildRuntimeCanvas();
            SuppressLegacyPresentation();
            ReportStartup(scene, built, "RuntimeBuilder");
        }

        private static void ReportStartup(Scene scene, GameObject hud, string prefabSource)
        {
            Canvas canvas = hud == null ? null : hud.GetComponent<Canvas>();
            RectTransform rect = hud == null ? null : hud.GetComponent<RectTransform>();
            CanvasGroup group = hud == null ? null : hud.GetComponent<CanvasGroup>();
            RoleAwareHudController controller = hud == null
                ? null
                : hud.GetComponent<RoleAwareHudController>();
            MinimapHudController minimap = hud == null
                ? null
                : hud.GetComponentInChildren<MinimapHudController>();
            LocalPlayerRoleSelector selector =
                UnityEngine.Object.FindFirstObjectByType<LocalPlayerRoleSelector>();
            string failure = string.Empty;
            if (hud == null)
            {
                failure = "HUD instance is null";
            }
            else if (!hud.activeSelf || !hud.activeInHierarchy)
            {
                failure = "HUD GameObject is inactive";
            }
            else if (rect == null || rect.localScale != Vector3.one)
            {
                failure = "HUD root scale is not one";
            }
            else if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                failure = "HUD Canvas is not Screen Space Overlay";
            }
            else if (group != null && group.alpha <= 0f)
            {
                failure = "HUD CanvasGroup alpha is zero";
            }

            Debug.Log(
                $"[HUDStartup] Scene:{scene.name} "
                + $"StartedFromBootstrap:{startedFromBootstrap} "
                + $"HudPrefab:{prefabSource} "
                + $"HudInstance:{(hud == null ? "None" : hud.name)} "
                + $"CanvasActive:{hud != null && hud.activeInHierarchy} "
                + $"CanvasScale:{(rect == null ? "None" : rect.localScale.ToString())} "
                + $"CanvasRenderMode:{(canvas == null ? "None" : canvas.renderMode.ToString())} "
                + $"SortingOrder:{(canvas == null ? -1 : canvas.sortingOrder)} "
                + $"PlayerRole:{(selector == null ? "Pending" : selector.ActiveRole.ToString())} "
                + $"PlayerBound:{(controller != null && controller.IsPlayerBound)} "
                + $"VoiceProvider:{(controller == null ? "Pending" : controller.VoiceProviderLabel)} "
                + $"MinimapCamera:{(minimap != null && minimap.MinimapCamera != null)} "
                + $"FailureReason:{(string.IsNullOrWhiteSpace(failure) ? "None" : failure)}");
        }

        private static void SuppressLegacyPresentation()
        {
            string[] names =
            {
                "Companion Command HUD",
                "Police HUD",
                "Thief HUD",
                "Common HUD"
            };
            int hidden = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    for (int index = 0; index < names.Length; index++)
                    {
                        if (!string.Equals(child.name, names[index], StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (child.gameObject.activeSelf)
                        {
                            child.gameObject.SetActive(false);
                            hidden++;
                        }

                        break;
                    }
                }
            }

            if (hidden > 0)
            {
                Debug.Log($"[HUDStartup] Suppressed {hidden} legacy HUD root(s); EssentialHudCanvas is authoritative.");
            }
        }

        public static GameObject BuildRuntimeCanvas()
        {
            var canvasObject = new GameObject(
                "HudCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            RectTransform rootRect = canvasObject.GetComponent<RectTransform>();
            rootRect.localScale = Vector3.one;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            canvas.targetDisplay = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            TMP_Text timer = CreateText(canvasObject.transform, "Match Timer", "10:00", 32f, TextAlignmentOptions.Center);
            Anchor(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(240f, 52f));

            RoleStatusPanelView roleStatus = BuildRoleStatus(canvasObject.transform);
            QuickSlotView[] quickSlots = BuildQuickSlots(canvasObject.transform);
            MicrophoneStatusView microphone = BuildMicrophone(
                canvasObject.transform,
                out Button voiceButton);
            VoiceCommandFeedView voiceFeed = BuildVoiceFeed(canvasObject.transform);
            ContextInteractionPromptView context = BuildContextPrompt(canvasObject.transform);
            GameObject inventoryPanel = BuildInventory(canvasObject.transform, out InventorySlotView[] inventorySlots);
            Button bagButton = BuildBagButton(canvasObject.transform);
            MinimapHudController minimap = BuildMinimap(canvasObject.transform);

            var controller = canvasObject.AddComponent<RoleAwareHudController>();
            controller.Configure(
                timer,
                roleStatus,
                quickSlots,
                Array.Empty<AnimalCommandShortcutView>(),
                microphone,
                null,
                voiceFeed,
                context,
                inventorySlots,
                inventoryPanel,
                bagButton,
                voiceButton,
                minimap);
            // CanvasScaler can touch a RectTransform while the runtime hierarchy
            // is being assembled in the editor. Reassert the production root
            // scale after all children and components exist.
            rootRect.localScale = Vector3.one;
            return canvasObject;
        }

        private static RoleStatusPanelView BuildRoleStatus(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Role Status", new Vector2(270f, 118f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -162f), new Vector2(270f, 118f));
            TMP_Text role = CreateText(panel.transform, "Role", "POLICE", 20f, TextAlignmentOptions.TopLeft);
            TMP_Text objective = CreateText(panel.transform, "Objective", "Protect your animal", 14f, TextAlignmentOptions.TopLeft);
            TMP_Text status = CreateText(panel.transform, "Status", "ACTIVE", 13f, TextAlignmentOptions.TopLeft);
            Image tint = CreateImage(panel.transform, "Role Tint", new Color(0.15f, 0.45f, 1f, 1f));
            Anchor(role.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-32f, 30f));
            Anchor(objective.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -48f), new Vector2(-32f, 24f));
            Anchor(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -78f), new Vector2(-32f, 22f));
            Anchor(tint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 0f));
            var view = panel.AddComponent<RoleStatusPanelView>();
            view.Configure(role, objective, status, tint);
            return view;
        }

        private static QuickSlotView[] BuildQuickSlots(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Quick Slots", new Vector2(286f, 70f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-143f, 28f), new Vector2(286f, 70f));
            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 5f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var result = new QuickSlotView[4];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = BuildQuickSlot(panel.transform, index + 1);
            }
            return result;
        }

        private static QuickSlotView BuildQuickSlot(Transform parent, int number)
        {
            GameObject slot = CreatePanel(parent, $"QuickSlot {number}", new Vector2(64f, 58f));
            TMP_Text key = CreateText(slot.transform, "Key", number.ToString(), 12f, TextAlignmentOptions.TopLeft);
            TMP_Text quantity = CreateText(slot.transform, "Quantity", string.Empty, 12f, TextAlignmentOptions.BottomRight);
            TMP_Text glyph = CreateText(slot.transform, "Item Glyph", string.Empty, 22f, TextAlignmentOptions.Center);
            Image icon = CreateImage(slot.transform, "Item Icon", Color.white);
            Image selected = CreateImage(slot.transform, "Selected Frame", new Color(1f, 0.82f, 0.2f, 0.35f));
            Image disabled = CreateImage(slot.transform, "Disabled", new Color(0f, 0f, 0f, 0.45f));
            Image cooldown = CreateImage(slot.transform, "Cooldown", new Color(0f, 0f, 0f, 0.6f));
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            Anchor(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -4f), new Vector2(24f, 18f));
            Anchor(quantity.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 4f), new Vector2(20f, 18f));
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 30f));
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            Stretch(selected.rectTransform); Stretch(disabled.rectTransform); Stretch(cooldown.rectTransform);
            selected.transform.SetAsFirstSibling();
            var view = slot.AddComponent<QuickSlotView>();
            view.Configure(key, quantity, icon, selected, disabled, cooldown, glyph);
            return view;
        }

        private static AnimalCommandShortcutView[] BuildAnimalCommands(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "ANIMAL COMMANDS", new Vector2(270f, 196f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 32f), new Vector2(270f, 196f));
            TMP_Text title = CreateText(panel.transform, "Title", "ANIMAL COMMANDS", 16f, TextAlignmentOptions.TopLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-32f, 26f));
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 44, 12);
            layout.spacing = 4f;
            layout.childForceExpandHeight = true;
            var labels = new[] { "TRACK", "WAIT", "RETURN", "HIDE" };
            var result = new AnimalCommandShortcutView[4];
            for (int index = 0; index < result.Length; index++)
            {
                GameObject row = new GameObject($"Shift Command {index + 1}", typeof(RectTransform));
                row.transform.SetParent(panel.transform, false);
                TMP_Text modifier = CreateText(row.transform, "Modifier", "SHIFT +", 13f, TextAlignmentOptions.Left);
                TMP_Text key = CreateText(row.transform, "Key", (index + 1).ToString(), 16f, TextAlignmentOptions.Left);
                TMP_Text command = CreateText(row.transform, "Command", labels[index], 13f, TextAlignmentOptions.Left);
                Anchor(modifier.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(58f, 24f));
                Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(28f, 24f));
                Anchor(command.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(94f, 0f), new Vector2(-94f, 24f));
                result[index] = row.AddComponent<AnimalCommandShortcutView>();
                result[index].Configure(modifier, key, command, null);
            }
            return result;
        }

        private static MicrophoneStatusView BuildMicrophone(
            Transform parent,
            out Button button)
        {
            GameObject panel = CreatePanel(parent, "Voice Button", new Vector2(86f, 70f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(190f, 28f), new Vector2(86f, 70f));
            button = panel.AddComponent<Button>();
            TMP_Text state = CreateText(panel.transform, "State", "READY", 9f, TextAlignmentOptions.Center);
            TMP_Text key = CreateText(panel.transform, "Key", "V", 20f, TextAlignmentOptions.Center);
            TMP_Text cooldown = CreateText(panel.transform, "Cooldown", string.Empty, 10f, TextAlignmentOptions.BottomRight);
            Image radial = CreateImage(panel.transform, "Recording Radial", new Color(0.15f, 0.85f, 1f, 0.55f));
            Image disabled = CreateImage(panel.transform, "Disabled", new Color(0f, 0f, 0f, 0.45f));
            radial.type = Image.Type.Filled; radial.fillMethod = Image.FillMethod.Radial360;
            Anchor(state.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-8f, 18f));
            Anchor(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(36f, 32f));
            Anchor(cooldown.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 4f), new Vector2(22f, 18f));
            Anchor(radial.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(58f, 58f));
            Stretch(disabled.rectTransform);
            var view = panel.AddComponent<MicrophoneStatusView>();
            view.Configure(state, key, cooldown, radial, disabled);
            return view;
        }

        private static VoiceCommandFeedView BuildVoiceFeed(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Voice Command Feed", new Vector2(360f, 54f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-180f, 106f), new Vector2(360f, 54f));
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            TMP_Text input = CreateText(panel.transform, "Input", string.Empty, 12f, TextAlignmentOptions.Center);
            TMP_Text command = CreateText(panel.transform, "Command", string.Empty, 11f, TextAlignmentOptions.Center);
            Anchor(input.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(10f, -2f), new Vector2(-20f, -4f));
            Anchor(command.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(10f, 2f), new Vector2(-20f, -4f));
            var view = panel.AddComponent<VoiceCommandFeedView>();
            view.Configure(input, command, group);
            return view;
        }

        private static Button BuildBagButton(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Bag Button", new Vector2(76f, 60f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f, 33f), new Vector2(76f, 60f));
            Button button = panel.AddComponent<Button>();
            TMP_Text label = CreateText(panel.transform, "Label", "BAG\n[TAB]", 11f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private static MinimapHudController BuildMinimap(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "TopRightMinimap", new Vector2(184f, 184f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(184f, 184f));

            GameObject viewportObject = new(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CircleGraphic),
                typeof(Mask));
            viewportObject.transform.SetParent(panel.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            CircleGraphic viewportImage = viewportObject.GetComponent<CircleGraphic>();
            viewportImage.color = new Color(0.08f, 0.12f, 0.15f, 1f);
            Mask mask = viewportObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject mapObject = new("Map", typeof(RectTransform), typeof(RawImage));
            mapObject.transform.SetParent(viewportObject.transform, false);
            RawImage map = mapObject.GetComponent<RawImage>();
            Stretch(map.rectTransform);
            map.color = Color.white;
            map.raycastTarget = false;

            GameObject borderObject = new("Circle Border", typeof(RectTransform), typeof(CircleGraphic));
            borderObject.transform.SetParent(panel.transform, false);
            CircleGraphic border = borderObject.GetComponent<CircleGraphic>();
            border.color = new Color(0.25f, 0.85f, 1f, 0.8f);
            border.RingThickness = 3f;
            border.raycastTarget = false;
            Stretch(border.rectTransform);

            RectTransform playerMarker = CreateMarker(
                panel.transform,
                "Player Marker",
                new Color(0.2f, 0.95f, 1f, 1f),
                12f);
            RectTransform alertMarker = CreateMarker(
                panel.transform,
                "Alert Marker",
                new Color(1f, 0.25f, 0.2f, 1f),
                12f);
            alertMarker.gameObject.SetActive(false);

            var controller = panel.AddComponent<MinimapHudController>();
            controller.Configure(null, map, playerMarker, alertMarker);
            return controller;
        }

        private static RectTransform CreateMarker(
            Transform parent,
            string name,
            Color color,
            float size)
        {
            GameObject markerObject = new(name, typeof(RectTransform), typeof(CircleGraphic));
            markerObject.transform.SetParent(parent, false);
            CircleGraphic marker = markerObject.GetComponent<CircleGraphic>();
            marker.color = color;
            marker.raycastTarget = false;
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            Anchor(markerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            return markerRect;
        }

        private static ContextInteractionPromptView BuildContextPrompt(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Context Interaction", new Vector2(300f, 34f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 170f), new Vector2(300f, 34f));
            TMP_Text key = CreateText(panel.transform, "Key", "[E]", 13f, TextAlignmentOptions.Center);
            TMP_Text action = CreateText(panel.transform, "Action", "", 12f, TextAlignmentOptions.Left);
            Image progress = CreateImage(panel.transform, "Hold Progress", new Color(0.2f, 0.85f, 1f, 0.6f));
            progress.type = Image.Type.Filled;
            Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(74f, 28f));
            Anchor(action.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(94f, 0f), new Vector2(-106f, 28f));
            Stretch(progress.rectTransform);
            var view = panel.AddComponent<ContextInteractionPromptView>();
            view.Configure(key, action, progress);
            return view;
        }

        private static GameObject BuildInventory(Transform parent, out InventorySlotView[] slots)
        {
            GameObject panel = CreatePanel(parent, "Inventory", new Vector2(560f, 300f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 300f));
            TMP_Text title = CreateText(panel.transform, "Title", "INVENTORY   [TAB]", 18f, TextAlignmentOptions.TopLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -22f), new Vector2(-48f, 34f));
            var layout = panel.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(104f, 84f);
            layout.spacing = new Vector2(10f, 10f);
            layout.padding = new RectOffset(24, 24, 62, 20);
            slots = new InventorySlotView[4];
            for (int index = 0; index < slots.Length; index++)
            {
                GameObject slot = CreatePanel(panel.transform, $"Inventory Slot {index + 1}", new Vector2(104f, 84f));
                TMP_Text key = CreateText(slot.transform, "Key", (index + 1).ToString(), 13f, TextAlignmentOptions.TopLeft);
                TMP_Text quantity = CreateText(slot.transform, "Quantity", string.Empty, 13f, TextAlignmentOptions.BottomRight);
                TMP_Text glyph = CreateText(slot.transform, "Item Glyph", string.Empty, 24f, TextAlignmentOptions.Center);
                Image icon = CreateImage(slot.transform, "Item Icon", Color.white);
                Image selected = CreateImage(slot.transform, "Selected Frame", new Color(1f, 0.82f, 0.2f, 0.35f));
                Image disabled = CreateImage(slot.transform, "Disabled", new Color(0f, 0f, 0f, 0.45f));
                Anchor(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(30f, 24f));
                Anchor(quantity.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 8f), new Vector2(26f, 24f));
                Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 46f));
                Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
                Stretch(selected.rectTransform); Stretch(disabled.rectTransform);
                slots[index] = slot.AddComponent<InventorySlotView>();
                slots[index].Configure(key, quantity, icon, selected, disabled, glyph);
            }
            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.08f, 0.94f);
            panel.GetComponent<RectTransform>().sizeDelta = size;
            return panel;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            float size,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            TMP_FontAsset runtimeFont = Resources.Load<TMP_FontAsset>(
                "PawsAndLootDefaultFont");
            if (runtimeFont != null)
            {
                text.font = runtimeFont;
            }
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
