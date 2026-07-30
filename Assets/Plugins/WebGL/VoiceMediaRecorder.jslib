mergeInto(LibraryManager.library, {
  PawsAndLoot_VoiceMediaRecorder_Start: function (gameObjectPtr, callbackPtr, maximumSeconds) {
    var gameObject = UTF8ToString(gameObjectPtr);
    var callback = UTF8ToString(callbackPtr);
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      SendMessage(gameObject, callback, JSON.stringify({ error: "MICROPHONE_UNSUPPORTED" }));
      return;
    }

    navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
      var mimeTypes = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg"];
      var mimeType = mimeTypes.find(function (candidate) {
        return window.MediaRecorder && MediaRecorder.isTypeSupported(candidate);
      });
      if (!mimeType) {
        stream.getTracks().forEach(function (track) { track.stop(); });
        SendMessage(gameObject, callback, JSON.stringify({ error: "MIME_UNSUPPORTED" }));
        return;
      }

      var recorder = new MediaRecorder(stream, { mimeType: mimeType });
      var chunks = [];
      var timer = window.setTimeout(function () {
        if (recorder.state !== "inactive") recorder.stop();
      }, Math.min(5000, Math.max(100, maximumSeconds * 1000)));
      window.PawsAndLootVoiceRecorder = {
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
        stream.getTracks().forEach(function (track) { track.stop(); });
        SendMessage(gameObject, callback, JSON.stringify({ error: "RECORDER_ERROR" }));
      };
      recorder.onstop = function () {
        window.clearTimeout(timer);
        stream.getTracks().forEach(function (track) { track.stop(); });
        var blob = new Blob(chunks, { type: mimeType });
        var reader = new FileReader();
        reader.onloadend = function () {
          var bytes = new Uint8Array(reader.result);
          var binary = "";
          for (var i = 0; i < bytes.length; i += 1) {
            binary += String.fromCharCode(bytes[i]);
          }
          SendMessage(gameObject, callback, JSON.stringify({
            mimeType: mimeType,
            base64Audio: window.btoa(binary)
          }));
        };
        reader.readAsArrayBuffer(blob);
        window.PawsAndLootVoiceRecorder = null;
      };
      recorder.start();
    }).catch(function (error) {
      SendMessage(
        gameObject,
        callback,
        JSON.stringify({ error: error.name || "MIC_PERMISSION_DENIED" }));
    });
  },

  PawsAndLoot_VoiceMediaRecorder_Stop: function () {
    var active = window.PawsAndLootVoiceRecorder;
    if (active && active.recorder && active.recorder.state !== "inactive") {
      active.recorder.stop();
    }
  }
});
