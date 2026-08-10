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
    // A real search command, but resolving it needs the world context only the
    // model has — so the absolute tier must stay out of it.
    expect(matcher.match("저 상자 뒤를 찾아봐")).toBeNull();
  });
});

describe("ExactCommandMatcher.suggest", () => {
  const matcher = new ExactCommandMatcher();

  it("finds a command inside an ordinary spoken sentence", () => {
    // Whole-string distance is the wrong measure here: this sentence is far from
    // "추적" by Levenshtein and contains it exactly.
    expect(matcher.suggest("강아지 냄새 추적해", "DOG")?.intent).toBe(
      "CHASE_TARGET"
    );
    expect(matcher.suggest("저 상자 뒤를 찾아봐", "CAT")?.intent).toBe(
      "SEARCH_AREA"
    );
  });

  it("recovers the two commands the transcriber actually got wrong", () => {
    // The reported failures, verbatim. Both survive because the stems are short
    // and compared at the jamo level: `짖` is ㅈㅣㅈ, present intact inside
    // "지지라고"; `숨` is ㅅㅜㅁ, one vowel away from the ㅅㅡㅁ in "스모".
    const bark = matcher.suggest("지지라고", "DOG");
    expect(bark?.intent).toBe("BARK");
    expect(bark?.approximate).toBe(true);

    const hide = matcher.suggest("스모", "CAT");
    expect(hide?.intent).toBe("HIDE");
    expect(hide?.approximate).toBe(true);
  });

  it("reports a recovered command as less certain than an intact one", () => {
    const intact = matcher.suggest("짖어", "DOG");
    const recovered = matcher.suggest("지지라고", "DOG");

    expect(intact?.intent).toBe("BARK");
    expect(intact?.approximate).toBe(false);
    // The gap is load-bearing: `PetCognitionResolver` multiplies confidence into
    // the obedience roll, so a shaky match becomes a confused animal rather than
    // a confident wrong action.
    expect(recovered!.confidence).toBeLessThan(intact!.confidence);
  });

  it("keeps each animal to its own commands", () => {
    // The cat cannot bark and the dog cannot hide; offering either would produce
    // an intent that `FromIntent` turns into a different command entirely.
    expect(matcher.suggest("짖어", "CAT")?.intent).not.toBe("BARK");
    expect(matcher.suggest("숨어", "DOG")?.intent).not.toBe("HIDE");
  });

  /**
   * CAT-010. "물어" and "물어와" are one jamo apart and mean different things —
   * bite the officer, fetch the treasure. The near-match pass takes the cheaper
   * edit, so this holds only while FETCH_OBJECT keeps its 와 and BITE does not.
   */
  it("tells the cat's bite apart from its fetch", () => {
    expect(matcher.suggest("경찰을 물어", "CAT")?.intent).toBe("BITE");
    expect(matcher.suggest("저거 물어와", "CAT")?.intent).toBe("FETCH_OBJECT");
    expect(matcher.suggest("깨물어", "CAT")?.intent).toBe("BITE");
  });

  it("gives the dog no bite at all", () => {
    // The officer already arrests. A dog that also bit would be a second way to
    // do the same thing, and `FromIntent` maps BITE for a dog to no command —
    // so offering it here would produce an intent the game throws away.
    expect(matcher.suggest("경찰을 물어", "DOG")?.intent).not.toBe("BITE");
    expect(matcher.vocabularyFor("DOG")).not.toContain("BITE");
    expect(matcher.vocabularyFor("CAT")).toContain("BITE");
  });

  it("never returns an intent the caller did not allow", () => {
    // The server clamps candidates to `allowedIntents` afterwards, so a
    // suggestion outside the list is silently dropped — better to not make it.
    const allowed = ["STOP", "FOLLOW_OWNER"];
    expect(matcher.suggest("냄새 추적해", "DOG", allowed)).toBeNull();
    expect(matcher.suggest("멈춰", "DOG", allowed)?.intent).toBe("STOP");
  });

  it("stays silent on speech that is not a command", () => {
    expect(matcher.suggest("오늘 날씨가 좋네", "DOG")).toBeNull();
    expect(matcher.suggest("", "CAT")).toBeNull();
  });

  it("offers a lexicon for biasing the transcriber", () => {
    // This is what gets handed to the speech model as expected vocabulary. The
    // Korean words are the point; the English aliases would only add noise.
    const lexicon = matcher.lexiconFor("DOG");
    expect(lexicon).toContain("짖");
    expect(matcher.transcriptionPromptFor("DOG")).toContain("짖어");
    expect(matcher.transcriptionPromptFor("CAT")).toContain("숨어");
    expect(matcher.transcriptionPromptFor("CAT")).toContain("물어");
    // A sentence, not a list — a comma-separated list measurably hurt accuracy.
    expect(matcher.transcriptionPromptFor("DOG")).toContain("상황이다");
    expect(lexicon).toContain("멈춰");
    expect(lexicon).not.toContain("bark");
    expect(matcher.lexiconFor("CAT")).toContain("숨");
  });
});
