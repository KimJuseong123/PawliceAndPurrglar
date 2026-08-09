using System;
using System.Threading.Tasks;
using PawsAndLoot.Logging;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
#if PAWS_RELAY
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// What a Relay call gave back: either an invite code, or a sentence
    /// explaining why there isn't one.
    ///
    /// A bool would have been enough for the code, and useless for the lobby.
    /// Every way this fails — the project was never linked, the code was typed
    /// wrong, the machine is offline — looks identical to a caller holding
    /// <c>false</c>, and they are three different things for the player to do
    /// something about.
    /// </summary>
    public readonly struct RelayOutcome
    {
        private RelayOutcome(bool ok, string inviteCode, string error)
        {
            Ok = ok;
            InviteCode = inviteCode;
            Error = error;
        }

        public bool Ok { get; }
        public string InviteCode { get; }
        public string Error { get; }

        public static RelayOutcome Success(string inviteCode) =>
            new(true, inviteCode, null);

        public static RelayOutcome Failure(string error) =>
            new(false, null, error);
    }

    /// <summary>
    /// Puts a room on Unity's Relay and hands back the code that reaches it.
    ///
    /// This exists because of one line in Unity Transport: a WebGL build is
    /// refused as a server unless the protocol is Relay. A browser cannot open
    /// a listening socket, so "one player hosts and reads out their IP" — which
    /// is what this game did — stops being possible the moment it is played in
    /// a browser. Relay is not a convenience here; it is the only shape a
    /// browser-hosted match can take.
    ///
    /// The connection type is always <c>wss</c>, on desktop as well as in the
    /// browser. The browser has no other option, and picking <c>dtls</c> for
    /// desktop would mean the transport under a playtest and the transport
    /// under the submitted build are different ones — the exact split this
    /// project already removed once when it dropped UDP.
    ///
    /// Nothing here touches the rules. It configures a transport and returns a
    /// string; who is police and who is thief is still decided by the role
    /// board, on the server, after the connection exists.
    /// </summary>
    public static class RelaySessionService
    {
        /// <summary>
        /// Peers besides the host. Relay counts the host separately, so a
        /// two-player match asks for one.
        /// </summary>
        public const int PeerCount = NetworkSessionController.MaximumPlayers - 1;

        /// <summary>
        /// Whether this build was compiled with the Relay package present.
        ///
        /// False is a real state, not a bug: the packages resolve from Unity's
        /// registry, and a checkout on a machine that has never been online
        /// compiles without them. The lobby says so rather than presenting a
        /// button that cannot work.
        /// </summary>
        public static bool IsAvailable =>
#if PAWS_RELAY
            true;
#else
            false;
#endif

        /// <summary>
        /// Signs in ahead of time so the first button press only costs the
        /// allocation.
        ///
        /// Creating a room is three round trips — initialise services, sign in
        /// anonymously, ask for an allocation — and the first two do not depend
        /// on anything the player has done yet. Left until the press, they are
        /// several seconds during which a button appears to have done nothing,
        /// which is the reading this project has had to fix three times for
        /// other reasons.
        ///
        /// Failure is deliberately quiet. The machine may simply be offline,
        /// and the player has not asked for anything yet; whatever is wrong
        /// will be said properly, once, when they press the button.
        /// </summary>
        public static Task PrewarmAsync()
        {
#if PAWS_RELAY
            return IsProjectLinked
                ? EnsureSignedInAsync(quiet: true)
                : Task.CompletedTask;
#else
            return Task.CompletedTask;
#endif
        }

        /// <summary>
        /// Creates a room and returns the code that joins it.
        ///
        /// The transport is configured before this returns, so the caller's
        /// next statement can be <c>StartHost()</c>.
        /// </summary>
        public static async Task<RelayOutcome> CreateRoomAsync(
            UnityTransport transport)
        {
#if PAWS_RELAY
            if (transport == null)
            {
                return RelayOutcome.Failure(
                    "UnityTransport 컴포넌트를 찾을 수 없습니다.");
            }

            if (!IsProjectLinked)
            {
                WarnUnlinked();
                return RelayOutcome.Failure(UnlinkedMessage);
            }

            string signInError = await EnsureSignedInAsync();
            if (signInError != null)
            {
                return RelayOutcome.Failure(signInError);
            }

            try
            {
                Allocation allocation =
                    await RelayService.Instance.CreateAllocationAsync(
                        PeerCount,
                        await ResolveRegionAsync());
                string joinCode =
                    await RelayService.Instance.GetJoinCodeAsync(
                        allocation.AllocationId);

                if (!TryFindSecureEndpoint(
                        allocation.ServerEndpoints,
                        out RelayServerEndpoint endpoint,
                        out string endpointError))
                {
                    return RelayOutcome.Failure(endpointError);
                }

                // The host is its own host: there is no other allocation whose
                // connection data it would need.
                Apply(
                    transport,
                    endpoint,
                    allocation.AllocationIdBytes,
                    allocation.ConnectionData,
                    allocation.ConnectionData,
                    allocation.Key);

                GameLogger.Info(
                    GameLogCategory.Network,
                    $"Relay room created in region '{allocation.Region}' "
                    + $"via {endpoint.Host}:{endpoint.Port}.");
                return RelayOutcome.Success(
                    InviteCode.Normalise(joinCode));
            }
            catch (Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Network,
                    exception,
                    "Relay refused to create a room.");
                return RelayOutcome.Failure(DescribeCreateFailure(exception));
            }
#else
            await Task.CompletedTask;
            return RelayOutcome.Failure(UnavailableMessage);
#endif
        }

        /// <summary>
        /// Joins the room a code names. The transport is configured before this
        /// returns, so the caller's next statement can be
        /// <c>StartClient()</c>.
        /// </summary>
        public static async Task<RelayOutcome> JoinRoomAsync(
            UnityTransport transport,
            string rawCode)
        {
            string problem = InviteCode.DescribeProblem(rawCode);
            if (problem != null)
            {
                return RelayOutcome.Failure(problem);
            }

            string code = InviteCode.Normalise(rawCode);
#if PAWS_RELAY
            if (transport == null)
            {
                return RelayOutcome.Failure(
                    "UnityTransport 컴포넌트를 찾을 수 없습니다.");
            }

            if (!IsProjectLinked)
            {
                WarnUnlinked();
                return RelayOutcome.Failure(UnlinkedMessage);
            }

            string signInError = await EnsureSignedInAsync();
            if (signInError != null)
            {
                return RelayOutcome.Failure(signInError);
            }

            try
            {
                JoinAllocation allocation =
                    await RelayService.Instance.JoinAllocationAsync(code);

                if (!TryFindSecureEndpoint(
                        allocation.ServerEndpoints,
                        out RelayServerEndpoint endpoint,
                        out string endpointError))
                {
                    return RelayOutcome.Failure(endpointError);
                }

                Apply(
                    transport,
                    endpoint,
                    allocation.AllocationIdBytes,
                    allocation.ConnectionData,
                    allocation.HostConnectionData,
                    allocation.Key);

                GameLogger.Info(
                    GameLogCategory.Network,
                    $"Joined Relay room via {endpoint.Host}:{endpoint.Port}.");
                return RelayOutcome.Success(code);
            }
            catch (Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Network,
                    exception,
                    $"Relay refused the invite code '{code}'.");
                return RelayOutcome.Failure(DescribeJoinFailure(exception));
            }
