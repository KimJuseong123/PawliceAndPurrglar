import type { FastifyInstance, FastifyReply } from "fastify";
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
    let transcript = "";

    // Wait for the answer in this response instead of over the socket. The
    // socket client is WebGL-only, so without this a Windows build cannot use
    // this backend and the two platforms drift onto different AI stacks.
    const wait = "wait" in (request.query as Record<string, unknown>);

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
        } else if (part.fieldname === "transcript") {
          transcript = String(part.value);
        }
      }

      // Refused rather than ignored. Silently dropping it would look like the
      // override was applied and the model disagreed.
      if (transcript && !env.allowTranscriptOverride) {
        return reply
          .code(403)
          .send({ errorCode: "TRANSCRIPT_OVERRIDE_DISABLED" });
      }

      if (!sessionId || !petId || !clientCommandId) {
        return reply.code(400).send({ errorCode: "INVALID_REQUEST" });
      }

      // With a transcript there is nothing to transcribe, so audio is optional —
      // that is what lets the intent and obedience chain be tuned with no
      // microphone and no speech key.
      if (!audio && !transcript) {
        return reply.code(400).send({ errorCode: "INVALID_REQUEST" });
      }

      if (!audio) {
        const result = commands.accept({
          token,
          sessionId,
          petId,
          clientCommandId,
          audio: Buffer.alloc(0),
          mimeType: "text/plain",
          filename: "override.txt",
          transcriptOverride: transcript
        });
        return await respond(reply, commands, result, token, wait);
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
        filename,
        transcriptOverride: transcript || undefined
      });
      return await respond(reply, commands, result, token, wait);
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

/**
 * 202 with an id, or — when asked to wait — the finished record.
 *
 * The waiting form is not a different pipeline: it runs the same processing and
 * then reads the same record the socket listener would have received. Two code
 * paths producing an answer would eventually produce two different answers.
 */
async function respond(
  reply: FastifyReply,
  commands: VoiceCommandService,
  result: { commandId: string; duplicate: boolean; completion: Promise<void> },
  token: string,
  wait: boolean
) {
  if (!wait) {
    return reply.code(202).send({
      commandId: result.commandId,
      status: result.duplicate ? "DUPLICATE" : "PROCESSING"
    });
  }

  await result.completion;
  return reply.code(200).send(commands.get(result.commandId, token));
}

function bearerToken(value: string | undefined): string {
  if (!value?.startsWith("Bearer ")) throw new Error("INVALID_CAPABILITY");
  return value.slice("Bearer ".length);
}
