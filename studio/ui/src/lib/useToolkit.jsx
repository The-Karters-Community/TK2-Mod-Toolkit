import { createContext, useContext, useEffect, useRef, useState } from "react";
import { api, trackWindow } from "./api.js";
import {
  acknowledge,
  createDraft,
  editCount,
  editDraft,
  effectiveState,
  liveEligible,
  snapshot,
} from "./settings.js";

const Toolkit = createContext(null);
export const useToolkit = () => useContext(Toolkit);

export function ToolkitProvider({ children }) {
  const [draft, setDraft] = useState(() => createDraft(null));
  const [busy, setBusy] = useState("");
  const [message, setMessage] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [live, setLive] = useState(
    () => localStorage.getItem("tk2-live-preview") === "true",
  );
  const [source, setSource] = useState(null);
  const [moduleId, setModuleId] = useState("all");
  const [page, setPage] = useState(() =>
    ["mods", "workshop", "installation"].includes(location.hash.slice(1))
      ? location.hash.slice(1)
      : "mods",
  );
  const current = useRef(draft),
    working = useRef(""),
    sourceRef = useRef(source);
  const refreshId = useRef(0),
    sourceId = useRef(0),
    previewRevision = useRef(-1);
  current.current = draft;
  sourceRef.current = source;
  const state = effectiveState(draft),
    dirty = editCount(draft),
    sourceDirty = Boolean(source && source.content !== source.base);
  const notify = (text, error = false) => setMessage({ text, error });
  const update = (fn) => {
    const next = fn(current.current);
    current.current = next;
    setDraft(next);
  };

  async function refresh() {
    const id = ++refreshId.current;
    const server = await api("state");
    if (id === refreshId.current)
      update((previous) => ({ ...previous, server }));
  }

  async function loadSource(file, discard = false) {
    const existing = sourceRef.current;
    if (!discard && existing && existing.content !== existing.base) {
      notify(
        "Save C# before switching files, or reload to discard your edits.",
        true,
      );
      return false;
    }
    const id = ++sourceId.current;
    try {
      const result = await api("source?file=" + encodeURIComponent(file));
      if (id !== sourceId.current) return false;
      setSource({ ...result, base: result.content });
      return true;
    } catch (error) {
      notify(error.message, true);
      return false;
    }
  }

  async function saveSource() {
    const value = sourceRef.current;
    if (!value || value.content === value.base) return;
    const result = await api("save-source", {
      file: value.file,
      hash: value.hash,
      content: value.content,
    });
    setSource((latest) =>
      latest?.file === value.file
        ? { ...latest, hash: result.hash, base: value.content }
        : latest,
    );
  }

  async function action(name, body = {}, after) {
    if (working.current) return;
    working.current = name;
    setBusy(name);
    if (name !== "settings")
      notify(name.startsWith("build") ? "Building the plugin…" : "Working…");
    try {
      if (name.startsWith("build")) await saveSource();
      const result = await api(name, body);
      await refresh().catch(() => {});
      if (after) await after(result, effectiveState(current.current));
      notify(result.message || "Done.");
      return result;
    } catch (error) {
      notify(error.message, true);
    } finally {
      working.current = "";
      setBusy("");
    }
  }

  async function saveSettings(eligibleOnly = false) {
    if (working.current || !current.current.server) return;
    const effective = effectiveState(current.current);
    const saved = snapshot(
      current.current,
      eligibleOnly ? (key) => liveEligible(effective, key) : undefined,
    );
    if (
      !Object.values(saved.revisions).some(
        (values) => Object.keys(values).length,
      )
    )
      return;
    working.current = "settings";
    setBusy("settings");
    setSaveError(null);
    try {
      const result = await api("settings", saved.body);
      update((previous) => acknowledge(previous, saved, result));
      await refresh().catch(() => {});
    } catch (error) {
      const conflicts =
        error.details?.conflicts || error.details?.conflictKeys || [];
      setSaveError({
        text: error.message + " Your unsaved edits are kept.",
        conflicts: Array.isArray(conflicts)
          ? conflicts
          : Object.keys(conflicts),
      });
    } finally {
      working.current = "";
      setBusy("");
    }
  }

  function change(field, key, value) {
    setSaveError(null);
    update((previous) =>
      editDraft(previous, field, key, value, working.current === "settings"),
    );
  }

  function navigate(next) {
    location.hash = next;
    setPage(next);
  }
  function editModule(id) {
    if (sourceDirty)
      return notify("Save your C# before switching modules.", true);
    const files = state.moduleSources?.[id];
    if (!files?.length)
      return notify("Source is not included for this module.", true);
    setModuleId(id);
    navigate("workshop");
    loadSource(files[0]);
  }

  useEffect(() => {
    const stop = trackWindow();
    refresh().catch((error) => notify(error.message, true));
    const timer = setInterval(() => {
      if (!working.current && !document.hidden) refresh().catch(() => {});
    }, 5000);
    const hash = () => {
      const value = location.hash.slice(1);
      if (["mods", "workshop", "installation"].includes(value)) setPage(value);
    };
    window.addEventListener("hashchange", hash);
    return () => {
      stop();
      clearInterval(timer);
      window.removeEventListener("hashchange", hash);
      ++refreshId.current;
    };
  }, []);

  useEffect(() => {
    localStorage.setItem("tk2-live-preview", String(live));
    if (
      !live ||
      !dirty ||
      busy ||
      previewRevision.current >= draft.revision ||
      !state
    )
      return;
    const timer = setTimeout(() => {
      previewRevision.current = current.current.revision;
      saveSettings(true);
    }, 600);
    return () => clearTimeout(timer);
  }, [live, dirty, draft.revision, busy]);

  useEffect(() => {
    const beforeUnload = (event) => {
      if (dirty || sourceDirty || busy) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    const keyboard = (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        if (busy) return;
        if (page === "workshop" && sourceDirty)
          action(
            "save-source",
            { file: source.file, hash: source.hash, content: source.content },
            (result) =>
              setSource((latest) => ({
                ...latest,
                hash: result.hash,
                base: source.content,
              })),
          );
        else if (dirty) saveSettings();
      }
    };
    window.addEventListener("beforeunload", beforeUnload);
    window.addEventListener("keydown", keyboard);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      window.removeEventListener("keydown", keyboard);
    };
  }, [dirty, sourceDirty, busy, page, source]);

  return (
    <Toolkit.Provider
      value={{
        state,
        draft,
        busy,
        message,
        notify,
        clearMessage: () => setMessage(null),
        dirty,
        saveError,
        live,
        setLive,
        change,
        saveSettings,
        refresh,
        action,
        page,
        navigate,
        source,
        setSource,
        sourceDirty,
        loadSource,
        saveSource,
        moduleId,
        setModuleId,
        editModule,
        reloadConflicts: async () => {
          update((previous) => ({
            ...previous,
            edits: Object.fromEntries(
              Object.entries(previous.edits).map(([field, edits]) => [
                field,
                Object.fromEntries(
                  Object.entries(edits).filter(
                    ([key]) => !saveError.conflicts.includes(key),
                  ),
                ),
              ]),
            ),
          }));
          setSaveError(null);
          await refresh();
        },
      }}
    >
      {children}
    </Toolkit.Provider>
  );
}
