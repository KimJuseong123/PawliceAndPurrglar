import { randomUUID } from "node:crypto";
import { env } from "../config/env.js";
import type {
  IntentClassificationResult,
  VoiceCommandRecord,
  VoiceEvent,
  VoiceWorldContext
} from "../domain/contracts.js";
import { SessionCapabilityService } from "../auth/session-capability-service.js";
import { ExactCommandMatcher } from "./exact-command-matcher.js";
import { resolveWithMatcher } from "./stub-voice-providers.js";
import type { IntentClassifierClient } from "./intent-classifier-client.js";
import type { SpeechToTextClient } from "./speech-to-text-client.js";
import { VoiceEventChannel } from "../ws/voice-event-channel.js";

export interface AudioSubmission {
  token: string;
  sessionId: string;
  petId: string;
  clientCommandId: string;
  audio: Buffer;
  mimeType: string;
  filename: string;
  /**
   * Skips transcription and uses this sentence. Development only — the route
   * refuses it unless `VOICE_ALLOW_TRANSCRIPT_OVERRIDE` is on.
   */
  transcriptOverride?: string;
}

export class VoiceCommandService {
  private readonly records = new Map<string, VoiceCommandRecord>();
  private readonly byClientId = new Map<string, string>();

  constructor(
    private readonly sessions: SessionCapabilityService,
    private readonly events: VoiceEventChannel,
    private readonly stt: SpeechToTextClient,
    private readonly classifier: IntentClassifierClient,
    private readonly matcher = new ExactCommandMatcher()
  ) {}

  /**
   * `completion` resolves when processing finishes, so a caller can wait for the
   * answer in the HTTP response instead of over the socket.
   *
   * The socket is a WebGL-only client (`VoiceBackendSocket.jslib`), so without a
   * synchronous option a Windows build cannot use this backend at all — and then
   * the two platforms are back on different AI backends, which is the thing
   * `VOICE-012` set out to end.
   */
  accept(input: AudioSubmission): {
    commandId: string;
    duplicate: boolean;
    completion: Promise<void>;
  } {
    this.sessions.authorize(input.token, input.sessionId, input.petId);
    const duplicateKey = `${input.sessionId}:${input.clientCommandId}`;
    const existing = this.byClientId.get(duplicateKey);
    if (existing) {
      return {
        commandId: existing,
        duplicate: true,
        completion: Promise.resolve()
      };
    }

    const commandId = randomUUID();
    const record: VoiceCommandRecord = {
      commandId,
      clientCommandId: input.clientCommandId,
      gameSessionId: input.sessionId,
      petId: input.petId,
      token: input.token,
      status: "PROCESSING",
      createdAt: Date.now()
    };
    this.records.set(commandId, record);
    this.byClientId.set(duplicateKey, commandId);
    const completion = this.process(record, input);
    void completion;
    return { commandId, duplicate: false, completion };
  }

  get(commandId: string, token: string): VoiceCommandRecord {
    const record = this.records.get(commandId);
    if (!record) throw new Error("COMMAND_NOT_FOUND");
    this.sessions.authorize(token, record.gameSessionId, record.petId);
    return { ...record, token: "" };
  }

  cancel(commandId: string, token: string): void {
    const record = this.records.get(commandId);
    if (!record) throw new Error("COMMAND_NOT_FOUND");
    this.sessions.authorize(token, record.gameSessionId, record.petId);
    if (record.status !== "PROCESSING") return;
    record.status = "CANCELLED";
    this.send(record, "VOICE_COMMAND_CANCELLED", {});
  }

