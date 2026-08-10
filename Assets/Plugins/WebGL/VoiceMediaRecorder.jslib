// Browser half of the voice capture. Unity's `Microphone` class does not
// capture on WebGL, so the browser records and hands Unity the encoded bytes.
//
// Every exit from here reports something. A silent return leaves the C# state
// machine sitting in `Recording` with no way to find out why, and in a shipped
// build there is no console open to look at.
var PawliceAndPurrglarVoiceLibrary = {
  $PawliceAndPurrglarVoice: {
    // `SendMessage` is the only way back into Unity from here, and which scope
    // exposes it has moved between Unity versions. Resolve it once, and say so
    // if it is genuinely absent rather than throwing inside a promise where
    // nothing catches it.
    send: function (gameObject, callback, payload) {
      var target = null;
      if (typeof SendMessage === "function") {
        target = SendMessage;
      } else if (typeof Module !== "undefined"
        && typeof Module.SendMessage === "function") {
        target = Module.SendMessage;
      } else if (typeof unityInstance !== "undefined"
        && unityInstance
        && typeof unityInstance.SendMessage === "function") {
        target = function (o, m, v) { unityInstance.SendMessage(o, m, v); };
      }

      if (!target) {
        console.error("[PawliceAndPurrglar] SendMessage is unavailable; the voice "
          + "result cannot reach Unity.");
        return;
      }

      target(gameObject, callback, payload);
    },

    stopTracks: function (stream) {
      if (!stream) return;
      stream.getTracks().forEach(function (track) { track.stop(); });
    }
  },

  PawliceAndPurrglar_VoiceMediaRecorder_Start: function (
    gameObjectPtr,
    callbackPtr,
    maximumSeconds
  ) {
    var gameObject = UTF8ToString(gameObjectPtr);
    var callback = UTF8ToString(callbackPtr);
    var send = PawliceAndPurrglarVoice.send;

    if (window.PawliceAndPurrglarVoiceRecorder) {
      // A previous recording never finished. Reporting is better than starting
      // a second recorder on the same device.
      send(gameObject, callback,
        JSON.stringify({ error: "RECORDER_ALREADY_RUNNING" }));
      return;
    }

    // `navigator.mediaDevices` is undefined outside a secure context, so a
    // build served over plain http on a LAN address has no microphone at all
    // while the same build on localhost works. That difference is invisible in
    // the game, so name it instead of reporting "unsupported".
    if (!window.isSecureContext) {
      send(gameObject, callback,
        JSON.stringify({ error: "MIC_REQUIRES_HTTPS" }));
      return;
    }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      send(gameObject, callback,
        JSON.stringify({ error: "MICROPHONE_UNSUPPORTED" }));
      return;
    }

    if (!window.MediaRecorder) {
      send(gameObject, callback,
        JSON.stringify({ error: "MEDIA_RECORDER_UNSUPPORTED" }));
      return;
    }

    navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
      var mimeTypes = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg"];
      var mimeType = mimeTypes.find(function (candidate) {
        return MediaRecorder.isTypeSupported(candidate);
      });
      if (!mimeType) {
        PawliceAndPurrglarVoice.stopTracks(stream);
        send(gameObject, callback, JSON.stringify({ error: "MIME_UNSUPPORTED" }));
        return;
      }

      var recorder = new MediaRecorder(stream, { mimeType: mimeType });
      var chunks = [];
      var timer = window.setTimeout(function () {
        if (recorder.state !== "inactive") recorder.stop();
      }, Math.min(5000, Math.max(100, maximumSeconds * 1000)));
      window.PawliceAndPurrglarVoiceRecorder = {
        recorder: recorder,
        stream: stream,
        timer: timer,
        gameObject: gameObject,
        callback: callback,
        chunks: chunks,
        mimeType: mimeType
      };
      recorder.ondataavailable = function (event) {
        if (event.data && event.data.size > 0) chunks.push(event.data);
      };
      recorder.onerror = function () {
        window.clearTimeout(timer);
        PawliceAndPurrglarVoice.stopTracks(stream);
        window.PawliceAndPurrglarVoiceRecorder = null;
        send(gameObject, callback, JSON.stringify({ error: "RECORDER_ERROR" }));
      };
      recorder.onstop = function () {
        window.clearTimeout(timer);
        PawliceAndPurrglarVoice.stopTracks(stream);
        window.PawliceAndPurrglarVoiceRecorder = null;
        var blob = new Blob(chunks, { type: mimeType });
        if (blob.size === 0) {
          send(gameObject, callback,
            JSON.stringify({ error: "VOICE_AUDIO_EMPTY" }));
          return;
        }

        var reader = new FileReader();
        reader.onerror = function () {
          send(gameObject, callback,
            JSON.stringify({ error: "VOICE_AUDIO_READ_FAILED" }));
        };
        reader.onloadend = function () {
          var bytes = new Uint8Array(reader.result);
          // Chunked so a five-second recording does not blow the argument
          // limit of `String.fromCharCode.apply`.
          var binary = "";
          var step = 8192;
          for (var i = 0; i < bytes.length; i += step) {
            binary += String.fromCharCode.apply(
              null,
              bytes.subarray(i, Math.min(i + step, bytes.length)));
          }

          send(gameObject, callback, JSON.stringify({
            mimeType: mimeType,
            base64Audio: window.btoa(binary)
          }));
        };
        reader.readAsArrayBuffer(blob);
      };
      recorder.start();
    }).catch(function (error) {
      window.PawliceAndPurrglarVoiceRecorder = null;
      send(
        gameObject,
        callback,
        JSON.stringify({ error: error && error.name
          ? error.name
          : "MIC_PERMISSION_DENIED" }));
    });
  },

  PawliceAndPurrglar_VoiceMediaRecorder_Stop: function () {
    var active = window.PawliceAndPurrglarVoiceRecorder;
    if (active && active.recorder && active.recorder.state !== "inactive") {
      active.recorder.stop();
    }
  }
};

autoAddDeps(PawliceAndPurrglarVoiceLibrary, "$PawliceAndPurrglarVoice");
mergeInto(LibraryManager.library, PawliceAndPurrglarVoiceLibrary);
