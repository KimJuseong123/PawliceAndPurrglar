import http from "node:http";

const DEFAULT_PORT = 8787;
const MAX_BODY_BYTES = 1024 * 1024;
const OPENAI_BASE_URL = "https://api.openai.com/v1";

export const CommandId = Object.freeze({
  TRACK: "TRACK",
  DISTRACT: "DISTRACT",
  NONE: "NONE",
});

export const ReasonCode = Object.freeze({
  MATCHED: "MATCHED",
  AMBIGUOUS: "AMBIGUOUS",
  OUT_OF_SCOPE: "OUT_OF_SCOPE",
  NO_SPEECH: "NO_SPEECH",
  ERROR: "ERROR",
});

export function validateVoiceRequest(value) {
  if (!value || typeof value !== "object") {
    return "Request body must be a JSON object.";
  }
  if (typeof value.requestId !== "string" || value.requestId.length < 1 || value.requestId.length > 128) {
    return "requestId must be a non-empty string up to 128 characters.";
  }
  if (value.role !== "Police" && value.role !== "Thief") {
    return "role must be Police or Thief.";
  }
  if (value.locale !== "ko-KR") {
    return "locale must be ko-KR.";
  }
  if (typeof value.audioWavBase64 !== "string" || value.audioWavBase64.length < 16) {
    return "audioWavBase64 must contain WAV audio.";
  }
  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(value.audioWavBase64)) {
    return "audioWavBase64 is not valid base64.";
  }
  return null;
}

export function extractResponseText(responseJson) {
  for (const item of responseJson?.output ?? []) {
    if (item?.type !== "message") continue;
    for (const content of item.content ?? []) {
      if (content?.type === "output_text" && typeof content.text === "string") {
        return content.text;
      }
    }
  }
  throw new Error("OpenAI Responses output did not contain output_text.");
}

export function validateClassifiedCommand(value, role) {
  const allowed = role === "Police" ? CommandId.TRACK : CommandId.DISTRACT;
  const commandId = Object.values(CommandId).includes(value?.commandId)
    ? value.commandId
    : CommandId.NONE;
  const reasonCode = Object.values(ReasonCode).includes(value?.reasonCode)
    ? value.reasonCode
    : ReasonCode.AMBIGUOUS;

  if (commandId !== CommandId.NONE && commandId !== allowed) {
    return {
      commandId: CommandId.NONE,
      reasonCode: ReasonCode.OUT_OF_SCOPE,
    };
  }
  return { commandId, reasonCode };
}

export async function transcribeAudio({
  apiKey,
  audioBuffer,
  model,
  fetchImpl = fetch,
  signal,
}) {
  const form = new FormData();
  form.append("file", new Blob([audioBuffer], { type: "audio/wav" }), "voice-command.wav");
  form.append("model", model);
  form.append("language", "ko");
  form.append(
    "prompt",
    "멍경찰과 냥도둑 게임의 짧은 동물 명령입니다. 경찰은 추적, 도둑은 교란 명령을 말합니다.",
  );

  const response = await fetchImpl(`${OPENAI_BASE_URL}/audio/transcriptions`, {
    method: "POST",
    headers: { Authorization: `Bearer ${apiKey}` },
    body: form,
    signal,
  });
  if (!response.ok) {
    throw new Error(`OpenAI transcription failed with HTTP ${response.status}.`);
  }
  const json = await response.json();
  return typeof json?.text === "string" ? json.text.trim() : "";
}

export async function classifyTranscript({
  apiKey,
  transcript,
  role,
  model,
  fetchImpl = fetch,
  signal,
}) {
  const allowed = role === "Police" ? "TRACK" : "DISTRACT";
  const systemPrompt = [
    "Classify a Korean voice command for a deterministic game companion.",
    `The player's role is ${role}. The only executable command is ${allowed}.`,
    "Return the executable command only when the utterance clearly asks for it.",
    "Return NONE for ambiguity, conversation, silence, the other role's command, or any unsupported action.",
    "Do not invent targets, positions, actions, or explanations.",
  ].join(" ");

  const response = await fetchImpl(`${OPENAI_BASE_URL}/responses`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      input: [
        { role: "system", content: systemPrompt },
        { role: "user", content: transcript },
      ],
      reasoning: { effort: "none" },
      max_output_tokens: 64,
      text: {
        format: {
          type: "json_schema",
          name: "companion_voice_command",
          strict: true,
          schema: {
            type: "object",
            additionalProperties: false,
            properties: {
              commandId: {
                type: "string",
                enum: ["TRACK", "DISTRACT", "NONE"],
              },
              reasonCode: {
                type: "string",
                enum: ["MATCHED", "AMBIGUOUS", "OUT_OF_SCOPE"],
              },
            },
            required: ["commandId", "reasonCode"],
          },
        },
      },
    }),
    signal,
  });
  if (!response.ok) {
    throw new Error(`OpenAI command classification failed with HTTP ${response.status}.`);
  }
  const responseJson = await response.json();
  const parsed = JSON.parse(extractResponseText(responseJson));
  return validateClassifiedCommand(parsed, role);
}

