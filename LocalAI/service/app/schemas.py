from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field, field_validator


ALLOWED_TARGET_TYPES = {
    "NONE",
    "SELF_POSITION",
    "LOOK_POSITION",
    "VISIBLE_TARGET",
    "NAMED_TARGET",
    "LAST_KNOWN_TARGET",
}


class Interpretation(BaseModel):
    intent: str = "NONE"
    targetType: str = "NONE"
    targetId: str = ""
    confidence: float = 0.0
    needsClarification: bool = True

    @field_validator("confidence")
    @classmethod
    def confidence_range(cls, value: float) -> float:
        if value != value or value in (float("inf"), float("-inf")):
            return 0.0
        return max(0.0, min(1.0, value))

    @field_validator("targetType")
    @classmethod
    def target_type_allowed(cls, value: str) -> str:
        value = value.strip().upper()
        return value if value in ALLOWED_TARGET_TYPES else "NONE"


class TimingMs(BaseModel):
    stt: int = 0
    llm: int = 0
    total: int = 0


class VoiceCommandResponse(BaseModel):
    requestId: str
    transcript: str = ""
    normalizedText: str = ""
    actorRole: str = ""
    animalType: str = ""
    interpretation: Interpretation
    timingMs: TimingMs
    fallbackUsed: bool = False
    failureCode: str = ""
    failureMessage: str = ""
    lookWorldPosition: dict[str, float] | None = None


def parse_context(raw: str) -> dict[str, Any]:
    import json

    try:
        value = json.loads(raw or "{}")
    except json.JSONDecodeError:
        return {}
    return value if isinstance(value, dict) else {}


def allowed_commands(context: dict[str, Any], animal_type: str) -> list[str]:
    values = context.get("availableCommands")
    if isinstance(values, list):
        return [str(value).upper() for value in values if str(value).strip()]
    if animal_type.upper() == "DOG":
        return ["TRACK", "SEARCH", "GUARD", "BARK", "STAY", "STOP"]
    return ["SCOUT", "DISTRACT", "ROOF", "HIDE", "BITE", "STAY", "STOP"]
