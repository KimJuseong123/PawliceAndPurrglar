import { ExactCommandMatcher, type PetType } from "./exact-command-matcher.js";
import type {
  IntentClassificationResult,
  VoiceWorldContext
} from "../domain/contracts.js";
import type { IntentClassifierClient } from "./intent-classifier-client.js";
import type {
  SpeechToTextClient,
  TranscriptionResult
} from "./speech-to-text-client.js";

/**
 * Stand-ins used when no speech provider is configured.
 *
 * The point is that the rest of the game stays playable and tunable while the
 * paid parts are absent. Everything after "here is a sentence" — matching,
 * intent, the obedience roll, the animal acting or not — is ours, and none of it
 * needs an API key to be exercised or balanced.
 *
 * They are deliberately obvious rather than clever. A stub that tried to look
 * like real transcription would eventually be mistaken for it.
 */

/**
 * Cycles through commands instead of listening.
 *
 * Deterministic on purpose: rotating in a fixed order means a playtest covers
 * every command in turn rather than landing on the same one, and a bug reproduces
 * on the same step twice. For a specific sentence, send a transcript override
 * instead — that is what it is for.
 */
export class StubSpeechToTextClient implements SpeechToTextClient {
  private static readonly rotation = [
    "따라와",
    "짖어",
    "냄새 추적해",
    "기다려",
    "숨어",
    "저 상자 뒤를 찾아봐",
    "멈춰"
  ];

  private index = 0;

  async transcribe(): Promise<TranscriptionResult> {
    const text =
      StubSpeechToTextClient.rotation[
        this.index % StubSpeechToTextClient.rotation.length
      ];
    this.index += 1;
    return { text, model: "stub", fallbackUsed: true };
  }
}

/**
 * Resolves with the deterministic matcher instead of a model.
 *
 * Weaker than the real thing — it cannot use the visible targets or reason about
 * an unusual phrasing — but it never answers "I did not understand", which is the
 * property the game actually depends on.
 */
export class StubIntentClassifierClient implements IntentClassifierClient {
  private readonly matcher = new ExactCommandMatcher();

  async classify(
    transcript: string,
    context: VoiceWorldContext
  ): Promise<IntentClassificationResult> {
    return resolveWithMatcher(this.matcher, transcript, context, true);
  }
}

/**
 * Last-resort resolution shared by the stub and the real pipeline.
 *
 * "무조건 이해는 해야 한다" lives here: when the model is absent, unsure, or
 * returns something the world does not allow, the closest command in this
 * animal's own vocabulary is used and the *confidence* carries the doubt
 * forward. `PetCognitionResolver` multiplies that confidence into the obedience
 * roll, so an uncertain match becomes a confused animal rather than a confident
 * wrong action — which is the behaviour we want anyway.
 *
 * Answering UNKNOWN would instead produce silence, and silence is
 * indistinguishable from a broken microphone.
 */
export function resolveWithMatcher(
  matcher: ExactCommandMatcher,
  transcript: string,
  context: VoiceWorldContext,
  fallbackUsed: boolean
): IntentClassificationResult {
  const petType: PetType = context.petType;
  const suggestion = matcher.suggest(
    transcript,
    petType,
    context.allowedIntents
  );

  if (!suggestion) {
    return {
      normalizedText: matcher.normalize(transcript),
      candidates: [
        { intent: "UNKNOWN", targetType: null, targetId: null, confidence: 1 }
      ],
      ambiguity: "HIGH",
      keywords: [],
      exactMatched: false,
      fallbackUsed
    };
  }

  return {
    normalizedText: suggestion.normalizedText,
    candidates: [
      {
        intent: suggestion.intent,
        targetType: null,
        targetId: null,
        confidence: suggestion.confidence
      }
    ],
    ambiguity: suggestion.approximate ? "MEDIUM" : "LOW",
    keywords: [suggestion.matchedStem],
    exactMatched: suggestion.tier === "absolute" && !suggestion.approximate,
    fallbackUsed
  };
}
