using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
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
        public const string ResultMessageName = "PawliceAndPurrglar.MatchResult";

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

        /// <summary>
        /// Room for the verdict and the counters, with slack for the next field
        /// somebody adds.
        /// </summary>
        private const int MessageCapacityBytes = 128;

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

            // The counters travel with the verdict, in the same message.
            //
            // Only the host builds them: the evaluator holds the wallet, the
            // arrest counter and the clock at the instant the match ended, and it
            // returns early on a client on purpose — a client that works the
            // result out for itself eventually disagrees. So the client had the
            // winner and nothing else, and its result screen said the officer had
            // won by arresting the thief zero times in --:--.
            //
            // Sent rather than replicated, for the same reason the verdict is:
            // deciding a winner unloads the match scene, and everything that
            // would have carried these numbers goes with it.
            MatchResultSession.TryGetSummary(out MatchSummary summary);

            // Sized with room to spare rather than counted field by field.
            //
            // Counting is how this broke: the buffer was sized for seven ints
            // and ten values were written, so the write overflowed and **the
            // message was never sent at all**. The client lost the verdict it
            // used to receive, and the only sign was a result screen with no
            // winner. Only the bytes actually written are transmitted, so a
            // generous capacity costs nothing and cannot be off by one.
            using var writer = new FastBufferWriter(
                MessageCapacityBytes,
                Allocator.Temp);
            writer.WriteValueSafe((int)result.Winner);
            writer.WriteValueSafe((int)result.Reason);
            writer.WriteValueSafe(result.SoldAmount);
            writer.WriteValueSafe(result.RemainingSeconds);
            writer.WriteValueSafe(summary.ElapsedSeconds);
            writer.WriteValueSafe(summary.CatchCount);
            writer.WriteValueSafe(summary.RequiredCatchCount);
            writer.WriteValueSafe(summary.SoldCount);
            writer.WriteValueSafe(summary.SoldAmount);
            writer.WriteValueSafe(summary.TargetAmount);

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
            reader.ReadValueSafe(out float elapsedSeconds);
            reader.ReadValueSafe(out int catchCount);
            reader.ReadValueSafe(out int requiredCatchCount);
            reader.ReadValueSafe(out int soldCount);
            reader.ReadValueSafe(out int summarySoldAmount);
            reader.ReadValueSafe(out int targetAmount);

            ReceivedResultCount++;
            var result = new MatchResult(
                (MatchWinner)winner,
                (MatchEndReason)reason,
                soldAmount,
                remainingSeconds);

            // Reported before the verdict is adopted, because adopting it is
            // what starts the scene change that shows them.
            //
            // A target of zero means the host had no summary either — an
            // out-of-band verdict, or a test that only had a result to hand. The
            // screen already knows how to say nothing rather than zero for that,
            // so it is passed through rather than invented here.
            if (targetAmount > 0)
            {
                MatchResultSession.ReportSummary(new MatchSummary(
                    elapsedSeconds,
                    catchCount,
                    requiredCatchCount,
                    soldCount,
                    summarySoldAmount,
                    targetAmount));
            }

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