#else
            await Task.CompletedTask;
            return RelayOutcome.Failure(UnavailableMessage);
#endif
        }

        private const string UnavailableMessage =
            "온라인 대전 패키지가 설치되지 않았습니다.";

        private const string UnlinkedMessage =
            "Unity 프로젝트 연결이 필요합니다.";

        /// <summary>
        /// Whether this build knows which Unity project it belongs to.
        ///
        /// Checked before anything is sent, because the failure it prevents is
        /// the worst-shaped one available here: an unlinked build reaches Relay,
        /// waits out a round trip, and comes back with an authorization error
        /// that reads like a network problem. The answer is knowable locally and
        /// instantly, and it is nearly always the answer — linking the project
        /// is a step somebody does once and forgets.
        ///
        /// It is also what keeps the lobby's own tests off the network. With no
        /// project linked there is nothing to call, so pressing 방 만들기 in the
        /// editor resolves before the first await instead of opening a request
        /// that outlives the test that started it.
        /// </summary>
        public static bool IsProjectLinked =>
            !string.IsNullOrEmpty(Application.cloudProjectId);

        /// <summary>
        /// A warning rather than an error, and said once.
        ///
        /// It is a setup step that has not been done yet, not a fault in the
        /// running game — and the lobby polls, so an error here would fill the
        /// log seven times a second and fail every Play Mode test that happened
        /// to be running nearby.
        /// </summary>
        private static void WarnUnlinked()
        {
            GameLogger.WarningOnce(
                GameLogCategory.Network,
                "relay-project-unlinked",
                "This build has no Unity project id, so Relay cannot be "
                + "reached. Link the project in Unity: Edit > Project Settings "
                + "> Services.");
        }

