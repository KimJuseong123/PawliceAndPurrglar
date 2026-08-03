using System;
using System.Collections.Generic;
using System.Text;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
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
        private const int InventorySlotCount = 25;
        private const int CatBagSlotCount = 4;

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
        [SerializeField] private GameObject catExchangePanel;
        [SerializeField] private InventorySlotView[] exchangePlayerSlots = Array.Empty<InventorySlotView>();
        [SerializeField] private InventorySlotView[] exchangeCatSlots = Array.Empty<InventorySlotView>();
        [SerializeField] private Button bagButton;
        [SerializeField] private Button voiceButton;
        [SerializeField] private MinimapHudController minimap;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text catchProgressText;
        [SerializeField] private PoliceCatchProgressView catchProgressView;

        private MatchRuntimeState matchRuntime;
        private ArrestCompletionController arrestCompletion;
        private PawsAndLoot.Gameplay.Arrest.ThiefJailState jailState;
        private ThiefLootWallet thiefWallet;
        private ToolCarrier carrier;
        private VoiceCommandInput voice;
        private PlayerInteractionScanner scanner;
        private PlayerInteractionInput interactionInput;
        private LootConfig lootConfig;
        private Sprite currencyIcon;
        private readonly Dictionary<ThrowableKind, Sprite> itemIconCache = new();
        private readonly Dictionary<string, Sprite> lootIconCache = new(
            StringComparer.OrdinalIgnoreCase);
        private CompanionCommandDispatcher dispatcher;
        private CompanionCommandDispatcher subscribedDispatcher;
        private CatInventoryInteractable activeCatInventory;
        private ToolCarrier exchangeCarrier;
        private bool inventoryOpen;
        private bool catExchangeOpen;
        private bool buttonListenersBound;
        private bool slotListenersBound;
        private bool graphicAuditLogged;
        private bool catInteractablesInstalled;

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
            GameObject configuredCatExchangePanel,
            InventorySlotView[] configuredExchangePlayerSlots,
            InventorySlotView[] configuredExchangeCatSlots,
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
            catExchangePanel = configuredCatExchangePanel;
            exchangePlayerSlots = configuredExchangePlayerSlots ?? Array.Empty<InventorySlotView>();
            exchangeCatSlots = configuredExchangeCatSlots ?? Array.Empty<InventorySlotView>();
            bagButton = configuredBagButton;
            voiceButton = configuredVoiceButton;
            minimap = configuredMinimap;
            objectiveText = configuredObjectiveText;
            catchProgressText = configuredCatchProgressText;
            catchProgressView = configuredCatchProgressView;
            ApplyEssentialLayoutDefaults();
            BindButtonListeners();
            BindSlotListeners();
        }

        private void OnEnable()
        {
            GameplayInputRouter.InventoryTogglePressed += ToggleInventory;
            GameplayInputRouter.EscapePressed += HandleEscape;
            GameplayInputRouter.BindingDisplayChanged += BindBindingLabels;
            GameplayInputRouter.AnimalCommandPressed += HandleAnimalCommandPressed;
            GameplayInputRouter.VoicePressed += HandleVoicePressed;
            CatInventoryInteractable.ExchangeRequested += OpenCatExchange;
            ApplyEssentialLayoutDefaults();
            ResolveSerializedTextFallbacks();
            HideUnusedMatchTimer();
            HideCentralObjective();
            HudRuntimeInstaller.SuppressLegacyPresentation();
            BindButtonListeners();
            BindSlotListeners();
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            if (catExchangePanel != null)
            {
                catExchangePanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            GameplayInputRouter.InventoryTogglePressed -= ToggleInventory;
            GameplayInputRouter.EscapePressed -= HandleEscape;
            GameplayInputRouter.BindingDisplayChanged -= BindBindingLabels;
            GameplayInputRouter.AnimalCommandPressed -= HandleAnimalCommandPressed;
            GameplayInputRouter.VoicePressed -= HandleVoicePressed;
            CatInventoryInteractable.ExchangeRequested -= OpenCatExchange;
            UnsubscribeDispatcher();
            if (buttonListenersBound)
            {
                bagButton?.onClick.RemoveListener(ToggleInventory);
                voiceButton?.onClick.RemoveListener(ToggleVoiceCapture);
                buttonListenersBound = false;
            }

            ClearSlotListeners();
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

        private void BindSlotListeners()
        {
            ClearExchangeSlots(exchangePlayerSlots);
            ClearExchangeSlots(exchangeCatSlots);
            slotListenersBound =
                BindExchangeSlots(exchangePlayerSlots, HandleExchangePlayerSlotClicked)
                | BindExchangeSlots(exchangeCatSlots, HandleExchangeCatSlotClicked);
        }

        private static bool BindExchangeSlots(
            InventorySlotView[] slots,
            Action<int> handler)
        {
            if (slots == null || handler == null)
            {
                return false;
            }

            bool bound = false;
            for (int index = 0; index < slots.Length; index++)
            {
                InventorySlotView slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                Button button = slot.GetComponent<Button>()
                    ?? slot.gameObject.AddComponent<Button>();
                int captured = index;
                button.onClick.AddListener(() => handler(captured));
                bound = true;
            }

            return bound;
        }

        private void ClearSlotListeners()
        {
            ClearExchangeSlots(exchangePlayerSlots);
            ClearExchangeSlots(exchangeCatSlots);
            slotListenersBound = false;
        }

        private static void ClearExchangeSlots(InventorySlotView[] slots)
        {
            if (slots == null)
            {
                return;
            }

            foreach (InventorySlotView slot in slots)
            {
                Button button = slot != null ? slot.GetComponent<Button>() : null;
                button?.onClick.RemoveAllListeners();
            }
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
            if (open)
            {
                SetCatExchangeOpen(false);
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(open);
            }

            RefreshInputSuppression();
        }

        public void OpenCatExchange(
            CatInventoryInteractable catInventory,
            ToolCarrier playerCarrier)
        {
            if (catInventory == null || playerCarrier == null)
            {
                return;
            }

            activeCatInventory = catInventory;
            exchangeCarrier = playerCarrier;
            SetInventoryOpen(false);
            SetCatExchangeOpen(true);
            voiceFeed?.ShowMessage(
                "고양이 가방",
                "아이템을 눌러 서로 옮길 수 있어요",
                2.5f);
        }

        private void SetCatExchangeOpen(bool open)
        {
            catExchangeOpen = open;
            if (!open)
            {
                activeCatInventory = null;
                exchangeCarrier = null;
            }

            if (catExchangePanel != null)
            {
                catExchangePanel.SetActive(open);
            }

            RefreshInputSuppression();
        }

        private void RefreshInputSuppression()
        {
            GameplayInputRouter.SetGameplayInputSuppressed(
                inventoryOpen || catExchangeOpen);
        }

        private void ToggleVoiceCapture()
        {
            ResolveSources();
            if (voice == null)
            {
                voiceFeed?.ShowMessage(
                    "VOICE UNAVAILABLE",
                    "Microphone input is not connected.",
                    2.5f);
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
            if (inventoryOpen || catExchangeOpen)
            {
                SetInventoryOpen(false);
                SetCatExchangeOpen(false);
            }
        }

        private void ResolveSources()
        {
            matchRuntime ??= FindFirstObjectByType<MatchRuntimeState>();
            arrestCompletion ??=
                FindFirstObjectByType<ArrestCompletionController>();
            jailState ??= FindFirstObjectByType<
                PawsAndLoot.Gameplay.Arrest.ThiefJailState>();
            thiefWallet ??= FindFirstObjectByType<ThiefLootWallet>();
            if (lootConfig == null && GameConfigService.IsInitialized)
            {
                lootConfig = GameConfigService.Current.Loot;
            }

            currencyIcon ??= Resources.Load<Sprite>("UI/CurrencyCoin");
            EnsureCatInteractablesInstalled();
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

            if (scanner == null || !MatchesRole(scanner, role))
            {
                scanner = null;
                interactionInput = null;
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

            if (scanner != null
                && (interactionInput == null
                    || interactionInput.GetComponent<PlayerInteractionScanner>() != scanner))
            {
                interactionInput = scanner.GetComponent<PlayerInteractionInput>();
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

        private void EnsureCatInteractablesInstalled()
        {
            if (catInteractablesInstalled)
            {
                return;
            }

            CatInventoryInteractable.InstallMissingInteractables();
            catInteractablesInstalled =
                FindFirstObjectByType<CatInventoryInteractable>() != null;
        }

        private static bool MatchesRole(Component component, PlayerRole role)
        {
            PlayerRoleIdentity identity =
                component != null ? component.GetComponent<PlayerRoleIdentity>() : null;
            return identity == null || identity.Role == role;
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

            // The sentence goes where the tally goes, because they are one
            // sentence in the player's head: how many times, and how long until
            // I can play again. Split across the screen they read as two
            // unrelated numbers.
            //
            // Shown only while it is running. A countdown reading zero all
            // match is a permanent piece of furniture that says nothing, and
            // the eye stops going to it before the one moment it matters.
            if (jailState != null && jailState.IsJailed)
            {
                catchProgressText.text = role == PlayerRole.Thief
                    ? $"붙잡힌 횟수 {current} / {required}"
                        + $"    유치장 {jailState.RemainingSeconds:0.0}초"
                    : $"도둑 체포 {current} / {required}"
                        + $"    유치 중 {jailState.RemainingSeconds:0.0}초";
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
                    && carrier.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);
                int quantity = hasItem
                    ? carrier.GetSlotQuantity(index)
                    : 0;
                bool selected = carrier != null && carrier.SelectedSlot == index;
                QuickSlotViewModel model = new(
                    GameplayInputRouter.GetQuickSlotLabel(index),
                    hasItem ? GetItemIcon(kind) : null,
                    quantity,
                    selected,
                    !hasItem,
                    0f,
                    hasItem && GetItemIcon(kind) == null
                        ? GetItemGlyph(kind)
                        : string.Empty);
                quickSlots[index]?.Bind(model);
            }

            BindInventorySlots();
            BindCatExchangeSlots();
        }

        private void BindInventorySlots()
        {
            LootItem[] lootItems = inventoryOpen
                ? FindVisibleLootItems()
                : Array.Empty<LootItem>();
            for (int index = 0; index < inventorySlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool quickSlotIndex = index < QuickSlotCount;
                bool hasItem = quickSlotIndex
                    && carrier != null
                    && carrier.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);
                int quantity = hasItem
                    ? carrier.GetSlotQuantity(index)
                    : 0;
                bool selected = hasItem
                    && carrier != null
                    && carrier.SelectedSlot == index;
                int lootIndex = index - QuickSlotCount;
                LootDefinition definition =
                    !quickSlotIndex && lootIndex >= 0 && lootIndex < lootItems.Length
                        ? lootItems[lootIndex].Definition
                        : null;
                bool hasLootPrice = definition != null;
                Sprite lootIcon = hasLootPrice ? GetLootIcon(definition) : null;

                inventorySlots[index]?.Bind(new InventorySlotViewModel(
                    quickSlotIndex
                        ? GameplayInputRouter.GetQuickSlotLabel(index)
                        : (index + 1).ToString(),
                    hasItem ? GetItemIcon(kind) : lootIcon,
                    quantity,
                    selected,
                    !(hasItem || hasLootPrice),
                    hasItem && GetItemIcon(kind) == null
                        ? GetItemGlyph(kind)
                        : string.Empty,
                    hasItem
                        ? ThrowableCatalog.GetDisplayName(kind)
                        : definition != null
                            ? definition.DisplayName
                            : string.Empty,
                    hasLootPrice ? GetLootPrice(definition) : 0,
                    hasLootPrice ? currencyIcon : null));
            }
        }

        private LootItem[] FindVisibleLootItems()
        {
            LootItem[] all = FindObjectsByType<LootItem>(FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                return Array.Empty<LootItem>();
            }

            var visible = new List<LootItem>(all.Length);
            foreach (LootItem item in all)
            {
                if (item == null
                    || item.Definition == null
                    || item.CurrentState == LootState.Sold)
                {
                    continue;
                }

                visible.Add(item);
            }

            visible.Sort((left, right) => string.Compare(
                left.Definition.DisplayName,
                right.Definition.DisplayName,
                StringComparison.CurrentCulture));
            return visible.ToArray();
        }

        private int GetLootPrice(LootDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            if (lootConfig == null && GameConfigService.IsInitialized)
            {
                lootConfig = GameConfigService.Current.Loot;
            }

            if (lootConfig != null)
            {
                return definition.GetPrice(lootConfig);
            }

            return definition.Rarity switch
            {
                LootRarity.Uncommon => 350,
                LootRarity.Rare => 500,
                _ => 200
            };
        }

        private void BindCatExchangeSlots()
        {
            ToolCarrier sourceCarrier = exchangeCarrier ?? carrier;
            for (int index = 0; index < exchangePlayerSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool hasItem = sourceCarrier != null
                    && sourceCarrier.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);
                exchangePlayerSlots[index]?.Bind(new InventorySlotViewModel(
                    GameplayInputRouter.GetQuickSlotLabel(index),
                    hasItem ? GetItemIcon(kind) : null,
                    hasItem ? sourceCarrier.GetSlotQuantity(index) : 0,
                    sourceCarrier != null && sourceCarrier.SelectedSlot == index,
                    !hasItem,
                    hasItem && GetItemIcon(kind) == null
                        ? GetItemGlyph(kind)
                        : string.Empty,
                    hasItem ? ThrowableCatalog.GetDisplayName(kind) : string.Empty));
            }

            for (int index = 0; index < exchangeCatSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool hasItem = activeCatInventory != null
                    && activeCatInventory.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);
                exchangeCatSlots[index]?.Bind(new InventorySlotViewModel(
                    (index + 1).ToString(),
                    hasItem ? GetItemIcon(kind) : null,
                    hasItem ? activeCatInventory.GetSlotQuantity(index) : 0,
                    false,
                    !hasItem,
                    hasItem && GetItemIcon(kind) == null
                        ? GetItemGlyph(kind)
                        : string.Empty,
                    hasItem ? ThrowableCatalog.GetDisplayName(kind) : string.Empty));
            }
        }

        private void HandleExchangePlayerSlotClicked(int index)
        {
            if (!catExchangeOpen
                || activeCatInventory == null
                || exchangeCarrier == null
                || !exchangeCarrier.TryGetSlot(index, out ThrowableKind kind))
            {
                return;
            }

            const int transferQuantity = 1;
            if (!activeCatInventory.CanStore(kind, transferQuantity))
            {
                voiceFeed?.ShowMessage("고양이 가방", "빈 칸이 없어요", 2f);
                return;
            }

            if (!exchangeCarrier.TryTakeOne(index, out kind))
            {
                return;
            }

            if (!activeCatInventory.TryStore(kind, transferQuantity))
            {
                exchangeCarrier.TryStore(kind, transferQuantity);
                voiceFeed?.ShowMessage("고양이 가방", "아이템을 옮기지 못했어요", 2f);
                return;
            }

            voiceFeed?.ShowMessage(
                "고양이에게 전달",
                ThrowableCatalog.GetDisplayName(kind),
                1.5f);
        }

        private void HandleExchangeCatSlotClicked(int index)
        {
            if (!catExchangeOpen
                || activeCatInventory == null
                || exchangeCarrier == null
                || !activeCatInventory.TryGetSlot(index, out ThrowableKind kind))
            {
                return;
            }

            const int transferQuantity = 1;
            if (!exchangeCarrier.CanStore(kind, transferQuantity))
            {
                voiceFeed?.ShowMessage("도둑 가방", "퀵슬롯이 가득 찼어요", 2f);
                return;
            }

            if (!activeCatInventory.TryTakeOne(index, out kind))
            {
                return;
            }

            if (!exchangeCarrier.TryStore(kind, transferQuantity))
            {
                activeCatInventory.TryStore(kind, transferQuantity);
                voiceFeed?.ShowMessage("도둑 가방", "아이템을 옮기지 못했어요", 2f);
                return;
            }

            voiceFeed?.ShowMessage(
                "가방으로 받음",
                ThrowableCatalog.GetDisplayName(kind),
                1.5f);
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

        private Sprite GetItemIcon(ThrowableKind kind)
        {
            if (itemIconCache.TryGetValue(kind, out Sprite cached))
            {
                return cached;
            }

            string path = GetItemIconResourcePath(kind);
            Sprite sprite = string.IsNullOrWhiteSpace(path)
                ? null
                : Resources.Load<Sprite>(path);
            itemIconCache[kind] = sprite;
            return sprite;
        }

        private Sprite GetLootIcon(LootDefinition definition)
        {
            string stableId = definition != null
                ? definition.StableId
                : string.Empty;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return null;
            }

            if (lootIconCache.TryGetValue(stableId, out Sprite cached))
            {
                return cached;
            }

            string path = stableId switch
            {
                "common-trinket" => "UI/ItemIcons/gold medal",
                "uncommon-watch" => "UI/ItemIcons/golden watch",
                "rare-jewel" => "UI/ItemIcons/blue gemstone",
                _ => string.Empty
            };
            Sprite sprite = string.IsNullOrWhiteSpace(path)
                ? null
                : Resources.Load<Sprite>(path);
            lootIconCache[stableId] = sprite;
            return sprite;
        }

        private static string GetItemIconResourcePath(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "UI/ItemIcons/rock",
                ThrowableKind.Banana => "UI/ItemIcons/banana",
                ThrowableKind.GlueTrap => "UI/ItemIcons/catnip pouch",
                ThrowableKind.SensorLight => "UI/ItemIcons/police lantern alarm",
                ThrowableKind.TunaCan => "UI/ItemIcons/fish can",
                ThrowableKind.DogTreat => "UI/ItemIcons/bone",
                ThrowableKind.RubberChicken => "UI/ItemIcons/yellow chicken",
                // No authored icons for these two yet; the can stands in, the
                // same way PlacedTrapView greyboxes the props themselves.
                ThrowableKind.Firework => "UI/ItemIcons/can",
                ThrowableKind.FrozenOctopus => "UI/ItemIcons/can",
                _ => string.Empty
            };
        }

        private static string GetItemGlyph(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "R",
                ThrowableKind.Banana => "B",
                ThrowableKind.GlueTrap => "G",
                ThrowableKind.SensorLight => "S",
                ThrowableKind.TunaCan => "T",
                ThrowableKind.DogTreat => "D",
                ThrowableKind.RubberChicken => "C",
                ThrowableKind.Firework => "F",
                ThrowableKind.FrozenOctopus => "O",
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
                interactionInput != null && interactionInput.IsHolding,
                interactionInput != null ? interactionInput.HoldProgress01 : 0f));
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
            matchTimer ??= FindText("Match Timer");
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
                new Vector2(420f, 68f));
            ApplyRect(
                "Context Interaction",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 230f),
                new Vector2(300f, 34f));
            ApplyRect(
                "ANIMAL COMMANDS",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(292f, 214f));
            ApplyRect(
                "Inventory",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -260f),
                new Vector2(470f, 780f));
            ApplyRect(
                "Cat Exchange",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(780f, 820f));
            ApplyRect(
                "Cat Exchange/Player Bag",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -124f),
                new Vector2(460f, 632f));
            ApplyRect(
                "Cat Exchange/Cat Bag",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(504f, -424f),
                new Vector2(252f, 302f));
            ApplyPanelImage(
                "Cat Exchange",
                new Color(0.015f, 0.035f, 0.055f, 0.52f),
                true);
            ApplyPanelImage(
                "Cat Exchange/Player Bag",
                new Color(0.02f, 0.06f, 0.09f, 0.48f),
                true);
            ApplyPanelImage(
                "Cat Exchange/Cat Bag",
                new Color(0.02f, 0.06f, 0.09f, 0.50f),
                true);
            ApplyRect(
                "Police Catches",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(330f, 108f));
            ApplyRect(
                "Match Timer",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -128f),
                new Vector2(170f, 32f));
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
            Transform child = transform.Find("Match Timer");
            if (child != null
                && (matchTimer == null
                    || !matchTimer.transform.IsChildOf(child)))
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

        private void ApplyPanelImage(
            string childName,
            Color color,
            bool raycastTarget)
        {
            Transform child = transform.Find(childName);
            Image image = child != null ? child.GetComponent<Image>() : null;
            if (image == null)
            {
                return;
            }

            image.color = color;
            image.raycastTarget = raycastTarget;
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
        private const int InventorySlotCount = 25;
        private const int CatBagSlotCount = 4;

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
            TMP_Text matchTimer = BuildMatchTimer(canvasObject.transform);

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
            GameObject catExchangePanel = BuildCatExchange(
                canvasObject.transform,
                out InventorySlotView[] exchangePlayerSlots,
                out InventorySlotView[] exchangeCatSlots);
            Button bagButton = BuildBagButton(canvasObject.transform);
            MinimapHudController minimap = BuildMinimap(canvasObject.transform);
            BuildSensorRadar(canvasObject.transform, canvasObject);

            var controller = canvasObject.AddComponent<RoleAwareHudController>();
            controller.Configure(
                matchTimer,
                roleStatus,
                quickSlots,
                animalCommands,
                microphone,
                null,
                voiceFeed,
                context,
                inventorySlots,
                inventoryPanel,
                catExchangePanel,
                exchangePlayerSlots,
                exchangeCatSlots,
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
            Image tint = CreateImage(panel.transform, "Role Tint", new Color(0.15f, 0.45f, 1f, 0.7f));
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
            panelRect.pivot = new Vector2(0f, 1f);
            Anchor(
                panelRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(292f, 214f));

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
                rowImage.color = new Color(0.02f, 0.07f, 0.09f, 0.54f);
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
            GameObject panel = CreatePanel(parent, "Voice Command Feed", new Vector2(420f, 68f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0f);
            Anchor(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(420f, 68f));
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            TMP_Text input = CreateText(panel.transform, "Input", string.Empty, 12f, TextAlignmentOptions.Center);
            TMP_Text command = CreateText(panel.transform, "Command", string.Empty, 11f, TextAlignmentOptions.Center);
            GameObject paw = BuildPawIcon(panel.transform);
            Anchor(input.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(56f, -2f), new Vector2(-66f, -4f));
            Anchor(command.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(56f, 2f), new Vector2(-66f, -4f));
            var view = panel.AddComponent<VoiceCommandFeedView>();
            view.Configure(input, command, group, paw);
            return view;
        }

        private static GameObject BuildPawIcon(Transform parent)
        {
            var root = new GameObject("Interpretation Paw", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(
                rect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(30f, 0f),
                new Vector2(34f, 34f));
            Color pawColor = new(0.75f, 0.9f, 1f, 0.92f);
            BuildPawCircle(root.transform, "Pad", new Vector2(0f, -5f), new Vector2(18f, 15f), pawColor);
            BuildPawCircle(root.transform, "Toe 1", new Vector2(-11f, 9f), new Vector2(8f, 8f), pawColor);
            BuildPawCircle(root.transform, "Toe 2", new Vector2(-3.5f, 13f), new Vector2(8f, 8f), pawColor);
            BuildPawCircle(root.transform, "Toe 3", new Vector2(4.5f, 13f), new Vector2(8f, 8f), pawColor);
            BuildPawCircle(root.transform, "Toe 4", new Vector2(12f, 9f), new Vector2(8f, 8f), pawColor);
            root.SetActive(false);
            return root;
        }

        private static void BuildPawCircle(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var circleObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CircleGraphic));
            circleObject.transform.SetParent(parent, false);
            CircleGraphic circle = circleObject.GetComponent<CircleGraphic>();
            circle.color = color;
            circle.raycastTarget = false;
            Anchor(
                circle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size);
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
            viewportImage.color = new Color(0.08f, 0.12f, 0.15f, 0.72f);
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
            var progressBackObject = new GameObject(
                "Hold Progress Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CircleGraphic));
            progressBackObject.transform.SetParent(panel.transform, false);
            CircleGraphic progressBack =
                progressBackObject.GetComponent<CircleGraphic>();
            progressBack.RingThickness = 3f;
            progressBack.color = new Color(0.2f, 0.85f, 1f, 0.16f);
            progressBack.raycastTarget = false;
            Anchor(
                progressBackObject.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(37f, 0f),
                new Vector2(28f, 28f));

            var progressObject = new GameObject(
                "Hold Progress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RadialProgressGraphic));
            progressObject.transform.SetParent(panel.transform, false);
            RadialProgressGraphic progress =
                progressObject.GetComponent<RadialProgressGraphic>();
            progress.RingThickness = 3.5f;
            progress.color = new Color(0.24f, 0.95f, 0.62f, 0.94f);
            progress.raycastTarget = false;
            progress.enabled = false;
            Anchor(
                progressObject.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(37f, 0f),
                new Vector2(28f, 28f));
            Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(46f, 28f));
            Anchor(action.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(72f, 0f), new Vector2(-84f, 28f));
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
            background.color = new Color(0.01f, 0.04f, 0.07f, 0.62f);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.9f, 1f, 0.72f);
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
                slotImage.color = new Color(0.02f, 0.05f, 0.08f, 0.56f);
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
                fill.color = new Color(0.05f, 0.08f, 0.10f, 0.62f);
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
                ring.color = new Color(0.20f, 0.28f, 0.34f, 0.72f);
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

        private static TMP_Text BuildMatchTimer(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Match Timer", new Vector2(170f, 32f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 1f);
            Anchor(
                panelRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -128f),
                new Vector2(170f, 32f));
            TMP_Text timer = CreateText(
                panel.transform,
                "Label",
                "04:00",
                20f,
                TextAlignmentOptions.Center);
            timer.color = new Color(0.92f, 0.98f, 1f, 0.92f);
            Stretch(timer.rectTransform);
            return timer;
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
            GameObject panel = CreatePanel(parent, "Inventory", new Vector2(470f, 780f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            Anchor(
                panelRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -260f),
                new Vector2(470f, 780f));
            TMP_Text title = CreateText(panel.transform, "Title", "INVENTORY   [TAB]", 20f, TextAlignmentOptions.TopLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -22f), new Vector2(-48f, 34f));
            Transform grid = CreateGridRoot(panel.transform, "Grid", 0f, 0f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(70f, 70f);
            layout.spacing = new Vector2(8f, 8f);
            layout.padding = new RectOffset(40, 40, 82, 84);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            slots = new InventorySlotView[InventorySlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = BuildInventorySlot(
                    grid,
                    $"Inventory Slot {index + 1}",
                    new Vector2(70f, 70f),
                    (index + 1).ToString(),
                    18f);
            }
            panel.SetActive(false);
            return panel;
        }

        private static GameObject BuildCatExchange(
            Transform parent,
            out InventorySlotView[] playerSlots,
            out InventorySlotView[] catSlots)
        {
            GameObject panel = CreatePanel(parent, "Cat Exchange", new Vector2(780f, 820f));
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.015f, 0.035f, 0.055f, 0.52f);
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            Anchor(
                panelRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(780f, 820f));

            TMP_Text title = CreateText(
                panel.transform,
                "Title",
                "\uACE0\uC591\uC774\uC640 \uC0C1\uD638\uC791\uC6A9",
                24f,
                TextAlignmentOptions.TopLeft);
            Anchor(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(54f, -24f),
                new Vector2(480f, 42f));

            TMP_Text quickTitle = CreateText(
                panel.transform,
                "Quick Slot Title",
                "QUICK SLOTS",
                15f,
                TextAlignmentOptions.TopLeft);
            Anchor(
                quickTitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(42f, -88f),
                new Vector2(220f, 24f));

            GameObject playerBag = CreatePanel(panel.transform, "Player Bag", new Vector2(460f, 632f));
            Image playerImage = playerBag.GetComponent<Image>();
            if (playerImage != null)
            {
                playerImage.color = new Color(0.02f, 0.06f, 0.09f, 0.48f);
            }

            Anchor(
                playerBag.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -124f),
                new Vector2(460f, 632f));
            TMP_Text playerTitle = CreateText(
                playerBag.transform,
                "Title",
                "\uB3C4\uB451 \uAC00\uBC29",
                21f,
                TextAlignmentOptions.TopLeft);
            Anchor(playerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -18f), new Vector2(-44f, 34f));
            Transform playerGridRoot = CreateGridRoot(playerBag.transform, "Grid", 0f, 0f);
            var playerGrid = playerGridRoot.gameObject.AddComponent<GridLayoutGroup>();
            playerGrid.cellSize = new Vector2(70f, 70f);
            playerGrid.spacing = new Vector2(8f, 8f);
            playerGrid.padding = new RectOffset(35, 35, 72, 72);
            playerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            playerGrid.constraintCount = 5;
            playerSlots = new InventorySlotView[InventorySlotCount];
            for (int index = 0; index < playerSlots.Length; index++)
            {
                playerSlots[index] = BuildInventorySlot(
                    playerGridRoot,
                    $"Player Exchange Slot {index + 1}",
                    new Vector2(70f, 70f),
                    (index + 1).ToString(),
                    18f);
            }

            GameObject catBag = CreatePanel(panel.transform, "Cat Bag", new Vector2(252f, 302f));
            Image catImage = catBag.GetComponent<Image>();
            if (catImage != null)
            {
                catImage.color = new Color(0.02f, 0.06f, 0.09f, 0.50f);
            }

            Anchor(
                catBag.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(504f, -424f),
                new Vector2(252f, 302f));
            TMP_Text catTitle = CreateText(
                catBag.transform,
                "Title",
                "\uACE0\uC591\uC774 \uAC00\uBC29",
                21f,
                TextAlignmentOptions.TopLeft);
            Anchor(catTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -18f), new Vector2(-44f, 34f));
            Transform catGridRoot = CreateGridRoot(catBag.transform, "Grid", 0f, 0f);
            var catGrid = catGridRoot.gameObject.AddComponent<GridLayoutGroup>();
            catGrid.cellSize = new Vector2(82f, 82f);
            catGrid.spacing = new Vector2(12f, 12f);
            catGrid.padding = new RectOffset(38, 38, 74, 28);
            catGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            catGrid.constraintCount = 2;
            catSlots = new InventorySlotView[CatBagSlotCount];
            for (int index = 0; index < catSlots.Length; index++)
            {
                catSlots[index] = BuildInventorySlot(
                    catGridRoot,
                    $"Cat Bag Slot {index + 1}",
                    new Vector2(82f, 82f),
                    (index + 1).ToString(),
                    24f);
            }

            TMP_Text hint = CreateText(
                panel.transform,
                "Hint",
                "\uC544\uC774\uD15C \uCE78\uC744 \uD074\uB9AD\uD574\uC11C \uC804\uB2EC\uD558\uAC70\uB098 \uB3CC\uB824\uBC1B\uAE30",
                15f,
                TextAlignmentOptions.Bottom);
            hint.color = new Color(0.85f, 0.95f, 1f, 0.82f);
            Anchor(
                hint.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(34f, 34f),
                new Vector2(-42f, 28f));

            panel.SetActive(false);
            return panel;
        }

        private static Transform CreateGridRoot(
            Transform parent,
            string name,
            float topOffset,
            float bottomOffset)
        {
            var gridObject = new GameObject(name, typeof(RectTransform));
            gridObject.transform.SetParent(parent, false);
            RectTransform rect = gridObject.GetComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(0f, bottomOffset);
            rect.offsetMax = new Vector2(0f, -topOffset);
            return gridObject.transform;
        }

        private static InventorySlotView BuildInventorySlot(
            Transform parent,
            string name,
            Vector2 size,
            string keyText,
            float glyphSize)
        {
            GameObject slot = CreatePanel(parent, name, size);
            TMP_Text key = CreateText(slot.transform, "Key", keyText, 12f, TextAlignmentOptions.TopLeft);
            TMP_Text quantity = CreateText(slot.transform, "Quantity", string.Empty, 12f, TextAlignmentOptions.BottomRight);
            TMP_Text itemName = CreateText(slot.transform, "Item Name", string.Empty, 11f, TextAlignmentOptions.Top);
            TMP_Text price = CreateText(slot.transform, "Price", string.Empty, 12f, TextAlignmentOptions.Right);
            TMP_Text glyph = CreateText(slot.transform, "Item Glyph", string.Empty, glyphSize, TextAlignmentOptions.Center);
            Image icon = CreateImage(slot.transform, "Item Icon", Color.white);
            Image priceIcon = CreateImage(slot.transform, "Currency Icon", Color.white);
            Image selected = CreateImage(slot.transform, "Selected Frame", new Color(1f, 0.82f, 0.2f, 0.34f));
            Image disabled = CreateImage(slot.transform, "Disabled", new Color(0f, 0f, 0f, 0.22f));
            Anchor(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -6f), new Vector2(28f, 22f));
            Anchor(quantity.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 6f), new Vector2(24f, 22f));
            Anchor(itemName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -17f), new Vector2(-16f, 22f));
            Anchor(price.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-33f, 8f), new Vector2(44f, 18f));
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 24f));
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 24f));
            Anchor(priceIcon.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(14f, 14f));
            itemName.color = new Color(1f, 0.96f, 0.86f, 0.96f);
            price.color = new Color(1f, 0.85f, 0.16f, 1f);
            priceIcon.raycastTarget = false;
            Stretch(selected.rectTransform);
            Stretch(disabled.rectTransform);
            Button button = slot.AddComponent<Button>();
            button.targetGraphic = slot.GetComponent<Image>();
            InventorySlotView view = slot.AddComponent<InventorySlotView>();
            view.Configure(key, quantity, icon, selected, disabled, glyph, itemName, price, priceIcon);
            return view;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.08f, 0.68f);
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
