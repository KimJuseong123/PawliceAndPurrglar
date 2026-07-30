import { z } from "zod";

export const visibleTargetSchema = z.object({
  id: z.string().min(1).max(128),
  type: z.string().min(1).max(32)
});

export const voiceWorldContextSchema = z.object({
  allowedIntents: z.array(z.string().min(1).max(64)).max(32),
  visibleTargets: z.array(visibleTargetSchema).max(128),
  petType: z.enum(["DOG", "CAT"]),
  ownerRole: z.enum(["POLICE", "THIEF"]),
  commandSequence: z.number().int().nonnegative(),
  gameSessionSeed: z.string().min(1).max(128)
});

export const voiceIntentCandidateSchema = z.object({
  intent: z.string().min(1).max(64),
  targetType: z.string().max(32).nullable().optional(),
  targetId: z.string().max(128).nullable().optional(),
  confidence: z.number().min(0).max(1)
});

export const intentClassificationResultSchema = z.object({
  normalizedText: z.string().max(1000),
  candidates: z.array(voiceIntentCandidateSchema).max(3),
  ambiguity: z.enum(["LOW", "MEDIUM", "HIGH"]).default("HIGH"),
  keywords: z.array(z.string().max(64)).max(16).default([]),
  exactMatched: z.boolean().default(false),
  fallbackUsed: z.boolean().default(false)
});

export const petDecisionSchema = z.object({
  resultType: z.enum([
    "CORRECT",
    "CONFUSED",
    "MISUNDERSTOOD",
    "IGNORED",
    "DISTRACTED",
    "FAILED"
  ]),
  selectedIntent: z.string().max(64).nullable(),
  selectedTargetId: z.string().max(128).nullable(),
  reaction: z.string().max(64),
  reasonCode: z.string().max(64),
  commandSequence: z.number().int().nonnegative()
});

export type VisibleTarget = z.infer<typeof visibleTargetSchema>;
export type VoiceWorldContext = z.infer<typeof voiceWorldContextSchema>;
export type VoiceIntentCandidate = z.infer<typeof voiceIntentCandidateSchema>;
export type IntentClassificationResult = z.infer<
  typeof intentClassificationResultSchema
>;
export type PetDecision = z.infer<typeof petDecisionSchema>;

export interface VoiceCommandRecord {
  commandId: string;
  clientCommandId: string;
  gameSessionId: string;
  petId: string;
  token: string;
  status: "PROCESSING" | "COMPLETED" | "FAILED" | "CANCELLED";
  createdAt: number;
  worldContext?: VoiceWorldContext;
  transcript?: string;
  classification?: IntentClassificationResult;
  decision?: PetDecision;
  errorCode?: string;
}

export interface VoiceEvent {
  eventVersion: 1;
  type:
    | "VOICE_COMMAND_TRANSCRIBED"
    | "VOICE_INTENT_CANDIDATES_READY"
    | "VOICE_COMMAND_FAILED"
    | "VOICE_COMMAND_CANCELLED"
    | "PET_DECISION_RECORDED";
  gameSessionId: string;
  commandId: string;
  petId: string;
  serverTimestamp: number;
  payload: Record<string, unknown>;
}
