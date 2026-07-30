import { randomBytes, randomUUID } from "node:crypto";
import { env } from "../config/env.js";

export interface SessionParticipant {
  clientId: string;
  role: "POLICE" | "THIEF";
  petId: string;
}

interface Capability {
  token: string;
  sessionId: string;
  clientId: string;
  role: "HOST" | "POLICE" | "THIEF";
  petId?: string;
  expiresAt: number;
}

export class SessionCapabilityService {
  private readonly capabilities = new Map<string, Capability>();

  register(
    sessionId: string,
    hostClientId: string,
    participants: SessionParticipant[],
    registrationKey: string
  ): Record<string, string> {
    if (env.sessionRegistrationKey && registrationKey !== env.sessionRegistrationKey) {
      throw new Error("INVALID_REGISTRATION_KEY");
    }

    const tokens: Record<string, string> = {
      [hostClientId]: this.issue(sessionId, hostClientId, "HOST")
    };
    for (const participant of participants) {
      tokens[participant.clientId] = this.issue(
        sessionId,
        participant.clientId,
        participant.role,
        participant.petId
      );
    }
    return tokens;
  }

  authorize(token: string, sessionId: string, petId: string): Capability {
    const capability = this.capabilities.get(token);
    if (
      !capability ||
      capability.expiresAt < Date.now() ||
      capability.sessionId !== sessionId ||
      (capability.role !== "HOST" && capability.petId !== petId)
    ) {
      throw new Error("INVALID_CAPABILITY");
    }
    return capability;
  }

  authorizeHost(token: string, sessionId: string): Capability {
    const capability = this.capabilities.get(token);
    if (
      !capability ||
      capability.expiresAt < Date.now() ||
      capability.sessionId !== sessionId ||
      capability.role !== "HOST"
    ) {
      throw new Error("INVALID_HOST_CAPABILITY");
    }
    return capability;
  }

  private issue(
    sessionId: string,
    clientId: string,
    role: Capability["role"],
    petId?: string
  ): string {
    const token = `${randomUUID()}-${randomBytes(12).toString("hex")}`;
    this.capabilities.set(token, {
      token,
      sessionId,
      clientId,
      role,
      petId,
      expiresAt: Date.now() + env.capabilityTtlSeconds * 1000
    });
    return token;
  }
}
