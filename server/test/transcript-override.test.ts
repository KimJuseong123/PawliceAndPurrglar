import { describe, expect, it } from "vitest";
import { SessionCapabilityService } from "../src/auth/session-capability-service.js";
import { VoiceEventChannel } from "../src/ws/voice-event-channel.js";
import { VoiceCommandService } from "../src/voice/voice-command-service.js";
import {
  StubIntentClassifierClient,
  StubSpeechToTextClient
} from "../src/voice/stub-voice-providers.js";

/**
 * The chain from a sentence to a command, with no key and no microphone.
 *
 * This is the whole point of the stub: everything after "here is a sentence" is
 * ours, and it has to be exercisable and tunable before anyone has paid for
 * speech.
 */
function buildService() {
  const sessions = new SessionCapabilityService();
  const events = new VoiceEventChannel(sessions);
  const commands = new VoiceCommandService(
    sessions,
    events,
    new StubSpeechToTextClient(),
    new StubIntentClassifierClient()
  );
  const tokens = sessions.register(
    "session-1",
    "host-1",
    [
      { clientId: "host-1", petId: "dog", role: "POLICE" },
      { clientId: "peer-1", petId: "cat", role: "THIEF" }
    ],
    ""
  );
  // A capability is bound to one pet, so the cat's sentences need the cat
  // owner's token — the police token is refused for `petId: "cat"`.
  return { commands, token: tokens["host-1"], catToken: tokens["peer-1"] };
}

async function run(transcript: string, petId: string, token: string, commands: VoiceCommandService) {
  const result = commands.accept({
    token,
    sessionId: "session-1",
    petId,
    clientCommandId: `c-${Math.random()}`,
    audio: Buffer.alloc(0),
    mimeType: "text/plain",
    filename: "override.txt",
    transcriptOverride: transcript
  });
  await result.completion;
  return commands.get(result.commandId, token);
}

describe("transcript override with stub providers", () => {
  it("turns a typed sentence into a command with no audio", async () => {
    const { commands, token } = buildService();
    const record = await run("짖어", "dog", token, commands);

    expect(record.transcript).toBe("짖어");
    expect(record.classification?.candidates[0]?.intent).toBe("BARK");
  });

  it("recovers a command the transcriber would have got wrong", async () => {
    const { commands, token } = buildService();
    const record = await run("지지라고", "dog", token, commands);

    expect(record.classification?.candidates[0]?.intent).toBe("BARK");
    // The doubt travels as confidence, not as a refusal — PetCognitionResolver
    // multiplies it into the obedience roll.
    expect(record.classification?.candidates[0]?.confidence).toBeLessThan(0.8);
  });

  it("never answers UNKNOWN for speech it can place", async () => {
    const { commands, token } = buildService();
    const record = await run("저 상자 뒤를 찾아봐", "dog", token, commands);

    expect(record.classification?.candidates[0]?.intent).not.toBe("UNKNOWN");
  });

  it("completes even when the sentence is not a command", async () => {
    const { commands, token } = buildService();
    const record = await run("오늘 날씨가 좋네", "dog", token, commands);

    // Silence would be indistinguishable from a broken microphone, so the
    // pipeline still finishes and says so.
    expect(record.transcript).toBe("오늘 날씨가 좋네");
    expect(record.classification).toBeDefined();
  });

  /**
   * No context is registered here — exactly the state a Windows build or the
   * editor is always in, because `RegisterVoiceContext` has an empty body off
   * WebGL. The fallback used to assume DOG, so a cat player's sentence was
   * looked up in the dog's vocabulary and came back as no command at all.
   *
   * "숨어" is the sharpest probe: it is a cat stem and there is nothing like it
   * on the dog's list, so a DOG lookup cannot accidentally pass.
   */
  it("uses the cat's vocabulary for the cat with no registered context", async () => {
    const { commands, catToken } = buildService();
    const record = await run("숨어", "cat", catToken, commands);

    expect(record.classification?.candidates[0]?.intent).toBe("HIDE");
  });

  it("still uses the dog's vocabulary for the dog", async () => {
    const { commands, token } = buildService();
    const record = await run("냄새 맡아", "dog", token, commands);

    expect(record.classification?.candidates[0]?.intent).toBe("CHASE_TARGET");
  });
});
