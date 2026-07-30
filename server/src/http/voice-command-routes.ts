import type { FastifyInstance } from "fastify";
import { fileTypeFromBuffer } from "file-type";
import { parseBuffer } from "music-metadata";
import { env } from "../config/env.js";
import {
  SessionCapabilityService,
  type SessionParticipant
} from "../auth/session-capability-service.js";
import { VoiceCommandService } from "../voice/voice-command-service.js";

const allowedMimeTypes = new Set([
  "audio/webm",
  "audio/ogg",
  "audio/mp4",
  "audio/wav",
  "audio/x-wav"
]);

export async function registerVoiceRoutes(
  app: FastifyInstance,
  commands: VoiceCommandService,
  sessions: SessionCapabilityService
): Promise<void> {
  app.post("/api/game/sessions", async (request, reply) => {
    const registrationKey = String(
      request.headers["x-session-registration-key"] ?? ""
    );
    const body = request.body as {
      gameSessionId?: string;
      hostClientId?: string;
      participants?: SessionParticipant[];
    };
    if (!body?.gameSessionId || !body.hostClientId) {
      return reply.code(400).send({ errorCode: "INVALID_SESSION" });
    }
    try {
      const tokens = sessions.register(
        body.gameSessionId,
        body.hostClientId,
        body.participants ?? [],
        registrationKey
      );
      const policeParticipant = body.participants?.find(
        (participant) => participant.role === "POLICE"
      );
      const thiefParticipant = body.participants?.find(
        (participant) => participant.role === "THIEF"
      );
      return reply.code(201).send({
        gameSessionId: body.gameSessionId,
        tokens,
        hostToken: tokens[body.hostClientId],
        policeToken: policeParticipant ? tokens[policeParticipant.clientId] : "",
        thiefToken: thiefParticipant ? tokens[thiefParticipant.clientId] : ""
      });
    } catch {
      return reply.code(401).send({ errorCode: "INVALID_REGISTRATION_KEY" });
    }
  });

  app.post("/api/game/voice-commands", async (request, reply) => {
    const token = bearerToken(request.headers.authorization);
    const parts = request.parts({
      limits: { files: 1, fileSize: env.maxFileSizeBytes }
    });
    let audio: Buffer | undefined;
    let mimeType = "";
    let filename = "voice.webm";
    let sessionId = "";
    let petId = "";
    let clientCommandId = "";

    try {
      for await (const part of parts) {
        if (part.type === "file") {
          mimeType = part.mimetype;
          filename = part.filename || filename;
          audio = await part.toBuffer();
        } else if (part.fieldname === "gameSessionId") {
          sessionId = String(part.value);
        } else if (part.fieldname === "petId") {
          petId = String(part.value);
        } else if (part.fieldname === "clientCommandId") {
          clientCommandId = String(part.value);
        }
      }

      if (!audio || !sessionId || !petId || !clientCommandId) {
        return reply.code(400).send({ errorCode: "INVALID_REQUEST" });
      }
      const baseMimeType = mimeType.split(";", 1)[0].trim().toLowerCase();
      if (!allowedMimeTypes.has(baseMimeType)) {
        return reply.code(415).send({ errorCode: "UNSUPPORTED_MEDIA" });
      }
      const detected = await fileTypeFromBuffer(audio);
      const webmSignature =
        baseMimeType === "audio/webm" && detected?.mime === "video/webm";
      if (detected && !allowedMimeTypes.has(detected.mime) && !webmSignature) {
        return reply.code(415).send({ errorCode: "INVALID_AUDIO_SIGNATURE" });
      }
      const metadata = await parseBuffer(audio, { mimeType: baseMimeType });
      if (
        metadata.format.duration !== undefined &&
        metadata.format.duration > env.maxDurationSeconds + 0.1
      ) {
        return reply.code(400).send({ errorCode: "AUDIO_TOO_LONG" });
      }

      const result = commands.accept({
        token,
        sessionId,
        petId,
        clientCommandId,
        audio,
        mimeType: baseMimeType,
        filename
      });
      return reply.code(202).send({
        commandId: result.commandId,
        status: result.duplicate ? "DUPLICATE" : "PROCESSING"
      });
    } catch (error) {
      const code = error instanceof Error ? error.message : "VOICE_COMMAND_ERROR";
      const status = code.includes("CAPABILITY")
        ? 403
        : code === "FST_REQ_FILE_TOO_LARGE"
          ? 413
          : 400;
      return reply.code(status).send({ errorCode: code });
    }
  });

  app.get<{ Params: { commandId: string } }>(
    "/api/game/voice-commands/:commandId",
    async (request, reply) => {
      try {
        const token = bearerToken(request.headers.authorization);
        return reply.send(commands.get(request.params.commandId, token));
      } catch (error) {
        return reply.code(404).send({
          errorCode: error instanceof Error ? error.message : "COMMAND_NOT_FOUND"
        });
      }
    }
  );

  app.post<{ Params: { commandId: string } }>(
    "/api/game/voice-commands/:commandId/cancel",
    async (request, reply) => {
      try {
        commands.cancel(
          request.params.commandId,
          bearerToken(request.headers.authorization)
        );
        return reply.code(204).send();
      } catch (error) {
        return reply.code(404).send({
          errorCode: error instanceof Error ? error.message : "COMMAND_NOT_FOUND"
        });
      }
    }
  );
}

function bearerToken(value: string | undefined): string {
  if (!value?.startsWith("Bearer ")) throw new Error("INVALID_CAPABILITY");
  return value.slice("Bearer ".length);
}
