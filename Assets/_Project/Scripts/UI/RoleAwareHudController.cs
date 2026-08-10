using System;
using System.Collections.Generic;
using System.Text;
using PawliceAndPurrglar.Audio;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Arrest;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Input;
using PawliceAndPurrglar.Integration.Voice;
using PawliceAndPurrglar.Match;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
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
        private const int CatBagSlotCount =
            PawliceAndPurrglar.Companions.CatInventoryInteractable.CatBagSlotCount;

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

        // The screen heading and the heading over the container grid. Both
        // said "고양이" when the cat was the only container there was.
        [SerializeField] private TMP_Text exchangeTitle;
        [SerializeField] private TMP_Text exchangeContainerTitle;
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
        private PawliceAndPurrglar.Gameplay.Arrest.ThiefJailState jailState;
        private ThiefLootWallet thiefWallet;
        private PoliceWallet policeWallet;
        private ToolCarrier carrier;

        /// <summary>
        /// The thief's loot bag, for the bag screen and the merchant screen.
        /// </summary>
        private LootCarrier lootCarrier;

        /// <summary>
        /// Both currency badges, found rather than assigned.
        ///
        /// An editor script's assignment into a serialized array of components is
        /// the thing that vanished in <c>ISSUE-031</c> and left a bar list empty in
        /// the build while the editor looked fine. Asking the hierarchy at runtime
        /// cannot go stale.
        /// </summary>
        private CurrencyBadgeView[] currencyBadges = Array.Empty<CurrencyBadgeView>();

        /// <summary>The raccoon's ledger, made the first time it is needed.</summary>
        private MerchantTradePresenter merchantWindow;
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
        // Whatever container is open: the cat's bag, a cupboard, a till.
        // Typed as the interface because the panel does the same job for all
        // of them, and a second screen per container kind would be a second
        // place for "the icon moved but the item did not".
        private ISlotContainer activeContainer;

        /// <summary>The canvas's one hover tooltip, found on first use.</summary>
        private ItemTooltipView itemTooltip;
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
            TMP_Text configuredExchangeTitle,
            TMP_Text configuredExchangeContainerTitle,
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
            exchangeTitle = configuredExchangeTitle;
            exchangeContainerTitle = configuredExchangeContainerTitle;
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
            GameplayInputRouter.ContextInteractionPressed += HandleInteractPressed;
            GameplayInputRouter.ScreenInteractionPressed += HandleCatBagPressed;
            // The cat's bag is deliberately absent from this list. It used to be
            // opened by a static event raised inside the interactable, which runs
            // on the **host** because the interact key is forwarded there — so a
            // thief on a client opened their own two bags on the officer's screen
            // (`ISSUE-055`). It is opened in HandleInteractPressed instead, on the
            // machine that pressed the key.
            SearchableContainer.SearchCompleted += OpenContainerExchange;
            GameplayInputRouter.TakeAllPressed += TakeEverythingFromContainer;
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

            // Added here rather than baked into the HUD prefab, for the same
            // reason the merchant window is: it builds its own label at runtime,
            // and a label an editor script writes into a prefab is one more thing
            // that can come back from disk switched off or unreadable.
            if (GetComponent<InteriorLootTallyPresenter>() == null)
            {
                gameObject.AddComponent<InteriorLootTallyPresenter>();
            }
        }

        private void OnDisable()
        {
            GameplayInputRouter.InventoryTogglePressed -= ToggleInventory;
            GameplayInputRouter.EscapePressed -= HandleEscape;
            GameplayInputRouter.BindingDisplayChanged -= BindBindingLabels;
            GameplayInputRouter.AnimalCommandPressed -= HandleAnimalCommandPressed;
            GameplayInputRouter.VoicePressed -= HandleVoicePressed;
            GameplayInputRouter.ContextInteractionPressed -= HandleInteractPressed;
            GameplayInputRouter.ScreenInteractionPressed -= HandleCatBagPressed;
            SearchableContainer.SearchCompleted -= OpenContainerExchange;
            GameplayInputRouter.TakeAllPressed -= TakeEverythingFromContainer;
            UnsubscribeDispatcher();
            if (buttonListenersBound)
            {
                bagButton?.onClick.RemoveListener(ToggleInventory);
                voiceButton?.onClick.RemoveListener(ToggleVoiceCapture);
                buttonListenersBound = false;
            }

            ClearSlotListeners();
            HideItemTooltip();
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
            ClearExchangeSlots(inventorySlots);

            // The thief's own bag cells answer a click too, now that the cat's bag
            // sits beside them rather than being redrawn inside its own screen.
            // They keep their drag handler: a click and a drag are different
            // gestures and Unity delivers both.
            slotListenersBound =
                BindExchangeSlots(exchangePlayerSlots, HandleExchangePlayerSlotClicked)
                | BindExchangeSlots(exchangeCatSlots, HandleExchangeCatSlotClicked)
                | BindExchangeSlots(inventorySlots, HandleBagSlotClicked);
        }

        /// <summary>
        /// A click on the thief's own bag while the cat's is open.
        ///
        /// Silent when the cat's bag is closed, because then a click on a bag cell
        /// is the start of a drag and moving the item somewhere would be a gesture
        /// the player did not make.
        /// </summary>
        private void HandleBagSlotClicked(int index)
        {
            if (!catExchangeOpen || activeContainer == null)
            {
                return;
            }

            HandleExchangePlayerSlotClicked(index);
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
            ClearExchangeSlots(inventorySlots);
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
            BindCurrencyBadges();
            BindMerchantWindow();
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

        /// <summary>
        /// Applies a drag from one cell onto another.
        ///
        /// Two stores meet on this grid — the four prop quick slots and the loot
        /// cells — and an item cannot cross between them: a prop slot holds a
        /// <c>ThrowableKind</c> the thrower reads, and a loot cell holds real
        /// world objects with their own state and replication. A cross-store drop
        /// is refused **with a sentence on screen**, because a refusal the player
        /// cannot see is indistinguishable from the drag not working, and they
        /// will try it again rather than aim somewhere else.
        /// </summary>
        public void HandleSlotDrop(
            InventorySlotDragHandler from,
            InventorySlotDragHandler to)
        {
            if (from == null || to == null)
            {
                return;
            }

            if (from.Group != to.Group)
            {
                voiceFeed?.ShowMessage(
                    "옮길 수 없어요",
                    from.Group == InventorySlotDragHandler.SlotGroup.Bag
                        ? "보물은 소품 칸에 넣을 수 없어요"
                        : "소품은 보물 칸에 넣을 수 없어요",
                    2f);
                return;
            }

            switch (from.Group)
            {
                case InventorySlotDragHandler.SlotGroup.Bag:
                    lootCarrier?.TryRearrange(from.Index, to.Index);
                    break;
                case InventorySlotDragHandler.SlotGroup.QuickSlot:
                    if (carrier == null)
                    {
                        break;
                    }

                    // Asked for, or done — never both.
                    //
                    // Doing both was the first attempt and it made the drag look
                    // broken on exactly one of the two machines: the client applied
                    // the swap, the host's next slot update arrived with the old
                    // arrangement, and the icon sprang back to where it started
                    // before the host's own swap landed. Two visible jumps for one
                    // drag reads as the drag failing.
                    if (carrier.IsRemoteControlled)
                    {
                        GameplayInputRouter.RequestQuickSlotSwap(
                            from.Index,
                            to.Index);
                    }
                    else
                    {
                        carrier.TrySwapSlots(from.Index, to.Index);
                    }

                    break;
            }
        }

        public void ToggleInventory()
        {
            SetInventoryOpen(!inventoryOpen);
        }

        public void SetInventoryOpen(bool open)
        {
            // Only when it actually moves. The key toggles, but this is also
            // called to force the bag shut on other paths, and a zip on every one
            // of those is a sound with nothing on screen to match it.
            if (inventoryOpen != open)
            {
                GameSoundService.Request(GameSoundId.InventoryToggle);
            }

            inventoryOpen = open;

            // Closing the bag takes the cat's with it, and opening it leaves the
            // cat's alone. The two are one screen the player reads across — a bag
            // that shut and left a cat's bag floating beside nothing would be a
            // panel with no counterpart to move things to.
            if (!open && catExchangeOpen)
            {
                SetCatExchangeOpen(false);
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(open);
            }

            HideItemTooltip();
            RefreshInputSuppression();
        }

        public void OpenContainerExchange(
            ISlotContainer container,
            ToolCarrier playerCarrier)
        {
            if (container == null || playerCarrier == null)
            {
                return;
            }

            // The thief's screen and nobody else's. Searching is the thief's half
            // of the game, and this is the second lock on the same door as
            // `ISSUE-055`: whatever reaches this method, an officer never ends up
            // looking at somebody else's bag.
            if (ResolveRole() != PlayerRole.Thief)
            {
                return;
            }

            activeContainer = container;
            exchangeCarrier = playerCarrier;
            if (exchangeTitle != null)
            {
                exchangeTitle.text = $"{container.DisplayName} 수색";
            }

            if (exchangeContainerTitle != null)
            {
                exchangeContainerTitle.text = container.DisplayName;
            }

            // Both, side by side. The thief's own bag is the other half of the
            // exchange: opening only the cat's would leave the player clicking on
            // a grid with nowhere to put anything.
            SetCatExchangeOpen(true);
            SetInventoryOpen(true);

            // Headed with the container's own name. The player is looking at two
            // grids of the same icons, and which one is theirs is the only thing
            // the heading has to answer.
            voiceFeed?.ShowMessage(
                container.DisplayName,
                "아이템을 눌러 서로 옮길 수 있어요",
                2.5f);
        }

        private void SetCatExchangeOpen(bool open)
        {
            catExchangeOpen = open;
            if (!open)
            {
                activeContainer = null;
                exchangeCarrier = null;
            }

            if (catExchangePanel != null)
            {
                catExchangePanel.SetActive(open);
            }

            HideItemTooltip();
            RefreshInputSuppression();
        }

        /// <summary>
        /// Puts the hover tooltip away when a panel opens or closes.
        ///
        /// Called on both edges, not just the closing one. A panel appearing
        /// under the cursor gets no pointer-enter either, so a tooltip left over
        /// from the bag would sit on top of the cat's bag describing a cell that
        /// has moved.
        ///
        /// Found rather than assigned, and looked up once. It is the only HUD
        /// reference not threaded through <c>Configure</c> — see the note where
        /// it is built.
        /// </summary>
        private void HideItemTooltip()
        {
            itemTooltip ??= GetComponentInChildren<ItemTooltipView>(true);
            if (itemTooltip != null)
            {
                itemTooltip.HideImmediately();
            }
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

            // A real toggle. The key is push-to-talk, but a button cannot be
            // held, so the second press has to be the one that stops — otherwise
            // clicking the microphone always cost the full five seconds.
            if (voice.State == VoiceCommandInputState.Recording)
            {
                voice.StopListening();
                return;
            }

            if (voice.CooldownRemainingSeconds > 0f)
            {
                ShowVoiceCooldownFeedback();
                return;
            }

            voice.StartListening();
        }

        private void HandleEscape()
        {
            // The raccoon's ledger first, because it is the one on top. Escape is
            // the key every player tries on a window they want gone, and the shop
            // was the only screen in the game that ignored it — it closed on E, at
            // the market, which is also the key that opened it and the key that
            // does five other things.
            if (merchantWindow != null && merchantWindow.IsOpen)
            {
                merchantWindow.Close();
                return;
            }

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
                PawliceAndPurrglar.Gameplay.Arrest.ThiefJailState>();
            thiefWallet ??= FindFirstObjectByType<ThiefLootWallet>();
            if (lootConfig == null && GameConfigService.IsInitialized)
            {
                lootConfig = GameConfigService.Current.Loot;
            }

            currencyIcon ??= Resources.Load<Sprite>("UI/CurrencyCoin");
            if (currencyBadges.Length == 0)
            {
                currencyBadges = GetComponentsInChildren<CurrencyBadgeView>(true);
            }

            policeWallet ??=
                FindFirstObjectByType<PoliceWallet>();
            EnsureCatInteractablesInstalled();
            PlayerRole role = ResolveRole();
            if (lootCarrier == null && role == PlayerRole.Thief)
            {
                // Only the thief has one, and only theirs. Taking whichever the
                // scene hands back first would draw the other player's bag on a
                // two-player machine.
                foreach (LootCarrier candidate in
                    FindObjectsByType<LootCarrier>(FindObjectsSortMode.None))
                {
                    PlayerRoleIdentity carrierIdentity =
                        candidate.GetComponent<PlayerRoleIdentity>();
                    if (carrierIdentity == null
                        || carrierIdentity.Role == PlayerRole.Thief)
                    {
                        lootCarrier = candidate;
                        break;
                    }
                }
            }

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

        /// <summary>
        /// The role selector, found once.
        ///
        /// <see cref="ResolveRole"/> is called several times per frame from
        /// <see cref="Update"/> and each call used to sweep every object in the
        /// scene. There is exactly one selector and it lives across scene loads,
        /// so the sweep answered the same thing thousands of times a second on a
        /// scene with thousands of objects — pure cost in a browser, where this
        /// game ships.
        /// </summary>
        private LocalPlayerRoleSelector roleSelector;

        private bool TryResolveRole(out PlayerRole role)
        {
            if (roleSelector == null)
            {
                roleSelector = FindFirstObjectByType<LocalPlayerRoleSelector>();
            }

            LocalPlayerRoleSelector selector = roleSelector;
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
                        : string.Empty,
                    hasItem ? BuildPropTooltip(kind) : default);
                quickSlots[index]?.Bind(model);
            }

            BindInventorySlots();
            BindCatExchangeSlots();
        }

        /// <summary>
        /// Draws the bag: the four prop slots, then the loot cells.
        ///
        /// The loot cells read the thief's own bag. They used to list every
        /// <c>LootItem</c> in the scene, which meant the bag screen showed things
        /// lying in shops across town and put their **price** where a count
        /// belongs — seven identical coins with 20, 10, 20, 10 under them. It
        /// looked like an inventory, so nobody read it as a bug.
        ///
        /// No name and no price in a cell. Both were drawn on top of the icon in
        /// a 70px square, and a cell that says three things says none of them.
        /// The name belongs in a tooltip and the price belongs in the merchant's
        /// ledger, where the player is deciding about money.
        /// </summary>
        private void BindInventorySlots()
        {
            LootBag bag = lootCarrier != null ? lootCarrier.Cells : null;
            LootItem newest = lootCarrier != null ? lootCarrier.LastAcquired : null;
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

                int cell = index - QuickSlotCount;
                LootDefinition definition = null;
                int lootCount = 0;
                bool isNew = false;
                if (!quickSlotIndex
                    && bag != null
                    && bag.TryGetCell(cell, out definition, out lootCount))
                {
                    quantity = lootCount;
                    isNew = newest != null
                        && bag.IndexOf(newest) == cell;
                }

                bool hasLoot = definition != null;
                inventorySlots[index]?.Bind(new InventorySlotViewModel(
                    quickSlotIndex
                        ? GameplayInputRouter.GetQuickSlotLabel(index)
                        : (index + 1).ToString(),
                    hasItem ? GetItemIcon(kind) : GetLootIcon(definition),
                    quantity,
                    selected,
                    !(hasItem || hasLoot),
                    hasItem
                        ? GetItemIcon(kind) == null
                            ? GetItemGlyph(kind)
                            : string.Empty
                        : hasLoot && GetLootIcon(definition) == null
                            ? GetLootGlyph(definition)
                            : string.Empty,
                    string.Empty,
                    0,
                    null,
                    isNew,
                    hasItem
                        ? BuildPropTooltip(kind)
                        : hasLoot
                            ? BuildLootTooltip(definition)
                            : default));
            }
        }

        /// <summary>
        /// What the hover tooltip should say about a prop, and about a piece of
        /// treasure.
        ///
        /// Two lines rather than one, because the two item layers are genuinely
        /// separate: a prop is an enum the whole game switches on, and a piece of
        /// treasure is an authored asset. The words for both are decided in
        /// <see cref="ItemTooltipCatalog"/> — this only picks which of the two is
        /// being asked about, and hands over the icon it already resolved for the
        /// cell so the panel and the cell cannot disagree about what it looks
        /// like.
        /// </summary>
        private ItemTooltipContent BuildPropTooltip(ThrowableKind kind)
        {
            return ItemTooltipCatalog.ForProp(kind, GetItemIcon(kind));
        }

        private ItemTooltipContent BuildLootTooltip(LootDefinition definition)
        {
            return ItemTooltipCatalog.ForLoot(definition, GetLootIcon(definition));
        }

        /// <summary>
        /// A letter for a piece with no artwork yet.
        ///
        /// Most of the thirty-odd loot definitions have no icon, and a cell with a
        /// count in the corner and nothing in the middle reads as a broken cell
        /// rather than as an unfinished one. A letter is at least the same letter
        /// every time, so the player can tell two kinds apart.
        /// </summary>
        private static string GetLootGlyph(LootDefinition definition)
        {
            string name = definition != null ? definition.DisplayName : string.Empty;
            return string.IsNullOrWhiteSpace(name)
                ? "?"
                : name.Substring(0, 1);
        }

        /// <summary>
        /// Opens the raccoon's ledger while the thief is standing at the market,
        /// and closes it when they walk off.
        ///
        /// Opened by standing there rather than by the interact key, which still
        /// does what it always did: hand over the piece in your hands. Two
        /// reasons. The key press is forwarded to the host and executed there, so
        /// hanging a window off it would open the window on whichever machine is
        /// the host rather than on the machine that pressed it. And it leaves the
        /// existing sale path — and the regressions that cover it — untouched.
        ///
        /// The window is created on demand rather than baked into the HUD prefab:
        /// it needs generated sprites and click handlers, and neither survives an
        /// editor script writing a prefab.
        /// </summary>
        private void BindMerchantWindow()
        {
            // Closed by walking away, never opened by standing still. Opening on
            // proximity was the first attempt and it was wrong in a way that only
            // shows up in play: the window appeared before the player had asked
            // for it, and there was nothing to press — so a player who wanted to
            // see the shop had no action to take and read it as broken.
            if (merchantWindow == null || !merchantWindow.IsOpen)
            {
                return;
            }

            // Range only, not role. Both roles have a screen here now, and keeping
            // the thief's condition would have slammed the officer's shop shut on
            // the frame after it opened.
            bool stillAtMarket = scanner != null
                && scanner.CurrentTarget is LootSaleZone;
            if (!stillAtMarket)
            {
                merchantWindow.Close();
            }
        }

        /// <summary>
        /// Opens or closes the raccoon's ledger on the interact key.
        ///
        /// Hung off the local key event rather than off the sale zone's
        /// <c>TryInteract</c>, and that distinction is the whole reason this
        /// works: the key press is forwarded to the host and executed there, so a
        /// window opened from the zone opens on **whichever machine is hosting**
        /// rather than on the machine whose player pressed the key. On a
        /// host-and-client pair that is the wrong screen exactly half the time.
        ///
        /// Refusals say why. An officer pressing E at the market gets a sentence
        /// rather than nothing, because nothing is indistinguishable from the key
        /// being broken — which is what it looked like.
        /// </summary>
        /// <summary>
        /// The cat's bag, on a key press that reaches here whether or not a panel
        /// is already open.
        ///
        /// Split out of <see cref="HandleInteractPressed"/> and hung off the
        /// ungated event. Opening the thief's own bag suppresses gameplay input,
        /// and the gated event is not raised while it is — so a thief with their
        /// bag open pressed E at the cat and nothing at all happened. The
        /// suppression exists to stop the *world* being acted on through an open
        /// panel, and a screen the HUD opens beside that panel is not the world.
        ///
        /// Opened here rather than by the host, which is the other half of
        /// `ISSUE-055`: the interact key is forwarded and run on the host, so a
        /// screen hung off the interaction itself opens on whichever machine is
        /// hosting rather than on the one that pressed the key.
        /// </summary>
        private void HandleCatBagPressed()
        {
            ResolveSources();
            if (scanner == null
                || carrier == null
                || scanner.CurrentTarget is not CatInventoryInteractable catBag
                || ResolveRole() != PlayerRole.Thief)
            {
                return;
            }

            if (catExchangeOpen)
            {
                SetCatExchangeOpen(false);
                return;
            }

            OpenContainerExchange(catBag, carrier);
        }

        private void HandleInteractPressed()
        {
            ResolveSources();
            if (scanner == null)
            {
                return;
            }

            if (scanner.CurrentTarget is not LootSaleZone)
            {
                return;
            }

            merchantWindow ??= gameObject.AddComponent<MerchantTradePresenter>();
            if (merchantWindow.IsOpen)
            {
                merchantWindow.Close();
                return;
            }

            // Both roles trade here, in opposite directions. The thief sells
            // treasure; the officer buys the trap and the sensor that answer a
            // thief who never comes out into the open. One landmark both players
            // walk to is worth more than two shops with one customer each.
            SetInventoryOpen(false);
            if (ResolveRole() == PlayerRole.Thief && lootCarrier != null)
            {
                merchantWindow.Open(lootCarrier, thiefWallet, lootConfig, carrier);
                return;
            }

            if (carrier != null)
            {
                merchantWindow.OpenShop(carrier, policeWallet);
                return;
            }

            voiceFeed?.ShowMessage(
                "너구리 암시장",
                "지금은 거래할 수 없어요",
                2f);
        }

        /// <summary>
        /// How much money the local player has, for both currency badges.
        /// </summary>
        private void BindCurrencyBadges()
        {
            if (currencyBadges == null || currencyBadges.Length == 0)
            {
                return;
            }

            int amount = ResolveRole() == PlayerRole.Thief
                ? GetThiefSoldAmount()
                : policeWallet != null
                    ? policeWallet.Amount
                    : 0;
            foreach (CurrencyBadgeView badge in currencyBadges)
            {
                if (badge != null)
                {
                    badge.Bind(amount);
                }
            }
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

            // A mirror of LootConfig's defaults, reached only when the config is
            // not up. It has to move whenever those move — see the same fallback
            // in MerchantTradePresenter.UnitPrice.
            return definition.Rarity switch
            {
                LootRarity.Uncommon => 70,
                LootRarity.Rare => 100,
                _ => 40
            };
        }

        private void BindCatExchangeSlots()
        {
            // The same twenty-five cells the bag screen draws: four prop slots and
            // then the loot.
            //
            // It used to read the prop carrier for all twenty-five, so cells 5-25
            // asked a four-slot store for slot 17 and drew nothing. A thief with a
            // full bag opened this screen and saw twenty-one empty squares — which
            // reads as the screen having lost the bag, not as "treasure does not go
            // in a cat". The treasure is drawn and the refusal is said out loud on
            // the click.
            ToolCarrier sourceCarrier = exchangeCarrier ?? carrier;
            LootBag bag = lootCarrier != null ? lootCarrier.Cells : null;
            for (int index = 0; index < exchangePlayerSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool quickSlotIndex = index < QuickSlotCount;
                bool hasItem = quickSlotIndex
                    && sourceCarrier != null
                    && sourceCarrier.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);

                LootDefinition definition = null;
                int lootCount = 0;
                if (!quickSlotIndex && bag != null)
                {
                    bag.TryGetCell(index - QuickSlotCount, out definition, out lootCount);
                }

                bool hasLoot = definition != null;
                exchangePlayerSlots[index]?.Bind(new InventorySlotViewModel(
                    quickSlotIndex
                        ? GameplayInputRouter.GetQuickSlotLabel(index)
                        : (index + 1).ToString(),
                    hasItem ? GetItemIcon(kind) : GetLootIcon(definition),
                    hasItem
                        ? sourceCarrier.GetSlotQuantity(index)
                        : lootCount,
                    hasItem
                        && sourceCarrier != null
                        && sourceCarrier.SelectedSlot == index,
                    !(hasItem || hasLoot),
                    hasItem
                        ? GetItemIcon(kind) == null
                            ? GetItemGlyph(kind)
                            : string.Empty
                        : hasLoot && GetLootIcon(definition) == null
                            ? GetLootGlyph(definition)
                            : string.Empty,
                    hasItem
                        ? ThrowableCatalog.GetDisplayName(kind)
                        : hasLoot
                            ? definition.DisplayName
                            : string.Empty,
                    tooltip: hasItem
                        ? BuildPropTooltip(kind)
                        : hasLoot
                            ? BuildLootTooltip(definition)
                            : default));
            }

            for (int index = 0; index < exchangeCatSlots.Length; index++)
            {
                ThrowableKind kind = ThrowableKind.Rock;
                bool hasItem = activeContainer != null
                    && activeContainer.TryGetSlot(index, out kind)
                    && ThrowableCatalog.CanUseInQuickSlot(kind);
                exchangeCatSlots[index]?.Bind(new InventorySlotViewModel(
                    (index + 1).ToString(),
                    hasItem ? GetItemIcon(kind) : null,
                    hasItem ? activeContainer.GetSlotQuantity(index) : 0,
                    false,
                    !hasItem,
                    hasItem && GetItemIcon(kind) == null
                        ? GetItemGlyph(kind)
                        : string.Empty,
                    hasItem ? ThrowableCatalog.GetDisplayName(kind) : string.Empty,
                    tooltip: hasItem ? BuildPropTooltip(kind) : default));
            }
        }

        /// <summary>
        /// Empties the open container into the bag, as far as the bag allows.
        ///
        /// The move itself is <c>ContainerTransfer</c>, the same routine a click
        /// goes through. Bulk transfer with its own copy of take-then-store is
        /// exactly where an item ends up in two places at once.
        /// </summary>
        private void TakeEverythingFromContainer()
        {
            if (!catExchangeOpen
                || activeContainer == null
                || exchangeCarrier == null)
            {
                return;
            }

            TransferResult result = exchangeCarrier.IsRemoteControlled
                ? MoveEverythingThroughHost()
                : ContainerTransfer.MoveEverything(
                    activeContainer,
                    exchangeCarrier);

            if (!result.MovedAnything)
            {
                voiceFeed?.ShowMessage(
                    activeContainer.DisplayName,
                    result.Blocked ? "가방이 가득 찼습니다" : "아무것도 없다",
                    2f);
                return;
            }

            voiceFeed?.ShowMessage(
                activeContainer.DisplayName,
                result.Blocked
                    ? $"{result.Moved}개를 가져왔습니다. 가방이 가득 찼습니다"
                    : $"{result.Moved}개를 가져왔습니다",
                2f);
        }

        /// <summary>
        /// Empties the container into the quick slots on a machine that does not
        /// own them.
        ///
        /// Bounded by the number of *empty* quick slots rather than by asking
        /// whether each item fits. The local carrier does not change as the
        /// requests go out — the host applies them and replicates back a frame or
        /// two later — so <c>CanStore</c> would keep answering "yes" for the same
        /// free slot and the surplus would leave the bag with nowhere to arrive.
        /// One item per empty slot is provably safe whatever the host does with
        /// them: identical kinds stack and free even more room.
        /// </summary>
        private TransferResult MoveEverythingThroughHost()
        {
            int room = 0;
            for (int slot = 0; slot < QuickSlotCount; slot++)
            {
                if (!exchangeCarrier.TryGetSlot(slot, out _))
                {
                    room++;
                }
            }

            int moved = 0;
            bool blocked = false;
            for (int index = 0; index < activeContainer.SlotCount; index++)
            {
                while (activeContainer.TryGetSlot(index, out ThrowableKind kind))
                {
                    if (moved >= room)
                    {
                        blocked = true;
                        break;
                    }

                    if (!activeContainer.TryTakeOne(index, out kind))
                    {
                        break;
                    }

                    GameplayInputRouter.RequestCatBagTransfer(
                        false,
                        index,
                        (int)kind);
                    moved++;
                }
            }

            return new TransferResult(moved, blocked);
        }

        private void HandleExchangePlayerSlotClicked(int index)
        {
            if (!catExchangeOpen
                || activeContainer == null
                || exchangeCarrier == null)
            {
                return;
            }

            // Said out loud rather than ignored. A treasure cell that does nothing
            // when clicked is indistinguishable from a click that did not
            // register, and the player tries it again rather than aiming
            // elsewhere.
            if (index >= QuickSlotCount)
            {
                voiceFeed?.ShowMessage(
                    activeContainer.DisplayName,
                    "보물은 넣을 수 없어요. 소품 칸만 옮겨져요",
                    2f);
                return;
            }

            if (!exchangeCarrier.TryGetSlot(index, out ThrowableKind kind))
            {
                return;
            }

            if (!MoveBetweenQuickSlotsAndContainer(true, index, kind))
            {
                voiceFeed?.ShowMessage(
                    activeContainer.DisplayName,
                    "빈 칸이 없어요",
                    2f);
                return;
            }

            voiceFeed?.ShowMessage(
                $"{activeContainer.DisplayName}에 넣음",
                ThrowableCatalog.GetDisplayName(kind),
                1.5f);
        }

        private void HandleExchangeCatSlotClicked(int index)
        {
            if (!catExchangeOpen
                || activeContainer == null
                || exchangeCarrier == null)
            {
                return;
            }

            if (!activeContainer.TryGetSlot(index, out ThrowableKind kind))
            {
                return;
            }

            if (!MoveBetweenQuickSlotsAndContainer(false, index, kind))
            {
                voiceFeed?.ShowMessage("도둑 가방", "퀵슬롯이 가득 찼어요", 2f);
                return;
            }

            voiceFeed?.ShowMessage(
                "가방으로 받음",
                ThrowableCatalog.GetDisplayName(kind),
                1.5f);
        }

        /// <summary>
        /// Moves one prop across the exchange, on whichever machine owns each half.
        ///
        /// The container — the cat's bag, a cupboard — is this machine's own: no
        /// other screen reads it and the officer never sees it. The quick slots are
        /// the host's, replicated back every frame. So a click applied wholly
        /// locally on a client puts the banana in the bag and then watches the
        /// host's next update put it back in the slot as well: one click, two
        /// bananas. The container half is moved here and the slot half is asked of
        /// the host, and the room is checked before anything leaves the container
        /// so a refusal on the far side cannot swallow the item.
        ///
        /// Offline and on a host both take the direct path, which is what keeps the
        /// editor tests and the single-machine playtest honest.
        /// </summary>
        private bool MoveBetweenQuickSlotsAndContainer(
            bool intoContainer,
            int index,
            ThrowableKind kind)
        {
            if (!exchangeCarrier.IsRemoteControlled)
            {
                TransferResult result = intoContainer
                    ? ContainerTransfer.MoveOne(
                        exchangeCarrier,
                        activeContainer,
                        index)
                    : ContainerTransfer.MoveOne(
                        activeContainer,
                        exchangeCarrier,
                        index);
                return result.MovedAnything;
            }

            if (intoContainer)
            {
                if (!activeContainer.CanStore(kind, 1)
                    || !activeContainer.TryStore(kind, 1))
                {
                    return false;
                }

                GameplayInputRouter.RequestCatBagTransfer(
                    true,
                    index,
                    (int)kind);
                return true;
            }

            if (!exchangeCarrier.CanStore(kind, 1)
                || !activeContainer.TryTakeOne(index, out kind))
            {
                return false;
            }

            GameplayInputRouter.RequestCatBagTransfer(false, index, (int)kind);
            return true;
        }

        /// <summary>
        /// Nothing, since the key bindings it labelled were removed.
        ///
        /// Left as an empty loop rather than deleted because `animalCommands` is
        /// a serialised array on a prefab: a HUD built before 2026-08-10 still
        /// has four rows in it, and this is what leaves them saying whatever
        /// they last said instead of throwing.
        /// </summary>
        private void BindBindingLabels()
        {
            for (int index = 0; index < animalCommands.Length; index++)
            {
                animalCommands[index]?.Bind(new AnimalCommandShortcutViewModel(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    true));
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

            // The hand-drawn icon if a piece has one, then the one baked from its
            // own model.
            //
            // Hand first, on purpose. Baking exists because twenty-nine kinds
            // share eleven pieces of artwork and the rest were drawn as a letter,
            // and it must not overwrite the drawings when they arrive — a render
            // of a model is a stand-in, not the answer.
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
            sprite ??= Resources.Load<Sprite>($"UI/ItemIcons/Loot/{stableId}");
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
            // Beside the bag, not over it. Reasserted here as well as in the
            // builder because a HUD loaded from the prefab carries whatever
            // geometry the prefab was saved with, and the prefab is regenerated
            // by hand.
            ApplyRect(
                "Cat Exchange",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                HudRuntimeInstaller.CatBagPanelPosition,
                new Vector2(
                    HudRuntimeInstaller.CatBagPanelWidth,
                    HudRuntimeInstaller.CatBagPanelHeight));
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

        /// <summary>
        /// The cat's bag, two by two.
        ///
        /// Kept here and in <c>CatInventoryInteractable</c>, and they have to
        /// agree: the panel decides how many cells are drawn and the container
        /// decides how many exist. If they disagree the extra cells ask a store
        /// for slots it does not have and draw nothing — which is exactly how the
        /// old exchange screen came to show twenty-one permanently empty squares.
        /// </summary>
        private const int CatBagSlotCount =
            PawliceAndPurrglar.Companions.CatInventoryInteractable.CatBagSlotCount;

        private const int CatBagColumns = 2;
        private const float CatBagCellSize = 86f;
        private const float CatBagCellGap = 12f;

        // 2 x 86 + 12 = 184, plus 28 either side.
        internal const float CatBagPanelWidth = 240f;

        // The same grid plus 70 above for the heading and 46 below for the hint.
        internal const float CatBagPanelHeight = 300f;

        /// <summary>
        /// Immediately right of the bag panel, which is 470 wide at x = 24.
        ///
        /// Beside rather than over. The two are read together — take from one,
        /// put in the other — and a panel that covers the bag would mean the
        /// player cannot see what they are moving out of.
        /// </summary>
        internal static readonly Vector2 CatBagPanelPosition =
            new(510f, -260f);

        /// <summary>
        /// How many of the bag's cells are prop quick slots rather than loot.
        ///
        /// The same number as the controller's, and it has to stay the same: the
        /// builder decides which cells get a prop drag handler and the controller
        /// decides which cells read the loot bag. If they disagree, one cell is
        /// drawn from one store and dragged in the other.
        /// </summary>
        private const int QuickSlotCount = 4;

        /// <summary>
        /// How much of a slot the item icon fills.
        ///
        /// 0.8 sits in the middle of the 75-85% the brief asks for. What is left is
        /// the corners, and the corners are what the key number, the quantity and
        /// the selection frame need: pushing it further makes the icon bigger and
        /// the count that says there are three of them unreadable.
        ///
        /// A share rather than the fixed 24px inset this replaced. That inset made
        /// the same artwork 66% of a 70px slot and 71% of an 82px one, so an item
        /// changed size depending on which panel it was in.
        /// </summary>
        private const float IconShareOfSlot = 0.88f;

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

            // Under the role panel, on screen the whole match. The thief's win
            // condition is a number, and putting it only inside the bag would
            // make "am I close yet?" a question you have to stop running to ask.
            BuildCurrencyBadge(
                canvasObject.transform,
                "Screen Currency",
                CurrencyBadgeView.Placement.Screen,
                new Vector2(1f, 1f),
                new Vector2(-24f, -396f),
                new Vector2(186f, 52f));
            QuickSlotView[] quickSlots = BuildQuickSlots(canvasObject.transform);

            // The `ANIMAL COMMANDS` panel that listed `CTRL + 1..4` was built
            // here until 2026-08-10. The keys were the stand-in for voice while
            // there was no microphone; with them gone the panel described a
            // control that does not exist. What replaced it is
            // `CompanionVoiceCommandTableView`, which installs itself at runtime
            // and prints the phrases to say — the top-left corner is now its.
            //
            // `Configure` still takes the array so the signature every test and
            // the prefab builder calls does not move. Empty means the binding
            // loop does nothing.
            AnimalCommandShortcutView[] animalCommands =
                System.Array.Empty<AnimalCommandShortcutView>();
            MicrophoneStatusView microphone = BuildMicrophone(
                canvasObject.transform,
                out Button voiceButton);
            VoiceCommandFeedView voiceFeed = BuildVoiceFeed(canvasObject.transform);
            ContextInteractionPromptView context = BuildContextPrompt(canvasObject.transform);
            GameObject inventoryPanel = BuildInventory(canvasObject.transform, out InventorySlotView[] inventorySlots);
            GameObject catExchangePanel = BuildCatExchange(
                canvasObject.transform,
                out InventorySlotView[] exchangePlayerSlots,
                out InventorySlotView[] exchangeCatSlots,
                out TMP_Text exchangeTitle,
                out TMP_Text exchangeContainerTitle);
            Button bagButton = BuildBagButton(canvasObject.transform);
            MinimapHudController minimap = BuildMinimap(canvasObject.transform);
            BuildSensorRadar(canvasObject.transform, canvasObject);

            // Last, so it is the last sibling and therefore drawn over every
            // panel it can describe. One for the whole canvas: the bag, the four
            // quick slots, the cat's bag and the merchant's grid all share it,
            // and it re-claims the top when it appears because the merchant's
            // window is added after this.
            //
            // Not handed to Configure. That signature is called by the prefab
            // builder, by the tests and by the scene, and this is the one thing
            // in the HUD the controller can find for itself without risking the
            // stale-serialized-reference problem that emptied a bar list in
            // ISSUE-031.
            BuildItemTooltip(canvasObject.transform);

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
                exchangeTitle,
                exchangeContainerTitle,
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

            // The panel behind the slot is an Image, so it is already something
            // the pointer can enter. Nothing else needs configuring — the trigger
            // finds the view beside it and the tooltip on the canvas.
            slot.AddComponent<ItemSlotTooltipTrigger>();
            return view;
        }

        /// <summary>
        /// Panel width. Height follows the text.
        /// </summary>
        internal const float TooltipWidth = 320f;

        /// <summary>
        /// The tooltip's icon, at the size the bag draws one.
        /// </summary>
        private const float TooltipIconSize = 46f;

        /// <summary>
        /// How much taller than its font a text rect has to be.
        ///
        /// TMP draws nothing at all when a rect is shorter than one line and the
        /// overflow mode is Ellipsis — not a clipped line, nothing (ISSUE-047).
        /// The labels here are set to Overflow so that cannot happen, and the
        /// rects are still given the headroom so a wrapped second line has
        /// somewhere to go.
        /// </summary>
        private const float MinimumTextHeightFactor = 1.45f;

        /// <summary>
        /// The one hover tooltip for the whole canvas.
        ///
        /// A holder that stretches over the canvas but draws nothing, and a panel
        /// child that is switched off until it is needed. The split is what lets
        /// the hover delay be counted while nothing is visible — a timer on the
        /// panel would only run once the panel was already up.
        ///
        /// Sized by a vertical layout and a content fitter rather than by hand,
        /// because the description is the one line whose length is not known in
        /// advance. Every graphic in it has raycasts off: a tooltip the pointer
        /// can hit steals the exit event for the cell underneath it, and then it
        /// flickers as fast as the mouse moves.
        /// </summary>
        private static ItemTooltipView BuildItemTooltip(Transform parent)
        {
            var holderObject = new GameObject("Item Tooltip", typeof(RectTransform));
            holderObject.transform.SetParent(parent, false);
            Stretch(holderObject.GetComponent<RectTransform>());

            GameObject panel = CreatePanel(
                holderObject.transform,
                "Panel",
                new Vector2(TooltipWidth, 132f));
            Skin(
                panel,
                HudPanelSkin.Shape.Window,
                HudSpriteLibrary.PanelFill,
                HudSpriteLibrary.Border,
                10,
                2);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Pivoted top-left, which is what the positioning maths assumes: the
            // anchored position is the corner nearest the cursor, so flipping
            // sides is one subtraction rather than a second set of rules.
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(TooltipWidth, 132f);
            panel.GetComponent<Image>().raycastTarget = false;

            var fade = panel.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            fade.interactable = false;

            var column = panel.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 12, 12);
            column.spacing = 6f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerObject = new GameObject("Header", typeof(RectTransform));
            headerObject.transform.SetParent(panel.transform, false);
            var headerRow = headerObject.AddComponent<HorizontalLayoutGroup>();
            headerRow.spacing = 12f;
            headerRow.childAlignment = TextAnchor.UpperLeft;
            headerRow.childControlWidth = true;
            headerRow.childControlHeight = true;
            headerRow.childForceExpandWidth = false;
            headerRow.childForceExpandHeight = false;
            LayoutElement headerElement = headerObject.AddComponent<LayoutElement>();
            headerElement.minHeight = TooltipIconSize;

            Image icon = CreateImage(headerObject.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            LayoutElement iconElement = icon.gameObject.AddComponent<LayoutElement>();
            iconElement.minWidth = TooltipIconSize;
            iconElement.minHeight = TooltipIconSize;
            iconElement.preferredWidth = TooltipIconSize;
            iconElement.preferredHeight = TooltipIconSize;

            var titlesObject = new GameObject("Titles", typeof(RectTransform));
            titlesObject.transform.SetParent(headerObject.transform, false);
            var titleColumn = titlesObject.AddComponent<VerticalLayoutGroup>();
            titleColumn.spacing = 2f;
            titleColumn.childAlignment = TextAnchor.UpperLeft;
            titleColumn.childControlWidth = true;
            titleColumn.childControlHeight = true;
            titleColumn.childForceExpandWidth = true;
            titleColumn.childForceExpandHeight = false;
            LayoutElement titlesElement = titlesObject.AddComponent<LayoutElement>();
            titlesElement.flexibleWidth = 1f;

            TMP_Text name = BuildTooltipLabel(
                titlesObject.transform,
                "Item Name",
                22f,
                HudSpriteLibrary.Gold);
            TMP_Text category = BuildTooltipLabel(
                titlesObject.transform,
                "Category",
                15f,
                new Color(0.62f, 0.78f, 0.88f, 0.88f));
            TMP_Text description = BuildTooltipLabel(
                panel.transform,
                "Description",
                16f,
                new Color(0.93f, 0.96f, 0.98f, 0.96f));
            TMP_Text usage = BuildTooltipLabel(
                panel.transform,
                "Usage Hint",
                14f,
                HudSpriteLibrary.Accent);

            var view = holderObject.AddComponent<ItemTooltipView>();
            view.Configure(
                panelRect,
                fade,
                icon,
                name,
                category,
                description,
                usage);
            panel.SetActive(false);
            return view;
        }

        private static TMP_Text BuildTooltipLabel(
            Transform parent,
            string name,
            float fontSize,
            Color color)
        {
            TMP_Text label = CreateText(
                parent,
                name,
                string.Empty,
                fontSize,
                TextAlignmentOptions.TopLeft);
            label.color = color;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = true;
            LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
            element.minHeight = fontSize * MinimumTextHeightFactor;
            return label;
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
            SkinWindow(panel);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            Anchor(
                panelRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -260f),
                new Vector2(470f, 780f));

            TMP_Text title = CreateText(panel.transform, "Title", "INVENTORY", 20f, TextAlignmentOptions.Left);
            title.color = HudSpriteLibrary.Accent;

            // 34px of rect for a 20pt line, and the height is the part that
            // matters: TMP draws nothing at all when a rect is shorter than one
            // line, so a heading that fits in theory disappears in practice
            // (ISSUE-047). 1.45x the point size is the floor used everywhere here.
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -24f), new Vector2(-196f, 34f));

            BuildCurrencyBadge(
                panel.transform,
                "Bag Currency",
                CurrencyBadgeView.Placement.Bag,
                new Vector2(1f, 1f),
                new Vector2(-18f, -18f),
                new Vector2(146f, 44f));

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

                // The first four cells are the prop quick slots and the rest are
                // loot cells, so a cell's place on screen is not its place in
                // either store. Both numbers are written down here, once.
                slots[index].SetCellIndex(index);
                bool isQuickSlot = index < QuickSlotCount;
                slots[index].gameObject
                    .AddComponent<InventorySlotDragHandler>()
                    .Configure(
                        isQuickSlot
                            ? InventorySlotDragHandler.SlotGroup.QuickSlot
                            : InventorySlotDragHandler.SlotGroup.Bag,
                        isQuickSlot ? index : index - QuickSlotCount);
            }

            TMP_Text hint = CreateText(
                panel.transform,
                "Hint",
                "칸을 끌어서 정리할 수 있어요",
                14f,
                TextAlignmentOptions.Center);
            hint.color = new Color(0.72f, 0.86f, 0.94f, 0.72f);
            Anchor(
                hint.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 30f),
                new Vector2(-40f, 24f));

            panel.SetActive(false);
            return panel;
        }

        /// <summary>
        /// A coin and a figure. Used in the bag header and on the screen itself.
        /// </summary>
        internal static CurrencyBadgeView BuildCurrencyBadge(
            Transform parent,
            string name,
            CurrencyBadgeView.Placement placement,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            GameObject badge = CreatePanel(parent, name, size);
            Skin(
                badge,
                HudPanelSkin.Shape.Solid,
                new Color(0.02f, 0.05f, 0.08f, 0.92f),
                Color.clear,
                14,
                0);
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.pivot = anchor;
            Anchor(rect, anchor, anchor, position, size);

            Image coin = CreateImage(badge.transform, "Coin", Color.white);
            coin.sprite = Resources.Load<Sprite>("UI/CurrencyCoin");
            coin.preserveAspect = true;
            coin.raycastTarget = false;

            // Switched off rather than left as a blank Image. An Image with no
            // sprite is a white square, not nothing (ISSUE-050's sibling), and a
            // white square next to the gold figure reads as a broken icon.
            coin.enabled = coin.sprite != null;
            Anchor(
                coin.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(26f, 26f));

            TMP_Text amount = CreateText(
                badge.transform,
                "Amount",
                "0",
                21f,
                TextAlignmentOptions.Right);
            amount.color = HudSpriteLibrary.Gold;
            Anchor(
                amount.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(9f, 0f),
                new Vector2(-58f, 32f));

            var view = badge.AddComponent<CurrencyBadgeView>();
            view.Configure(placement, amount, coin);
            return view;
        }

        internal static void SkinWindow(GameObject panel)
        {
            Skin(
                panel,
                HudPanelSkin.Shape.Window,
                HudSpriteLibrary.PanelFill,
                HudSpriteLibrary.Border,
                14,
                2);
        }

        internal static void Skin(
            GameObject target,
            HudPanelSkin.Shape shape,
            Color fill,
            Color border,
            int cornerRadius,
            int borderWidth)
        {
            if (target == null)
            {
                return;
            }

            HudPanelSkin skin = target.GetComponent<HudPanelSkin>()
                ?? target.AddComponent<HudPanelSkin>();
            skin.Configure(shape, fill, border, cornerRadius, borderWidth);
        }

        /// <summary>
        /// The cat's bag: two by two, beside the thief's own bag rather than
        /// inside a screen of its own.
        ///
        /// It used to be a 780x820 window that redrew the thief's twenty-five
        /// cells next to the cat's four — a second copy of a screen the player had
        /// just been looking at, with its own bugs. The bag they already know is
        /// the bag, and the cat's is a small panel that appears next to it.
        /// </summary>
        private static GameObject BuildCatExchange(
            Transform parent,
            out InventorySlotView[] playerSlots,
            out InventorySlotView[] catSlots,
            out TMP_Text screenTitle,
            out TMP_Text containerTitle)
        {
            // No second copy of the thief's grid. The caller keeps the parameter
            // because the HUD prefab builder and the tests both pass it, and an
            // empty array is what "there is no such grid" looks like to every
            // loop that reads it.
            playerSlots = Array.Empty<InventorySlotView>();

            GameObject panel = CreatePanel(
                parent,
                "Cat Exchange",
                new Vector2(CatBagPanelWidth, CatBagPanelHeight));
            Skin(
                panel,
                HudPanelSkin.Shape.Window,
                HudSpriteLibrary.PanelFill,
                HudSpriteLibrary.Border,
                14,
                2);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            Anchor(
                panelRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                CatBagPanelPosition,
                new Vector2(CatBagPanelWidth, CatBagPanelHeight));

            TMP_Text title = CreateText(
                panel.transform,
                "Title",
                "고양이 가방",
                24f,
                TextAlignmentOptions.TopLeft);
            title.color = HudSpriteLibrary.Accent;
            Anchor(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(24f, -18f),
                new Vector2(-48f, 40f));

            Transform gridRoot = CreateGridRoot(panel.transform, "Grid", 0f, 0f);
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CatBagCellSize, CatBagCellSize);
            grid.spacing = new Vector2(CatBagCellGap, CatBagCellGap);
            grid.padding = new RectOffset(28, 28, 70, 46);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CatBagColumns;

            catSlots = new InventorySlotView[CatBagSlotCount];
            for (int index = 0; index < catSlots.Length; index++)
            {
                catSlots[index] = BuildInventorySlot(
                    gridRoot,
                    $"Cat Bag Slot {index + 1}",
                    new Vector2(CatBagCellSize, CatBagCellSize),
                    (index + 1).ToString(),
                    22f);
            }

            TMP_Text hint = CreateText(
                panel.transform,
                "Hint",
                "칸을 눌러 서로 옮깁니다",
                15f,
                TextAlignmentOptions.Bottom);
            hint.color = new Color(0.66f, 0.80f, 0.90f, 0.80f);
            Anchor(
                hint.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 16f),
                new Vector2(-40f, 26f));

            screenTitle = title;
            containerTitle = title;
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

        internal static InventorySlotView BuildInventorySlot(
            Transform parent,
            string name,
            Vector2 size,
            string keyText,
            float glyphSize)
        {
            GameObject slot = CreatePanel(parent, name, size);
            Skin(
                slot,
                HudPanelSkin.Shape.Slot,
                HudSpriteLibrary.SlotFill,
                HudSpriteLibrary.BorderSoft,
                8,
                2);
            TMP_Text key = CreateText(slot.transform, "Key", keyText, 12f, TextAlignmentOptions.TopLeft);
            key.color = new Color(0.62f, 0.78f, 0.88f, 0.85f);
            TMP_Text quantity = CreateText(slot.transform, "Quantity", string.Empty, 15f, TextAlignmentOptions.BottomRight);
            TMP_Text itemName = CreateText(slot.transform, "Item Name", string.Empty, 11f, TextAlignmentOptions.Top);
            TMP_Text price = CreateText(slot.transform, "Price", string.Empty, 12f, TextAlignmentOptions.Right);
            TMP_Text glyph = CreateText(slot.transform, "Item Glyph", string.Empty, glyphSize, TextAlignmentOptions.Center);
            Image icon = CreateImage(slot.transform, "Item Icon", Color.white);
            Image priceIcon = CreateImage(slot.transform, "Currency Icon", Color.white);
            Image selected = CreateImage(slot.transform, "Selected Frame", Color.white);
            Skin(
                selected.gameObject,
                HudPanelSkin.Shape.Outline,
                Color.clear,
                HudSpriteLibrary.Accent,
                8,
                3);
            Image disabled = CreateImage(slot.transform, "Disabled", new Color(0f, 0f, 0f, 0.22f));
            Anchor(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -6f), new Vector2(28f, 22f));
            Anchor(quantity.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 6f), new Vector2(24f, 22f));
            Anchor(itemName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -17f), new Vector2(-16f, 22f));
            Anchor(price.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-33f, 8f), new Vector2(44f, 18f));
            // Sized as a share of the slot rather than by a fixed inset.
            //
            // A flat 24px inset left a 70px slot showing its icon at 66% and an 82px
            // slot at 71%, so the same artwork was a different size depending on
            // which panel it was in and neither reached the 75-85% the brief asks
            // for. A fraction is the same everywhere and stays right if a slot is
            // ever resized.
            Vector2 iconSize = size * IconShareOfSlot;
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, iconSize);
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, iconSize);

            // Without this a tall icon is stretched to a square. It was never set,
            // which is why the watch and the gemstone read as different shapes from
            // the ones in the loot they represent.
            icon.preserveAspect = true;
            Anchor(priceIcon.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(14f, 14f));
            itemName.color = new Color(1f, 0.96f, 0.86f, 0.96f);
            price.color = new Color(1f, 0.85f, 0.16f, 1f);
            priceIcon.raycastTarget = false;
            Stretch(selected.rectTransform);
            Stretch(disabled.rectTransform);

            GameObject newBadge = CreatePanel(slot.transform, "New Badge", new Vector2(42f, 18f));
            Skin(
                newBadge,
                HudPanelSkin.Shape.Solid,
                HudSpriteLibrary.SellGreen,
                Color.clear,
                6,
                0);
            RectTransform badgeRect = newBadge.GetComponent<RectTransform>();
            Anchor(
                badgeRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-2f, -2f),
                new Vector2(42f, 18f));
            badgeRect.pivot = new Vector2(1f, 1f);
            TMP_Text newLabel = CreateText(
                newBadge.transform,
                "Label",
                "NEW",
                11f,
                TextAlignmentOptions.Center);
            Stretch(newLabel.rectTransform);
            newBadge.SetActive(false);

            Button button = slot.AddComponent<Button>();
            button.targetGraphic = slot.GetComponent<Image>();
            InventorySlotView view = slot.AddComponent<InventorySlotView>();
            view.Configure(
                key,
                quantity,
                icon,
                selected,
                disabled,
                glyph,
                itemName,
                price,
                priceIcon,
                newBadge);

            // Every cell built through here gets one, which is what makes the bag,
            // the cat's bag and the merchant's grid behave the same without any of
            // them knowing the tooltip exists. It has nothing to configure: a
            // serialized reference an editor script writes into a prefab is the
            // thing that comes back from disk empty (ISSUE-031), so it resolves
            // the view beside it and the panel on its canvas at runtime.
            slot.AddComponent<ItemSlotTooltipTrigger>();
            return view;
        }

        internal static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.08f, 0.68f);
            panel.GetComponent<RectTransform>().sizeDelta = size;
            return panel;
        }

        internal static Image CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TMP_Text CreateText(
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
                "PawliceAndPurrglarDefaultFont");
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

        internal static void Anchor(
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

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
