function numberEnv(name: string, fallback: number): number {
  const value = process.env[name];
  if (value === undefined || value === "") return fallback;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new Error(`${name} must be a number`);
  return parsed;
}

function booleanEnv(name: string, fallback: boolean): boolean {
  const value = process.env[name];
  if (value === undefined || value === "") return fallback;
  return value.toLowerCase() === "true";
}

export const env = {
  openAiApiKey: process.env.OPENAI_API_KEY ?? "",
  openAiBaseUrl: process.env.OPENAI_API_BASE_URL || undefined,
  transcribeModel:
    process.env.OPENAI_TRANSCRIBE_MODEL ?? "gpt-4o-mini-transcribe",
  intentModel: process.env.OPENAI_INTENT_MODEL ?? "",
  maxDurationSeconds: numberEnv("VOICE_COMMAND_MAX_DURATION_SECONDS", 5),
  maxFileSizeBytes:
    numberEnv("VOICE_COMMAND_MAX_FILE_SIZE_MB", 5) * 1024 * 1024,
  sttTimeoutMs: numberEnv("VOICE_STT_TIMEOUT_MS", 8000),
  intentTimeoutMs: numberEnv("VOICE_INTENT_TIMEOUT_MS", 5000),
  providerRetryCount: numberEnv("VOICE_PROVIDER_RETRY_COUNT", 1),
  rateLimitPerMinute: numberEnv("VOICE_RATE_LIMIT_PER_MINUTE", 20),
  debugSaveAudio: booleanEnv("VOICE_DEBUG_SAVE_AUDIO", false),
  allowedOrigins: (process.env.VOICE_ALLOWED_ORIGINS ?? "")
    .split(",")
    .map((value) => value.trim())
    .filter(Boolean),
  sessionRegistrationKey:
    process.env.VOICE_SESSION_REGISTRATION_KEY ?? "",
  capabilityTtlSeconds: numberEnv("VOICE_CAPABILITY_TTL_SECONDS", 3600),

  /**
   * Lets a request carry the transcript instead of audio.
   *
   * This is how the intent and obedience chain gets tuned before anyone has paid
   * for a speech key or plugged in a microphone: type `짖어`, watch the dog, and
   * turn the numbers in `PetCognitionConfig` until the misbehaviour rate feels
   * right.
   *
   * **Off unless explicitly enabled.** It is a way to put words in a player's
   * mouth, which is exactly what a submitted build must not have.
   */
  allowTranscriptOverride: booleanEnv("VOICE_ALLOW_TRANSCRIPT_OVERRIDE", false)
};

/**
 * True when no speech provider is configured.
 *
 * Derived rather than declared: a stub that has to be switched on by hand gets
 * left on, and a stub that stays on while a real key is present would silently
 * throw away the thing you are paying for.
 */
export const usingStubVoiceProviders = env.openAiApiKey.length === 0;

if (env.maxDurationSeconds <= 0 || env.maxDurationSeconds > 5) {
  throw new Error("VOICE_COMMAND_MAX_DURATION_SECONDS must be between 0 and 5");
}
