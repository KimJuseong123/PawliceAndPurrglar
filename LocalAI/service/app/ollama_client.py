from __future__ import annotations

import json
import time
from typing import Any

import httpx


class OllamaClient:
    def __init__(self, base_url: str, model: str, timeout: float = 8.0):
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout = timeout

    def ready(self) -> bool:
        try:
            response = httpx.get(f"{self.base_url}/api/tags", timeout=3.0)
            response.raise_for_status()
            return self.model.lower() in response.text.lower()
        except httpx.HTTPError:
            return False

    def interpret(self, transcript: str, context: dict[str, Any], commands: list[str]) -> tuple[dict[str, Any], int]:
        system = (
            "You are the command parser for a Korean game. Return JSON only. "
            "Do not explain, use Markdown, invent targets, or create commands outside allowedCommands. "
            "Interpret the user's intent accurately; do not intentionally make the dog stupid. "
            "If uncertain return intent NONE. If target is unclear use targetType NONE. "
            "Return exactly: intent, targetType, targetId, confidence, needsClarification."
        )
        payload = {
            "model": self.model,
            "stream": False,
            "format": "json",
            "options": {"temperature": 0.0},
            "messages": [
                {"role": "system", "content": system},
                {
                    "role": "user",
                    "content": json.dumps(
                        {
                            "transcript": transcript,
                            "context": context,
                            "allowedCommands": commands,
                        },
                        ensure_ascii=False,
                    ),
                },
            ],
        }
        started = time.perf_counter()
        response = httpx.post(f"{self.base_url}/api/chat", json=payload, timeout=self.timeout)
        response.raise_for_status()
        body = response.json()
        content = body.get("message", {}).get("content", "")
        return json.loads(content), round((time.perf_counter() - started) * 1000)
