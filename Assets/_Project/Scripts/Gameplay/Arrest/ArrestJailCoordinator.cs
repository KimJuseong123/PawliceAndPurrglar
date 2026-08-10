using System;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Arrest
{
    /// <summary>
    /// Turns a completed arrest into a spell in the cells and a return to the
    /// map.
    ///
    /// Separate from both halves on purpose. The arrest controller decides
    /// whether a catch happened and knows nothing about where the station is;
    /// the jail moves a character and knows nothing about arrests. This is the
    /// only piece that needs both, and it is the only piece that needs the map.
    /// </summary>
    public sealed class ArrestJailCoordinator : MonoBehaviour
    {
        [SerializeField]
        private ArrestCompletionController arrestCompletion;

        [SerializeField]
        private ThiefJailState jail;

        [SerializeField]
        private Transform cellPoint;

        [SerializeField]
        private Transform releasePoint;

        [SerializeField]
        private ArrestConfig arrestConfig;

        /// <summary>
        /// Where a released thief may come back, when there is more than one
        /// place. Optional: without it the single release point is used, which
        /// is what every test fixture builds.
        /// </summary>
        [SerializeField]
        private PawliceAndPurrglar.Gameplay.Players.ThiefSpawnPoints releasePoints;

        /// <summary>
        /// The room the cell is, or Outside when there is no cell to put them
        /// in. Told to the thief so the interior camera and the indoor rules
        /// apply while they serve their time.
        /// </summary>
        [SerializeField]
        private int jailInteriorId =
            PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState.Outside;

        /// <summary>
        /// Cached rather than searched on every arrest. There is one wallet and
        /// it lives as long as the match does.
        /// </summary>
        [SerializeField]
        private PawliceAndPurrglar.Gameplay.Players.PoliceWallet policeWallet;

        private bool _subscribed;

        public void Configure(
            ArrestCompletionController configuredArrestCompletion,
            ThiefJailState configuredJail,
            Transform configuredCellPoint,
            Transform configuredReleasePoint,
            ArrestConfig configuredArrestConfig,
            PawliceAndPurrglar.Gameplay.Players.ThiefSpawnPoints configuredReleasePoints
                = null,
            int configuredJailInteriorId =
                PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState.Outside)
        {
            releasePoints = configuredReleasePoints;
            jailInteriorId = configuredJailInteriorId;
            Unsubscribe();
            arrestCompletion = configuredArrestCompletion;
            jail = configuredJail;
            cellPoint = configuredCellPoint;
            releasePoint = configuredReleasePoint;
            arrestConfig = configuredArrestConfig;
            ValidateOrThrow();
            Subscribe();
        }

        public void ValidateOrThrow()
        {
            if (arrestCompletion == null
                || jail == null
                || cellPoint == null
                || releasePoint == null
                || arrestConfig == null)
            {
                throw new InvalidOperationException(
                    $"ArrestJailCoordinator '{name}' has missing references.");
            }
        }

        private void HandleArrestCompleted()
        {
            PayTheBounty();

            if (jail == null || cellPoint == null || releasePoint == null)
            {
                return;
            }

            // Drawn here rather than when the jail term ends, so the whole
            // sentence has one answer. Deciding at release would let the same
            // arrest resolve differently if this ran twice.
            //
            // And drawn on this machine only. This handler fires from the
            // arrest being completed, which is a host decision; the client's
            // thief arrives at the drawn corner by the position replication
            // that was already carrying it.
            Vector3 release = releasePoints != null
                ? releasePoints.Draw(releasePoint.position)
                : releasePoint.position;

            jail.TryJail(
                arrestConfig.JailSeconds,
                cellPoint.position,
                release);

            // Faced one known way, and told they are in a room.
            //
            // The cell uses the interior camera like any other room, and that
            // camera takes its starting yaw from whichever way the character is
            // pointing when it wakes up. An arrest leaves them pointing
            // wherever the chase did — and on the client that rotation is still
            // interpolating when the room arrives, so the camera locked its yaw
            // to a half-turned pose and W walked the thief backwards.
            //
            // Facing +Z is arbitrary and that is the point: it is the same
            // arbitrary on both machines.
            jail.transform.rotation = Quaternion.LookRotation(
                Vector3.forward,
                Vector3.up);
            SetThiefRoom(jailInteriorId);

        }

        /// <summary>
        /// Re-arms the arrest only when the thief is actually back out.
        ///
        /// Doing it on a timer of its own would eventually drift away from the
        /// release and leave a window where the thief is still in the cell and
        /// can be arrested again, which reads as the officer scoring twice for
        /// standing still.
        /// </summary>
        /// <summary>
        /// Marks the thief as inside the cell, or back in the street.
        ///
        /// Only where this machine decides. A client copy is moved by having
        /// its position written, so acting on the state there would fight the
        /// host; the id itself reaches the client through NetworkPlayerLink
        /// like the sentence timer does.
        /// </summary>
        private void SetThiefRoom(int interiorId)
        {
            if (jail == null)
            {
                return;
            }

            var room = jail
                .GetComponent<
                    PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState>();
            if (room != null && room.HasAuthority)
            {
                room.SetInterior(interiorId);
            }
        }

        // Historical note, kept because it was nearly the wrong fix.
        //
        // They were, so the interior camera would take over and show the cell
        // from inside it. But the mark is host-only — a client's character is
        // moved by having its position written, so the state never crossed —
        // and the two machines ended up running different cameras. Movement is
        // relative to the camera, so the client's keys came out turned round:
        // pressing back walked the thief forward.
        //
        // The cell sits far outside the map where the town camera can see it
        // perfectly well, and the same camera on both machines means the same
        // controls on both. The interior camera is for rooms a player walks
        // into by choice.


        /// <summary>
        /// Pays the officer for the catch.
        ///
        /// Before this, an arrest paid only through confiscation — which takes a
        /// share of what the thief has *sold*. Early in a match that is nothing,
        /// so the first catch of every game was worth zero and the officer could
        /// not afford the tools that make the second catch easier. A flat bounty
        /// makes the first catch fund the next one.
        ///
        /// Paid here, before the jail term is set up, and unconditionally: the
        /// guards below are about having somewhere to put the thief, and an
        /// arrest with no cell is still an arrest.
        ///
        /// Host only. This handler runs where the arrest was decided, and the
        /// officer's wallet is replicated from there — paying on both machines
        /// would pay twice on the host's screen and once on the client's, which
        /// is the shape of a desync nobody can explain from the numbers.
        /// </summary>
        private void PayTheBounty()
        {
            if (arrestConfig == null || arrestConfig.ArrestRewardGold <= 0)
            {
                return;
            }

            PawliceAndPurrglar.Gameplay.Players.PoliceWallet wallet = ResolveWallet();
            if (wallet == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Arrest,
                    "An arrest completed and there is no PoliceWallet to pay, "
                    + "so the bounty was dropped.",
                    this);
                return;
            }

            wallet.Recover(arrestConfig.ArrestRewardGold);
            GameLogger.Info(
                GameLogCategory.Arrest,
                $"Arrest bounty paid: {arrestConfig.ArrestRewardGold} gold.",
                this);
        }

        /// <summary>
        /// The officer's wallet, found once and kept. There is exactly one, and
        /// it is created with the match and never replaced — but the search is
        /// retried while it is null, because this coordinator exists before the
        /// players are spawned.
        /// </summary>
        private PawliceAndPurrglar.Gameplay.Players.PoliceWallet ResolveWallet()
        {
            if (policeWallet == null)
            {
                policeWallet = FindFirstObjectByType<
                    PawliceAndPurrglar.Gameplay.Players.PoliceWallet>();
            }

            return policeWallet;
        }

        private void HandleReleased()
        {
            SetThiefRoom(
                PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState.Outside);


            arrestCompletion?.ClearForNextArrest();
            GameLogger.Info(
                GameLogCategory.Arrest,
                "Arrest re-armed after release.",
                this);
        }

        private void Subscribe()
        {
            if (_subscribed || arrestCompletion == null || jail == null)
            {
                return;
            }

            arrestCompletion.ArrestCompleted += HandleArrestCompleted;
            jail.Released += HandleReleased;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (arrestCompletion != null)
            {
                arrestCompletion.ArrestCompleted -= HandleArrestCompleted;
            }

            if (jail != null)
            {
                jail.Released -= HandleReleased;
            }

            _subscribed = false;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
