using System;
using System.Text;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
using PawsAndLoot.Integration.Voice;
using PawsAndLoot.Match;
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
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text catchProgressText;
        [SerializeField] private PoliceCatchProgressView catchProgressView;

        private MatchRuntimeState matchRuntime;
        private ArrestCompletionController arrestCompletion;
        private ThiefLootWallet thiefWallet;
        private ToolCarrier carrier;
        private VoiceCommandInput voice;
        private PlayerInteractionScanner scanner;
        private CompanionCommandDispatcher dispatcher;
        private CompanionCommandDispatcher subscribedDispatcher;
        private bool inventoryOpen;
        private bool buttonListenersBound;
        private bool graphicAuditLogged;

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
            MinimapHudController configuredMinimap,
            TMP_Text configuredObjectiveText = null,
            TMP_Text configuredCatchProgressText = null,
            PoliceCatchProgressView configuredCatchProgressView = null)
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
            objectiveText = configuredObjectiveText;
            catchProgressText = configuredCatchProgressText;
            catchProgressView = configuredCatchProgressView;
            BindButtonListeners();
        }

        private void OnEnable()
        {
            GameplayInputRouter.InventoryTogglePressed += ToggleInventory;
            GameplayInputRouter.EscapePressed += HandleEscape;
            GameplayInputRouter.BindingDisplayChanged += BindBindingLabels;
            GameplayInputRouter.AnimalCommandPressed += HandleAnimalCommandPressed;
            GameplayInputRouter.VoicePressed += HandleVoicePressed;
            ApplyEssentialLayoutDefaults();
            HideUnusedMatchTimer();
            HideCentralObjective();
            HudRuntimeInstaller.SuppressLegacyPresentation();
            ResolveSerializedTextFallbacks();
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
            GameplayInputRouter.AnimalCommandPressed -= HandleAnimalCommandPressed;
            GameplayInputRouter.VoicePressed -= HandleVoicePressed;
            UnsubscribeDispatcher();
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
            BindMatchTimer();
            BindRoleStatus();
            HideCentralObjective();
            BindCatchProgress();
            BindBindingLabels();
            BindQuickSlots();
            BindMicrophone();
            BindContextPrompt();
            LogGraphicAuditOnce();
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

            if (voice.State != VoiceCommandInputState.Recording
                && voice.CooldownRemainingSeconds <= 0f)
            {
                voice.StartListening();
            }
            else if (voice.CooldownRemainingSeconds > 0f)
            {
                ShowVoiceCooldownFeedback();
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
            matchRuntime ??= FindFirstObjectByType<MatchRuntimeState>();
            arrestCompletion ??=
                FindFirstObjectByType<ArrestCompletionController>();
            thiefWallet ??= FindFirstObjectByType<ThiefLootWallet>();
            PlayerRole role = ResolveRole();
            if (carrier == null || carrier.Role != role)
            {
                carrier = null;
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

            if (dispatcher == null)
            {
                dispatcher =
                    FindFirstObjectByType<CompanionCommandDispatcher>();
            }

            SubscribeDispatcherIfNeeded();
        }

        private void BindMatchTimer()
        {
            if (matchRuntime != null && matchTimer != null)
            {
                SetMatchTime(matchRuntime.RemainingMatchSeconds);
            }
        }

        private PlayerRole ResolveRole()
        {
            return TryResolveRole(out PlayerRole role)
                ? role
                : PlayerRole.Police;
        }

        private bool TryResolveRole(out PlayerRole role)
        {
            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            if (selector != null)
            {
                role = selector.ActiveRole;
                return true;
            }

            if (carrier != null)
            {
                role = carrier.Role;
                return true;
            }

            role = PlayerRole.Police;
            return false;
        }

        private void BindRoleStatus()
        {
            if (roleStatus == null) return;
            PlayerRole role = ResolveRole();
            HudRole hudRole = role == PlayerRole.Thief ? HudRole.Thief : HudRole.Police;
            roleStatus.Bind(new RoleStatusPanelViewModel(
                hudRole,
                role == PlayerRole.Thief ? "THIEF" : "POLICE",
                GetRoleObjectiveText(role),
                GetRoleStatusText(role)));
        }

        private void HideCentralObjective()
        {
            if (objectiveText == null)
            {
                return;
            }

            Transform holder = objectiveText.transform.parent;
            if (holder != null
                && string.Equals(holder.name, "Objective Text", StringComparison.Ordinal))
            {
                holder.gameObject.SetActive(false);
                return;
            }

            objectiveText.gameObject.SetActive(false);
        }

        private void BindCatchProgress()
        {
            if (catchProgressView == null && catchProgressText == null)
            {
                return;
            }

            int current = arrestCompletion != null
                ? arrestCompletion.CurrentCatchCount
                : 0;
            int required = arrestCompletion != null
                ? arrestCompletion.RequiredCatchCount
                : ArrestCompletionController.DefaultRequiredCatchCount;
            PlayerRole role = ResolveRole();
            if (catchProgressView != null)
            {
                catchProgressView.Bind(current, required, role);
            }

            if (catchProgressText == null)
            {
                return;
            }

            catchProgressText.text = role == PlayerRole.Thief
                ? $"붙잡힌 횟수 {current} / {required}"
                : $"도둑 체포 {current} / {required}";
        }

        private string GetRoleObjectiveText(PlayerRole role)
        {
            return role == PlayerRole.Thief
                ? $"{GetThiefTargetAmount()}골드 모으기"
                : "도둑 3회 체포 또는 골드 저지";
        }

        private string GetRoleStatusText(PlayerRole role)
        {
            return role == PlayerRole.Thief
                ? $"골드 {GetThiefSoldAmount()} / {GetThiefTargetAmount()}"
                : $"도둑 골드 {GetThiefSoldAmount()} / {GetThiefTargetAmount()}";
        }

        private int GetThiefSoldAmount()
        {
            return thiefWallet != null ? thiefWallet.SoldAmount : 0;
        }

        private int GetThiefTargetAmount()
        {
            return thiefWallet != null && thiefWallet.TargetAmount > 0
                ? thiefWallet.TargetAmount
                : 1000;
        }

        private void BindQuickSlots()
        {
            for (int index = 0; index < quickSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool hasItem = carrier != null
                    && carrier.TryGetSlot(index, out kind);
                int quantity = hasItem
                    ? carrier.GetSlotQuantity(index)
                    : 0;
                bool selected = carrier != null && carrier.SelectedSlot == index;
                QuickSlotViewModel model = new(
                    GameplayInputRouter.GetQuickSlotLabel(index),
                    null,
                    quantity,
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
                        quantity,
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
                    "CTRL +",
                    GameplayInputRouter.GetAnimalCommandLabel(index + 1).Replace("CTRL + ", string.Empty),
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
            bool processing = voice.State == VoiceCommandInputState.Starting
                || voice.State == VoiceCommandInputState.Encoding
                || voice.State == VoiceCommandInputState.Transcribing
                || voice.State == VoiceCommandInputState.Interpreting;
            bool permissionFailed = voice.State == VoiceCommandInputState.Error;
            float maximum = Mathf.Max(0.1f, voice.MaximumRecordingSeconds);
            string stateLabel = voice.State switch
            {
                VoiceCommandInputState.Starting => "STARTING",
                VoiceCommandInputState.Recording => "RECORDING",
                VoiceCommandInputState.Encoding => "ENCODING",
                VoiceCommandInputState.Transcribing => "TRANSCRIBING",
                VoiceCommandInputState.Interpreting => "INTERPRETING",
                VoiceCommandInputState.Executing => "EXECUTING",
                VoiceCommandInputState.Cooldown => "COOLDOWN",
                VoiceCommandInputState.Error => "FAILED",
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
                bool available = voice.State != VoiceCommandInputState.Encoding
                    && voice.State != VoiceCommandInputState.Transcribing
                    && voice.State != VoiceCommandInputState.Interpreting
                    && voice.CooldownRemainingSeconds <= 0f;
                voiceButton.interactable = available;
            }
        }

        private void HandleAnimalCommandPressed(int numberKey)
        {
            PlayerRole role = ResolveRole();
            CompanionCommandId command =
                CompanionCommandCatalog.FromNumberKey(role, numberKey);
            if (command == CompanionCommandId.None)
            {
                return;
            }

            voiceFeed?.ShowMessage(
                $"CTRL+{numberKey} 동물 명령",
                $"{CompanionCommandCatalog.GetDisplayName(command)} 전송");
        }

        private void HandleVoicePressed()
        {
            ResolveSources();
            if (voice != null && voice.CooldownRemainingSeconds > 0f)
            {
                ShowVoiceCooldownFeedback();
            }
        }

        private void ShowVoiceCooldownFeedback()
        {
            if (voice == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(voice.CooldownRemainingSeconds);
            voiceFeed?.ShowMessage(
                "아직 명령 쿨타임",
                $"{seconds}초 후 다시 말할 수 있어요",
                2.5f);
        }

        private void SubscribeDispatcherIfNeeded()
        {
            if (dispatcher == null || subscribedDispatcher == dispatcher)
            {
                return;
            }

            UnsubscribeDispatcher();
            subscribedDispatcher = dispatcher;
            subscribedDispatcher.CommandAccepted += HandleCommandAccepted;
            subscribedDispatcher.CommandRejected += HandleCommandRejected;
        }

        private void UnsubscribeDispatcher()
        {
            if (subscribedDispatcher == null)
            {
                return;
            }

            subscribedDispatcher.CommandAccepted -= HandleCommandAccepted;
            subscribedDispatcher.CommandRejected -= HandleCommandRejected;
            subscribedDispatcher = null;
        }

        private void HandleCommandAccepted(CompanionCommandRequest request)
        {
            if (request.IssuerRole != ResolveRole()
                || request.InputSource != CompanionCommandInputSource.Keyboard)
            {
                return;
            }

            voiceFeed?.ShowMessage(
                "동물 명령 실행",
                CompanionCommandCatalog.GetDisplayName(request.CommandId));
        }

        private void HandleCommandRejected(
            CompanionCommandRequest request,
            CompanionCommandRejection rejection)
        {
            if (request.IssuerRole != ResolveRole()
                || request.InputSource != CompanionCommandInputSource.Keyboard)
            {
                return;
            }

            string second = rejection == CompanionCommandRejection.OnCooldown
                ? "명령 쿨타임"
                : $"실패: {rejection}";
            voiceFeed?.ShowMessage(
                "동물 명령 실패",
                second);
        }

        private static string GetItemGlyph(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "R",
                ThrowableKind.Banana => "B",
                ThrowableKind.GlueTrap => "G",
                ThrowableKind.SensorLight => "S",
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
            PositionContextPrompt(visible);
        }

        private void PositionContextPrompt(bool visible)
        {
            if (!visible
                || scanner == null
                || scanner.CurrentTarget == null
                || scanner.CurrentTarget.InteractionTransform == null
                || contextPrompt.transform is not RectTransform promptRect)
            {
                return;
            }

            Camera viewCamera = Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            Vector3 screenPoint = viewCamera.WorldToScreenPoint(
                scanner.CurrentTarget.InteractionTransform.position
                + Vector3.up * 1.25f);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            const float margin = 48f;
            screenPoint.x = Mathf.Clamp(screenPoint.x, margin, Screen.width - margin);
            screenPoint.y = Mathf.Clamp(screenPoint.y, margin, Screen.height - margin);
            promptRect.position = screenPoint;
        }

        private void ResolveSerializedTextFallbacks()
        {
            objectiveText ??= FindText("Objective Text");
            catchProgressText ??= FindText("Catch Progress");
            catchProgressView ??= FindView<PoliceCatchProgressView>(
                "Police Catches");
        }

        private TMP_Text FindText(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private T FindView<T>(string childName) where T : Component
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponentInChildren<T>(true) : null;
        }

        private void ApplyEssentialLayoutDefaults()
        {
            ApplyRect(
                "TopRightMinimap",
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-24f, -24f),
                new Vector2(250f, 250f));
            ApplyRect(
                "Role Status",
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-24f, -288f),
                new Vector2(270f, 96f));
            ApplyRect(
                "Quick Slots",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(286f, 70f));
            ApplyRect(
                "Bag Button",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-226f, 24f),
                new Vector2(76f, 60f));
            ApplyRect(
                "Voice Button",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(226f, 24f),
                new Vector2(86f, 70f));
            ApplyRect(
                "Voice Command Feed",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 104f),
                new Vector2(360f, 54f));
            ApplyRect(
                "Context Interaction",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 230f),
                new Vector2(300f, 34f));
            ApplyRect(
                "ANIMAL COMMANDS",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 126f),
                new Vector2(292f, 214f));
            ApplyRect(
                "Police Catches",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(330f, 108f));
            ApplyRect(
                "Catch Progress",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -92f),
                new Vector2(260f, 36f));
        }

        private void HideUnusedMatchTimer()
        {
            matchTimer = null;
            Transform child = transform.Find("Match Timer");
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void ApplyRect(
            string childName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Transform child = transform.Find(childName);
            if (child == null
                || child is not RectTransform rect)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private void LogGraphicAuditOnce()
        {
            if (graphicAuditLogged
                || (!Application.isEditor && !Debug.isDebugBuild))
            {
                return;
            }

            graphicAuditLogged = true;
            bool nullGraphics = string.Equals(
                SystemInfo.graphicsDeviceType.ToString(),
                "Null",
                StringComparison.OrdinalIgnoreCase);
            int magentaLike = 0;
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                Material material = graphic.material;
                Shader shader = material != null ? material.shader : null;
                bool unsupported = shader == null
                    || (!nullGraphics && !shader.isSupported);
                bool internalError = shader != null
                    && shader.name.IndexOf(
                        "InternalError",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                if (unsupported || internalError)
                {
                    magentaLike++;
                }

                TMP_Text tmp = graphic as TMP_Text;
                Image image = graphic as Image;
                RawImage raw = graphic as RawImage;
                Canvas canvas = graphic.canvas;
                Debug.Log(
                    "[HudGraphicAudit] "
                    + $"Path:{BuildPath(graphic.transform)} "
                    + $"Component:{graphic.GetType().Name} "
                    + $"FontAsset:{(tmp == null || tmp.font == null ? "None" : tmp.font.name)} "
                    + $"SharedMaterial:{(material == null ? "None" : material.name)} "
                    + $"Shader:{(shader == null ? "None" : shader.name)} "
                    + $"ShaderSupported:{(shader != null && shader.isSupported)} "
                    + $"NullGraphics:{nullGraphics} "
                    + $"MainTexture:{(graphic.mainTexture == null ? "None" : graphic.mainTexture.name)} "
                    + $"Sprite:{(image == null || image.sprite == null ? "None" : image.sprite.name)} "
                    + $"RawTexture:{(raw == null || raw.texture == null ? "None" : raw.texture.name)} "
                    + $"Active:{graphic.gameObject.activeInHierarchy} "
                    + $"Color:{graphic.color} "
                    + $"Canvas:{(canvas == null ? "None" : canvas.name)} "
                    + $"SortingOrder:{(canvas == null ? 0 : canvas.sortingOrder)}",
                    graphic);
            }

            Debug.Log(
                $"[HudGraphicAuditSummary] MagentaOrUnsupportedGraphics:{magentaLike}",
                this);
        }

        private static string BuildPath(Transform leaf)
        {
            var builder = new StringBuilder(leaf.name);
            Transform current = leaf.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }
    }

    public static class HudRuntimeInstaller
    {
        private static bool startedFromBootstrap;
        private static readonly Type[] LegacyPresenterTypes =
        {
            typeof(FirstPlayGuidePresenter),
            typeof(ToolHudPresenter),
            typeof(CommonHudPresenter),
            typeof(RoleObjectivePresenter),
            typeof(PoliceHudPresenter),
            typeof(ThiefHudPresenter),
            typeof(ArrestHudPresenter),
            typeof(CompanionCommandHudPresenter),
            typeof(VoiceCommandHudPresenter)
        };

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
            if (!string.Equals(scene.name, "Game", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (UnityEngine.Object.FindFirstObjectByType<RoleAwareHudController>() != null)
            {
                SuppressLegacyPresentation();
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

        internal static void SuppressLegacyPresentation()
        {
            string[] names =
            {
                "Companion Command HUD",
                "Arrest HUD",
                "Police HUD",
                "Thief HUD",
                "Common HUD",
                "Role Objective",
                "Command Feedback",
                "Tool HUD",
                "Held Tool",
                "Tool Slot",
                "Tool Label",
                "Current Goal",
                "Held Loot",
                "First Play Guide"
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

            foreach (Type presenterType in LegacyPresenterTypes)
            {
                UnityEngine.Object[] presenters =
                    UnityEngine.Object.FindObjectsByType(
                        presenterType,
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                foreach (UnityEngine.Object found in presenters)
                {
                    if (found is not Component presenter
                        || presenter.gameObject.GetComponent<RoleAwareHudController>() != null
                        || !presenter.gameObject.activeSelf)
                    {
                        continue;
                    }

                    presenter.gameObject.SetActive(false);
                    hidden++;
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
                "EssentialHudCanvas",
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

            PoliceCatchProgressView catchProgress =
                BuildPoliceCatchProgress(canvasObject.transform);

            RoleStatusPanelView roleStatus = BuildRoleStatus(canvasObject.transform);
            QuickSlotView[] quickSlots = BuildQuickSlots(canvasObject.transform);
            AnimalCommandShortcutView[] animalCommands =
                BuildAnimalCommands(canvasObject.transform);
            MicrophoneStatusView microphone = BuildMicrophone(
                canvasObject.transform,
                out Button voiceButton);
            VoiceCommandFeedView voiceFeed = BuildVoiceFeed(canvasObject.transform);
            ContextInteractionPromptView context = BuildContextPrompt(canvasObject.transform);
            GameObject inventoryPanel = BuildInventory(canvasObject.transform, out InventorySlotView[] inventorySlots);
            Button bagButton = BuildBagButton(canvasObject.transform);
            MinimapHudController minimap = BuildMinimap(canvasObject.transform);
            BuildSensorRadar(canvasObject.transform, canvasObject);

            var controller = canvasObject.AddComponent<RoleAwareHudController>();
            controller.Configure(
                null,
                roleStatus,
                quickSlots,
                animalCommands,
                microphone,
                null,
                voiceFeed,
                context,
                inventorySlots,
                inventoryPanel,
                bagButton,
                voiceButton,
                minimap,
                null,
                null,
                catchProgress);
            // CanvasScaler can touch a RectTransform while the runtime hierarchy
            // is being assembled in the editor. Reassert the production root
            // scale after all children and components exist.
            rootRect.localScale = Vector3.one;
            return canvasObject;
        }

        private static RoleStatusPanelView BuildRoleStatus(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Role Status", new Vector2(270f, 96f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = Vector2.one;
            Anchor(panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -288f), new Vector2(270f, 96f));
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
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(286f, 70f));
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
            Image selected = CreateImage(slot.transform, "Selected Frame", new Color(1f, 0.82f, 0.2f, 0.58f));
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
            GameObject panel = CreatePanel(parent, "ANIMAL COMMANDS", new Vector2(292f, 214f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = Vector2.zero;
            Anchor(panelRect, Vector2.zero, Vector2.zero, new Vector2(24f, 126f), new Vector2(292f, 214f));

            TMP_Text title = CreateText(
                panel.transform,
                "Title",
                "ANIMAL COMMAND",
                15f,
                TextAlignmentOptions.Left);
            title.color = new Color(0.05f, 0.95f, 1f, 1f);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -14f), new Vector2(-36f, 26f));

            var result = new AnimalCommandShortcutView[4];
            for (int index = 0; index < result.Length; index++)
            {
                GameObject row = CreatePanel(
                    panel.transform,
                    $"Ctrl Command {index + 1}",
                    new Vector2(252f, 32f));
                Image rowImage = row.GetComponent<Image>();
                rowImage.color = new Color(0.02f, 0.07f, 0.09f, 0.92f);
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.pivot = new Vector2(0f, 1f);
                Anchor(
                    rowRect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -54f - index * 38f),
                    new Vector2(252f, 32f));

                TMP_Text modifier = CreateText(row.transform, "Modifier", "CTRL +", 11f, TextAlignmentOptions.Left);
                TMP_Text key = CreateText(row.transform, "Key", (index + 1).ToString(), 16f, TextAlignmentOptions.Left);
                TMP_Text command = CreateText(row.transform, "Command", string.Empty, 12f, TextAlignmentOptions.Left);
                modifier.color = new Color(0.05f, 0.95f, 1f, 1f);
                key.color = new Color(0.85f, 1f, 1f, 1f);
                Anchor(modifier.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(52f, 24f));
                Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(24f, 24f));
                Anchor(command.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(92f, 0f), new Vector2(-102f, 24f));
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
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(226f, 24f), new Vector2(86f, 70f));
            button = panel.AddComponent<Button>();
            TMP_Text state = CreateText(panel.transform, "State", "READY", 9f, TextAlignmentOptions.Center);
            TMP_Text key = CreateText(panel.transform, "Key", "V", 20f, TextAlignmentOptions.Center);
            TMP_Text label = CreateText(panel.transform, "Label", "VOICE", 9f, TextAlignmentOptions.Center);
            TMP_Text cooldown = CreateText(panel.transform, "Cooldown", string.Empty, 10f, TextAlignmentOptions.BottomRight);
            Image radial = CreateImage(panel.transform, "Recording Radial", new Color(0.15f, 0.85f, 1f, 0.55f));
            Image disabled = CreateImage(panel.transform, "Disabled", new Color(0f, 0f, 0f, 0.45f));
            radial.type = Image.Type.Filled; radial.fillMethod = Image.FillMethod.Radial360;
            Anchor(state.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-8f, 18f));
            Anchor(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(36f, 32f));
            Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 5f), new Vector2(-8f, 16f));
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
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(360f, 54f));
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
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-226f, 24f), new Vector2(76f, 60f));
            Button button = panel.AddComponent<Button>();
            TMP_Text label = CreateText(panel.transform, "Label", "BAG\n[TAB]", 11f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private static MinimapHudController BuildMinimap(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "TopRightMinimap", new Vector2(250f, 250f));
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.raycastTarget = false;
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = Vector2.one;
            Anchor(panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(250f, 250f));

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

        private static SensorRadarPresenter BuildSensorRadar(
            Transform parent,
            GameObject presenterHost)
        {
            var rootObject = new GameObject("Sensor Radar", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            Anchor(
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(240f, 240f));

            float[] radii = { 26f, 43f, 60f, 77f, 94f, 111f, 128f };
            for (int index = 0; index < radii.Length; index++)
            {
                var arcObject = new GameObject(
                    $"Sensor Arc {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(SensorArcGraphic));
                arcObject.transform.SetParent(rootObject.transform, false);
                RectTransform arcRect = arcObject.GetComponent<RectTransform>();
                Anchor(
                    arcRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(240f, 240f));
                SensorArcGraphic arc = arcObject.GetComponent<SensorArcGraphic>();
                arc.Configure(radii[index], 8f, 96f);
                arc.color = new Color(0.95f, 0.16f, 0.16f, 1f);
                arc.raycastTarget = false;
            }

            rootObject.SetActive(false);
            SensorRadarPresenter presenter =
                presenterHost.AddComponent<SensorRadarPresenter>();
            presenter.Configure(root, null);
            return presenter;
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
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(300f, 34f));
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

        private static TMP_Text BuildObjectiveText(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Objective Text", new Vector2(620f, 42f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(620f, 42f));
            TMP_Text objective = CreateText(panel.transform, "Label", "목표: 역할 확인 중...", 15f, TextAlignmentOptions.Center);
            Stretch(objective.rectTransform);
            return objective;
        }

        private static PoliceCatchProgressView BuildPoliceCatchProgress(
            Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Police Catches", new Vector2(330f, 108f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 1f);
            Anchor(panelRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(330f, 108f));
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.01f, 0.04f, 0.07f, 0.86f);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.9f, 1f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            TMP_Text title = CreateText(
                panel.transform,
                "Title",
                "POLICE CATCHES",
                18f,
                TextAlignmentOptions.Center);
            title.color = new Color(0.05f, 0.95f, 1f, 1f);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 28f));

            TMP_Text counter = CreateText(
                panel.transform,
                "Counter",
                "0 / 3",
                11f,
                TextAlignmentOptions.Center);
            counter.color = new Color(0.72f, 0.92f, 1f, 0.78f);
            Anchor(counter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(76f, 18f));

            var backplates = new Image[3];
            var rings = new CircleGraphic[3];
            var fills = new CircleGraphic[3];
            for (int index = 0; index < backplates.Length; index++)
            {
                GameObject slot = CreatePanel(
                    panel.transform,
                    $"Catch Slot {index + 1}",
                    new Vector2(58f, 58f));
                Image slotImage = slot.GetComponent<Image>();
                slotImage.color = new Color(0.02f, 0.05f, 0.08f, 0.92f);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                Anchor(
                    slotRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((index - 1) * 72f, -10f),
                    new Vector2(58f, 58f));

                GameObject fillObject = new(
                    "Fill",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(CircleGraphic));
                fillObject.transform.SetParent(slot.transform, false);
                CircleGraphic fill = fillObject.GetComponent<CircleGraphic>();
                fill.color = new Color(0.05f, 0.08f, 0.10f, 0.85f);
                fill.raycastTarget = false;
                Anchor(
                    fill.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(38f, 38f));

                GameObject ringObject = new(
                    "Ring",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(CircleGraphic));
                ringObject.transform.SetParent(slot.transform, false);
                CircleGraphic ring = ringObject.GetComponent<CircleGraphic>();
                ring.RingThickness = 4f;
                ring.color = new Color(0.20f, 0.28f, 0.34f, 0.9f);
                ring.raycastTarget = false;
                Anchor(
                    ring.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(42f, 42f));

                backplates[index] = slotImage;
                rings[index] = ring;
                fills[index] = fill;
            }

            var view = panel.AddComponent<PoliceCatchProgressView>();
            view.Configure(title, counter, backplates, rings, fills);
            view.Bind(0, ArrestCompletionController.DefaultRequiredCatchCount, PlayerRole.Police);
            return view;
        }

        private static TMP_Text BuildCatchProgress(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Catch Progress", new Vector2(260f, 36f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 1f);
            Anchor(panelRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(260f, 36f));
            TMP_Text progress = CreateText(panel.transform, "Label", "도둑 체포 0 / 3", 16f, TextAlignmentOptions.Center);
            Stretch(progress.rectTransform);
            return progress;
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
                text.fontSharedMaterial = runtimeFont.material;
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
