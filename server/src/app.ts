import Fastify from "fastify";
import cors from "@fastify/cors";
import multipart from "@fastify/multipart";
import rateLimit from "@fastify/rate-limit";
import websocket from "@fastify/websocket";
import { env, usingStubVoiceProviders } from "./config/env.js";
import { SessionCapabilityService } from "./auth/session-capability-service.js";
import { VoiceEventChannel } from "./ws/voice-event-channel.js";
import { OpenAiIntentClassifierClient } from "./voice/intent-classifier-client.js";
import { OpenAiSpeechToTextClient } from "./voice/speech-to-text-client.js";
import {
  StubIntentClassifierClient,
  StubSpeechToTextClient
} from "./voice/stub-voice-providers.js";
import { VoiceCommandService } from "./voice/voice-command-service.js";
import { registerVoiceRoutes } from "./http/voice-command-routes.js";

export function buildApp() {
  const app = Fastify({ logger: true });
  const sessions = new SessionCapabilityService();
  const events = new VoiceEventChannel(sessions);

  // Stubs when no speech provider is configured, so the game stays playable and
  // the obedience numbers stay tunable before anyone has paid for a key.
  const commands = new VoiceCommandService(
    sessions,
    events,
    usingStubVoiceProviders
      ? new StubSpeechToTextClient()
      : new OpenAiSpeechToTextClient(),
    usingStubVoiceProviders
      ? new StubIntentClassifierClient()
      : new OpenAiIntentClassifierClient()
  );

  if (usingStubVoiceProviders) {
    app.log.warn(
      "No OPENAI_API_KEY: voice is running on stubs. Transcription is a fixed "
        + "rotation, not speech. Set the key before judging accuracy."
    );
  }

  void app.register(cors, {
    origin: env.allowedOrigins.length > 0 ? env.allowedOrigins : false
  });
  void app.register(multipart, {
    limits: { files: 1, fileSize: env.maxFileSizeBytes }
  });
  void app.register(rateLimit, {
    max: env.rateLimitPerMinute,
    timeWindow: "1 minute",
    hook: "onRequest"
  });
  app.get("/health", async () => ({ status: "ok" }));

  // The websocket route lives inside a plugin that awaits the websocket plugin
  // first, and this is not a style choice.
  //
  // `app.register` is deferred: the plugin's `onRoute` hook is not installed
  // until `ready()`, so a route declared beside it never gets wrapped. Fastify
  // then treats it as an ordinary GET and calls the handler with
  // `(request, reply)` — our first parameter is named `socket`, so the first
  // thing it does is `socket.on(...)` on a Fastify Request, and every single
  // connection dies with `TypeError: socket.on is not a function` and a 500.
  //
  // Nothing upstream says so. The voice command upload is a separate POST and
  // keeps returning 200, so speech is transcribed and the answer is simply
  // never delivered — on screen that is "음성이 자꾸 실패한다", which points at
  // the microphone, the model, or the network, and never at route ordering.
  void app.register(async (instance) => {
    await instance.register(websocket);
    instance.get("/api/game/voice-events", { websocket: true }, (socket) => {
    socket.on("message", (raw) => {
      try {
        const message = JSON.parse(raw.toString()) as {
          type?: string;
          gameSessionId?: string;
          token?: string;
          commandId?: string;
          context?: unknown;
        };
        if (message.type === "REGISTER_HOST" && message.gameSessionId && message.token) {
          events.registerHost(socket, message.gameSessionId, message.token);
        } else if (
          message.type === "VOICE_CONTEXT" &&
          message.commandId &&
          message.context
        ) {
          events.registerContext(socket, message.commandId, message.context);
        }
      } catch {
        socket.send(
          JSON.stringify({
            type: "VOICE_COMMAND_FAILED",
            errorCode: "INVALID_WS_MESSAGE"
          })
        );
      }
      });
    });
  });

  void app.register(async (instance) => {
    await registerVoiceRoutes(instance, commands, sessions);
  });
  return app;
}
