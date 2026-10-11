const token = document.querySelector('meta[name="tk2-session"]').content;

export async function api(path, body) {
  const response = await fetch("/api/" + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "X-TK2-Token": token,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const data = await response.json();
  if (!response.ok) {
    const error = new Error(data.error || "Request failed. Try again.");
    error.details = data;
    throw error;
  }
  return data;
}

const windowClientId =
  globalThis.crypto?.randomUUID?.() ||
  `tk2-${Date.now()}-${Math.random().toString(36).slice(2)}`;

export function reportWindowPresence(open) {
  fetch("/api/client-presence", {
    method: "POST",
    headers: { "X-TK2-Token": token, "Content-Type": "application/json" },
    body: JSON.stringify({ clientId: windowClientId, open }),
    ...(!open ? { keepalive: true } : {}),
  }).catch(() => {});
}

export function startWindowPresence() {
  reportWindowPresence(true);
  const windowPresenceTimer = setInterval(
    () => reportWindowPresence(true),
    15000,
  );
  window.addEventListener("pagehide", () => {
    clearInterval(windowPresenceTimer);
    reportWindowPresence(false);
  });
}
