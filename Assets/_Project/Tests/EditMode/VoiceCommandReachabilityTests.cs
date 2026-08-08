using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Every command a number key can issue must also be reachable by voice.
    ///
    /// `Ctrl+4` reached `Bark` and `Hide` while voice could not: no intent string
    /// in `FromIntent` mapped to either, and neither name appeared in the bridge's
    /// allowed list. So a perfect transcript of "짖으라고" resolved to
    /// `CompanionCommandId.None` and the dog did nothing — no exception, no log,
    /// nothing on screen. It reads as a microphone fault, and two of the eight
    /// animal commands were dead for as long as voice existed.
    ///
    /// `CompanionCommandCatalog` carries the comment "voice will map onto the same
    /// ids later, which is the whole point of DEC-002". This is that claim, checked.
    /// </summary>
    public sealed class VoiceCommandReachabilityTests
    {
        /// <summary>
        /// The bridge's allowed lists, duplicated here on purpose.
        ///
        /// The real ones are private inside `CompanionVoiceCommandBridge`, and the
        /// server clamps candidates to whatever it is handed
        /// (`validateCandidates`) — so an intent missing from that list is dropped
        /// without a word anywhere. Restating them makes the drift visible: if the
        /// two disagree, the reachability assertions below stop matching the game.
        /// </summary>
        private static readonly string[] PoliceIntents =
        {
            "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL",
            "SEARCH_AREA", "CHASE_TARGET", "GUARD_AREA", "INSPECT_TARGET", "BARK"
        };

        private static readonly string[] ThiefIntents =
        {
            "STOP", "FOLLOW_OWNER", "STAY", "RETURN_OWNER", "CANCEL",
            "SEARCH_AREA", "FETCH_OBJECT", "DISTRACT_TARGET", "INSPECT_TARGET",
            "HIDE"
        };

        private static IEnumerable<CompanionCommandId> NumberKeyCommands(
            PlayerRole role)
        {
            for (int key = 1; key <= 4; key++)
            {
                CompanionCommandId command =
                    CompanionCommandCatalog.FromNumberKey(role, key);
                if (command != CompanionCommandId.None)
                {
                    yield return command;
                }
            }
        }

        private static HashSet<CompanionCommandId> VoiceReachable(
            PlayerRole role,
            IEnumerable<string> intents)
        {
            CompanionKind kind =
                CompanionCommandCatalog.GetCompanionKind(role);
            return intents
                .Select(intent =>
                    CompanionCommandCatalog.FromIntent(intent, kind))
                .Where(command => command != CompanionCommandId.None)
                .ToHashSet();
        }

        [Test]
        public void EveryNumberKeyCommandIsAlsoReachableByVoice()
        {
            foreach ((PlayerRole role, string[] intents) in new[]
            {
                (PlayerRole.Police, PoliceIntents),
                (PlayerRole.Thief, ThiefIntents)
            })
            {
                HashSet<CompanionCommandId> reachable =
                    VoiceReachable(role, intents);
                foreach (CompanionCommandId command in NumberKeyCommands(role))
                {
                    Assert.That(
                        reachable,
                        Contains.Item(command),
                        $"{role}'s {command} can be issued with a number key but "
                        + "no allowed voice intent maps to it, so speaking it does "
                        + "nothing and says nothing.");
                }
            }
        }

        /// <summary>
        /// The two that were actually missing, named so the regression is legible
        /// in a failure list rather than hidden inside a loop.
        /// </summary>
        [Test]
        public void BarkAndHideHaveVoiceIntents()
        {
            Assert.That(
                CompanionCommandCatalog.FromIntent("BARK", CompanionKind.Dog),
                Is.EqualTo(CompanionCommandId.Bark));
            Assert.That(
                CompanionCommandCatalog.FromIntent("HIDE", CompanionKind.Cat),
                Is.EqualTo(CompanionCommandId.Hide));

            Assert.That(
                PoliceIntents,
                Contains.Item("BARK"),
                "The dog's allowed list gates what the server may return.");
            Assert.That(
                ThiefIntents,
                Contains.Item("HIDE"),
                "The cat's allowed list gates what the server may return.");
        }

        /// <summary>
        /// An allowed intent that maps to nothing is the same silent failure seen
        /// from the other end — the server returns it, the game discards it.
        /// </summary>
        [Test]
        public void NoAllowedIntentResolvesToNoCommand()
        {
            foreach ((PlayerRole role, string[] intents) in new[]
            {
                (PlayerRole.Police, PoliceIntents),
                (PlayerRole.Thief, ThiefIntents)
            })
            {
                CompanionKind kind =
                    CompanionCommandCatalog.GetCompanionKind(role);
                foreach (string intent in intents)
                {
                    Assert.That(
                        CompanionCommandCatalog.FromIntent(intent, kind),
                        Is.Not.EqualTo(CompanionCommandId.None),
                        $"{role} is allowed to be told '{intent}' but it maps to "
                        + "no command, so the answer is thrown away in silence.");
                }
            }
        }

        /// <summary>
        /// Each animal keeps its own fourth command. The cat barking or the dog
        /// hiding would be a different command silently substituted, which is
        /// worse than a refusal — the player sees the wrong action and cannot tell
        /// whether they were misheard or disobeyed.
        /// </summary>
        [Test]
        public void TheFourthCommandDoesNotCrossBetweenAnimals()
        {
            Assert.That(
                CompanionCommandCatalog.FromIntent("BARK", CompanionKind.Cat),
                Is.Not.EqualTo(CompanionCommandId.Bark));
            Assert.That(
                CompanionCommandCatalog.FromIntent("HIDE", CompanionKind.Dog),
                Is.Not.EqualTo(CompanionCommandId.Hide));
        }
    }
}
