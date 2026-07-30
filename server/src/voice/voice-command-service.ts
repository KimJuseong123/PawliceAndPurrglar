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

  accept(input: AudioSubmission): { commandId: string; duplicate: boolean } {
    this.sessions.authorize(input.token, input.sessionId, input.petId);
    const duplicateKey = `${input.sessionId}:${input.clientCommandId}`;
    const existing = this.byClientId.get(duplicateKey);
    if (existing) return { commandId: existing, duplicate: true };

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
    void this.process(record, input);
    return { commandId, duplicate: false };
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
      const transcription = await this.withRetry(
        () =>
          this.stt.transcribe(
            input.audio,
            input.mimeType,
            input.filename,
            controller.signal
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

      const context = this.events.takeContext(record.clientCommandId) ?? {
        allowedIntents: [],
        visibleTargets: [],
        petType: "DOG" as const,
        ownerRole: "POLICE" as const,
        commandSequence: 0,
        gameSessionSeed: "unregistered"
      };
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

      const safeClassification = this.validateCandidates(classification, context);
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

  private validateCandidates(
    classification: IntentClassificationResult,
    context: VoiceWorldContext
  ): IntentClassificationResult {
    const allowed = new Set(context.allowedIntents);
    const targets = new Set(context.visibleTargets.map((target) => target.id));
    return {
      ...classification,
      candidates: classification.candidates
        .filter(
          (candidate) =>
            allowed.has(candidate.intent) || candidate.intent === "UNKNOWN"
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
