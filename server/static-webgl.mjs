import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const webglRoot = resolve(
  process.env.WEBGL_ROOT ?? join(projectRoot, "Builds", "Playtest", "WebGL")
);
const port = Number(process.env.PORT ?? 8080);

const mimeTypes = {
  ".css": "text/css; charset=utf-8",
  ".data": "application/octet-stream",
  ".html": "text/html; charset=utf-8",
  ".ico": "image/x-icon",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".wasm": "application/wasm",
  ".webmanifest": "application/manifest+json; charset=utf-8"
};

function safePath(urlPath) {
  const decoded = decodeURIComponent(urlPath.split("?", 1)[0]);
  const requested = normalize(decoded === "/" ? "/index.html" : decoded);
  const candidate = resolve(webglRoot, `.${requested}`);
  const containment = relative(webglRoot, candidate);
  if (containment.startsWith("..") || containment.includes(`..\\`)) {
    return null;
  }
  return candidate;
}

const server = createServer(async (request, response) => {
  try {
    const requested = safePath(request.url ?? "/");
    if (!requested) {
      response.writeHead(400);
      response.end("Invalid path");
      return;
    }

    let filePath = requested;
    try {
      const details = await stat(filePath);
      if (details.isDirectory()) filePath = join(filePath, "index.html");
    } catch {
      filePath = join(webglRoot, "index.html");
    }

    const details = await stat(filePath);
    if (!details.isFile()) throw new Error("Not a file");

    response.writeHead(200, {
      "Cache-Control": "no-cache",
      "Content-Length": details.size,
      "Content-Type": mimeTypes[extname(filePath).toLowerCase()] ??
        "application/octet-stream"
    });
    createReadStream(filePath).pipe(response);
  } catch (error) {
    response.writeHead(404);
    response.end("WebGL build is not available");
    if (process.env.NODE_ENV !== "production") {
      console.error(error instanceof Error ? error.message : error);
    }
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Paws & Loot WebGL: http://localhost:${port}`);
  console.log(`Serving: ${webglRoot}`);
});
