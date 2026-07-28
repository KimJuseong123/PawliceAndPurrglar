using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// One prop slot per player, separate from the loot slot.
    ///
    /// It has to be separate. "The thief carries one piece of loot" is a
    /// confirmed rule in <c>docs/03_GAME_RULES.md</c>, and sharing that slot
    /// would mean picking up a banana makes you drop the jewels — which turns
    /// every item into a punishment rather than a choice.
    ///
    /// This class only tracks what is held and hands out the decision to use it.
    /// It never applies a stun and never moves anyone: the host resolves the
    /// throw, so a client cannot stun anybody by holding a rock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolCarrier : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;
        private bool _hasTool;
        private ThrowableKind _kind;

        public event Action<bool> HeldToolChanged;

        public bool HasTool => _hasTool;
        public ThrowableKind HeldKind => _kind;
        public PlayerRole Role =>
            identity != null ? identity.Role : PlayerRole.Police;

        public ThrowableUse HeldUse => ThrowableCatalog.GetUse(_kind);

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            IMatchStateReader configuredMatchState)
        {
            identity = configuredIdentity;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            _hasTool = false;
        }

        /// <summary>
        /// Takes a prop. Refused when a match is not running or the slot is
        /// already full, so walking over a pickup cannot silently swap the tool
        /// a player was saving.
        /// </summary>
        public bool TryPickUp(ThrowableKind kind)
        {
            if (ResolveMatchState()?.IsGameplayActive != true || _hasTool)
            {
                return false;
            }

            _hasTool = true;
            _kind = kind;
            GameLogger.Info(
                GameLogCategory.Player,
                $"{Role} picked up {kind}.",
                this);
            HeldToolChanged?.Invoke(true);
            return true;
        }

        /// <summary>
        /// Spends the held prop. Returns false when there is nothing to spend,
        /// which is what stops one pickup from producing two rocks.
        /// </summary>
        public bool TryConsume(out ThrowableKind kind)
        {
            kind = _kind;
            if (!_hasTool
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return false;
            }

            _hasTool = false;
            HeldToolChanged?.Invoke(false);
            return true;
        }

        /// <summary>
        /// Sets the slot to what the host says it is, on a machine that does not
        /// decide.
        /// </summary>
        /// <remarks>
        /// Deliberately skips the match-state gate that <see cref="TryPickUp"/>
        /// applies. This is not a request and cannot be refused — the host has
        /// already decided, and a client that quietly declined would show an
        /// empty hand for a rock it is really holding. That was the bug: picking
        /// a rock up worked on the host and the other player's HUD kept saying
        /// they had nothing, which is indistinguishable from the pickup being
        /// broken.
        /// </remarks>
        public void ApplyReplicated(bool hasTool, ThrowableKind kind)
        {
            if (_hasTool == hasTool && _kind == kind)
            {
                return;
            }

            _hasTool = hasTool;
            _kind = kind;
            HeldToolChanged?.Invoke(hasTool);
        }

        /// <summary>
        /// Drops the prop without using it, for match end and restarts.
        /// </summary>
        public void Clear()
        {
            if (!_hasTool)
            {
                return;
            }

            _hasTool = false;
            HeldToolChanged?.Invoke(false);
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }
    }
}
