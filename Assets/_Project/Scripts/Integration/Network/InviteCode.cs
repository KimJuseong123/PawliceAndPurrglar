using System.Text;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// The six characters one player reads out and the other types in.
    ///
    /// The value itself is Relay's join code, not something this game invents:
    /// the allocation exists on Unity's servers before the code does, so making
    /// up our own would mean keeping a second table mapping one to the other,
    /// with nothing to put it in.
    ///
    /// What this type owns is the trip between the two players. A code arrives
    /// pasted out of a chat window, and chat windows add things — a trailing
    /// newline, a hyphen somebody inserted to make it readable, the space that
    /// comes with a double-click selection. All of those name the same room, so
    /// they are stripped rather than refused. Case is folded up for the same
    /// reason: Relay issues upper case, phones offer lower.
    /// </summary>
    public static class InviteCode
    {
        /// <summary>
        /// How long a Relay join code is. Not a preference — Relay decides it,
        /// and this constant is here so the check and the message agree.
        /// </summary>
        public const int Length = 6;

        /// <summary>
        /// Strips a pasted code down to the code.
        ///
        /// Deliberately narrow about what it removes. Whitespace, hyphens and
        /// underscores are formatting somebody added; everything else is left
        /// in place so <see cref="IsValid"/> can reject it and the player is
        /// told they pasted the wrong thing, rather than having the wrong thing
        /// silently filed down into a plausible-looking room that does not
        /// exist.
        /// </summary>
        public static string Normalise(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);
            foreach (char character in raw)
            {
                if (char.IsWhiteSpace(character)
                    || character == '-'
                    || character == '_'
                    // Zero-width space and the byte-order mark. Both ride along
                    // invisibly on a copy out of a browser and neither can be
                    // seen in the input field, so a code that looks exactly
                    // right fails to match.
                    || character == '\u200B'
                    || character == '\uFEFF')
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(character));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Whether this is already a code: six characters, letters and digits
        /// only, upper case.
        /// </summary>
        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length)
            {
                return false;
            }

            foreach (char character in code)
            {
                bool digit = character >= '0' && character <= '9';
                bool upper = character >= 'A' && character <= 'Z';
                if (!digit && !upper)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryNormalise(string raw, out string code)
        {
            code = Normalise(raw);
            return IsValid(code);
        }

        /// <summary>
        /// Why a code was refused, in a sentence the lobby can show, or null
        /// when there is nothing wrong with it.
        ///
        /// Separate from <see cref="IsValid"/> because "wrong length" and
        /// "contains a slash" send the player to different places: the first
        /// means they missed a character, the second means they pasted a URL.
        /// A single "invalid code" would leave them retyping the same thing.
        /// </summary>
        public static string DescribeProblem(string raw)
        {
            string code = Normalise(raw);
            if (code.Length == 0)
            {
                return "초대코드를 입력하세요.";
            }

            foreach (char character in code)
            {
                bool digit = character >= '0' && character <= '9';
                bool upper = character >= 'A' && character <= 'Z';
                if (!digit && !upper)
                {
                    return "초대코드는 영문자와 숫자뿐입니다.";
                }
            }

            if (code.Length != Length)
            {
                return $"초대코드는 {Length}글자입니다. "
                    + $"(지금 {code.Length}글자)";
            }

            return null;
        }
    }
}
