from __future__ import annotations

import time
from pathlib import Path
from typing import Any


class SttService:
    def __init__(self, model_path: str, language: str, device: str, gpu_compute_type: str, cpu_compute_type: str):
        self.model_path = Path(model_path)
        self.language = language
        self.device = device
        self.compute_type = gpu_compute_type
        self.model: Any = None
        self.error = ""

    @property
    def ready(self) -> bool:
        return self.model is not None

    def load(self) -> None:
        if not self.model_path.exists():
            self.error = f"STT_MODEL_MISSING:{self.model_path}"
            return

        from faster_whisper import WhisperModel

        try:
            self.model = WhisperModel(
                str(self.model_path),
                device=self.device,
                compute_type=self.compute_type,
            )
        except Exception as gpu_error:
            self.device = "cpu"
            self.compute_type = "int8"
            try:
                self.model = WhisperModel(
                    str(self.model_path),
                    device="cpu",
                    compute_type="int8",
                )
                self.error = f"GPU_FALLBACK:{gpu_error}"
            except Exception as cpu_error:
                self.model = None
                self.error = f"STT_LOAD_FAILED:{cpu_error}"

    def transcribe(self, audio_path: str) -> tuple[str, int]:
        if not self.ready:
            raise RuntimeError(self.error or "STT_NOT_READY")
        started = time.perf_counter()
        segments, _ = self.model.transcribe(
            audio_path,
            language=self.language,
            vad_filter=True,
            beam_size=5,
        )
        transcript = " ".join(segment.text.strip() for segment in segments).strip()
        return transcript, round((time.perf_counter() - started) * 1000)
