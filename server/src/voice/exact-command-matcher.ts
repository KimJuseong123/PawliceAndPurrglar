export type AbsoluteIntent =
  | "STOP"
  | "FOLLOW_OWNER"
  | "STAY"
  | "RETURN_OWNER"
  | "CANCEL";

const synonyms: Record<AbsoluteIntent, string[]> = {
  STOP: ["멈춰", "그만", "서", "멈추어", "멈춰라"],
  FOLLOW_OWNER: ["따라와", "나따라와", "이리와", "따라오세요", "나를따라와"],
  STAY: ["기다려", "거기있어", "가만히있어", "기다리세요"],
  RETURN_OWNER: ["돌아와", "나한테돌아와", "돌아오세요", "이리돌아와"],
  CANCEL: ["취소", "하지마", "그거하지마", "취소해"]
};

function normalize(value: string): string {
  return value
    .normalize("NFKC")
    .toLowerCase()
    .replace(/[\p{P}\p{S}]/gu, "")
    .replace(/\s+/g, "")
    .trim();
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

export interface ExactMatch {
  intent: AbsoluteIntent;
  normalizedText: string;
  confidence: number;
}

export class ExactCommandMatcher {
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

  normalize(text: string): string {
    return normalize(text);
  }
}
