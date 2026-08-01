using System;
using System.Text;

namespace PawsAndLoot.Companions
{
    public enum VoiceUnderstanding
    {
        Misunderstood = 0,
        Partial = 1,
        Exact = 2
    }

    public readonly struct CoreCommandMatch
    {
        public CoreCommandMatch(
            CompanionCommandId commandId,
            VoiceUnderstanding understanding,
            float confidence)
        {
            CommandId = commandId;
            Understanding = understanding;
            Confidence = confidence;
        }

        public CompanionCommandId CommandId { get; }
        public VoiceUnderstanding Understanding { get; }
        public float Confidence { get; }
        public bool Accepted => CommandId != CompanionCommandId.None;
    }

    /// <summary>
    /// Deterministic core vocabulary used before any external LLM service.
    /// </summary>
    public static class CoreCommandMatcher
    {
        public static CoreCommandMatch Match(
            string transcript,
            CompanionKind kind)
        {
            string normalized = Normalize(transcript);
            if (string.IsNullOrEmpty(normalized))
            {
                return new CoreCommandMatch(
                    CompanionCommandId.None,
                    VoiceUnderstanding.Misunderstood,
                    0f);
            }

            if (kind == CompanionKind.Dog)
            {
                if (ContainsAny(normalized, "냄새", "흔적", "scent", "track"))
                {
                    return Match(CompanionCommandId.DogScentTrack, normalized, "추적", "track");
                }

                if (ContainsAny(normalized, "경계", "지켜", "guard"))
                {
                    return Match(CompanionCommandId.DogGuard, normalized, "경계", "guard");
                }

                if (ContainsAny(normalized, "쫓아", "추격", "chase"))
                {
                    return Match(CompanionCommandId.DogChase, normalized, "쫓아", "chase");
                }

                if (ContainsAny(normalized, "물어", "물기", "bite"))
                {
                    return Match(CompanionCommandId.DogBite, normalized, "물", "bite");
                }
            }
            else
            {
                if (ContainsAny(normalized, "지붕", "옥상", "climb", "roof"))
                {
                    return Match(CompanionCommandId.CatClimbRoof, normalized, "지붕", "roof");
                }

                if (ContainsAny(normalized, "할퀴", "scratch"))
                {
                    return Match(CompanionCommandId.CatScratch, normalized, "할퀴", "scratch");
                }

                if (ContainsAny(normalized, "소리", "울어", "scream"))
                {
                    return Match(CompanionCommandId.CatScream, normalized, "소리", "scream");
                }

                if (ContainsAny(normalized, "은신처", "숨을 곳", "hideout"))
                {
                    return Match(CompanionCommandId.CatFindHideout, normalized, "은신처", "hideout");
                }
            }

            return new CoreCommandMatch(
                CompanionCommandId.None,
                VoiceUnderstanding.Misunderstood,
                0.05f);
        }

        private static CoreCommandMatch Match(
            CompanionCommandId command,
            string normalized,
            string koreanKeyword,
            string englishKeyword)
        {
            bool exact = normalized == koreanKeyword
                || normalized == englishKeyword
                || normalized.Contains("강아지" + koreanKeyword)
                || normalized.Contains("고양이" + koreanKeyword);
            return new CoreCommandMatch(
                command,
                exact ? VoiceUnderstanding.Exact : VoiceUnderstanding.Partial,
                exact ? 0.98f : 0.76f);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsWhiteSpace(character)
                    || char.IsPunctuation(character)
                    || char.IsSymbol(character))
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