export function createVoiceGateway({
  apiKey = process.env.OPENAI_API_KEY,
  transcribeModel = process.env.OPENAI_TRANSCRIBE_MODEL ?? "gpt-4o-mini-transcribe",
  intentModel = process.env.OPENAI_INTENT_MODEL ?? "gpt-5.6-luna",
  requestTimeoutMs = Number(process.env.OPENAI_REQUEST_TIMEOUT_MS ?? 12000),
  fetchImpl = fetch,
  logger = console,
} = {}) {
  return http.createServer(async (request, response) => {
    const startedAt = Date.now();
    if (request.method === "GET" && request.url === "/health") {
      return sendJson(response, 200, {
        status: "ok",
        configured: Boolean(apiKey),
      });
    }
    if (request.method !== "POST" || request.url !== "/v1/voice-command") {
      return sendJson(response, 404, { error: "Not found." });
    }

    let requestBody;
    try {
      requestBody = await readJsonBody(request, MAX_BODY_BYTES);
    } catch (error) {
      return sendJson(response, error.statusCode ?? 400, { error: error.message });
    }

    const validationError = validateVoiceRequest(requestBody);
    if (validationError) {
      return sendJson(response, 400, {
        requestId: requestBody?.requestId ?? "",
        transcript: "",
        commandId: CommandId.NONE,
        reasonCode: ReasonCode.ERROR,
        error: validationError,
      });
    }
    if (!apiKey) {
      return sendJson(response, 503, {
        requestId: requestBody.requestId,
        transcript: "",
        commandId: CommandId.NONE,
        reasonCode: ReasonCode.ERROR,
        error: "Voice gateway is not configured.",
      });
    }

    const abortController = new AbortController();
    const timeout = setTimeout(() => abortController.abort(), requestTimeoutMs);
    try {
      const audioBuffer = Buffer.from(requestBody.audioWavBase64, "base64");
      const transcript = await transcribeAudio({
        apiKey,
        audioBuffer,
        model: transcribeModel,
        fetchImpl,
        signal: abortController.signal,
      });
      if (!transcript) {
        return sendJson(response, 200, {
          requestId: requestBody.requestId,
          transcript: "",
          commandId: CommandId.NONE,
          reasonCode: ReasonCode.NO_SPEECH,
        });
      }

      const classified = await classifyTranscript({
        apiKey,
        transcript,
        role: requestBody.role,
        model: intentModel,
        fetchImpl,
        signal: abortController.signal,
      });
      logger.info?.(
        `[VoiceGateway] request=${requestBody.requestId} role=${requestBody.role} command=${classified.commandId} elapsedMs=${Date.now() - startedAt}`,
      );
      return sendJson(response, 200, {
        requestId: requestBody.requestId,
        transcript,
        ...classified,
      });
    } catch (error) {
      const message = error?.name === "AbortError"
        ? "Voice service timed out."
        : "Voice service request failed.";
      logger.error?.(
        `[VoiceGateway] request=${requestBody.requestId} role=${requestBody.role} error=${error?.name ?? "Error"} elapsedMs=${Date.now() - startedAt}`,
      );
      return sendJson(response, 502, {
        requestId: requestBody.requestId,
        transcript: "",
        commandId: CommandId.NONE,
        reasonCode: ReasonCode.ERROR,
        error: message,
      });
    } finally {
      clearTimeout(timeout);
    }
  });
}

async function readJsonBody(request, maxBytes) {
  const chunks = [];
  let total = 0;
  for await (const chunk of request) {
    total += chunk.length;
    if (total > maxBytes) {
      const error = new Error("Request body exceeds 1 MB.");
      error.statusCode = 413;
      throw error;
    }
    chunks.push(chunk);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    const error = new Error("Request body must be valid JSON.");
    error.statusCode = 400;
    throw error;
  }
}

function sendJson(response, statusCode, value) {
  const body = JSON.stringify(value);
  response.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store",
  });
  response.end(body);
}

export function startVoiceGateway() {
  const port = Number(process.env.VOICE_GATEWAY_PORT ?? DEFAULT_PORT);
  const server = createVoiceGateway();
  server.listen(port, "127.0.0.1", () => {
    console.log(`[VoiceGateway] listening on http://127.0.0.1:${port}`);
  });
  return server;
}