#if PAWS_RELAY
        /// <summary>
        /// Brings the services up and signs in anonymously, once.
        ///
        /// Anonymous because the match needs an identity that lasts as long as
        /// the tab does and no longer. There is nothing to remember between
        /// sessions, so asking a player to make an account would buy nothing
        /// and cost the two people this game is for.
        /// </summary>
        private static async Task<string> EnsureSignedInAsync(bool quiet = false)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance
                        .SignInAnonymouslyAsync();
                }

                return null;
            }
            catch (Exception exception)
            {
                if (quiet)
                {
                    // A prewarm that failed costs nothing yet: the press will
                    // try again and report properly. Logged once so a machine
                    // that can never sign in still leaves a trace.
                    GameLogger.WarningOnce(
                        GameLogCategory.Network,
                        "relay-prewarm-failed",
                        "Signing in ahead of time failed; the first room will "
                        + "take longer. " + exception.Message);
                }
                else
                {
                    GameLogger.Exception(
                        GameLogCategory.Network,
                        exception,
                        "Unity Services would not start, so no room can be "
                        + "created or joined.");
                }

                return "온라인 서비스에 연결하지 못했습니다.";
            }
        }

        /// <summary>
        /// Picks the <c>wss</c> endpoint, and says so plainly when there isn't
        /// one.
        ///
        /// Not a fallback to <c>dtls</c>. A browser cannot use it, so falling
        /// back would produce a build that works on the developer's desktop and
        /// fails on the machine it is submitted from — the failure would arrive
        /// at the worst possible moment and look like a network problem.
        /// </summary>
        private static bool TryFindSecureEndpoint(
            System.Collections.Generic.List<RelayServerEndpoint> endpoints,
            out RelayServerEndpoint found,
            out string error)
        {
            found = null;
            error = null;
            if (endpoints != null)
            {
                foreach (RelayServerEndpoint endpoint in endpoints)
                {
                    if (endpoint != null
                        && endpoint.ConnectionType
                            == RelayServerEndpoint.ConnectionTypeWss)
                    {
                        found = endpoint;
                        return true;
                    }
                }
            }

            error = "Relay가 보안 접속 주소를 주지 않았습니다.";
            GameLogger.Error(
                GameLogCategory.Network,
                "The Relay allocation carries no 'wss' endpoint. A browser "
                + "cannot connect over any of the others.");
            return false;
        }

        /// <summary>
        /// Regions to prefer, nearest first, for a game played in Korea.
        ///
        /// Matched as substrings against whatever ids Relay actually offers, so
        /// a renamed or retired region degrades to the next choice instead of
        /// throwing. Unity has published these under several naming schemes.
        /// </summary>
        private static readonly string[] PreferredRegions =
        {
            "seoul", "asia-northeast3", "tokyo", "asia-northeast",
            "asia-south", "asia"
        };

        /// <summary>
        /// Which Relay region to allocate in, or null to let Relay decide.
        ///
        /// Chosen explicitly because the automatic choice cannot work here.
        /// Relay picks a region from QoS measurements, and QoS measures with UDP
        /// pings — which a browser cannot send. So a WebGL host gets whatever
        /// the fallback is, and the fallback is not necessarily on this
        /// continent.
        ///
        /// That is paid entirely by the guest. The host is the server and sees
        /// its own actions immediately; the client's every keypress makes the
        /// round trip client → Relay → host → Relay → client, because this game
        /// does not predict client movement (`NetworkPlayerLink`: "client input
        /// -> RPC -> host simulates -> position replicates"). A relay on the
        /// wrong continent turns that into a third of a second, on the client
        /// only — which is exactly the shape of "호스트는 멀쩡한데 클라이언트만
        /// 렉이 심하다".
        ///
        /// Returns null on any failure. A room in a far region is worse than a
        /// near one and much better than no room at all.
        /// </summary>
        private static async Task<string> ResolveRegionAsync()
        {
            try
            {
                System.Collections.Generic.List<Region> regions =
                    await RelayService.Instance.ListRegionsAsync();
                if (regions == null || regions.Count == 0)
                {
                    return null;
                }

                foreach (string wanted in PreferredRegions)
                {
                    foreach (Region region in regions)
                    {
                        if (region?.Id != null
                            && region.Id.ToLowerInvariant().Contains(wanted))
                        {
                            GameLogger.InfoOnce(
                                GameLogCategory.Network,
                                "relay-region",
                                $"Relay region '{region.Id}' chosen from "
                                + $"{regions.Count} offered.");
                            return region.Id;
                        }
                    }
                }

                GameLogger.WarningOnce(
                    GameLogCategory.Network,
                    "relay-region-missing",
                    "No preferred Relay region is on offer; letting Relay "
                    + "choose. Offered: "
                    + string.Join(", ", regions.ConvertAll(r => r.Id)));
                return null;
            }
            catch (Exception exception)
            {
                GameLogger.WarningOnce(
                    GameLogCategory.Network,
                    "relay-region-failed",
                    "Could not list Relay regions; letting Relay choose. "
                    + exception.Message);
                return null;
            }
        }

        private static void Apply(
            UnityTransport transport,
            RelayServerEndpoint endpoint,
            byte[] allocationId,
            byte[] connectionData,
            byte[] hostConnectionData,
            byte[] key)
        {
            // Both flags, or the transport logs a mismatch and connects to
            // nothing: UseWebSockets picks the network interface, isWebSocket
            // on the server data picks the URL scheme, and Unity Transport
            // checks that the two agree.
            transport.UseWebSockets = true;
            transport.SetRelayServerData(
                new RelayServerData(
                    endpoint.Host,
                    (ushort)endpoint.Port,
                    allocationId,
                    connectionData,
                    hostConnectionData,
                    key,
                    isSecure: true,
                    isWebSocket: true));
        }

        private static string DescribeCreateFailure(Exception exception)
        {
            if (exception is RelayServiceException relay)
            {
                return relay.Reason switch
                {
                    RelayExceptionReason.Unauthorized
                        or RelayExceptionReason.Forbidden =>
                        "Unity 프로젝트 연결이 필요합니다.",
                    RelayExceptionReason.InactiveProject =>
                        "대시보드에서 Relay가 꺼져 있습니다.",
                    RelayExceptionReason.RateLimited =>
                        "요청이 너무 잦습니다. 잠시 후 다시 시도하세요.",
                    _ => "방을 만들지 못했습니다. 다시 시도하세요."
                };
            }

            return "방을 만들지 못했습니다. 연결을 확인하세요.";
        }

        private static string DescribeJoinFailure(Exception exception)
        {
            if (exception is RelayServiceException relay)
            {
                return relay.Reason switch
                {
                    RelayExceptionReason.JoinCodeNotFound
                        or RelayExceptionReason.AllocationNotFound
                        or RelayExceptionReason.EntityNotFound =>
                        "그 코드의 방이 없습니다. 다시 확인하세요.",
                    RelayExceptionReason.Unauthorized
                        or RelayExceptionReason.Forbidden =>
                        "Unity 프로젝트 연결이 필요합니다.",
                    RelayExceptionReason.RateLimited =>
                        "요청이 너무 잦습니다. 잠시 후 다시 시도하세요.",
                    _ => "방에 들어가지 못했습니다. 다시 시도하세요."
                };
            }

            return "방에 들어가지 못했습니다. 연결을 확인하세요.";
        }
#endif
    }
}
