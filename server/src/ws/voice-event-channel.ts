import type { WebSocket } from "ws";
import type { VoiceEvent, VoiceWorldContext } from "../domain/contracts.js";
import { voiceWorldContextSchema } from "../domain/contracts.js";
import { SessionCapabilityService } from "../auth/session-capability-service.js";

interface HostConnection {
  socket: WebSocket;
  sessionId: string;
}

export class VoiceEventChannel {
  private readonly hosts = new Map<string, HostConnection>();
  private readonly contexts = new Map<string, VoiceWorldContext>();

  constructor(private readonly sessions: SessionCapabilityService) {}

  registerHost(socket: WebSocket, sessionId: string, token: string): void {
    this.sessions.authorizeHost(token, sessionId);
    this.hosts.set(sessionId, { socket, sessionId });
    socket.on("close", () => {
      if (this.hosts.get(sessionId)?.socket === socket) {
        this.hosts.delete(sessionId);
      }
    });
  }

  registerContext(socket: WebSocket, clientCommandId: string, context: unknown): void {
    const host = [...this.hosts.values()].find((connection) => connection.socket === socket);
    if (!host) throw new Error("UNREGISTERED_HOST");
    this.contexts.set(clientCommandId, voiceWorldContextSchema.parse(context));
  }

  takeContext(clientCommandId: string): VoiceWorldContext | undefined {
    const context = this.contexts.get(clientCommandId);
    this.contexts.delete(clientCommandId);
    return context;
  }

  send(sessionId: string, event: VoiceEvent): boolean {
    const connection = this.hosts.get(sessionId);
    if (!connection || connection.socket.readyState !== 1) return false;
    connection.socket.send(JSON.stringify(event));
    return true;
  }
}
