using System;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Companions
{
    [DisallowMultipleComponent]
    public sealed class CatInventoryInteractable :
        MonoBehaviour,
        IPlayerInteractable,
        IInteractionPriority,
        IScreenAnsweredInteractable,
        ISlotContainer
    {
        [SerializeField] private CompanionAgent agent;
        [SerializeField] private MonoBehaviour matchStateSource;
        [SerializeField, Min(1)] private int maximumStackSize = 9;
        [SerializeField, Min(0.1f)] private float fallbackInteractionRadius =
            0.8f;

        private readonly QuickSlotController slots = new();
        private IMatchStateReader matchState;

        public static event Action<CatInventoryInteractable, ToolCarrier>
            ExchangeRequested;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType => PlayerInteractionType.Generic;
        public string Prompt => "고양이 가방 열기";

        // Named for the panel heading rather than reusing the prompt: "고양이 가방
        // 열기" is an instruction and belongs over a key, not over a grid.
        public string DisplayName => "고양이 가방";
        public int SlotCount => QuickSlotController.SlotCount;
        /// <summary>
        /// Below everything else, because the cat is the one interactable the
        /// player never walks up to.
        ///
        /// It follows the thief at their feet, so it is permanently the nearest
        /// candidate and a distance-ranked scanner hands it every contest it
        /// enters. At priority 0 it took the target away from a rock the player
        /// was standing on. Losing every tie still leaves it reachable, because
        /// nothing else is in range when the player means to open the bag.
        /// </summary>
        public int InteractionPriority => -1;
        public bool IsAvailable =>
            (agent == null || agent.CompanionKind == CompanionKind.Cat)
            && ResolveMatchState()?.IsGameplayActive == true;

        private void Awake()
        {
            EnsureInteractionCollider();
        }

        public void Configure(
            CompanionAgent configuredAgent,
            IMatchStateReader configuredMatchState,
            int configuredMaximumStackSize = 9)
        {
            agent = configuredAgent;
            matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            maximumStackSize = Mathf.Max(1, configuredMaximumStackSize);
            EnsureInteractionCollider();
        }

        public bool TryGetSlot(int index, out ThrowableKind kind)
        {
            return slots.TryGet(index, out kind);
        }

        public int GetSlotQuantity(int index)
        {
            return slots.GetQuantity(index);
        }

        public bool CanStore(ThrowableKind kind, int quantity)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && slots.CanStore(kind, quantity, maximumStackSize);
        }

        public bool TryStore(ThrowableKind kind, int quantity)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && slots.CanStore(kind, quantity, maximumStackSize)
                && slots.TryStore(kind, quantity, maximumStackSize, out _);
        }

        public bool TryTakeSlot(
            int index,
            out ThrowableKind kind,
            out int quantity)
        {
            return slots.TryTakeSlot(index, out kind, out quantity);
        }

        public bool TryTakeOne(
            int index,
            out ThrowableKind kind)
        {
            return slots.TryTakeOne(index, out kind);
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null
                || context.Role != PlayerRole.Thief
                || !IsAvailable)
            {
                return false;
            }

            ToolCarrier carrier = context.Player.GetComponent<ToolCarrier>();
            if (carrier == null)
            {
                return false;
            }

            ExchangeRequested?.Invoke(this, carrier);
            return true;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                matchState = reader;
            }

            matchState ??= FindFirstObjectByType<MatchRuntimeState>();
            return matchState;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForCurrentScene()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallMissingInteractables();
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            InstallMissingInteractables();
        }

        public static void InstallMissingInteractables()
        {
            MatchRuntimeState runtime =
                FindFirstObjectByType<MatchRuntimeState>();
            foreach (CompanionAgent candidate in
                FindObjectsByType<CompanionAgent>(FindObjectsSortMode.None))
            {
                if (candidate == null
                    || candidate.CompanionKind != CompanionKind.Cat
                    || candidate.GetComponent<CatInventoryInteractable>() != null)
                {
                    continue;
                }

                candidate.gameObject.AddComponent<CatInventoryInteractable>()
                    .Configure(candidate, runtime);
            }
        }

        private void EnsureInteractionCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = fallbackInteractionRadius;
            trigger.center = new Vector3(0f, 0.55f, 0f);
        }
    }
}
