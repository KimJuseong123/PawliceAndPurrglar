using System.Collections.Generic;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Input;
using PawliceAndPurrglar.Logging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// NET-003 input half. Reads this machine's keys and sends them to the host
    /// for its own role only.
    ///
    /// The local keyboard components are switched off in a session so a machine
    /// cannot move a character directly; everything goes through the host. That
    /// is what satisfies "only control your own character": a machine has no
    /// path at all to the other role.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkInputBridge : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager networkManager;

        [SerializeField]
        private List<NetworkPlayerLink> links = new();

        [SerializeField]
        private NetworkRoleBoard roleBoard;

        private bool _configuredLocalControl;

        // One line each, not one per frame. This runs in Update.
        private bool _warnedNoRole;
        private bool _warnedNoLink;

        /// <summary>
        /// How long "cannot send input" has been true without a break.
        ///
        /// Both of the conditions below are legitimate for a moment: leaving a
        /// session clears the local role while the manager is still listening,
        /// and the links take a beat to spawn after the match scene loads.
        /// Saying so on the first frame turned a normal transition into an error
        /// — it failed `LobbyReentryPlayModeTests` on the way in.
        ///
        /// Three seconds is far longer than either transition and far shorter
        /// than a player's patience with a character that will not walk.
        /// </summary>
        private float _blockedSeconds;

        private const float ComplainAfterSeconds = 3f;

        /// <summary>
        /// One pending slot swap from the bag screen's drag.
        ///
        /// Static because the forwarding runs in a static method alongside the key
        /// reads, and one place is enough: a second drag before the first is sent
        /// would mean the player moved the mouse faster than a frame, and the
        /// newer intent is the right one to keep.
        /// </summary>
        private static int _pendingSwapLeft = -1;
        private static int _pendingSwapRight = -1;
        private static bool _swapSubscribed;

        private void OnEnable()
        {
            if (_swapSubscribed)
            {
                return;
            }

            Input.GameplayInputRouter.QuickSlotSwapRequested += QueueSlotSwap;
            Input.GameplayInputRouter.LootSaleRequested += QueueSale;
            Input.GameplayInputRouter.PropPurchaseRequested += QueuePurchase;
            Input.GameplayInputRouter.CatBagTransferRequested +=
                QueueCatBagTransfer;
            _swapSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_swapSubscribed)
            {
                return;
            }

            Input.GameplayInputRouter.QuickSlotSwapRequested -= QueueSlotSwap;
            Input.GameplayInputRouter.LootSaleRequested -= QueueSale;
            Input.GameplayInputRouter.PropPurchaseRequested -= QueuePurchase;
            Input.GameplayInputRouter.CatBagTransferRequested -=
                QueueCatBagTransfer;
            _swapSubscribed = false;
            _pendingSwapLeft = -1;
            _pendingSwapRight = -1;
            _pendingSales.Clear();
            _pendingPurchases.Clear();
            _pendingCatBagMoves.Clear();
        }

        /// <summary>
        /// Cat-bag moves waiting for a frame.
        ///
        /// A list, like the purchases: the exchange screen answers a click
        /// immediately and the player can empty four slots faster than a frame,
        /// and a dropped move is an item that vanishes from the bag without
        /// arriving in the slot.
        /// </summary>
        private static readonly List<(bool ToCat, int Slot, int Kind)>
            _pendingCatBagMoves = new();

        private static void QueueCatBagTransfer(
            bool toCat,
            int slotIndex,
            int throwableKindValue)
        {
            _pendingCatBagMoves.Add((toCat, slotIndex, throwableKindValue));
        }

        private static void ForwardPendingCatBagMoves(NetworkPlayerLink link)
        {
            if (_pendingCatBagMoves.Count == 0)
            {
                return;
            }

            var moves =
                new List<(bool ToCat, int Slot, int Kind)>(_pendingCatBagMoves);
            _pendingCatBagMoves.Clear();
            foreach ((bool toCat, int slot, int kind) in moves)
            {
                link.SubmitCatBagTransferRpc(toCat, slot, kind);
            }
        }

        /// <summary>
        /// Purchases from the officer's shop screen, waiting to be sent.
        ///
        /// A list rather than one slot: the officer can press BUY twice on the same
        /// row faster than a frame, and dropping the second press would take a
        /// click that the shop appeared to accept.
        /// </summary>
        private static readonly List<int> _pendingPurchases = new();

        private static void QueuePurchase(int throwableKindValue)
        {
            _pendingPurchases.Add(throwableKindValue);
        }

        private static void ForwardPendingPurchases(NetworkPlayerLink link)
        {
            if (_pendingPurchases.Count == 0)
            {
                return;
            }

            var purchases = new List<int>(_pendingPurchases);
            _pendingPurchases.Clear();
            foreach (int kind in purchases)
            {
                link.SubmitBuyPropRpc(kind);
            }
        }

        /// <summary>
        /// Sale requests from the merchant screen, waiting for a frame to be sent.
        ///
        /// A list rather than one slot, unlike the slot swap: a SELL press asks
        /// for several kinds at once and dropping all but the last would sell one
        /// row of a four-row ledger with no sign that the rest went missing.
        /// </summary>
        private static readonly List<(int Hash, int Count)> _pendingSales = new();

        private static void QueueSale(int definitionIdHash, int count)
        {
            _pendingSales.Add((definitionIdHash, count));
        }

        private static void ForwardPendingSales(NetworkPlayerLink link)
        {
            if (_pendingSales.Count == 0)
            {
                return;
            }

            var sales = new List<(int Hash, int Count)>(_pendingSales);
            _pendingSales.Clear();
            foreach ((int hash, int count) in sales)
            {
                link.SubmitSellRpc(hash, count);
            }
        }

        /// <summary>
        /// Whether the thing in range on this machine is answered by a screen.
        ///
        /// Checked before forwarding the key rather than after, because the host
        /// runs whatever it is sent: forwarding an E press at the raccoon's pitch
        /// would sell the piece in the thief's hands while they were only asking
        /// to see the shop. The HUD opens the ledger off the same key press,
        /// locally, where the player who pressed it can see it.
        ///
        /// Asked of every scanner rather than the local role's, because a machine
        /// only ever has one player standing in front of something — and the cost
        /// of being wrong is a key press that does nothing, not a wrong sale.
        /// </summary>
        /// <summary>
        /// The scanner belonging to the role this machine drives.
        ///
        /// By role rather than "any scanner that says yes". The permissive form
        /// was safe for the screen check — a wrong answer there only ever
        /// *withholds* a key press — and it is not safe for the hold check, which
        /// answers "may I send this every frame". One player standing at a piece
        /// of treasure would have let the other hold E at the raccoon and sell
        /// their bag one item per frame.
        /// </summary>
        /// <summary>
        /// The network id of what this machine is looking at, or zero.
        ///
        /// Zero for anything that is not a spawned object — doors, ladders, bins
        /// are scene furniture with no id — and the host falls back to its own
        /// scan for those. They are single objects in a place, so the two machines
        /// cannot disagree about which one is meant.
        /// </summary>
        private static ulong NamedTarget(PlayerInteractionScanner scanner)
        {
            Transform at = scanner != null && scanner.CurrentTarget != null
                ? scanner.CurrentTarget.InteractionTransform
                : null;
            NetworkObject spawned = at != null
                ? at.GetComponentInParent<NetworkObject>()
                : null;
            return spawned != null && spawned.IsSpawned
                ? spawned.NetworkObjectId
                : 0UL;
        }

        private static PlayerInteractionScanner LocalScanner(PlayerRole role)
        {
            foreach (PlayerInteractionScanner scanner in
                Object.FindObjectsByType<PlayerInteractionScanner>(
                    FindObjectsSortMode.None))
            {
                PlayerRoleIdentity identity =
                    scanner != null
                        ? scanner.GetComponent<PlayerRoleIdentity>()
                        : null;
                if (identity != null && identity.Role == role)
                {
                    return scanner;
                }
            }

            return null;
        }

        private static void QueueSlotSwap(int left, int right)
        {
            _pendingSwapLeft = left;
            _pendingSwapRight = right;
        }

        private static void ForwardPendingSlotSwap(NetworkPlayerLink link)
        {
            if (_pendingSwapLeft < 0 || _pendingSwapRight < 0)
            {
                return;
            }

            int left = _pendingSwapLeft;
            int right = _pendingSwapRight;

            // Cleared before the send, not after. A refused or dropped call must
            // not leave the request in the queue to be replayed every frame —
            // that would fight the player's next drag.
            _pendingSwapLeft = -1;
            _pendingSwapRight = -1;
            link.SubmitSwapToolSlotsRpc(left, right);
        }

        public PlayerRole LocalRole { get; private set; } =
            PlayerRole.Police;
        public bool HasLocalRole { get; private set; }

        public void Configure(
            NetworkManager manager,
            IEnumerable<NetworkPlayerLink> playerLinks)
        {
            networkManager = manager;
            links = new List<NetworkPlayerLink>(playerLinks);
        }

        private NetworkRoleBoard ResolveRoleBoard()
        {
            if (roleBoard == null)
            {
                roleBoard = Object.FindFirstObjectByType<
                    NetworkRoleBoard>();
            }

            return roleBoard;
        }

        private NetworkPlayerLink FindLink(PlayerRole role)
        {
            foreach (NetworkPlayerLink link in links)
            {
                if (link != null && link.Role == role)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>
        /// Turns off every local keyboard driver once a session owns movement.
        /// Done once, and only in a session, so the offline playtest keeps its
        /// direct local control.
        /// </summary>
        private void EnsureLocalControlDisabled()
        {
            if (_configuredLocalControl)
            {
                return;
            }

            _configuredLocalControl = true;
            foreach (PlayerKeyboardInput input in
                Object.FindObjectsByType<PlayerKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            // NET-005/006. The action keys are switched off for the same reason
            // movement is: a machine must have no local path that bypasses the
            // host, or the two sides can disagree about who picked up what.
            foreach (PlayerInteractionInput input in
                Object.FindObjectsByType<PlayerInteractionInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (LootDropInput input in
                Object.FindObjectsByType<LootDropInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (CompanionCommandKeyboardInput input in
                Object.FindObjectsByType<CompanionCommandKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            // THROW-007. The prop key joins the rest: a client that resolved its
            // own throw would decide it hit while the host decided it missed.
            foreach (PawliceAndPurrglar.Gameplay.Items.ToolUseInput input in
                Object.FindObjectsByType<
                    PawliceAndPurrglar.Gameplay.Items.ToolUseInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (QuickSlotKeyboardInput input in
                Object.FindObjectsByType<QuickSlotKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.Configure(null, false);
            }
        }

        private void Update()
        {
            // Resolved at runtime because the manager lives in the Bootstrap
            // scene and only exists once a session has started.
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null
                || !networkManager.IsListening)
            {
                return;
            }

            // Read the role from the local value the server committed before
            // the scene load. The board itself does not exist in the match
            // scene on a client, which is exactly why the role is stored
            // locally rather than looked up here.
            PlayerRole? assigned =
                LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                // Said once. Both of the early returns in this method mean "this
                // machine sends no input at all", which the player experiences as
                // a character that will not move while the bag still opens — and
                // neither of them used to write a line anywhere.
                //
                // No role means `CommitRolesRpc` never arrived. It is sent to
                // everyone in the lobby before the scene load and stored in a
                // plain static, so its absence is a lobby problem, not a
                // movement one.
                _blockedSeconds += Time.unscaledDeltaTime;
                if (!_warnedNoRole
                    && _blockedSeconds >= ComplainAfterSeconds)
                {
                    _warnedNoRole = true;
                    GameLogger.Error(
                        GameLogCategory.Network,
                        "이 기계에 역할이 배정되지 않아 입력을 보내지 않습니다. "
                        + "로비에서 역할 확정(CommitRolesRpc)이 도착하지 "
                        + "않았습니다 — 로그에 'Committed local role'이 있는지 "
                        + "확인하세요.",
                        this);
                }

                return;
            }

            EnsureLocalControlDisabled();
            LocalRole = assigned.Value;
            HasLocalRole = true;

            NetworkPlayerLink link = FindLink(LocalRole);
            if (link == null || !link.IsSpawned)
            {
                // The other silent way to be unable to move, and the more common
                // one: the link exists in the scene but never spawned, because
                // the two machines are running builds whose in-scene
                // `GlobalObjectIdHash` values disagree. NGO reports that as a
                // wall of "soft synchronization failure" with nothing saying
                // "different build".
                _blockedSeconds += Time.unscaledDeltaTime;
                if (!_warnedNoLink
                    && _blockedSeconds >= ComplainAfterSeconds)
                {
                    _warnedNoLink = true;
                    GameLogger.Error(
                        GameLogCategory.Network,
                        $"역할 {LocalRole}의 NetworkPlayerLink가 "
                        + (link == null ? "없어서" : "스폰되지 않아서")
                        + " 입력을 보내지 않습니다. 두 기계의 빌드가 다르면 "
                        + "경기 씬의 오브젝트가 스폰되지 않습니다 — 같은 커밋에서, "
                        + "그리고 커밋되지 않은 변경 없이 양쪽을 다시 빌드하세요.",
                        this);
                }

                return;
            }

            // Got this far, so input is flowing. The clock only measures an
            // unbroken run of being unable to send.
            _blockedSeconds = 0f;

            Keyboard keyboard = Keyboard.current;
            Vector2 move = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(keyboard.aKey, keyboard.dKey),
                    ReadAxis(keyboard.sKey, keyboard.wKey));
            bool dash = keyboard != null
                && (keyboard.leftShiftKey.wasPressedThisFrame
                    || keyboard.rightShiftKey.wasPressedThisFrame);

            // Sent with the yaw of the camera this player is looking through,
            // because that is the frame their keys mean something in. The host
            // has its own camera and it is not this one.
            UnityEngine.Camera view = UnityEngine.Camera.main;
            link.SubmitInputRpc(
                move,
                dash,
                view != null ? view.transform.eulerAngles.y : float.NaN);

            // Sent only on the frame it is pressed, and as its own message. A jump
            // is an event; folding it into the movement stream would drop presses
            // that landed between sends.
            if (keyboard != null
                && keyboard.spaceKey.wasPressedThisFrame)
            {
                link.SubmitJumpRpc();

                // Played here rather than waiting for the host to answer, which
                // would put the sound a whole latency behind the key. But the
                // one refusal a player notices — pressing again while already in
                // the air — is asked about first, using the airborne flag the
                // host already replicates for the leg pose.
                if (!link.IsAirborneReplicated)
                {
                    Audio.GameSoundService.Request(Audio.GameSoundId.Jump);
                }
            }
            SubmitActions(link, keyboard);
        }

        /// <summary>
        /// Where this machine's cursor is pointing, measured from the character
        /// it controls.
        ///
        /// Read here rather than on the host because a cursor only exists on the
        /// machine holding the mouse. The host still decides everything that
        /// follows from it.
        /// </summary>
        private static Vector3 ReadAim(NetworkPlayerLink link)
        {
            Vector3? aim =
                PawliceAndPurrglar.Gameplay.Items.ToolUseInput.ReadAimDirection(
                    link.transform.position);
            // Zero tells the host "no aim", and it falls back to the
            // character's facing rather than throwing at the ground.
            return aim ?? Vector3.zero;
        }

        /// <summary>
        /// Actions are edge-triggered, so they are sent only on the frame the key
        /// goes down. Sending them continuously would let one press be applied
        /// many times on the host.
        /// </summary>
        private static void SubmitActions(
            NetworkPlayerLink link,
            Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            // Held for a theft, tapped for everything else.
            //
            // The host charges a theft by counting the frames the client keeps
            // asking, so one request per press bought one frame of a two-second
            // wait and the treasure never left the shelf — with no message,
            // because from the host's side nothing failed. Doors, ladders and
            // shop counters stay on the press edge: repeating those would work a
            // door back and forth for as long as the key is down.
            //
            // The screen guard applies to both. It is what stops an E at the
            // raccoon's pitch selling the piece in the thief's hands while they
            // were only asking to look at the shop.
            PlayerInteractionScanner local = LocalScanner(link.Role);
            bool answeredByAScreen =
                local != null && local.CurrentTargetIsAnsweredByAScreen;
            bool heldToUse = local != null && local.CurrentTargetIsHeldToUse;
            if (!answeredByAScreen
                && (heldToUse
                    ? keyboard.eKey.isPressed
                    : keyboard.eKey.wasPressedThisFrame))
            {
                // Named, when the thing in range is a spawned object the host can
                // look up. Both machines scan independently and rank by distance,
                // so two pieces on one shelf are routinely ranked differently —
                // the prompt said one thing and another went into the bag.
                link.SubmitInteractRpc(NamedTarget(local));
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                link.SubmitDropRpc();
            }

            ForwardPendingSlotSwap(link);
            ForwardPendingSales(link);
            ForwardPendingPurchases(link);
            ForwardPendingCatBagMoves(link);

            int quickSlot = ReadQuickSlotKey(keyboard);
            if (quickSlot >= 0)
            {
                link.SubmitSelectToolSlotRpc(quickSlot);
            }

            // Ctrl+1..4 used to be read here and sent on. The animals are
            // commanded by voice now (`COMP-002`); the number keys were the
            // stand-in for it while there was no microphone, and leaving both
            // in meant the table on screen described one of two ways to do the
            // same thing. `SubmitCompanionCommandRpc` is untouched — voice
            // still goes through it.
        }

        /// <summary>
        /// The quick slots still ignore a held Ctrl.
        ///
        /// Nothing reads Ctrl+digit any more, so this could go — but Ctrl+1 is
        /// a browser tab switch and a WebGL player that swapped a tool on the
        /// way past would be a bug nobody could explain.
        /// </summary>
        private static int ReadQuickSlotKey(Keyboard keyboard)
        {
            bool ctrl = keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed;
            if (ctrl)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            return -1;
        }

        private static float ReadAxis(
            KeyControl negative,
            KeyControl positive)
        {
            float value = 0f;
            if (negative != null && negative.isPressed)
            {
                value -= 1f;
            }

            if (positive != null && positive.isPressed)
            {
                value += 1f;
            }

            return value;
        }
    }
}
