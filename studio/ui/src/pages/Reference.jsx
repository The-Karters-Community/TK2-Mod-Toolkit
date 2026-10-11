import { useEffect, useRef, useState } from "react";
import { api } from "../lib/api.js";
import { useToolkit } from "../lib/useToolkit.jsx";
import { Button, Empty } from "../components/Controls.jsx";

const topics = {
  important: "Important modding APIs",
  camera: "Camera",
  driving: "Kart & driving",
  boost: "Boost & items",
  health: "Health",
  interface: "Interface & audio",
  race: "Race & input",
  all: "All indexed functions",
};

// Both explorers use the same request/selection component. Only their API contract differs.
function Explorer({ native = false, onSource }) {
  const { notify } = useToolkit();
  const [query, setQuery] = useState(""),
    [topic, setTopic] = useState("important"),
    [recovered, setRecovered] = useState(false);
  const [result, setResult] = useState(null),
    [selected, setSelected] = useState(null),
    [detail, setDetail] = useState(null),
    [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const sequence = useRef(0),
    selection = useRef(0),
    active = useRef(false),
    controller = useRef(null);
  const rows = native ? result?.results || [] : result?.methods || [];
  async function search(more = false) {
    if (more && (active.current || result?.next == null)) return;
    controller.current?.abort();
    const request = new AbortController();
    controller.current = request;
    const id = ++sequence.current;
    active.current = true;
    setLoading(true);
    setError("");
    if (!more) {
      setResult(null);
      setSelected(null);
      setDetail(null);
      ++selection.current;
    }
    const params = new URLSearchParams({
      q: query,
      offset: more ? result.next : 0,
      topic,
      recovered,
    });
    try {
      const next = await api(
        (native ? "pseudocode?" : "functions?") + params,
        undefined,
        request.signal,
      );
      if (id !== sequence.current) return;
      const field = native ? "results" : "methods";
      setResult((previous) => ({
        ...next,
        [field]: more
          ? [...(previous?.[field] || []), ...(next[field] || [])]
          : next[field],
      }));
    } catch (failure) {
      if (failure.name !== "AbortError" && id === sequence.current)
        setError(failure.message);
    } finally {
      if (id === sequence.current) {
        active.current = false;
        setLoading(false);
      }
    }
  }
  async function choose(row) {
    const id = ++selection.current;
    setSelected(row);
    setDetail(null);
    try {
      const value = await api(
        (native ? "pseudocode?id=" : "function?id=") +
          encodeURIComponent(row.id),
      );
      if (id === selection.current) setDetail(value);
    } catch (failure) {
      if (id === selection.current) notify(failure.message, true);
    }
  }
  useEffect(() => {
    if (native) return;
    const timer = setTimeout(() => search(), 300);
    return () => clearTimeout(timer);
  }, [query, topic, recovered]);
  useEffect(
    () => () => {
      controller.current?.abort();
      ++sequence.current;
      ++selection.current;
    },
    [],
  );
  return (
    <section className="space-y-4">
      <h2>{native ? "Native pseudocode" : "Game API reference"}</h2>
      <p className="max-w-3xl text-sm text-muted">
        {native
          ? "Search your local Ghidra exports. This is approximate native output, not original C#. First use may build a local index."
          : "Browse signatures and reviewed reconstructions. A declaration lists types and parameters; it does not contain executable game logic."}
      </p>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          search();
        }}
      >
        <label className="min-w-0 flex-1 space-y-2 text-sm">
          <span>
            {native ? "Find native behavior" : "Find a game function"}
          </span>
          <input
            className="field block w-full"
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
        {!native && (
          <label className="space-y-2 text-sm">
            <span>Category</span>
            <select
              className="field block"
              value={topic}
              onChange={(event) => setTopic(event.target.value)}
            >
              {Object.entries(topics).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
        )}
        <Button disabled={loading} onClick={() => search()}>
          Search
        </Button>
      </form>
      {!native && (
        <label className="flex items-center gap-2 text-sm">
          <input
            className="checkbox"
            type="checkbox"
            checked={recovered}
            onChange={(event) => setRecovered(event.target.checked)}
          />
          Has reviewed C# logic
        </label>
      )}
      {error && (
        <div role="alert" className="error-box">
          {error}
          <Button className="ml-3" onClick={() => search()}>
            Retry
          </Button>
        </div>
      )}
      <p className="text-xs text-muted" aria-live="polite">
        {loading
          ? "Loading results…"
          : result
            ? `${result.total?.toLocaleString() || 0} results · ${result.indexed?.toLocaleString() || 0} indexed${native ? "" : ` · ${result.reconstructed || 0} full methods · ${result.behaviorSlices || 0} behavior slices`}. ${result.message || ""}`
            : "Enter a query to search."}
      </p>
      <div className="grid min-w-0 gap-4 lg:grid-cols-[minmax(230px,1fr)_minmax(0,1.6fr)]">
        <div
          className="max-h-[520px] overflow-auto rounded-lg border border-line"
          aria-label="Search results"
          aria-busy={loading}
          onScroll={(event) => {
            const list = event.currentTarget;
            if (list.scrollHeight - list.scrollTop - list.clientHeight < 150)
              search(true);
          }}
        >
          {rows.map((row) => (
            <button
              key={row.id}
              type="button"
              className="result-row"
              aria-pressed={selected?.id === row.id}
              onClick={() => choose(row)}
            >
              <strong className="block break-all text-sm">
                {row.label || row.name || row.signature}
              </strong>
              <small className="mt-1 block break-all text-muted">
                {native ? row.address : row.type}
              </small>
              <span className="mt-1 block text-xs text-muted">
                {native
                  ? row.reconstruction
                    ? (row.reconstruction.kind === "behavior slice"
                        ? "Reviewed C# behavior slice · "
                        : "Reviewed C# method reconstruction · ") + row.snippet
                    : row.snippet
                  : row.source
                    ? "Reconstructed C# method"
                    : row.behaviorSlice
                      ? "Partial C# behavior slice · " + row.behaviorSlice.behavior
                      : "API declaration"}
              </span>
            </button>
          ))}
          {loading && (
            <div className="space-y-2 p-3">
              <div className="skeleton h-10" />
              <div className="skeleton h-10" />
            </div>
          )}
          {!loading && result && !rows.length && (
            <Empty title="No results">Try another query or category.</Empty>
          )}
          {result?.next != null && (
            <Button
              className="m-3"
              disabled={loading}
              onClick={() => search(true)}
            >
              Load more
            </Button>
          )}
        </div>
        <div className="min-w-0 space-y-3">
          <h3 className="break-all">
            {selected ? selected.label || selected.name : "Choose a result"}
          </h3>
          {selected && !detail ? (
            <div className="skeleton h-32" aria-busy="true" />
          ) : detail ? (
            <>
              <p className="text-xs text-muted">
                {native
                  ? `VA 0x${selected.address}`
                  : `${detail.type} · ${detail.status}. ${detail.confidence || ""}`}
              </p>
              <pre className="max-h-[500px]">
                {native
                  ? detail.code +
                    (detail.truncated ? "\n\n" + detail.message : "")
                  : detail.declaration}
              </pre>
              {!native && detail.source && (
                <Button onClick={() => onSource(detail.source)}>
                  Open reconstructed source
                </Button>
              )}
              {!native && !detail.source && detail.behaviorSlice && (
                <div className="space-y-2 rounded-lg border border-line p-3">
                  <p className="text-sm">Partial C# behavior slice: {detail.behaviorSlice.behavior}</p>
                  <p className="text-xs text-muted">{detail.behaviorSlice.confidence}</p>
                  <Button onClick={() => onSource(detail.behaviorSlice.source)}>
                    Open reconstructed source
                  </Button>
                </div>
              )}
              {native && detail.reconstruction && (
                <div className="space-y-2 rounded-lg border border-line p-3">
                  <p className="text-sm">
                    {detail.reconstruction.kind === "behavior slice"
                      ? "Reviewed C# behavior slice: "
                      : "Reviewed C# method reconstruction: "}
                    {detail.reconstruction.method}
                  </p>
                  <p className="text-xs text-muted">
                    {detail.reconstruction.confidence}
                  </p>
                  <Button onClick={() => onSource(detail.reconstruction.source)}>
                    Open reconstructed source
                  </Button>
                </div>
              )}
            </>
          ) : (
            <p className="text-sm text-muted">
              Select a result to inspect its read-only reference.
            </p>
          )}
        </div>
      </div>
    </section>
  );
}

export default function Reference({ onSource }) {
  return (
    <div className="space-y-8">
      <Explorer onSource={onSource} />
      <div className="border-t border-line pt-6">
        <Explorer native onSource={onSource} />
      </div>
    </div>
  );
}
