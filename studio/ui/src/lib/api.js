export function sessionToken() {
  return document.querySelector('meta[name="tk2-session"]')?.content || "";
}

export async function api(path, body, signal) {
  const response = await fetch("/api/" + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "X-TK2-Token": sessionToken(),
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    signal,
  });
  const data = await response.json();
  if (!response.ok) {
    const error = new Error(data.error || "Request failed. Try again.");
    error.details = data;
    throw error;
  }
  return data;
}

export function trackWindow() {
  const clientId =
    globalThis.crypto?.randomUUID?.() || `tk2-${Date.now()}-${Math.random()}`;
  const report = (open) =>
    fetch("/api/client-presence", {
      method: "POST",
      headers: {
        "X-TK2-Token": sessionToken(),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ clientId, open }),
      keepalive: !open,
    }).catch(() => {});
  report(true);
  const timer = setInterval(() => report(true), 15000);
  const close = () => {
    clearInterval(timer);
    report(false);
  };
  window.addEventListener("pagehide", close);
  return () => {
    window.removeEventListener("pagehide", close);
    close();
  };
}

export async function downloadPackage(result) {
  const response = await fetch(
    "/api/download?id=" + encodeURIComponent(result.id),
    { headers: { "X-TK2-Token": sessionToken() } },
  );
  if (!response.ok) throw new Error("Could not download the package.");
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = result.filename;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}
