/**
 * Deterministic Korean command matching, before any model is called.
 *
 * Two tiers, deliberately separate:
 *
 * - `match()` covers the five **absolute** commands only, and answers with high
 *   confidence or not at all. These have to work when the model is unreachable,
 *   so they must never be guessed at — "멈춰" stopping the animal is a safety
 *   promise, not a best effort.
 * - `suggest()` covers the **whole** vocabulary and is allowed to be unsure. It
 *   feeds the model a hint and is the last resort that keeps the pipeline from
 *   ever answering "I did not understand".
 *
 * Keeping them apart is what lets `match()` return null for "저 상자 뒤를 찾아봐"
 * — that sentence is a real search command, but resolving it needs the world
 * context only the model has.
 */

export type AbsoluteIntent =
  | "STOP"
  | "FOLLOW_OWNER"
  | "STAY"
  | "RETURN_OWNER"
  | "CANCEL";

export type PetType = "DOG" | "CAT";

const synonyms: Record<AbsoluteIntent, string[]> = {
  STOP: ["멈춰", "그만", "서", "멈추어", "멈춰라"],
  FOLLOW_OWNER: ["따라와", "나따라와", "이리와", "따라오세요", "나를따라와"],
  STAY: ["기다려", "거기있어", "가만히있어", "기다리세요"],
  RETURN_OWNER: ["돌아와", "나한테돌아와", "돌아오세요", "이리돌아와"],
  CANCEL: ["취소", "하지마", "그거하지마", "취소해"]
};

/**
 * Gameplay vocabulary, by intent, split by which animal can be told it.
 *
 * **Stems, not sentences.** People say "짖으라고 해" and "좀 숨어 있어", not
 * "짖어" — and the transcript arrives with slips in it. A short stem compared at
 * the jamo level survives both: `짖` is three jamo (ㅈㅣㅈ) and it is present,
 * exactly, inside a transcript that came back as "지지라고".
 *
 * Intent names come from `CompanionCommandCatalog.FromIntent`. Anything not
 * mapped there resolves to no command, so adding a name here without adding it
 * on the Unity side produces silence — which is how BARK and HIDE went missing.
 */
const gameplayVocabulary: {
  intent: string;
  pets: PetType[];
  stems: string[];
}[] = [
  // Shared.
  {
    intent: "SEARCH_AREA",
    pets: ["DOG", "CAT"],
    stems: ["찾아", "찾어", "수색", "뒤져", "살펴", "search", "find"]
  },
  {
    intent: "INSPECT_TARGET",
    pets: ["DOG", "CAT"],
    stems: ["확인", "봐줘", "정찰", "살펴봐", "inspect", "scout"]
  },

  // Police dog.
  {
    intent: "CHASE_TARGET",
    pets: ["DOG"],
    stems: ["냄새", "흔적", "추적", "쫓아", "추격", "track", "chase", "scent"]
  },
  {
    intent: "GUARD_AREA",
    pets: ["DOG"],
    stems: ["경계", "지켜", "지키", "감시", "guard", "watch"]
  },
  {
    // Unreachable from voice until 2026-08-07: no intent name mapped to
    // `CompanionCommandId.Bark`, so a perfect transcript still did nothing.
    intent: "BARK",
    pets: ["DOG"],
    stems: ["짖", "짓어", "왕왕", "소리질러", "bark"]
  },

  // Thief cat.
  {
    intent: "FETCH_OBJECT",
    pets: ["CAT"],
    stems: ["훔쳐", "가져와", "물어와", "훔치", "지붕", "옥상", "steal", "fetch"]
  },
  {
    intent: "DISTRACT_TARGET",
    pets: ["CAT"],
    stems: ["할퀴", "울어", "야옹", "관심", "유인", "distract", "scratch"]
  },
  {
    // Same story as BARK.
    intent: "HIDE",
    pets: ["CAT"],
    stems: ["숨", "은신", "숨어", "hide"]
  },
  {
    // CAT-010. Cat only. The dog has no bite — an officer who says it gets
    // nothing, which `CompanionCommandCatalog.FromIntent` enforces.
    //
    // "물어" is deliberately absent from FETCH_OBJECT's list above, which has
    // "물어와". They are one jamo apart and the near-match pass takes the
    // cheaper edit, so "경찰을 물어" reaches BITE and "이거 물어와" reaches the
    // fetch — but only because the fetch stem keeps its 와.
    intent: "BITE",
    pets: ["CAT"],
    stems: ["물어", "깨물", "물기", "공격", "bite", "attack"]
  }
];

function normalize(value: string): string {
  return value
    .normalize("NFKC")
    .toLowerCase()
    .replace(/[\p{P}\p{S}]/gu, "")
    .replace(/\s+/g, "")
    .trim();
}

/**
 * Leading and trailing consonants share one alphabet here, on purpose.
 *
 * Unicode gives choseong ㅁ (U+1106) and jongseong ㅁ (U+11B7) different code
 * points, and using them as-is defeats the whole exercise: the most common way a
 * Korean transcript goes wrong is a consonant migrating across a syllable
 * boundary. "숨어" heard as "스모" is exactly that — the ㅁ moved from the tail of
 * one syllable to the head of the next. With separate code points those two ㅁ do
 * not match and the distance comes out 2; with a shared alphabet it is 1, which
 * is the difference between recovering the command and dropping it.
 *
 * Compound finals decompose (ㄺ → ㄹㄱ) so they cost their real number of edits.
 */
