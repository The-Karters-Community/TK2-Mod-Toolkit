import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { resolve } from "node:path";

const root = fileURLToPath(new URL(".", import.meta.url));
const repo = resolve(root, "../..");

export default defineConfig(({ command, mode }) => {
  let session;
  if (command === "serve" && mode !== "test") {
    try {
      session = JSON.parse(
        readFileSync(resolve(repo, "local/garage-session.json"), "utf8"),
      );
      if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(session.origin) || !session.token)
        throw new Error();
    } catch {
      throw new Error(
        "Start `python launch.py --no-browser` at the repository root before `npm run dev`.",
      );
    }
  }
  const version = readFileSync(
    resolve(repo, "studio/version.py"),
    "utf8",
  ).match(/APP_VERSION\s*=\s*"([^"]+)"/)[1];
  return {
    root,
    plugins: [
      react(),
      tailwindcss(),
      {
        name: "tk2-session",
        transformIndexHtml(html) {
          return session
            ? html
                .replaceAll("__SESSION_TOKEN__", session.token)
                .replaceAll("__APP_VERSION__", version)
            : html;
        },
      },
    ],
    server: {
      host: "127.0.0.1",
      ...(session && {
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
      }),
    },
    build: { outDir: "../web-dist", emptyOutDir: true, sourcemap: false },
    test: {
      environment: "jsdom",
      include: ["src/**/*.test.{js,jsx}"],
      globals: true,
    },
  };
});
