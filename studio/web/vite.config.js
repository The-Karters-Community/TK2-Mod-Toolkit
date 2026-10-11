import { defineConfig } from "vite";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { htmlTemplates } from "./tooling/html-templates.js";

const webRoot = fileURLToPath(new URL(".", import.meta.url));
const repoRoot = resolve(webRoot, "../..");

export default defineConfig(({ command }) => {
  let session;
  if (command === "serve") {
    try {
      session = JSON.parse(
        readFileSync(resolve(repoRoot, "local/garage-session.json"), "utf8"),
      );
      if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(session.origin) || !session.token)
        throw new Error();
    } catch {
      throw new Error(
        "Start `python launch.py --no-browser` in the repository before `npm run dev`.",
      );
    }
  }
  const version = readFileSync(
    resolve(repoRoot, "studio/version.py"),
    "utf8",
  ).match(/APP_VERSION\s*=\s*"([^"]+)"/)[1];
  return {
    plugins: [htmlTemplates(webRoot, session, version)],
    server: session
      ? {
          host: "127.0.0.1",
          proxy: Object.fromEntries(
            ["/api", "/assets"].map((path) => [
              path,
              {
                target: session.origin,
                changeOrigin: true,
                headers: { Origin: session.origin },
              },
            ]),
          ),
        }
      : undefined,
    root: fileURLToPath(new URL(".", import.meta.url)),
    base: "/",
    build: {
      outDir: "../web-dist",
      emptyOutDir: true,
      assetsDir: "assets",
    },
  };
});
