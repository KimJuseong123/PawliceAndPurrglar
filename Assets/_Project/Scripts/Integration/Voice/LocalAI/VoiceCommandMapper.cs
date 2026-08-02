using System;
using PawsAndLoot.Companions;

namespace PawsAndLoot.Integration.Voice
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
                "STEAL" or "FETCH_OBJECT"
                    when kind == CompanionKind.Cat => CompanionCommandId.Steal,
                "HIDE" or "GUARD_AREA" or "MOVE_TO_POSITION"
                    when kind == CompanionKind.Cat => CompanionCommandId.Hide,
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
