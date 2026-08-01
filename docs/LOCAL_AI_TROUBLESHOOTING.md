# Local AI troubleshooting

## Gateway does not become ready

- Run `tools/verify-local-ai-installation.ps1`.
- Check that `LocalAI/runtime/gateway/paws-local-ai.exe` exists.
- Check the Gateway port and the selected fallback port in the Unity HUD/log.
- Do not terminate an unrelated process occupying a localhost port.

## Ollama is unavailable

- Confirm `LocalAI/runtime/ollama/ollama.exe` exists.
- Check `http://127.0.0.1:11434/api/tags`.
- Confirm the exact model tag is `qwen3:4b-instruct`.
- An externally started Ollama process is not terminated by Unity.

## STT falls back to CPU

This is expected when CUDA initialization fails. The Gateway reports the active
device in `/health` and retries with CPU `int8`. This guide does not claim a
performance comparison.

## Voice input is unavailable

Check microphone permission, the available Unity microphone device, and the
HUD error. Voice errors do not disable numeric-key or button commands.

## Qwen returns invalid JSON

The Gateway retries once with a repair instruction and then uses the bounded
core matcher. The response records `fallbackUsed`; Unity still performs its own
command, target, role, coordinate, distance, and cooldown validation.
