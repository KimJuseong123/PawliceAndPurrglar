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
        self.cpu_compute_type = cpu_compute_type or "int8"
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
            if self._load_on_cpu():
                self.error = f"GPU_FALLBACK:{gpu_error}"

    def _load_on_cpu(self) -> bool:
        from faster_whisper import WhisperModel

        try:
            self.model = WhisperModel(
                str(self.model_path),
                device="cpu",
                compute_type=self.cpu_compute_type,
            )
            self.device = "cpu"
            self.compute_type = self.cpu_compute_type
            return True
        except Exception as cpu_error:
            self.model = None
            self.error = f"STT_LOAD_FAILED:{cpu_error}"
            return False

    def _run(self, audio_path: str) -> str:
        segments, _ = self.model.transcribe(
            audio_path,
            language=self.language,
            vad_filter=True,
            beam_size=5,
        )
        return " ".join(segment.text.strip() for segment in segments).strip()

    def transcribe(self, audio_path: str) -> tuple[str, int]:
        if not self.ready:
            raise RuntimeError(self.error or "STT_NOT_READY")
        started = time.perf_counter()
        try:
            transcript = self._run(audio_path)
        except RuntimeError as gpu_error:
            # The CUDA failure does not happen when the model is constructed.
            # ctranslate2 loads `cublas64_12.dll` on the first encode, so a
            # machine without the CUDA runtime reports `device: cuda` and
            # `ready: true` at /health and then fails **every** request with a
            # 500. The load-time fallback above never sees it. So fall back here
            # too, once, and keep serving on the CPU.
            if self.device == "cpu" or not self._is_device_failure(gpu_error):
                raise

            self.error = f"GPU_RUNTIME_FALLBACK:{gpu_error}"
            if not self._load_on_cpu():
                raise
            transcript = self._run(audio_path)

        return transcript, round((time.perf_counter() - started) * 1000)

    @staticmethod
    def _is_device_failure(error: Exception) -> bool:
        text = str(error).lower()
        return any(
            marker in text
            for marker in ("cublas", "cudnn", "cuda", "libcu", "no kernel image")
        )
