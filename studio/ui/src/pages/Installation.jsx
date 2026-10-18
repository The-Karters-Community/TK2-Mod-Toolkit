import { useEffect, useState } from "react";
import { CheckIcon, MinusIcon } from "@radix-ui/react-icons";
import { useToolkit } from "../lib/useToolkit.jsx";
import { api } from "../lib/api.js";
import { Button, Disclosure, Empty } from "../components/Controls.jsx";

export default function Installation() {
  const { state, busy, action, notify, dirty, sourceDirty } = useToolkit();
  const [path, setPath] = useState(state.game || ""),
    [games, setGames] = useState([]),
    [open, setOpen] = useState({});
  useEffect(() => setPath(state.game || ""), [state.game]);
  const setup = state.setup || {},
    compatibility = state.prebuiltCompatibility;
  const blocked = Boolean(busy);
  function select(name, body) {
    if (dirty || sourceDirty)
      return notify(
        "Save pending edits before changing the game installation.",
        true,
      );
    action(name, body);
  }
  async function scan() {
    try {
      const result = await api("scan-games", {});
      setGames(result.games);
      if (!result.games.length)
        notify("No Steam installation found. Paste the game folder.", true);
    } catch (error) {
      notify(error.message, true);
    }
  }
  const pluginLabel = state.packCurrent
    ? "Plugin up to date"
    : compatibility?.compatible === false
      ? "Build for this installation first"
      : state.installed
        ? "Update plugin"
        : "Install plugin";
  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1>Installation</h1>
          <p className="mt-2 text-sm text-muted">
            Connect the game, verify its loader, then install the plugin.
          </p>
        </div>
        <Button disabled={blocked} onClick={() => action("diagnose")}>
          Check again
        </Button>
      </header>
      <section className="space-y-4 border-b border-line pb-6">
        <h2>
          <span className="mr-2 text-muted">01</span>Game folder
        </h2>
        <label className="block space-y-2 text-sm">
          <span>Installation folder</span>
          <input
            className="field block w-full"
            value={path}
            onChange={(event) => setPath(event.target.value)}
            placeholder="Folder containing TheKarters2.exe"
          />
        </label>
        <div className="flex flex-wrap gap-2">
          <Button
            primary
            disabled={blocked || !path.trim()}
            onClick={() =>
              select("select-game", { path: path.trim(), discardEdits: true })
            }
          >
            Use folder
          </Button>
          <Button disabled={blocked} onClick={() => select("browse-game")}>
            Browse…
          </Button>
          <Button disabled={blocked} onClick={scan}>
            Find in Steam
          </Button>
          <Button
            disabled={blocked || !state.game}
            onClick={() => action("open-game-folder")}
          >
            Open folder
          </Button>
        </div>
        {games.map((game) => (
          <Button
            key={game}
            onClick={() => setPath(game)}
            className="block break-all text-left"
          >
            {game}
          </Button>
        ))}
      </section>
      <section className="space-y-4 border-b border-line pb-6">
        <h2>
          <span className="mr-2 text-muted">02</span>BepInEx loader
        </h2>
        <p className="text-sm">
          {setup.message || "Select the game to check its loader."}
        </p>
        <ul className="grid gap-2 text-sm sm:grid-cols-2">
          {(setup.checks || []).map((check) => (
            <li key={check.name} className="flex items-center gap-2">
              {check.ok ? (
                <CheckIcon className="text-accent" />
              ) : (
                <MinusIcon className="text-warning" />
              )}
              {check.name}
            </li>
          ))}
        </ul>
        <p className="text-xs text-muted">
          Supported: Unity IL2CPP x64 6.0.0-be.788 (5b766a3). After loader
          setup, start the game yourself once, close it, then check again.
        </p>
        {setup.loaderSource && (
          <Button
            disabled={blocked || !state.game}
            onClick={() => action("install-loader")}
          >
            {setup.stage === "install-loader"
              ? "Prepare BepInEx"
              : "Repair BepInEx"}
          </Button>
        )}
      </section>
      <section className="space-y-4">
        <h2>
          <span className="mr-2 text-muted">03</span>Toolkit plugin
        </h2>
        <p className="text-sm text-muted">
          One DLL contains the modules. Close the game before installation.
          Settings reload live after the plugin loads.
        </p>
        {(compatibility?.compatible === false || compatibility?.warning) && (
          <p className="text-sm text-warning">{compatibility.reason}</p>
        )}
        <Button
          primary
          disabled={
            blocked ||
            !setup.ready ||
            state.packCurrent ||
            compatibility?.compatible === false
          }
          onClick={() => action("install")}
        >
          {pluginLabel}
        </Button>
      </section>
      <Disclosure
        title="Other plugins and configs"
        description="Review possible conflicts. Files are renamed, never deleted."
        open={Boolean(open.audit)}
        onChange={(value) => setOpen({ ...open, audit: value })}
      >
        <p className="mb-4 text-xs text-muted">
          A filename cannot prove a conflict. Config files do not load code by
          themselves. Close the game before disabling or restoring a file.
        </p>
        {[
          ["Plugins", state.plugins || [], "toggle-plugin"],
          ["Configs", state.configs || [], "toggle-config"],
        ].map(([title, items, command]) => (
          <section key={title} className="mb-4">
            <h3 className="mb-2">{title}</h3>
            {items.length ? (
              items.map((item) => (
                <div
                  key={item.relative}
                  className="flex items-center justify-between gap-3 border-b border-line py-3"
                >
                  <span className="min-w-0 break-all text-sm">
                    {item.relative}
                    <span className="mt-1 block text-xs text-muted">
                      {item.toolkit
                        ? "Toolkit plugin · protected"
                        : item.enabled
                          ? "Enabled"
                          : "Disabled · available to restore"}
                    </span>
                  </span>
                  {!item.toolkit && (
                    <Button
                      disabled={blocked}
                      onClick={() => action(command, { path: item.relative })}
                    >
                      {item.enabled ? "Disable" : "Restore"}
                    </Button>
                  )}
                </div>
              ))
            ) : (
              <p className="text-sm text-muted">None found.</p>
            )}
          </section>
        ))}
      </Disclosure>
      <Disclosure
        title="Build output and diagnostics"
        open={Boolean(open.logs)}
        onChange={(value) => setOpen({ ...open, logs: value })}
      >
        <pre>
          {state.buildLog ||
            (state.logs || []).join("\n") ||
            "No builds in this session."}
        </pre>
        <h3 className="my-3">Runtime log</h3>
        <pre>
          {[
            ...(setup.runtimeErrors || []),
            ...(setup.runtimeWarnings || []),
          ].join("\n") || "No errors or warnings in the available log."}
        </pre>
      </Disclosure>
      <Disclosure
        title="Backups and restore"
        open={Boolean(open.backups)}
        onChange={(value) => setOpen({ ...open, backups: value })}
      >
        {state.backups?.length ? (
          state.backups.map((backup) => (
            <div
              key={backup.id}
              className="flex items-center justify-between gap-3 border-b border-line py-3"
            >
              <div className="min-w-0">
                <p className="break-all text-sm">{backup.file}</p>
                <p className="text-xs text-muted">
                  {backup.id} ·{" "}
                  {backup.existed
                    ? "Restore previous contents"
                    : "Remove newly installed file"}
                </p>
              </div>
              <Button
                disabled={blocked}
                onClick={() => action("restore", { id: backup.id })}
              >
                Restore
              </Button>
            </div>
          ))
        ) : (
          <Empty title="No backups yet">
            Installation and file changes create reversible backups here.
          </Empty>
        )}
      </Disclosure>
    </div>
  );
}
