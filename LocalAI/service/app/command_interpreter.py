from __future__ import annotations

import re
from typing import Any

from .ollama_client import OllamaClient
from .schemas import Interpretation, allowed_commands


def normalize_text(value: str) -> str:
    return re.sub(r"[^\w가-힣]+", "", value.lower())


def core_fallback(transcript: str, animal_type: str) -> Interpretation:
    text = transcript.lower()
    if animal_type.upper() == "DOG":
        if any(token in text for token in ("냄새", "흔적", "추적", "track")):
            return Interpretation(intent="TRACK", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("경계", "지켜", "guard")):
            return Interpretation(intent="GUARD", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("쫓아", "추격", "chase")):
            return Interpretation(intent="TRACK", targetType="VISIBLE_TARGET", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("물어", "물기", "bite")):
            return Interpretation()
    return Interpretation()


def interpret(
    client: OllamaClient,
    transcript: str,
    context: dict[str, Any],
    animal_type: str,
) -> tuple[Interpretation, int, bool]:
    commands = allowed_commands(context, animal_type)
    try:
        raw, elapsed = client.interpret(transcript, context, commands)
        parsed = Interpretation.model_validate(raw)
        if parsed.intent.upper() not in commands and parsed.intent.upper() != "NONE":
            raise ValueError("intent outside allowedCommands")
        visible_ids = {str(value) for value in context.get("visibleTargetIds", [])}
        if parsed.targetId and parsed.targetId not in visible_ids:
            parsed.targetType = "NONE"
            parsed.targetId = ""
            parsed.needsClarification = True
        return parsed, elapsed, False
    except Exception:
        try:
            repair_context = dict(context)
            repair_context["repairInstruction"] = "Return one valid JSON object and nothing else."
            raw, elapsed = client.interpret(transcript, repair_context, commands)
            parsed = Interpretation.model_validate(raw)
            if parsed.intent.upper() not in commands and parsed.intent.upper() != "NONE":
                raise ValueError("intent outside allowedCommands")
            return parsed, elapsed, True
        except Exception:
            return core_fallback(transcript, animal_type), 0, True
