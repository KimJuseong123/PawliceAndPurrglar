mergeInto(LibraryManager.library, {
  PawsAndLoot_VoiceBackendSocket_Connect: function (gameObjectPtr, urlPtr, sessionPtr, tokenPtr) {
    var gameObject = UTF8ToString(gameObjectPtr);
    var url = UTF8ToString(urlPtr);
    var sessionId = UTF8ToString(sessionPtr);
    var token = UTF8ToString(tokenPtr);
    var socket = new WebSocket(url);
    window.PawsAndLootVoiceSocket = socket;
    socket.onopen = function () {
      socket.send(JSON.stringify({
        type: "REGISTER_HOST",
        gameSessionId: sessionId,
        token: token
      }));
    };
    socket.onmessage = function (event) {
      SendMessage(gameObject, "OnBackendEvent", event.data);
    };
  },

  PawsAndLoot_VoiceBackendSocket_SendContext: function (gameObjectPtr, commandPtr, contextPtr) {
    var socket = window.PawsAndLootVoiceSocket;
    if (!socket || socket.readyState !== WebSocket.OPEN) return;
    socket.send(JSON.stringify({
      type: "VOICE_CONTEXT",
      commandId: UTF8ToString(commandPtr),
      context: JSON.parse(UTF8ToString(contextPtr))
    }));
  }
});
