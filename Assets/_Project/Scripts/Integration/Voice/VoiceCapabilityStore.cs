using System;

namespace PawliceAndPurrglar.Integration.Voice
{
    public static class VoiceCapabilityStore
    {
        public static event Action Changed;

        public static string SessionId { get; private set; } = string.Empty;
        public static string Token { get; private set; } = string.Empty;

        public static void Set(string sessionId, string token)
        {
            SessionId = sessionId ?? string.Empty;
            Token = token ?? string.Empty;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            SessionId = string.Empty;
            Token = string.Empty;
            Changed?.Invoke();
        }
    }
}