const CHOSEONG = [..."ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ"];
const JUNGSEONG = [..."ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ"];
const JONGSEONG = [
  "", "ㄱ", "ㄲ", "ㄱㅅ", "ㄴ", "ㄴㅈ", "ㄴㅎ", "ㄷ", "ㄹ", "ㄹㄱ", "ㄹㅁ",
  "ㄹㅂ", "ㄹㅅ", "ㄹㅌ", "ㄹㅍ", "ㄹㅎ", "ㅁ", "ㅂ", "ㅂㅅ", "ㅅ", "ㅆ",
  "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"
];

/**
 * Splits Hangul syllables into their jamo so a slip in one consonant costs one
 * edit instead of a whole character.
 *
 * At the syllable level `지` and `짖` are simply different and share no useful
 * distance; at the jamo level they differ by one trailing consonant.
 * Non-Hangul characters pass through, so the English stems still work.
 */
function toJamo(value: string): string {
  let out = "";
  for (const character of value) {
    const code = character.codePointAt(0) ?? 0;
    const offset = code - 0xac00;
    if (offset < 0 || offset > 11171) {
      out += character;
      continue;
    }

    out += CHOSEONG[Math.floor(offset / 588)];
    out += JUNGSEONG[Math.floor((offset % 588) / 28)];
    out += JONGSEONG[offset % 28];
  }

  return out;
}

function distance(left: string, right: string): number {
  const row = Array.from({ length: right.length + 1 }, (_, index) => index);
  for (let i = 1; i <= left.length; i += 1) {
    let previous = row[0];
    row[0] = i;
    for (let j = 1; j <= right.length; j += 1) {
      const current = row[j];
      row[j] = Math.min(
        row[j] + 1,
        row[j - 1] + 1,
        previous + (left[i - 1] === right[j - 1] ? 0 : 1)
      );
      previous = current;
    }
  }
  return row[right.length];
}

/**
 * Cheapest edit distance between `needle` and *any* substring of `haystack`.
 *
 * The whole-string distance is the wrong measure for a spoken sentence: "강아지
 * 냄새 추적해" is far from "추적" by Levenshtein and contains it exactly. Zeroing
 * the first row lets the match start anywhere, and taking the row minimum lets it
 * end anywhere.
 */
function substringDistance(needle: string, haystack: string): number {
  if (!needle) return 0;
  if (!haystack) return needle.length;

  let row = new Array<number>(haystack.length + 1).fill(0);
  for (let i = 1; i <= needle.length; i += 1) {
    const next = new Array<number>(haystack.length + 1);
    next[0] = i;
    for (let j = 1; j <= haystack.length; j += 1) {
      next[j] = Math.min(
        row[j] + 1,
        next[j - 1] + 1,
        row[j - 1] + (needle[i - 1] === haystack[j - 1] ? 0 : 1)
      );
    }
    row = next;
  }

  return Math.min(...row);
}

/**
 * How wrong a stem is allowed to be. Relative to its length, so a two-jamo stem
 * stays strict and a long phrase gets slack — a fixed allowance of one edit is
 * generous on short words and useless on long ones.
 */
function tolerance(jamoLength: number): number {
  return Math.max(1, Math.floor(jamoLength * 0.34));
}

export interface ExactMatch {
  intent: AbsoluteIntent;
  normalizedText: string;
  confidence: number;
}

export interface CommandSuggestion {
  intent: string;
  normalizedText: string;
  confidence: number;
  /** `absolute` may bypass the model. `gameplay` is a hint, not a verdict. */
  tier: "absolute" | "gameplay";
  matchedStem: string;
  /** True when the stem was recovered through a slip rather than found intact. */
  approximate: boolean;
}

export class ExactCommandMatcher {
  /**
   * The five safety commands, or null. Unchanged contract: free speech that
   * happens to be a real gameplay command still returns null here so the model
   * gets to use the world context.
   */
  match(text: string): ExactMatch | null {
    const normalizedText = normalize(text);
    if (!normalizedText) return null;

    for (const [intent, values] of Object.entries(synonyms) as [
      AbsoluteIntent,
      string[]
    ][]) {
      if (values.map(normalize).includes(normalizedText)) {
        return { intent, normalizedText, confidence: 1 };
      }
    }

    if (normalizedText.length < 2) return null;
    for (const [intent, values] of Object.entries(synonyms) as [
      AbsoluteIntent,
      string[]
    ][]) {
      for (const value of values.map(normalize)) {
        if (distance(normalizedText, value) <= 1) {
          return { intent, normalizedText, confidence: 0.94 };
        }
      }
    }
    return null;
  }

