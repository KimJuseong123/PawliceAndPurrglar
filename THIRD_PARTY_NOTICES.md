# Third-party notices

## faster-whisper

- Package: `faster-whisper`
- Model: `Systran/faster-whisper-small`
- Purpose: local Korean speech-to-text
- Source: https://github.com/SYSTRAN/faster-whisper and https://huggingface.co/Systran/faster-whisper-small
- License: see `Licenses/faster-whisper-MIT.txt` and `Licenses/faster-whisper-small-MIT.txt`
- Modification: none
- Execution: local Python Gateway only

## Qwen3

- Exact tag: `qwen3:4b-instruct`
- Purpose: bounded Korean command intent extraction
- Source: Ollama model registry and Qwen upstream documentation
- License: Apache License 2.0; see `Licenses/Qwen3-Apache-2.0.txt`
- Modification: none
- Execution: local Ollama process only

## Ollama

- Runtime: official Windows `ollama.exe`
- Purpose: local Qwen3 model serving
- Source: https://ollama.com/download/windows
- License: see `Licenses/Ollama-license.txt` and the license shipped with the runtime
- Modification: none

Audio is sent only to the loopback Gateway at `127.0.0.1`. The Windows local
voice path does not send audio, transcripts, or command context to an external
server. Temporary WAV files are deleted after processing unless
`PreserveDebugRecordings` is explicitly enabled for development.
