from __future__ import annotations

import argparse
import os
from dataclasses import dataclass
from pathlib import Path


@dataclass
class GatewayConfig:
    host: str = "127.0.0.1"
    port: int = 8765
    ollama_host: str = "127.0.0.1"
    ollama_port: int = 11434
    ollama_model: str = "qwen3:4b-instruct"
    stt_model_path: str = ""
    stt_language: str = "ko"
    shutdown_token: str = ""
    owner_pid: int = 0
    model_device: str = "cuda"
    gpu_compute_type: str = "int8_float16"
    cpu_compute_type: str = "int8"

    @property
    def ollama_url(self) -> str:
        return f"http://{self.ollama_host}:{self.ollama_port}"


def project_root() -> Path:
    return Path(__file__).resolve().parents[3]


def default_model_path() -> Path:
    return project_root() / "LocalAI" / "models" / "faster-whisper-small"


def parse_args(argv: list[str] | None = None) -> GatewayConfig:
    parser = argparse.ArgumentParser(description="Paws & Loot local AI gateway")
    parser.add_argument("--host", default=os.getenv("PAWS_GATEWAY_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.getenv("PAWS_GATEWAY_PORT", "8765")))
    parser.add_argument("--ollama-host", default=os.getenv("OLLAMA_HOST", "127.0.0.1"))
    parser.add_argument("--ollama-port", type=int, default=int(os.getenv("PAWS_OLLAMA_PORT", "11434")))
    parser.add_argument("--ollama-model", default=os.getenv("PAWS_OLLAMA_MODEL", "qwen3:4b-instruct"))
    parser.add_argument("--stt-model-path", default=os.getenv("PAWS_STT_MODEL_PATH", str(default_model_path())))
    parser.add_argument("--language", dest="stt_language", default=os.getenv("PAWS_STT_LANGUAGE", "ko"))
    parser.add_argument("--shutdown-token", default=os.getenv("PAWS_SHUTDOWN_TOKEN", ""))
    parser.add_argument("--owner-pid", type=int, default=int(os.getenv("PAWS_OWNER_PID", "0")))
    parser.add_argument("--device", dest="model_device", default=os.getenv("PAWS_STT_DEVICE", "cuda"))
    parser.add_argument("--gpu-compute-type", default="int8_float16")
    parser.add_argument("--cpu-compute-type", default="int8")
    args = parser.parse_args(argv)
    return GatewayConfig(**vars(args))