  private async process(
    record: VoiceCommandRecord,
    input: AudioSubmission
  ): Promise<void> {
    const controller = new AbortController();
    const timeout = setTimeout(
      () => controller.abort(),
      env.sttTimeoutMs + env.intentTimeoutMs + 1000
    );
    try {
      // Taken before transcription, not after. The animal decides which words to
      // bias the speech model toward, and a lexicon fetched afterwards is a
      // lexicon that arrived too late to be worth having.
      const context = this.events.takeContext(input.clientCommandId) ?? {
        allowedIntents: [],
        visibleTargets: [],
        petType: "DOG" as const,
        ownerRole: "POLICE" as const,
        commandSequence: 0,
        gameSessionSeed: "unregistered"
      };

      const transcription = input.transcriptOverride
        ? {
            text: input.transcriptOverride,
            model: "transcript-override",
            fallbackUsed: true
          }
        : await this.withRetry(
            () =>
              this.stt.transcribe(
                input.audio,
                input.mimeType,
                input.filename,
                controller.signal,
                this.matcher.transcriptionPromptFor(context.petType)
              ),
            controller.signal
          );
      if (record.status === "CANCELLED") return;
      if (!transcription.text) throw new Error("EMPTY_TRANSCRIPT");

      record.transcript = transcription.text;
      this.send(record, "VOICE_COMMAND_TRANSCRIBED", {
        transcript: transcription.text,
        model: transcription.model
      });

      const exact = this.matcher.match(transcription.text);
      const classification: IntentClassificationResult = exact
        ? {
            normalizedText: exact.normalizedText,
            candidates: [
              {
                intent: exact.intent,
                targetType: null,
                targetId: null,
                confidence: exact.confidence
              }
            ],
            ambiguity: "LOW",
            keywords: [],
            exactMatched: true,
            fallbackUsed: false
          }
        : await this.withRetry(
            () =>
              this.classifier.classify(
                transcription.text,
                context,
                controller.signal
              ),
            controller.signal
          );

      // "무조건 이해는 해야 한다". Clamping to the allowed list can empty the
      // candidates, and the model's own fallback answers UNKNOWN — either way the
      // game receives nothing and the player sees silence, which is
      // indistinguishable from a broken microphone. The deterministic matcher
      // gets the last word, and carries its doubt as low confidence rather than
      // as a refusal.
      let safeClassification = this.validateCandidates(classification, context);
      if (this.needsResolution(safeClassification)) {
        safeClassification = this.validateCandidates(
          resolveWithMatcher(this.matcher, transcription.text, context, true),
          context
        );
      }

      record.classification = safeClassification;
      this.send(record, "VOICE_INTENT_CANDIDATES_READY", {
        classification: safeClassification,
        commandSequence: context.commandSequence
      });
    } catch (error) {
      if (record.status === "CANCELLED") return;
      record.status = "FAILED";
      record.errorCode = error instanceof Error ? error.message : "PROVIDER_ERROR";
      this.send(record, "VOICE_COMMAND_FAILED", { errorCode: record.errorCode });
    } finally {
      clearTimeout(timeout);
    }
  }

  /** No usable candidate survived, so the answer would be silence. */
  private needsResolution(
    classification: IntentClassificationResult
  ): boolean {
    return (
      classification.candidates.length === 0 ||
      classification.candidates.every(
        (candidate) => candidate.intent === "UNKNOWN"
      )
    );
  }

  private validateCandidates(
    classification: IntentClassificationResult,
    context: VoiceWorldContext
  ): IntentClassificationResult {
    // An empty list means "unspecified", not "forbid everything".
    //
    // The distinction is load-bearing: the world context arrives over the
    // socket, and the socket is a WebGL-only client — so on the synchronous path
    // a Windows build takes, there is no context and the list is empty. Treating
    // that as a deny-all silently discarded *every* command, with the game
    // seeing nothing and no error anywhere. Caught by the stub pipeline test
    // before it ever ran against a real transcript.
    const allowed = context.allowedIntents.length > 0
      ? new Set(context.allowedIntents)
      : null;
    const targets = new Set(context.visibleTargets.map((target) => target.id));
    return {
      ...classification,
      candidates: classification.candidates
        .filter(
          (candidate) =>
            allowed === null ||
            allowed.has(candidate.intent) ||
            candidate.intent === "UNKNOWN"
        )
        .map((candidate) => ({
          ...candidate,
          targetId:
            candidate.targetId && targets.has(candidate.targetId)
              ? candidate.targetId
              : null
        }))
        .slice(0, 3)
    };
  }

  private send(
    record: VoiceCommandRecord,
    type: VoiceEvent["type"],
    payload: Record<string, unknown>
  ): void {
    const event: VoiceEvent = {
      eventVersion: 1,
      type,
      gameSessionId: record.gameSessionId,
      commandId: record.commandId,
      petId: record.petId,
      serverTimestamp: Date.now(),
      payload
    };
    this.events.send(record.gameSessionId, event);
  }

  private async withRetry<T>(
    operation: () => Promise<T>,
    signal: AbortSignal
  ): Promise<T> {
    let lastError: unknown;
    for (let attempt = 0; attempt <= env.providerRetryCount; attempt += 1) {
      try {
        return await operation();
      } catch (error) {
        lastError = error;
        if (signal.aborted || attempt === env.providerRetryCount) break;
        await new Promise((resolve) => setTimeout(resolve, 100 * (attempt + 1)));
      }
    }
    throw lastError instanceof Error ? lastError : new Error("PROVIDER_ERROR");
  }
}
