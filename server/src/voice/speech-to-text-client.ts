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
    signal: AbortSignal
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
    signal: AbortSignal
  ): Promise<TranscriptionResult> {
    if (!this.client) {
      return { text: "", model: env.transcribeModel, fallbackUsed: true };
    }

    const response = await this.client.audio.transcriptions.create(
      {
        file: await toFile(audio, filename, { type: mimeType }),
        model: env.transcribeModel,
        response_format: "json"
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
