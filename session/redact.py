"""Strips the secrets out of the session transcript, in place.

This transcript is going into a repository that a competition reviewer will be
given access to. It was written while standing up a server, so it carries the
things you say out loud while doing that: a DNS token, an API key that is
billed to somebody, the address and key path of a machine that is on the
internet right now.

Redacting rather than deleting the file. The transcript is the record of why
the deployment looks the way it does — which failures were real, which
diagnoses were wrong, what the numbers actually were — and that is worth
keeping. What is not worth keeping is the twenty characters that let a stranger
repoint the domain.

Deliberately not redacting the Unity project id. It is committed in
`ProjectSettings.asset` because it has to be — both machines must name the same
project — so hiding it here would be theatre that makes the next reader think
it is a secret.
"""

import io
import re
import sys

# Ordered: the OpenAI key must go before the loose-byte rule, or the rule that
# catches the hexdump fragment eats the prefix and leaves the tail behind.
RULES = [
    # The DuckDNS token. Full value, and anything shaped like it.
    (r"7752dc37-74d7-4581-a573-de3ac44a81dd", "[REDACTED-DUCKDNS-TOKEN]"),
    # OpenAI keys and any prefix of one.
    (r"sk-[A-Za-z0-9_\-]{4,}", "[REDACTED-OPENAI-KEY]"),
    # The hexdump that leaked about twenty characters of the key. `od -c` puts
    # a space between every character, which is exactly why the redaction that
    # ran at the time did not catch it.
    (r"<redacted>(?:\s{2,}[A-Za-z0-9])+(?:\s*\\n)?", "[REDACTED-KEY-BYTES]"),
    # Every address this session touched: the instance, its elastic and public
    # addresses, its private one, and the two home connections that showed up
    # in the ssh logs.
    (r"\b(?:13\.125\.155\.145|15\.164\.222\.48|172\.31\.34\.222"
     r"|119\.204\.103\.204|121\.153\.39\.133|114\.207\.139\.42)\b",
     "[REDACTED-IP]"),
    # The key file. The name alone tells an attacker what to look for.
    (r"paws-and-loot-key\.pem", "[REDACTED-KEY].pem"),
    (r"ec2-user@\[REDACTED-IP\]", "ec2-user@[REDACTED-IP]"),
]


def redact(text):
    for pattern, replacement in RULES:
        text = re.sub(pattern, replacement, text)
    return text


def main(path):
    original = io.open(path, encoding="utf-8").read()
    cleaned = redact(original)
    io.open(path, "w", encoding="utf-8", newline="").write(cleaned)

    # Counted and printed, because "I ran the redactor" is not evidence that
    # anything was removed — a rule that matches nothing looks identical to a
    # transcript that was already clean.
    print(f"{len(original) - len(cleaned):+d} characters")
    for pattern, replacement in RULES:
        print(f"  {replacement:26s} {len(re.findall(pattern, original))}")


if __name__ == "__main__":
    main(sys.argv[1])
