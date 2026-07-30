import OpenAI from "openai";
import { env } from "../config/env.js";
import {
  intentClassificationResultSchema,
  type IntentClassificationResult,
  type VoiceWorldContext
} from "../domain/contracts.js";

const outputSchema = {
  type: "object",
  additionalProperties: false,
  required: ["normalizedText", "candidates", "ambiguity", "keywords"],
  properties: {
    normalizedText: { type: "string" },
    candidates: {
      type: "array",
      maxItems: 3,
      items: {
        type: "object",
        additionalProperties: false,
        required: ["intent", "targetType", "targetId", "confidence"],
        properties: {
          intent: { type: "string" },
          targetType: { type: ["string", "null"] },
          targetId: { type: ["string", "null"] },
          confidence: { type: "number", minimum: 0, maximum: 1 }
        }
      }
    },
    ambiguity: { type: "string", enum: ["LOW", "MEDIUM", "HIGH"] },
    keywords: { type: "array", items: { type: "string" }, maxItems: 16 }
  }
} as const;

export interface IntentClassifierClient {
  classify(
    transcript: string,
    context: VoiceWorldContext,
    signal: AbortSignal
  ): Promise<IntentClassificationResult>;
}

export class OpenAiIntentClassifierClient implements IntentClassifierClient {
  private readonly client?: OpenAI;

  constructor() {
    if (env.openAiApiKey && env.intentModel) {
      this.client = new OpenAI({
        apiKey: env.openAiApiKey,
        baseURL: env.openAiBaseUrl
      });
    }
  }

  async classify(
    transcript: string,
    context: VoiceWorldContext,
    signal: AbortSignal
  ): Promise<IntentClassificationResult> {
    if (!this.client) return fallback(transcript);

    const response = await this.client.chat.completions.create(
      {
        model: env.intentModel,
        temperature: 0,
        messages: [
          {
            role: "system",
            content:
              "Classify the transcript into at most three allowed game intents. " +
              "Return only the requested JSON. Never invent target ids."
          },
          {
            role: "user",
            content: JSON.stringify({
              transcript,
              petType: context.petType,
              availableIntents: context.allowedIntents,
              visibleTargets: context.visibleTargets
            })
          }
        ],
        response_format: {
          type: "json_schema",
          json_schema: {
            name: "intent_classification",
            strict: true,
            schema: outputSchema
          }
        }
      },
      { signal }
    );

    const raw = response.choices[0]?.message?.content;
    if (!raw) return fallback(transcript);
    const parsed = intentClassificationResultSchema.parse(JSON.parse(raw));
    return { ...parsed, exactMatched: false, fallbackUsed: false };
  }
}

function fallback(transcript: string): IntentClassificationResult {
  return {
    normalizedText: transcript.trim(),
    candidates: [
      {
        intent: "UNKNOWN",
        targetType: null,
        targetId: null,
        confidence: 1
      }
    ],
    ambiguity: "HIGH",
    keywords: [],
    exactMatched: false,
    fallbackUsed: true
  };
}
