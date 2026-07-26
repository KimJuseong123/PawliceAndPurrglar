import assert from "node:assert/strict";
import { once } from "node:events";
import { test } from "node:test";
import {
  CommandId,
  ReasonCode,
  createVoiceGateway,
  extractResponseText,
  validateClassifiedCommand,
  validateVoiceRequest,
} from "./voice-gateway.mjs";

test("validates the bounded request contract", () => {
  assert.equal(validateVoiceRequest({
    requestId: "request-1",
    role: "Police",
    locale: "ko-KR",
    audioWavBase64: Buffer.from("a valid wav placeholder").toString("base64"),
  }), null);
  assert.match(validateVoiceRequest({
    requestId: "request-1",
    role: "Wizard",
    locale: "ko-KR",
    audioWavBase64: "AAAAAAAAAAAAAAAA",
  }), /role/);
});

test("rejects a command that does not belong to the supplied role", () => {
  assert.deepEqual(
    validateClassifiedCommand({
      commandId: CommandId.DISTRACT,
      reasonCode: ReasonCode.MATCHED,
    }, "Police"),
    {
      commandId: CommandId.NONE,
      reasonCode: ReasonCode.OUT_OF_SCOPE,
    },
  );
});

test("extracts strict output text from a Responses API payload", () => {
  assert.equal(extractResponseText({
    output: [{
      type: "message",
      content: [{ type: "output_text", text: "{\"commandId\":\"TRACK\"}" }],
    }],
  }), "{\"commandId\":\"TRACK\"}");
});

test("serves transcription and classification without logging transcript", async () => {
  const logs = [];
  let call = 0;
  const fetchImpl = async () => {
    call += 1;
    if (call === 1) {
      return new Response(JSON.stringify({ text: "도둑 흔적 찾아" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response(JSON.stringify({
      output: [{
        type: "message",
        content: [{
          type: "output_text",
          text: JSON.stringify({
            commandId: CommandId.TRACK,
            reasonCode: ReasonCode.MATCHED,
          }),
        }],
      }],
    }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  };
  const server = createVoiceGateway({
    apiKey: "test-key",
    fetchImpl,
    logger: {
      info: (message) => logs.push(message),
      error: (message) => logs.push(message),
    },
  });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const { port } = server.address();

  try {
    const response = await fetch(`http://127.0.0.1:${port}/v1/voice-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        requestId: "request-2",
        role: "Police",
        locale: "ko-KR",
        audioWavBase64: Buffer.from("a valid wav placeholder").toString("base64"),
      }),
    });
    assert.equal(response.status, 200);
    const json = await response.json();
    assert.equal(json.commandId, CommandId.TRACK);
    assert.equal(json.transcript, "도둑 흔적 찾아");
    assert.equal(logs.some((value) => value.includes("도둑")), false);
  } finally {
    server.close();
    await once(server, "close");
  }
});

test("reports an unconfigured gateway without exposing a key", async () => {
  const server = createVoiceGateway({ apiKey: "" });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const { port } = server.address();
  try {
    const health = await fetch(`http://127.0.0.1:${port}/health`);
    assert.deepEqual(await health.json(), {
      status: "ok",
      configured: false,
    });
  } finally {
    server.close();
    await once(server, "close");
  }
});
