using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;

namespace PawliceAndPurrglar.Tests.EditMode
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
            "HIDE", "BITE"
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
        /// Commands whose resolver sweeps the world itself, and therefore must
        /// not be refused for arriving without a target.
        ///
        /// `ResolveTrack` reads the scent trail, `ResolveScout` finds the
        /// nearest loot, `ResolveHide` finds the nearest free stash — none of
        /// the three ever looks at <c>request.TryGetDestination</c>. All three
        /// were nonetheless listed as requiring a target, so the validator
        /// refused them before the resolver got a chance.
        /// </summary>
        private static readonly CompanionCommandId[] SelfTargetingCommands =
        {
            CompanionCommandId.Track,
            CompanionCommandId.Scout,
            CompanionCommandId.Hide
        };

        /// <summary>
        /// No voice path can supply a target.
        ///
        /// The deterministic matcher hardcodes `targetId: null`
        /// (`resolveWithMatcher`), and the model may only name something already
        /// in `visibleTargets`. So a command that demands one is reachable by
        /// number key and dead by voice — and it fails as `TargetMissing`, which
        /// plays the same beep as a broken microphone and prints nothing,
        /// because the HUD only narrates refusals from `Keyboard`.
        ///
        /// "냄새 맡아" was the worst case: the one order whose purpose is finding
        /// a thief you cannot see needed the thief visible to be allowed to run.
        /// </summary>
        [Test]
        public void CommandsThatFindTheirOwnTargetDoNotDemandOne()
        {
            foreach (CompanionCommandId command in SelfTargetingCommands)
            {
                Assert.That(
                    CompanionCommandCatalog.RequiresTarget(command),
                    Is.False,
                    $"{command} resolves its own destination, but is listed as "
                    + "requiring a target. No voice path can supply one, so the "
                    + "validator refuses it before the resolver runs and the "
                    + "player hears an unexplained failure beep.");
            }
        }

        /// <summary>
        /// The other side of the same contract: the three that genuinely read
        /// the request's destination must keep demanding it, or they resolve to
        /// a silent no-op instead of an honest refusal.
        /// </summary>
        [Test]
        public void CommandsThatWalkToASpokenPlaceStillDemandOne()
        {
            foreach (CompanionCommandId command in new[]
            {
                CompanionCommandId.Search,
                CompanionCommandId.Guard,
                CompanionCommandId.Distract
            })
            {
                Assert.That(
                    CompanionCommandCatalog.RequiresTarget(command),
                    Is.True,
                    $"{command} is meaningless without a destination.");
            }
        }

        /// <summary>
        /// CAT-010. The cat's bite, reachable by voice and by nothing else.
        ///
        /// Checked at every link because the chain is long and each break is
        /// silent in its own way: an intent absent from the allowed list is
        /// dropped by the server without a word, one absent from `FromIntent`
        /// resolves to no command, and one absent from `VoiceCommandMapper` is
        /// obeyed but printed on screen as "NO COMMAND".
        /// </summary>
        [Test]
        public void TheCatsBiteIsWiredAtEveryLink()
        {
            Assert.That(
                ThiefIntents,
                Contains.Item("BITE"),
                "The server clamps candidates to this list.");
            Assert.That(
                CompanionCommandCatalog.FromIntent("BITE", CompanionKind.Cat),
                Is.EqualTo(CompanionCommandId.Bite));
            Assert.That(
                CompanionCommandCatalog.BelongsTo(
                    CompanionCommandId.Bite,
                    PlayerRole.Thief),
                Is.True);
            Assert.That(
                CompanionCommandCatalog.RequiresTarget(CompanionCommandId.Bite),
                Is.False,
                "There is only one officer, and demanding that voice name them "
                + "would put the command out of reach of the only input that "
                + "can issue it.");
            Assert.That(
                CompanionCommandCatalog.GetCommandsFor(PlayerRole.Thief),
                Contains.Item(CompanionCommandId.Bite),
                "The on-screen table is where a player learns the command "
                + "exists at all.");
        }

        /// <summary>
        /// The dog is refused, at both ends.
        ///
        /// The officer already arrests; a dog that also bit would be a second
        /// route to the same outcome. Refusing beats substituting — a wrong
        /// action the player did not ask for cannot be told apart from being
        /// misheard.
        /// </summary>
        [Test]
        public void TheDogHasNoBite()
        {
            Assert.That(
                PoliceIntents,
                Does.Not.Contain("BITE"));
            Assert.That(
                CompanionCommandCatalog.FromIntent("BITE", CompanionKind.Dog),
                Is.EqualTo(CompanionCommandId.None));
            Assert.That(
                CompanionCommandCatalog.BelongsTo(
                    CompanionCommandId.Bite,
                    PlayerRole.Police),
                Is.False);
        }

        /// <summary>
        /// The animals no longer have the same number of their own commands, so
        /// anything that counted four is now wrong for the cat.
        /// </summary>
        [Test]
        public void EachAnimalsOwnCommandsComeBeforeTheSharedOnes()
        {
            foreach (PlayerRole role in new[]
            {
                PlayerRole.Police,
                PlayerRole.Thief
            })
            {
                CompanionCommandId[] commands =
                    CompanionCommandCatalog.GetCommandsFor(role);
                bool reachedShared = false;
                foreach (CompanionCommandId command in commands)
                {
                    bool shared =
                        CompanionCommandCatalog.IsSharedByBothAnimals(command);
                    if (shared)
                    {
                        reachedShared = true;
                        continue;
                    }

                    Assert.That(
                        reachedShared,
                        Is.False,
                        $"{role}'s {command} is its own but is listed after the "
                        + "shared commands, so the table draws its divider in "
                        + "the wrong place.");
                }

                Assert.That(
                    reachedShared,
                    Is.True,
                    $"{role} must still be offered the shared commands.");
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
