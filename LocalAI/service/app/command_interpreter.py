from __future__ import annotations

import re
from typing import Any

from .ollama_client import OllamaClient
from .schemas import Interpretation, allowed_commands


def normalize_text(value: str) -> str:
    return re.sub(r"[^\w가-힣]+", "", value.lower())


def core_fallback(transcript: str, animal_type: str) -> Interpretation:
    text = normalize_text(transcript)
    if animal_type.upper() == "DOG":
        if any(token in text for token in ("물어", "물기", "bite")):
            return Interpretation(intent="NONE", confidence=0.9, needsClarification=False)
        if any(token in text for token in ("냄새", "흔적", "추적", "따라", "쫓아", "쫓", "track", "chase")):
            return Interpretation(intent="TRACK", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("찾아", "찾아봐", "수색", "살펴", "둘러", "search")):
            return Interpretation(intent="SEARCH", targetType="LOOK_POSITION", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("경계", "지켜", "막아", "기다려", "guard")):
            return Interpretation(intent="GUARD", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("짖어", "짖", "소리", "bark")):
            return Interpretation(intent="BARK", confidence=0.76, needsClarification=False)
    if animal_type.upper() == "CAT":
        # CAT-010. Before the steal check: "물어와" is a fetch and "물어" is not,
        # and the steal list matches on the bare "물".
        if any(token in text for token in ("물어", "깨물", "물기", "공격", "bite")):
            return Interpretation(intent="BITE", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("지붕", "옥상", "올라", "roof", "climb")):
            return Interpretation(intent="ROOF", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("정찰", "확인", "살펴", "봐", "scout", "inspect")):
            return Interpretation(intent="SCOUT", targetType="LOOK_POSITION", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("교란", "방해", "시선", "유인", "distract")):
            return Interpretation(intent="DISTRACT", confidence=0.76, needsClarification=False)
        if any(token in text for token in ("훔쳐", "훔", "가져", "집어", "steal", "fetch")):
            return Interpretation(intent="NONE", confidence=0.65, needsClarification=False)
        if any(token in text for token in ("숨겨", "숨", "감춰", "hide")):
            return Interpretation(intent="HIDE", confidence=0.76, needsClarification=False)
    return Interpretation()


def interpret(
    client: OllamaClient,
    transcript: str,
    context: dict[str, Any],
    animal_type: str,
) -> tuple[Interpretation, int, bool]:
    commands = allowed_commands(context, animal_type)
    fallback = core_fallback(transcript, animal_type)
    fallback_intent = fallback.intent.upper()
    if (fallback_intent == "NONE" and fallback.confidence > 0.0) or fallback_intent in commands:
        return fallback, 0, True

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
