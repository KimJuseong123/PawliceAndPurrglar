using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Presses the shipping lobby's own controls.
    ///
    /// The lobby this replaces looked complete and did nothing: its buttons were
    /// transparent rectangles over a picture, and one release shipped six of
    /// them with no listener at all. Checking that a control exists is not the
    /// same as checking that pressing it reaches anything, so every case here
    /// invokes the real button and looks for the effect on the real session.
    ///
    /// No case starts a host. A started NetworkManager moves itself out of the
    /// scene and would outlive the unload, taking the rest of the Play Mode
    /// suite with it. The session's own input validation gives an observable
    /// answer without opening a socket.
    /// </summary>
    public sealed class LobbyInteractionPlayModeTests
    {
        private const string SceneName = "Bootstrap";

        /// <summary>
        /// Three characters, so the session rejects it before it can reach the
        /// network. Every case here has to give the same answer on a machine
        /// with no internet as on one with it, and a code that could be looked
        /// up would leave the result depending on whether the project happens
        /// to be linked today.
        /// </summary>
        private const string ShortCode = "ABC";

        private const string ShortCodeRejection =
            "초대코드는 6글자입니다. (지금 3글자)";

        private const string CreatingRoom = "방을 만드는 중입니다...";

        private Scene _scene;
        private GameObject _lobby;
        private NetworkLobbyPresenter _presenter;
        private LobbyMicrophoneDiagnostics _microphone;
        private NetworkSessionController _session;
        private GameObject _networkManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_scene.IsValid() && _scene.isLoaded, Is.True, SceneName);

            _lobby = _scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "LobbyCanvas");
            Assert.That(_lobby, Is.Not.Null, "Bootstrap has no LobbyCanvas.");

            _presenter = _lobby.GetComponent<NetworkLobbyPresenter>();
            _microphone = _lobby.GetComponent<LobbyMicrophoneDiagnostics>();
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_microphone, Is.Not.Null);

            // One frame so OnEnable has bound the buttons and the first refresh
            // has run.
            yield return null;

            // Taken from the presenter, not from a scene search. An earlier
            // Play Mode test can leave a NetworkManager in DontDestroyOnLoad,
            // and a search would then hand back a session no button here talks
            // to — which reads as "the button did nothing".
            _session = _presenter.Session;
            Assert.That(
                _session,
                Is.Not.Null,
                "The lobby was not given a session.");
            _networkManager = _session.gameObject;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Reset unconditionally. It is a static on the test framework, and a
            // case that left it set would hide the next case's error logs.
            LogAssert.ignoreFailingMessages = false;

            if (_session != null)
            {
                _session.Leave();
            }

            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            // A NetworkManager that ever initialised moves itself into
            // DontDestroyOnLoad and survives the unload. Left behind, it broke
            // three unrelated raccoon cases that pass on their own — the
            // failures showed up in the raccoon class, which is where anyone
            // would have started looking.
            if (_networkManager != null)
            {
                var manager = _networkManager.GetComponent<NetworkManager>();
                if (manager != null)
                {
                    manager.Shutdown();
                }

                Object.DestroyImmediate(_networkManager);
            }

            _networkManager = null;
            _lobby = null;
            _presenter = null;
            _microphone = null;
            _session = null;
        }

        /// <summary>
        /// Asserted on the first status rather than the whole list, because how
        /// the rest of it goes depends on the machine: with the project linked
        /// this really does open a Relay allocation, and without it the refusal
        /// arrives in the same frame. What is the same either way is that the
        /// press reached the session, once, and said so before waiting.
        /// </summary>
        [UnityTest]
        public IEnumerator HostButtonReachesTheSession()
        {
            // 방 만들기 is the one control here that genuinely leaves the
            // machine: it asks Relay for an allocation. On a batch run with no
            // network — or with the project not linked today — that comes back
            // as an error log a moment later, from an async continuation, and
            // NUnit fails the test on the unexpected message rather than on any
            // assertion. What is being checked is that the press reaches the
            // session and says so; whether the room is then actually created is
            // not this test's business and cannot be made to answer the same way
            // on every machine.
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            List<string> seen = Press("Host Button");
            Assert.That(
                seen,
                Is.Not.Empty,
                "Pressing 방 만들기 did not reach NetworkSessionController.");
            Assert.That(
                seen[0],
                Is.EqualTo(CreatingRoom),
                "방 만들기 did not start by saying it was working.");
            Assert.That(
                seen.Count(status => status == CreatingRoom),
                Is.EqualTo(1),
                "방 만들기 is bound more than once.");
        }

        [UnityTest]
        public IEnumerator JoinButtonReachesTheSession()
        {
            Field("Invite Code Field").text = ShortCode;
            yield return null;

            Assert.That(
                Press("Join Button"),
                Is.EqualTo(new[] { ShortCodeRejection }),
                "Pressing 방 입장 did not reach NetworkSessionController "
                + "exactly once.");
            Assert.That(
                _session.Mode,
                Is.EqualTo(NetworkSessionController.SessionMode.Offline));
        }

        /// <summary>
        /// The code box takes what is typed and hands the same characters to
        /// the session. Relay issues upper case; a phone keyboard offers lower,
        /// and a code that is right but rejected for its case is the kind of
        /// failure a player has no way to see.
        /// </summary>
        [UnityTest]
        public IEnumerator TypedCodeIsFoldedToUpperCase()
        {
            Field("Invite Code Field").text = "abc";
            yield return null;
            yield return null;

            Assert.That(Field("Invite Code Field").text, Is.EqualTo("ABC"));
        }

        /// <summary>
        /// Disabling and re-enabling the lobby is what returning from a match
        /// does. Listeners must not accumulate across it: a doubly bound host
        /// button starts a session and then reports it as already running.
        /// </summary>
        [UnityTest]
        public IEnumerator ReEnablingTheLobbyDoesNotStackListeners()
        {
            _lobby.SetActive(false);
            yield return null;
            _lobby.SetActive(true);
            yield return null;

            Field("Invite Code Field").text = ShortCode;
            yield return null;

            Assert.That(
                Press("Join Button"),
                Is.EqualTo(new[] { ShortCodeRejection }));
        }

        /// <summary>
        /// The role a player is given changes that team's pose and nothing
        /// else's. Both teams keep their side, and the team that was not chosen
        /// keeps standing.
        /// </summary>
        [UnityTest]
        public IEnumerator RoleChangesOnlyTheChosenTeamsPose()
        {
            var view = _lobby.GetComponentInChildren<LobbyCharacterView>(true);
            Assert.That(view, Is.Not.Null, "The lobby has no character view.");
            yield return null;

            view.Apply(false, false);
            Sprite policeIdle = view.PoliceSprite;
            Sprite thiefIdle = view.ThiefSprite;
            Assert.That(
                view.State,
                Is.EqualTo(RoleSelectionState.None));
            Assert.That(policeIdle, Is.Not.Null);
            Assert.That(thiefIdle, Is.Not.Null);

            view.Apply(true, true);
            Assert.That(
                view.State,
                Is.EqualTo(RoleSelectionState.Police));
            Assert.That(
                view.PoliceSprite,
                Is.Not.EqualTo(policeIdle),
                "Choosing police left the police pair standing.");
            Assert.That(
                view.ThiefSprite,
                Is.EqualTo(thiefIdle),
                "Choosing police changed the thief pair as well.");

            view.Apply(true, false);
            Assert.That(
                view.State,
                Is.EqualTo(RoleSelectionState.Thief));
            Assert.That(
                view.PoliceSprite,
                Is.EqualTo(policeIdle),
                "Choosing thief changed the police pair as well.");
            Assert.That(
                view.ThiefSprite,
                Is.Not.EqualTo(thiefIdle),
                "Choosing thief left the thief pair standing.");

            view.Apply(false, false);
            Assert.That(view.PoliceSprite, Is.EqualTo(policeIdle));
            Assert.That(view.ThiefSprite, Is.EqualTo(thiefIdle));
        }

        /// <summary>
        /// Presses a button and returns everything the session reported while
        /// it was pressed.
        ///
        /// Counting rather than reading <c>LastStatus</c> afterwards: a stale
        /// status left by an earlier case would let a press that reached
        /// nothing at all pass. An empty list means nothing was reached; more
        /// than one entry means the handler is bound more than once.
        /// </summary>
        private List<string> Press(string buttonName)
        {
            var seen = new List<string>();

            void Record(string status)
            {
                seen.Add(status);
            }

            _session.StatusChanged += Record;
            try
            {
                Button(buttonName).onClick.Invoke();
            }
            finally
            {
                _session.StatusChanged -= Record;
            }

            return seen;
        }

        /// <summary>
        /// The gates the specification asks for: a client cannot start the
        /// match, nobody can swap before roles exist, and leaving is only
        /// offered once there is something to leave.
        /// </summary>
        [UnityTest]
        public IEnumerator OfflineLocksTheControlsThatNeedASession()
        {
            yield return null;

            Assert.That(Button("Host Button").interactable, Is.True);
            Assert.That(Button("Join Button").interactable, Is.True);
            Assert.That(
                Button("Start Match Button").interactable,
                Is.False,
                "게임 시작 is pressable with no session.");
            Assert.That(
                Button("Swap Role Button").interactable,
                Is.False,
                "역할 바꾸기 is pressable before roles are assigned.");
            Assert.That(
                Button("Leave Button").interactable,
                Is.False,
                "나가기 is pressable with nothing to leave.");
            Assert.That(
                Button("Copy Code Button").interactable,
                Is.False,
                "코드 복사 is pressable with no code to copy.");
        }

        /// <summary>
        /// A disabled button with no explanation is how a lobby strands someone.
        /// </summary>
        [UnityTest]
        public IEnumerator StatusSaysWhatToDoNext()
        {
            yield return null;

            Assert.That(
                Label("ConnectionStatusText").text,
                Is.EqualTo("방을 만들거나 받은 코드로 입장하세요."));
        }

        [UnityTest]
        public IEnumerator MicrophoneButtonReachesTheCheck()
        {
            string before = _microphone.StatusText;
            Button("Mic Test Button").onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(
                _microphone.StatusText,
                Is.Not.EqualTo(before),
                "Pressing 마이크 확인 did not reach the microphone check.");
        }

        /// <summary>
        /// Nothing on screen may be blank.
        ///
        /// Under Ellipsis, TMP draws nothing at all when its rect is shorter
        /// than one line, which is how the field captions disappeared while
        /// every size assertion still passed.
        /// </summary>
        [UnityTest]
        public IEnumerator NoVisibleLabelIsBlankOrClipped()
        {
            yield return null;
            yield return null;

            foreach (TMP_Text label in
                     _lobby.GetComponentsInChildren<TMP_Text>(false))
            {
                if (IsFilledAtRuntime(label))
                {
                    continue;
                }

                Assert.That(
                    label.text,
                    Is.Not.Empty,
                    $"'{label.name}' is visible but has no text.");

                label.ForceMeshUpdate();
                Assert.That(
                    label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                    $"'{label.name}' needs "
                    + $"{label.preferredHeight:0.#} but its rect is "
                    + $"{label.rectTransform.rect.height:0.#} tall, so it "
                    + "clips or blanks.");
            }
        }

        /// <summary>
        /// Labels a running lobby writes into. Empty is the correct state for
        /// them while nothing has happened yet.
        /// </summary>
        private static bool IsFilledAtRuntime(TMP_Text label)
        {
            return label.name == "Text"
                || label.name == "Placeholder"
                || label.transform.parent != null
                && label.transform.parent.name.StartsWith("Room Slot");
        }

        private Button Button(string name)
        {
            Button found = _lobby
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == name);
            Assert.That(found, Is.Not.Null, $"The lobby has no '{name}'.");
            return found;
        }

        private TMP_InputField Field(string name)
        {
            TMP_InputField found = _lobby
                .GetComponentsInChildren<TMP_InputField>(true)
                .FirstOrDefault(field => field.name == name);
            Assert.That(found, Is.Not.Null, $"The lobby has no '{name}'.");
            return found;
        }

        private TMP_Text Label(string name)
        {
            TMP_Text found = _lobby
                .GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.name == name);
            Assert.That(found, Is.Not.Null, $"The lobby has no '{name}'.");
            return found;
        }
    }
}
