from __future__ import annotations

import asyncio
import os
import tempfile
import time
import uuid
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, File, Form, Header, HTTPException, UploadFile

from .command_interpreter import interpret, normalize_text
from .config import GatewayConfig, parse_args
from .ollama_client import OllamaClient
from .schemas import Interpretation, TimingMs, VoiceCommandResponse, parse_context
from .stt_service import SttService


class Runtime:
    def __init__(self, config: GatewayConfig):
        self.config = config
        self.status = "starting"
        self.llm_ready = False
        self.stt = SttService(
            config.stt_model_path,
            config.stt_language,
            config.model_device,
            config.gpu_compute_type,
            config.cpu_compute_type,
        )
        self.ollama = OllamaClient(config.ollama_url, config.ollama_model)

    def load(self) -> None:
        self.status = "loading_models"
        try:
            self.stt.load()
        except Exception as error:
            self.stt.error = f"STT_LOAD_FAILED:{error}"
        self.llm_ready = self.ollama.ready()
        if not self.stt.ready or not self.llm_ready:
            self.status = "degraded"
            return
        self.status = "ready"


config = parse_args()
runtime = Runtime(config)


@asynccontextmanager
async def lifespan(_: FastAPI):
    await asyncio.to_thread(runtime.load)
    yield


app = FastAPI(title="Paws & Loot Local AI Gateway", lifespan=lifespan)


@app.get("/health")
def health() -> dict:
    return {
        "service": "paws-local-ai",
        "status": runtime.status,
        "stt": {
            "ready": runtime.stt.ready,
            "model": "faster-whisper-small",
            "device": runtime.stt.device,
            "error": runtime.stt.error,
        },
        "llm": {
            "ready": runtime.llm_ready,
            "model": runtime.ollama.model,
        },
    }


@app.post("/v1/voice-command", response_model=VoiceCommandResponse)
async def voice_command(
    audio: UploadFile = File(...),
    language: str = Form("ko"),
    actorRole: str = Form("POLICE"),
    animalType: str = Form("DOG"),
    context: str = Form("{}"),
) -> VoiceCommandResponse:
    if runtime.status not in {"ready", "degraded"} or not runtime.stt.ready:
        raise HTTPException(status_code=503, detail="LOCAL_AI_NOT_READY")

    request_id = str(uuid.uuid4())
    started = time.perf_counter()
    suffix = Path(audio.filename or "command.wav").suffix or ".wav"
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temporary:
        temporary.write(await audio.read())
        audio_path = temporary.name

    try:
        transcript, stt_ms = await asyncio.to_thread(runtime.stt.transcribe, audio_path)
        context_data = parse_context(context)
        interpretation, llm_ms, fallback = await asyncio.to_thread(
            interpret,
            runtime.ollama,
            transcript,
            context_data,
            animalType,
        )
        look_position = context_data.get("lookWorldPosition")
        return VoiceCommandResponse(
            requestId=request_id,
            transcript=transcript,
            normalizedText=normalize_text(transcript),
            actorRole=actorRole,
            animalType=animalType,
            interpretation=interpretation,
            timingMs=TimingMs(
                stt=stt_ms,
                llm=llm_ms,
                total=round((time.perf_counter() - started) * 1000),
            ),
            fallbackUsed=fallback,
            lookWorldPosition=look_position if isinstance(look_position, dict) else None,
        )
    finally:
        try:
            os.unlink(audio_path)
        except OSError:
            pass


@app.post("/shutdown")
async def shutdown(x_local_ai_token: str | None = Header(default=None)) -> dict:
    if not config.shutdown_token or x_local_ai_token != config.shutdown_token:
        raise HTTPException(status_code=403, detail="SHUTDOWN_NOT_AUTHORIZED")
    if config.owner_pid:
        try:
            os.kill(config.owner_pid, 0)
        except OSError:
            raise HTTPException(status_code=403, detail="OWNER_PROCESS_NOT_FOUND")

    async def terminate() -> None:
        await asyncio.sleep(0.15)
        os._exit(0)

    asyncio.create_task(terminate())
    return {"status": "shutting_down"}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host=config.host, port=config.port)
