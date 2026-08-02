using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Sends the decided result to the client as a named message.
    ///
    /// It was a <see cref="NetworkVariable"/> on a scene object first, and that
    /// cannot work here: deciding a winner is what starts the match scene
    /// unloading, so the variable's owner is being destroyed in the same breath
    /// as the value changes. The host reached the result screen while the client
    /// sat in Playing forever.
    ///
    /// Named messages go through the <see cref="NetworkManager"/>, which
    /// outlives every scene. This project already routes its rematch and its
    /// role handoff the same way and for the same reason (`ISSUE-016`): a value
    /// that has to survive a scene change cannot ride on something the scene
    /// change destroys.
    ///
    /// One-way. Only the host sends, only the client acts, and the client's own
    /// evaluator has no authority to disagree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMatchResultMessenger : MonoBehaviour
    {
        public const string ResultMessageName = "PawsAndLoot.MatchResult";

        [SerializeField]
        private MatchResultEvaluator evaluator;

        private NetworkManager _networkManager;
        private bool _registered;
        private bool _subscribed;
        private bool _sent;

        /// <summary>
        /// How many verdicts this machine has received. Zero on the host, and
        /// exactly one on a client that played to the end.
        /// </summary>
        public int ReceivedResultCount { get; private set; }

        public void Configure(MatchResultEvaluator configuredEvaluator)
        {
            Unsubscribe();
            evaluator = configuredEvaluator;
            Subscribe();
        }

        private void Send(MatchResult result)
        {
            NetworkManager manager = ResolveManager();
            if (_sent
                || manager == null
                || !manager.IsListening
                || !manager.IsServer
                || manager.CustomMessagingManager == null)
            {
                return;
            }

            _sent = true;
            using var writer = new FastBufferWriter(
                sizeof(int) * 3 + sizeof(float),
                Allocator.Temp);
            writer.WriteValueSafe((int)result.Winner);
            writer.WriteValueSafe((int)result.Reason);
            writer.WriteValueSafe(result.SoldAmount);
            writer.WriteValueSafe(result.RemainingSeconds);

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                manager.CustomMessagingManager.SendNamedMessage(
                    ResultMessageName,
                    clientId,
                    writer);
            }

            GameLogger.Info(
                GameLogCategory.Network,
                $"Sent the match result to the client: {result.Winner} / "
                + $"{result.Reason}.",
                this);
        }

        private void HandleResultMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int winner);
            reader.ReadValueSafe(out int reason);
            reader.ReadValueSafe(out int soldAmount);
            reader.ReadValueSafe(out float remainingSeconds);

            ReceivedResultCount++;
            var result = new MatchResult(
                (MatchWinner)winner,
                (MatchEndReason)reason,
                soldAmount,
                remainingSeconds);

            GameLogger.Info(
                GameLogCategory.Network,
                $"Match result received from host: {result.Winner} / "
                + $"{result.Reason}.",
                this);

            ResolveEvaluator()?.AdoptDecidedResult(result);
            AdoptPurse(result.SoldAmount);
        }

        /// <summary>
        /// Puts the final takings into the purse the screen reads from.
        ///
        /// The purse normally replicates on the player's own object, and that
        /// object is destroyed by the scene unload that deciding a winner
        /// starts — the same trap the verdict itself fell into, and the reason
        /// this class exists. So the last sale, the one that wins the match,
        /// was the one sale that never arrived: the client declared the thief
        /// the winner over a counter reading eight hundred of a thousand.
        ///
        /// The figure is already in the message. It only had to be carried the
        /// last few feet.
        /// </summary>
        private void AdoptPurse(int soldAmount)
        {
            foreach (ThiefLootWallet wallet in
                FindObjectsByType<ThiefLootWallet>(FindObjectsSortMode.None))
            {
                wallet.ApplyRemoteSale(soldAmount);
            }
        }

        /// <summary>
        /// The evaluator lives in the match scene and this does not, so the
        /// reference is re-found rather than held. A stale one from the previous
        /// match would silently swallow the next verdict.
        /// </summary>
        private MatchResultEvaluator ResolveEvaluator()
        {
            if (evaluator == null)
            {
                evaluator = FindFirstObjectByType<MatchResultEvaluator>();
            }

            return evaluator;
        }

        private NetworkManager ResolveManager()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            return _networkManager;
        }

        private void EnsureRegistered()
        {
            NetworkManager manager = ResolveManager();
            if (_registered
                || manager == null
                || manager.CustomMessagingManager == null)
            {
                return;
            }

            _registered = true;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                ResultMessageName,
                HandleResultMessage);
        }

        private void Subscribe()
        {
            MatchResultEvaluator resolved = ResolveEvaluator();
            if (_subscribed || resolved == null)
            {
                return;
            }

            resolved.ResultDecided += Send;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && evaluator != null)
            {
                evaluator.ResultDecided -= Send;
            }

            _subscribed = false;
        }

        private void Update()
        {
            // Both halves are retried because neither the manager nor the
            // evaluator is guaranteed to exist when this wakes: the manager
            // starts with the session and the evaluator arrives with each match
            // scene.
            EnsureRegistered();
            if (!_subscribed)
            {
                Subscribe();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_registered && _networkManager != null)
            {
                _networkManager.CustomMessagingManager
                    ?.UnregisterNamedMessageHandler(ResultMessageName);
            }
        }
    }
}
