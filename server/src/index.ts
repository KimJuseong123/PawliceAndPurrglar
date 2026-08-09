import { buildApp } from "./app.js";

/**
 * Which interfaces to accept connections on.
 *
 * `0.0.0.0` is right for a container and wrong for the deployed instance, where
 * this process sits behind nginx and nothing else should ever reach it. It was
 * hardcoded, so `HOST` in the unit file and in `.env` was accepted and silently
 * ignored — the service listened on every interface while both the systemd
 * comment and the env file said loopback. A security posture that is only
 * written down is not a security posture.
 *
 * The default stays `0.0.0.0` so a docker-compose run keeps working; the
 * deployed unit sets `HOST=127.0.0.1` and now gets it.
 */
const host = process.env.HOST ?? "0.0.0.0";
const port = Number(process.env.PORT ?? 3000);

const app = buildApp();
app.listen({ host, port }).catch((error) => {
  app.log.error(error);
  process.exit(1);
});
