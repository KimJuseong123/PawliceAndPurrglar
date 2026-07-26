# Local Voice Gateway

The Unity client sends a short WAV recording to this localhost-only service.
The service keeps the OpenAI API key outside the game process, transcribes the
audio, and returns one bounded command ID.

Requirements:

- Node.js 20 or newer
- An OpenAI API key with access to the configured transcription and text models

Run from PowerShell:

```powershell
$env:OPENAI_API_KEY = "your-key"
node Tools/VoiceGateway/server.mjs
```

Optional environment variables:

```text
VOICE_GATEWAY_PORT=8787
OPENAI_TRANSCRIBE_MODEL=gpt-4o-mini-transcribe
OPENAI_INTENT_MODEL=gpt-5.6-luna
OPENAI_REQUEST_TIMEOUT_MS=12000
```

Health check:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/health
```

Tests do not call OpenAI:

```powershell
node --test Tools/VoiceGateway/voice-gateway.test.mjs
```

Never place an API key in Unity assets, source files, `.env` files committed to
Git, screenshots, test output, or game logs.
