import { readFileSync } from "node:fs";
import { dirname, resolve, relative } from "node:path";

const include = /<!--\s*@include\s+([^\s]+)\s*-->/g;

export function renderTemplates(file, root, stack = []) {
  const resolved = resolve(file);
  const local = relative(root, resolved);
  if (local.startsWith("..") || !resolved.endsWith(".html")) {
    throw new Error(
      "Frontend templates must stay inside the web source directory.",
    );
  }
  if (stack.includes(resolved))
    throw new Error("Circular frontend template include.");
  const source = readFileSync(resolved, "utf8");
  return source.replace(include, (_, name) =>
    renderTemplates(resolve(dirname(resolved), name), root, [
      ...stack,
      resolved,
    ]),
  );
}

export function htmlTemplates(root, session, version) {
  return {
    name: "tk2-html-templates",
    transformIndexHtml: {
      order: "pre",
      handler(_, context) {
        let html = renderTemplates(resolve(root, "index.html"), root);
        if (context.server && session) {
          html = html
            .replaceAll("__SESSION_TOKEN__", session.token)
            .replaceAll("__APP_VERSION__", version);
        }
        return html;
      },
    },
    handleHotUpdate(context) {
      if (context.file.endsWith(".html"))
        context.server.ws.send({ type: "full-reload" });
    },
  };
}
