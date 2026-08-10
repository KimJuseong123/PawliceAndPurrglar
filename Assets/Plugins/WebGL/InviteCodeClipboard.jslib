// Browser half of "코드 복사".
//
// Unity's `GUIUtility.systemCopyBuffer` is a no-op on WebGL — it writes to a
// buffer inside the player that the page's clipboard never sees — so the copy
// has to happen in JavaScript, inside the click that asked for it. Both paths
// below depend on being called from a user gesture; called from a timer or a
// coroutine tick they are refused by the browser without an error anybody sees.
var PawliceAndPurrglarClipboardLibrary = {
  // Returns 1 when the copy was handed to the browser, 0 when neither path was
  // available. The async clipboard API resolves after this returns, so a 1 is a
  // claim that the request was accepted, not that the write finished — which is
  // as much as can be known synchronously, and is why the lobby says "복사했습니다"
  // rather than proving it.
  PawliceAndPurrglarCopyText: function (textPointer) {
    var text = UTF8ToString(textPointer);
    if (!text) {
      return 0;
    }

    // The modern path. Only exists in a secure context, which is one more
    // reason this game is served over https.
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text);
        return 1;
      }
    } catch (error) {
      console.warn("[PawliceAndPurrglar] navigator.clipboard failed: " + error);
    }

    // The old path, for browsers without the clipboard API. The textarea has to
    // be in the document and focusable for execCommand to see a selection, so
    // it is placed off-screen rather than hidden — `display: none` cannot be
    // selected and the copy silently produces nothing.
    try {
      var area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.top = "-1000px";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.focus();
      area.select();
      var copied = document.execCommand("copy");
      document.body.removeChild(area);
      return copied ? 1 : 0;
    } catch (error) {
      console.warn("[PawliceAndPurrglar] execCommand copy failed: " + error);
      return 0;
    }
  }
};

mergeInto(LibraryManager.library, PawliceAndPurrglarClipboardLibrary);
