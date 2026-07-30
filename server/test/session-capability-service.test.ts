import { describe, expect, it } from "vitest";
import { SessionCapabilityService } from "../src/auth/session-capability-service.js";

describe("SessionCapabilityService", () => {
  it("scopes a participant token to its pet and session", () => {
    const service = new SessionCapabilityService();
    const tokens = service.register(
      "session-1",
      "host",
      [{ clientId: "police-client", role: "POLICE", petId: "dog" }],
      ""
    );

    expect(service.authorize(tokens["police-client"], "session-1", "dog")).toBeTruthy();
    expect(() =>
      service.authorize(tokens["police-client"], "session-1", "cat")
    ).toThrow("INVALID_CAPABILITY");
    expect(() =>
      service.authorize(tokens["police-client"], "session-2", "dog")
    ).toThrow("INVALID_CAPABILITY");
  });
});
