import OpenAI, { toFile } from "openai";
import { env } from "../config/env.js";

export interface TranscriptionResult {
  text: string;
  durationMs?: number;
  model: string;
  fallbackUsed: boolean;
}

export interface SpeechToTextClient {
  transcribe(
    audio: Buffer,
    mimeType: string,
    filename: string,
    signal: AbortSignal,
    /**
     * A sample of the expected transcript, biasing the model toward game
     * vocabulary. Built by `ExactCommandMatcher.transcriptionPromptFor` — it must
     * read like a sentence, not a word list (a list made accuracy *worse*).
     */
    prompt?: string
  ): Promise<TranscriptionResult>;
}

export class OpenAiSpeechToTextClient implements SpeechToTextClient {
  private readonly client?: OpenAI;

  constructor() {
    if (env.openAiApiKey) {
      this.client = new OpenAI({
        apiKey: env.openAiApiKey,
        baseURL: env.openAiBaseUrl
      });
    }
  }

  async transcribe(
    audio: Buffer,
    mimeType: string,
    filename: string,
    signal: AbortSignal,
    prompt?: string
  ): Promise<TranscriptionResult> {
    if (!this.client) {
      return { text: "", model: env.transcribeModel, fallbackUsed: true };
    }

    const response = await this.client.audio.transcriptions.create(
      {
        file: await toFile(audio, filename, { type: mimeType }),
        model: env.transcribeModel,
        response_format: "json",
        // Korean, because the utterances are — leaving it to detection on a
        // half-second command is a coin toss the rest of the pipeline pays for.
        language: "ko",
        ...(prompt ? { prompt } : {})
      },
      { signal }
    );
    return {
      text: response.text?.trim() ?? "",
      model: env.transcribeModel,
      fallbackUsed: false
    };
  }
}
