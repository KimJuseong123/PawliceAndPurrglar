import { describe, expect, it } from "vitest";
import { ExactCommandMatcher } from "../src/voice/exact-command-matcher.js";

describe("ExactCommandMatcher", () => {
  const matcher = new ExactCommandMatcher();

  it("matches Korean synonyms after punctuation and spacing normalization", () => {
    expect(matcher.match("  그만! ")?.intent).toBe("STOP");
    expect(matcher.match("나 따라와")?.intent).toBe("FOLLOW_OWNER");
    expect(matcher.match("거기 있어")?.intent).toBe("STAY");
  });

  it("does not classify arbitrary free speech as an absolute command", () => {
    expect(matcher.match("저 상자 뒤를 찾아봐")).toBeNull();
  });
});
