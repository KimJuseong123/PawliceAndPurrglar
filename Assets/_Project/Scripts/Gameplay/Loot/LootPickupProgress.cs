using System;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// Makes taking something take time.
    ///
    /// Stealing was instant. Reaching the treasure was the whole theft, so an
    /// officer who arrived a second late had already lost, and every piece in
    /// the building cost the same nothing to take. There was no moment where
    /// the thief was committed.
    ///
    /// Now there is. The thief holds still beside the piece for as long as its
    /// size demands, and anything that interrupts them — walking off, being
    /// stunned, the piece being taken by somebody else — throws the progress
    /// away rather than banking it. Nothing is half stolen.
    ///
    /// Host-authoritative in a session, like every other rule: the client asks
    /// by repeating its interact request, and this decides. The request is the
    /// same one that used to acquire directly, so nothing above needed to learn
    /// a new verb — it simply takes several requests now instead of one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootPickupProgress : MonoBehaviour
    {
        [SerializeField]
        private LootCarrier carrier;

        [SerializeField]
        private PlayerMovementMotor movementMotor;

        /// <summary>
        /// How far the thief may drift before the attempt is abandoned.
        ///
        /// Not zero. A character standing still still slides a few millimetres
        /// against the ground each frame, and a threshold of zero would make
        /// every theft impossible while looking like a rule about movement.
        /// </summary>
        private const float DriftAllowanceMeters = 0.45f;

        /// <summary>
        /// How long after the last request before the attempt lapses.
        ///
        /// The client repeats the request every frame while the button is held,
        /// so a gap means it stopped asking. Generous enough to survive a
        /// dropped packet, short enough that letting go stops the theft.
        /// </summary>
        private const float RequestGraceSeconds = 0.35f;

        private LootItem _target;
        private Vector3 _startedAt;
        private float _elapsed;
        private float _sinceRequest;

        public event Action<LootItem> Started;
        public event Action<LootItem, LootPickupInterruption> Interrupted;

        public LootItem Target => _target;
        public bool IsInProgress => _target != null;

        /// <summary>
        /// How far along, from zero to one. Zero when nothing is being taken,
        /// so a bar can read this every frame without asking twice.
        /// </summary>
        public float Normalized =>
            _target == null || RequiredSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(_elapsed / RequiredSeconds);

        public float RequiredSeconds =>
            _target != null && _target.Definition != null
                ? _target.Definition.PickupSeconds
                : 0f;

        public void Configure(
            LootCarrier configuredCarrier,
            PlayerMovementMotor configuredMotor)
        {
            carrier = configuredCarrier;
            movementMotor = configuredMotor;
            Cancel(LootPickupInterruption.Reconfigured);
        }

        /// <summary>
        /// Asks to take a piece, and reports whether it is now in hand.
        ///
        /// Called every frame while the thief is asking. Returns true on the
        /// single frame the piece is actually acquired.
        /// </summary>
        public bool Request(LootItem loot)
        {
            if (loot == null || carrier == null)
            {
                Cancel(LootPickupInterruption.NoTarget);
                return false;
            }

            if (!ReferenceEquals(loot, _target))
            {
                // Switching targets restarts. Carrying progress across from one
                // piece to another would let a thief part-open every case in
                // the room and then empty it in a second.
                Cancel(LootPickupInterruption.ChangedTarget);
                _target = loot;
                _startedAt = transform.position;
                _elapsed = 0f;
                Started?.Invoke(loot);
            }

            _sinceRequest = 0f;

            if (_elapsed < RequiredSeconds)
            {
                return false;
            }

            LootItem taken = _target;
            _target = null;
            _elapsed = 0f;
            return carrier.TryAcquire(taken);
        }

        /// <summary>
        /// Advances the attempt and drops it if the thief moved, stopped
        /// asking, was stunned, or the piece went away.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_target == null || deltaTime <= 0f)
            {
                return;
            }

            _sinceRequest += deltaTime;
            if (_sinceRequest > RequestGraceSeconds)
            {
                Cancel(LootPickupInterruption.StoppedAsking);
                return;
            }

            if (_target.CurrentState != LootState.Available
                && _target.CurrentState != LootState.Dropped)
            {
                Cancel(LootPickupInterruption.TargetGone);
                return;
            }

            // Asked of the motor rather than of the keys. A held thief is
            // pressing the same keys they were a moment ago and going nowhere,
            // and a thief being dragged is not standing still no matter what
            // they press.
            if (movementMotor != null && !movementMotor.CanMove)
            {
                Cancel(LootPickupInterruption.Immobilised);
                return;
            }

            Vector3 drift = transform.position - _startedAt;
            drift.y = 0f;
            if (drift.sqrMagnitude
                > DriftAllowanceMeters * DriftAllowanceMeters)
            {
                Cancel(LootPickupInterruption.MovedAway);
                return;
            }

            _elapsed += deltaTime;
        }

        public void Cancel(LootPickupInterruption reason)
        {
            if (_target == null)
            {
                return;
            }

            LootItem abandoned = _target;
            _target = null;
            _elapsed = 0f;
            _sinceRequest = 0f;
            Interrupted?.Invoke(abandoned, reason);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// Why a theft was abandoned. Named rather than a bool so the screen can
    /// say something useful and a test can tell "you walked off" from "somebody
    /// else got there first".
    /// </summary>
    public enum LootPickupInterruption
    {
        NoTarget = 0,
        ChangedTarget = 1,
        StoppedAsking = 2,
        MovedAway = 3,
        Immobilised = 4,
        TargetGone = 5,
        Reconfigured = 6
    }
}
