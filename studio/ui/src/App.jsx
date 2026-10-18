import { lazy, Suspense, useEffect, useState } from "react";
import {
  CubeIcon,
  CodeIcon,
  DownloadIcon,
  SunIcon,
  MoonIcon,
  Cross2Icon,
  PlayIcon,
} from "@radix-ui/react-icons";
import { useToolkit } from "./lib/useToolkit.jsx";
import { Button } from "./components/Controls.jsx";

const Mods = lazy(() => import("./pages/Mods.jsx"));
const Workshop = lazy(() => import("./pages/Workshop.jsx"));
const Installation = lazy(() => import("./pages/Installation.jsx"));
const pages = {
  mods: { name: "Mod library", icon: CubeIcon, component: Mods },
  workshop: { name: "Workshop", icon: CodeIcon, component: Workshop },
  installation: {
    name: "Installation",
    icon: DownloadIcon,
    component: Installation,
  },
};

export default function App() {
  const {
    state,
    busy,
    dirty,
    sourceDirty,
    page,
    navigate,
    action,
    message,
    clearMessage,
    notify,
    saveSettings,
    saveError,
    reloadConflicts,
  } = useToolkit();
  const [theme, setTheme] = useState(
    () =>
      localStorage.getItem("tk2-theme") ||
      (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"),
  );
  const [visited, setVisited] = useState(() => new Set([page]));
  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("tk2-theme", theme);
  }, [theme]);
  useEffect(
    () => setVisited((previous) => new Set([...previous, page])),
    [page],
  );
  const version = document.querySelector('meta[name="tk2-version"]')?.content;
  const release = state?.release || {},
    ready = state?.setup || {};
  const updatePlugin =
    ready.ready &&
    state?.installed &&
    !state?.packCurrent &&
    state?.prebuiltCompatibility?.compatible !== false;
  function launch() {
    if (dirty || sourceDirty)
      return notify("Save your pending edits before launching the game.", true);
    action("launch-game");
  }
  return (
    <div className="app-shell">
      <aside className="app-rail">
        <a
          href="#mods"
          aria-label="Toolkit home"
          onClick={() => navigate("mods")}
          className="block"
        >
          <img src="/assets/logo.png" alt="The Karters 2" className="w-36" />
        </a>
        <div className="hidden text-xs font-semibold uppercase tracking-widest text-muted md:block">
          Community toolkit
        </div>
        <nav
          aria-label="Main navigation"
          className="flex flex-wrap gap-1 md:flex-col md:gap-2"
        >
          {Object.entries(pages).map(([key, { name, icon: Icon }]) => (
            <Button
              key={key}
              className={"nav-button " + (page === key ? "selected" : "")}
              aria-current={page === key ? "page" : undefined}
              onClick={() => navigate(key)}
            >
              <Icon className="h-4 w-4" />
              {name}
            </Button>
          ))}
        </nav>
        <div className="mt-auto flex items-center gap-3 md:block">
          <Button
            aria-label={
              "Switch to " + (theme === "dark" ? "light" : "dark") + " mode"
            }
            onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
            className="filter"
          >
            {theme === "dark" ? <SunIcon /> : <MoonIcon />}
            <span className="hidden md:inline">
              {theme === "dark" ? "Light" : "Dark"} mode
            </span>
          </Button>
          <div className="mt-0 text-xs leading-6 text-muted md:mt-6">
            <span>Toolkit {version}</span>
            <br className="hidden md:block" />
            <span className="hidden md:block">The Karters 2</span>
          </div>
        </div>
      </aside>
      <main className="app-main">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
          <span className="text-xs uppercase tracking-widest text-muted">
            Toolkit / {pages[page].name}
          </span>
          <div className="flex flex-wrap items-center gap-2">
            {release.state === "available" && (
              <Button
                primary
                disabled={Boolean(busy)}
                onClick={() =>
                  action(release.canInstall ? "update-toolkit" : "open-release")
                }
              >
                Update toolkit · {release.version}
              </Button>
            )}
            {updatePlugin ? (
              <Button
                primary
                disabled={Boolean(busy)}
                onClick={() => action("install")}
              >
                Update plugin
              </Button>
            ) : (
              <span className="text-xs text-muted">
                {state?.packCurrent
                  ? "Plugin up to date"
                  : state?.installed
                    ? "Plugin update needed"
                    : "Plugin not installed"}
              </span>
            )}
          </div>
        </div>
        {!state ? (
          <div
            aria-busy="true"
            aria-label="Loading toolkit"
            className="space-y-4"
          >
            <div className="skeleton h-10 w-48" />
            <div className="skeleton h-24" />
            <div className="skeleton h-32" />
          </div>
        ) : (
          <Suspense
            fallback={<div aria-busy="true" className="skeleton h-32" />}
          >
            {Object.entries(pages).map(
              ([key, { component: Page }]) =>
                visited.has(key) && (
                  <div key={key} hidden={page !== key}>
                    <Page />
                  </div>
                ),
            )}
          </Suspense>
        )}
        {saveError && (
          <div role="alert" className="error-box mt-4">
            <p>{saveError.text}</p>
            {saveError.conflicts.length > 0 && (
              <Button
                className="mt-2"
                disabled={Boolean(busy)}
                onClick={reloadConflicts}
              >
                Reload conflicting values
              </Button>
            )}
          </div>
        )}
        {page === "mods" && (
          <footer className="save-bar">
            <div className="text-sm text-muted" aria-live="polite">
              {dirty
                ? `${dirty} unsaved setting${dirty === 1 ? "" : "s"}`
                : "All changes saved."}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button disabled={Boolean(busy) || !state?.game} onClick={launch}>
                <PlayIcon />
                Launch game
              </Button>
              <Button
                primary
                disabled={Boolean(busy) || !dirty}
                onClick={() => saveSettings()}
              >
                {busy === "settings" ? "Saving…" : "Save changes"}
              </Button>
            </div>
          </footer>
        )}
      </main>
      {message && (
        <div
          role={message.error ? "alert" : "status"}
          className={"notification " + (message.error ? "error-box" : "")}
        >
          <span>{message.text}</span>
          <Button
            aria-label="Dismiss notification"
            onClick={clearMessage}
            className="px-2"
          >
            <Cross2Icon />
          </Button>
        </div>
      )}
    </div>
  );
}