  /**
   * Best guess across the whole vocabulary, for this animal.
   *
   * Three passes, most trustworthy first: an absolute command, a stem found
   * intact, then a stem recovered through a slip. Confidence drops with each,
   * and that drop is meaningful downstream — `PetCognitionResolver` multiplies it
   * into the obedience roll, so a shaky match legitimately becomes a confused or
   * mistaken animal rather than a confident wrong action.
   */
  suggest(
    text: string,
    petType: PetType,
    allowedIntents?: readonly string[]
  ): CommandSuggestion | null {
    const normalizedText = normalize(text);
    if (!normalizedText) return null;

    const allowed = allowedIntents?.length
      ? new Set(allowedIntents)
      : null;
    const permits = (intent: string): boolean =>
      allowed === null || allowed.has(intent);

    const absolute = this.match(text);
    if (absolute && permits(absolute.intent)) {
      return {
        intent: absolute.intent,
        normalizedText,
        confidence: absolute.confidence,
        tier: "absolute",
        matchedStem: absolute.normalizedText,
        approximate: absolute.confidence < 1
      };
    }

    const candidates = gameplayVocabulary.filter(
      (entry) => entry.pets.includes(petType) && permits(entry.intent)
    );

    // Intact first, and among those the longest stem wins — "추적" beating a
    // one-jamo coincidence is the whole reason length is the tiebreak.
    let bestIntact: { intent: string; stem: string } | null = null;
    for (const entry of candidates) {
      for (const stem of entry.stems) {
        const needle = normalize(stem);
        if (!needle || !normalizedText.includes(needle)) continue;
        if (!bestIntact || needle.length > bestIntact.stem.length) {
          bestIntact = { intent: entry.intent, stem: needle };
        }
      }
    }

    if (bestIntact) {
      return {
        intent: bestIntact.intent,
        normalizedText,
        confidence: 0.8,
        tier: "gameplay",
        matchedStem: bestIntact.stem,
        approximate: false
      };
    }

    const haystack = toJamo(normalizedText);
    let bestNear:
      | { intent: string; stem: string; cost: number; length: number }
      | null = null;
    for (const entry of candidates) {
      for (const stem of entry.stems) {
        const needle = toJamo(normalize(stem));
        if (needle.length < 2) continue;

        const cost = substringDistance(needle, haystack);
        if (cost > tolerance(needle.length)) continue;

        // Fewer edits wins; ties go to the longer stem, which is the less
        // likely coincidence.
        const better =
          bestNear === null ||
          cost < bestNear.cost ||
          (cost === bestNear.cost && needle.length > bestNear.length);
        if (better) {
          bestNear = {
            intent: entry.intent,
            stem: normalize(stem),
            cost,
            length: needle.length
          };
        }
      }
    }

    if (!bestNear) return null;

    return {
      intent: bestNear.intent,
      normalizedText,
      confidence: bestNear.cost === 0 ? 0.72 : 0.55,
      tier: "gameplay",
      matchedStem: bestNear.stem,
      approximate: true
    };
  }

  normalize(text: string): string {
    return normalize(text);
  }

  /** Every intent this matcher can produce for an animal. */
  vocabularyFor(petType: PetType): string[] {
    return [
      ...Object.keys(synonyms),
      ...gameplayVocabulary
        .filter((entry) => entry.pets.includes(petType))
        .map((entry) => entry.intent)
    ];
  }

  /**
   * What to hand the transcriber as expected style.
   *
   * **A sentence, not a word list.** Measured on the two commands that were
   * actually failing, with `gpt-4o-transcribe`:
   *
   * | said | no prompt | keyword list | this |
   * |---|---|---|---|
   * | 짖으라고 | 치즈라고 | 지지라고 | **짖으라고** |
   * | 숨어 | 相撲 | 주먹 | **숨어** |
   *
   * A comma-separated list was not merely useless, it was *harmful*: it pulled
   * short audio onto whichever listed word was nearest, and an earlier run turned
   * "숨어" into "그만" — a **valid but different command**, which nothing
   * downstream can catch. Garbage the jamo matcher rejects; a confident wrong
   * command it cannot.
   *
   * The prompt is read as a sample of the expected transcript, so it has to look
   * like one. Examples only, kept few — the full vocabulary is what turned it
   * back into a list.
   */
  transcriptionPromptFor(petType: PetType): string {
    const animal = petType === "DOG" ? "강아지" : "고양이";
    const examples = petType === "DOG"
      ? ["짖어", "냄새 추적해", "경계해", "따라와", "기다려", "멈춰"]
      : ["숨어", "훔쳐와", "할퀴어", "경찰을 물어", "따라와", "멈춰"];
    return `${animal}에게 짧게 명령하는 상황이다. 예: ${examples.join(". ")}.`;
  }

  /**
   * Every word the matcher knows. Kept for callers that want the raw vocabulary
   * — biasing should go through {@link transcriptionPromptFor} instead.
   */
  lexiconFor(petType: PetType): string[] {
    const words = new Set<string>();
    for (const values of Object.values(synonyms)) {
      for (const value of values) words.add(value);
    }
    for (const entry of gameplayVocabulary) {
      if (!entry.pets.includes(petType)) continue;
      for (const stem of entry.stems) {
        if (/^[a-z]+$/.test(stem)) continue;
        words.add(stem);
      }
    }
    return [...words];
  }
}
