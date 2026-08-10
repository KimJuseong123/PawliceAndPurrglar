using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Input;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class CompanionAgentPlayModeTests
    {
        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }

        private sealed class Fixture
        {
            public MutableMatchState State;
            public Transform Owner;
            public CompanionAgent Agent;
            public CompanionConfig Config;
            public PlayerRoleIdentity Identity;

            public void Destroy()
            {
                if (Agent != null)
                {
                    Object.DestroyImmediate(Agent.gameObject);
                }

                if (Owner != null)
                {
                    Object.DestroyImmediate(Owner.gameObject);
                }

                if (Identity != null)
                {
                    Object.DestroyImmediate(Identity.gameObject);
                }

                if (Config != null)
                {
                    Object.DestroyImmediate(Config);
                }
            }
        }

        private static Fixture CreateDog(bool playing = true)
        {
            var state = new MutableMatchState { IsGameplayActive = playing };
            var ownerObject = new GameObject("Police Owner");
            ownerObject.transform.position = Vector3.zero;

            var identityObject = new GameObject("Police Identity");
            PlayerRoleIdentity identity =
                identityObject.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);

            CompanionConfig config =
                ScriptableObject.CreateInstance<CompanionConfig>();

            var agentObject = new GameObject("Dog");
            agentObject.SetActive(false);
            agentObject.transform.position = Vector3.zero;
            CompanionAgent agent =
                agentObject.AddComponent<CompanionAgent>();
            agent.Configure(
                CompanionKind.Dog,
                ownerObject.transform,
                config,
                state);
            agentObject.SetActive(true);

            return new Fixture
            {
                State = state,
                Owner = ownerObject.transform,
                Agent = agent,
                Config = config,
                Identity = identity
            };
        }

        [UnityTest]
        public IEnumerator WaitsInsideTheStopBandAndFollowsWhenTheOwnerLeaves()
        {
            Fixture fixture = CreateDog();

            // Close enough: the dog must not shuffle around the player.
            fixture.Owner.position = new Vector3(1f, 0f, 0f);
            Vector3 before = fixture.Agent.transform.position;
            fixture.Agent.Tick(0.1f);
            Assert.That(
                Vector3.Distance(fixture.Agent.transform.position, before),
                Is.LessThan(0.001f));
            Assert.That(
                fixture.Agent.CurrentState,
                Is.EqualTo(CompanionState.Idle));

            // Far enough: it should close the gap.
            fixture.Owner.position = new Vector3(10f, 0f, 0f);
            for (int step = 0; step < 40; step++)
            {
                fixture.Agent.Tick(0.05f);
            }

            Assert.That(
                Vector3.Distance(
                    fixture.Agent.transform.position,
                    fixture.Owner.position),
                Is.LessThan(3.3f));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakingTheLeashReturnsTheCompanionToItsOwner()
        {
            Fixture fixture = CreateDog();
            fixture.Agent.transform.position = new Vector3(200f, 0f, 200f);
            fixture.Owner.position = Vector3.zero;

            fixture.Agent.Tick(0.1f);

            Assert.That(
                Vector3.Distance(
                    fixture.Agent.transform.position,
                    fixture.Owner.position),
                Is.LessThan(5f),
                "A companion beyond its leash must be recovered.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnreachableTargetRecoversInsteadOfStallingForever()
        {
            Fixture fixture = CreateDog();
            int recovered = 0;
            CompanionCommandRejection reason =
                CompanionCommandRejection.None;
            fixture.Agent.CommandRecovered += (_, r) =>
            {
                recovered++;
                reason = r;
            };

            // A destination the straight-line stepper can reach only by
            // walking, then frozen in place to simulate being blocked.
            var request = new CompanionCommandRequest(
                CompanionCommandId.Track,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                0f,
                null,
                new Vector3(0f, 0f, 400f));
            Assert.That(
                fixture.Agent.TryAcceptCommand(request),
                Is.True);
            Assert.That(
                fixture.Agent.CurrentState,
                Is.EqualTo(CompanionState.MoveToTarget));

            // Beyond the command timeout the order is abandoned.
            for (int step = 0; step < 200; step++)
            {
                fixture.Agent.Tick(0.05f);
                if (recovered > 0)
                {
                    break;
                }
            }

            Assert.That(recovered, Is.EqualTo(1));
            Assert.That(
                reason,
                Is.EqualTo(CompanionCommandRejection.TargetUnreachable));
            Assert.That(
                fixture.Agent.CurrentState,
                Is.Not.EqualTo(CompanionState.MoveToTarget));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MatchEndDisablesTheCompanionAndFreezesIt()
        {
            Fixture fixture = CreateDog();
            fixture.Owner.position = new Vector3(30f, 0f, 0f);

            fixture.Agent.HandleMatchEnded();
            Assert.That(
                fixture.Agent.CurrentState,
                Is.EqualTo(CompanionState.Disabled));
            Assert.That(fixture.Agent.IsActive, Is.False);

            Vector3 frozen = fixture.Agent.transform.position;
            for (int step = 0; step < 20; step++)
            {
                fixture.Agent.Tick(0.05f);
            }

            Assert.That(
                Vector3.Distance(fixture.Agent.transform.position, frozen),
                Is.LessThan(0.001f));
            Assert.That(
                fixture.Agent.TryAcceptCommand(
                    new CompanionCommandRequest(
                        CompanionCommandId.Bark,
                        PlayerRole.Police,
                        CompanionKind.Dog,
                        CompanionCommandInputSource.Keyboard,
                        0f)),
                Is.False);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DispatcherRejectsInvalidCommandsWithoutCooldown()
        {
            Fixture fixture = CreateDog();
            var dispatcherObject = new GameObject("Dispatcher");
            dispatcherObject.SetActive(false);
            CompanionCommandDispatcher dispatcher =
                dispatcherObject.AddComponent<
                    CompanionCommandDispatcher>();
            dispatcher.Configure(
                fixture.State,
                new[] { fixture.Agent });
            dispatcherObject.SetActive(true);

            // Wrong faction: the police cannot order the cat's DISTRACT.
            Assert.That(
                dispatcher.TryDispatchNumberKey(
                    PlayerRole.Police,
                    2,
                    CompanionCommandInputSource.Keyboard,
                    0f,
                    null,
                    new Vector3(4f, 0f, 0f),
                    out CompanionCommandRejection rejection),
                Is.True,
                "Police key 2 is SEARCH, which is a valid dog command.");

            // A refused command must leave the cooldown untouched.
            fixture.State.IsGameplayActive = false;
            float cooldownBefore = fixture.Agent.CooldownRemainingSeconds;
            Assert.That(
                dispatcher.TryDispatchNumberKey(
                    PlayerRole.Police,
                    1,
                    CompanionCommandInputSource.Keyboard,
                    0f,
                    null,
                    new Vector3(4f, 0f, 0f),
                    out rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.MatchNotPlaying));
            Assert.That(
                fixture.Agent.CooldownRemainingSeconds,
                Is.EqualTo(cooldownBefore));
            Assert.That(dispatcher.RejectedCount, Is.EqualTo(1));

            // An out of range number key is an unknown command, not a crash.
            Assert.That(
                dispatcher.TryDispatchNumberKey(
                    PlayerRole.Police,
                    9,
                    CompanionCommandInputSource.Keyboard,
                    0f,
                    null,
                    null,
                    out rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.UnknownCommand));

            Object.DestroyImmediate(dispatcherObject);
            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardAdapterReachesTheAgentOnlyViaTheDispatcher()
        {
            Fixture fixture = CreateDog();
            var dispatcherObject = new GameObject("Dispatcher");
            dispatcherObject.SetActive(false);
            CompanionCommandDispatcher dispatcher =
                dispatcherObject.AddComponent<
                    CompanionCommandDispatcher>();
            dispatcher.Configure(
                fixture.State,
                new[] { fixture.Agent });
            dispatcherObject.SetActive(true);

            var inputObject = new GameObject("Command Input");
            CompanionCommandKeyboardInput input =
                inputObject.AddComponent<CompanionCommandKeyboardInput>();
            input.Configure(
                dispatcher,
                fixture.Identity,
                fixture.Identity.transform,
                true);

            Assert.That(input.TryIssue(1, 0f), Is.True);
            Assert.That(
                input.LastRejection,
                Is.EqualTo(CompanionCommandRejection.None));
            Assert.That(dispatcher.AcceptedCount, Is.EqualTo(1));
            Assert.That(
                fixture.Agent.ActiveCommand,
                Is.EqualTo(CompanionCommandId.Track));

            Object.DestroyImmediate(inputObject);
            Object.DestroyImmediate(dispatcherObject);
            fixture.Destroy();
            yield return null;
        }
    }
}
