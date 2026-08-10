using System;
using PawliceAndPurrglar.Companions;

namespace PawliceAndPurrglar.Integration.Voice
{
    public static class VoiceCommandMapper
    {
        public static bool TryMap(
            string intent,
            CompanionKind kind,
            out CompanionCommandId commandId)
        {
            string normalized = (intent ?? string.Empty).Trim().ToUpperInvariant();
            commandId = normalized switch
            {
                "TRACK" or "TRACK_SCENT" or "CHASE" or "CHASE_TARGET"
                    when kind == CompanionKind.Dog => CompanionCommandId.Track,
                "SEARCH" or "SEARCH_AREA" or "INSPECT_TARGET"
                    when kind == CompanionKind.Dog => CompanionCommandId.Search,
                "GUARD" or "GUARD_AREA" or "MOVE_TO_POSITION"
                    when kind == CompanionKind.Dog => CompanionCommandId.Guard,
                "BARK" when kind == CompanionKind.Dog => CompanionCommandId.Bark,
                "SCOUT" or "INSPECT_TARGET"
                    when kind == CompanionKind.Cat => CompanionCommandId.Scout,
                "DISTRACT" or "DISTRACT_TARGET"
                    when kind == CompanionKind.Cat => CompanionCommandId.Distract,
                "ROOF" or "CLIMB_ROOF" or "STEAL" or "FETCH_OBJECT"
                    when kind == CompanionKind.Cat => CompanionCommandId.Steal,
                "HIDE" or "GUARD_AREA" or "MOVE_TO_POSITION"
                    when kind == CompanionKind.Cat => CompanionCommandId.Hide,
                // CAT-010. No dog arm: the dog biting overlaps the arrest, so an
                // officer who says it gets nothing rather than a substitute.
                "BITE" or "ATTACK"
                    when kind == CompanionKind.Cat => CompanionCommandId.Bite,
                "STAY" => CompanionCommandId.Stay,
                "STOP" => CompanionCommandId.Stop,
                "FOLLOW_OWNER" => CompanionCommandId.FollowOwner,
                "RETURN_OWNER" => CompanionCommandId.ReturnOwner,
                "CANCEL" => CompanionCommandId.Cancel,
                _ => CompanionCommandId.None
            };

            return commandId != CompanionCommandId.None;
        }

        public static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
