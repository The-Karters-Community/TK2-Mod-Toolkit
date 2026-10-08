"""Native Windows GUI. Worker threads never call tkinter or Unity."""
from __future__ import annotations

import configparser
import json
import queue
import threading
import tkinter as tk
from tkinter import ttk, filedialog, messagebox, simpledialog
from tkinter.scrolledtext import ScrolledText
from pathlib import Path
from . import core


class Studio(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("TK2 Mod Studio — local authoring workspace")
        self.geometry("1200x820")
        self.minsize(960, 680)
        self.option_add("*Font", "{Segoe UI} 10")
        self.queue = queue.Queue()
        self.busy = False
        self.catalog = None
        self.editor_path = None
        self.config_path = None
        self.config_original = None
        self.project = core.ROOT / "plugins/TK2.Customization/TK2.Customization.csproj"
        self.artifact = core.ROOT / "artifacts/TK2.Customization"
        settings = core.ROOT / "local/studio.json"
        saved = json.loads(settings.read_text()) if settings.exists() else {}
        self.game_var = tk.StringVar(value=saved.get("game", str(core.DEFAULT_GAME)))
        self.status = tk.StringVar(value="Ready · local workspace · starter features require runtime testing")
        header = ttk.Frame(self, padding=14)
        header.pack(fill="x")
        ttk.Label(header, text="TK2 MOD STUDIO", font=("Segoe UI", 20, "bold")).pack(side="left")
        ttk.Label(header, text="Inspect → edit C# → build → deploy", foreground="#666666").pack(side="right")
        location = ttk.Frame(self, padding=(14, 0, 14, 12))
        location.pack(fill="x")
        ttk.Label(location, text="Game folder").pack(side="left", padx=(0, 8))
        ttk.Entry(location, textvariable=self.game_var).pack(side="left", fill="x", expand=True)
        ttk.Button(location, text="Browse", command=self.choose_game).pack(side="left", padx=6)
        ttk.Button(location, text="Inspect installation", command=self.inspect).pack(side="left")
        self.tabs = ttk.Notebook(self)
        self.tabs.pack(fill="both", expand=True, padx=14)
        self.tab_frames = {}
        for label in ("Installation", "Code browser", "C# editor", "Mods & backups", "Customization", "Configs", "Logs & roadmap"):
            frame = ttk.Frame(self.tabs, padding=10)
            self.tabs.add(frame, text=label)
            self.tab_frames[label] = frame
        self.build_installation()
        self.build_browser()
        self.build_editor()
        self.build_mods()
        self.build_customization()
        self.build_configs()
        self.build_logs()
        ttk.Label(self, textvariable=self.status, padding=(14, 8)).pack(fill="x")
        self.protocol("WM_DELETE_WINDOW", self.close)
        self.bind_all("<Control-s>", lambda e: self.save_active())
        self.after(100, self.drain)
        self.load_catalog()
        self.refresh_sources()
        self.refresh_projects()

    def game(self):
        return Path(self.game_var.get()).resolve()

    def report(self, message):
        self.queue.put(("log", str(message)))

    def run_task(self, work, done=lambda result: None):
        if self.busy:
            messagebox.showinfo("Task running", "Wait for the current operation to finish.")
            return
        self.busy = True
        self.status.set("Working… details appear in Logs & roadmap")
        def execute():
            try: self.queue.put(("done", (done, work())))
            except Exception as error: self.queue.put(("error", str(error)))
        threading.Thread(target=execute, daemon=True).start()

    def drain(self):
        try:
            while True:
                kind, payload = self.queue.get_nowait()
                if kind == "log":
                    self.log_text.configure(state="normal")
                    self.log_text.insert("end", payload + "\n")
                    self.log_text.see("end")
                    self.log_text.configure(state="disabled")
                elif kind == "done":
                    self.busy = False
                    done, result = payload
                    self.status.set("Operation completed")
                    try: done(result)
                    except Exception as error: messagebox.showerror("Display error", str(error))
                else:
                    self.busy = False
                    self.status.set("Operation failed · see logs")
                    self.report(payload)
                    messagebox.showerror("Operation failed", payload)
        except queue.Empty:
            pass
        self.after(100, self.drain)

    @staticmethod
    def set_text(widget, text, readonly=True):
        widget.configure(state="normal")
        widget.delete("1.0", "end")
        widget.insert("1.0", text)
        widget.edit_modified(False)
        if readonly: widget.configure(state="disabled")

    def choose_game(self):
        chosen = filedialog.askdirectory(initialdir=self.game())
        if chosen:
            self.game_var.set(chosen)
            core.write_json(core.ROOT / "local/studio.json", {"game": chosen})
            self.inspect()

    def build_installation(self):
        frame = self.tab_frames["Installation"]
        ttk.Label(frame, text="Compatibility and installation evidence", font=("Segoe UI", 14, "bold")).pack(anchor="w")
        ttk.Label(frame, text="Hashes identify this installation. Log entries describe the last recorded run.").pack(anchor="w", pady=8)
        self.install_text = ScrolledText(frame, wrap="word", font=("Consolas", 10), state="disabled")
        self.install_text.pack(fill="both", expand=True)
        self.set_text(self.install_text, "Click Inspect installation to identify the game, loader, current interop and logged failures.\n\n"
                      "No game launch is needed to inspect or build.\n\n"
                      "Use Code browser for metadata declarations and Ghidra pseudocode; C# editor for executable mod source.")

    def inspect(self):
        game = self.game()
        def show(result):
            core.write_json(core.ROOT / "local/installation.json", result)
            self.set_text(self.install_text, json.dumps(result, indent=2))
            self.refresh_mods()
            self.refresh_configs()
            self.tabs.select(self.tab_frames["Installation"])
        self.run_task(lambda: core.diagnose(game), show)

    def build_browser(self):
        frame = self.tab_frames["Code browser"]
        toolbar = ttk.Frame(frame)
        toolbar.pack(fill="x")
        self.search = tk.StringVar()
        ttk.Label(toolbar, text="Find type or file").pack(side="left")
        search = ttk.Entry(toolbar, textvariable=self.search, width=32)
        search.pack(side="left", padx=6)
        search.bind("<Return>", lambda e: self.refresh_browser())
        ttk.Button(toolbar, text="Search", command=self.refresh_browser).pack(side="left")
        ttk.Button(toolbar, text="Index dump.cs", command=self.index).pack(side="left", padx=6)
        ttk.Button(toolbar, text="Export Ghidra", command=self.ghidra_export).pack(side="left")
        ttk.Button(toolbar, text="Recover mod C#", command=self.managed_export).pack(side="left", padx=6)
        self.browser_mode = tk.StringVar(value="Game declarations")
        combo = ttk.Combobox(frame, textvariable=self.browser_mode, state="readonly",
                             values=("Game declarations", "Native pseudocode", "Recovered mod C#", "SDK documentation"))
        combo.pack(fill="x", pady=8)
        combo.bind("<<ComboboxSelected>>", lambda e: self.refresh_browser())
        self.browser_note = tk.StringVar(value="")
        ttk.Label(frame, textvariable=self.browser_note, wraplength=1050).pack(anchor="w", pady=(0, 6))
        split = ttk.Panedwindow(frame, orient="horizontal")
        split.pack(fill="both", expand=True)
        left = ttk.Frame(split)
        self.browser_list = tk.Listbox(left, width=40, exportselection=False)
        self.browser_list.pack(side="left", fill="both", expand=True)
        scroll = ttk.Scrollbar(left, command=self.browser_list.yview)
        scroll.pack(side="right", fill="y")
        self.browser_list.configure(yscrollcommand=scroll.set)
        self.browser_list.bind("<<ListboxSelect>>", self.show_browser_item)
        self.browser_text = ScrolledText(split, wrap="none", font=("Consolas", 10), state="disabled")
        split.add(left, weight=1)
        split.add(self.browser_text, weight=3)

    def load_catalog(self):
        path = core.ROOT / "local/catalog.json"
        if path.exists():
            self.catalog = json.loads(path.read_text(encoding="utf-8"))
            source = Path(self.catalog["source"])
            if not source.is_file() or core.sha256(source) != self.catalog["sha256"]:
                self.catalog = None
                self.report("Catalog source changed. Re-index dump.cs before browsing.")
        self.refresh_browser()

    def refresh_browser(self):
        query = self.search.get().lower()
        mode = self.browser_mode.get()
        self.browser_list.delete(0, "end")
        self.browser_items = []
        if mode == "Game declarations":
            entries = self.catalog["types"] if self.catalog else []
            entries = [e for e in entries if query in (e["namespace"] + "." + e["name"]).lower()]
            self.browser_items = entries[:500]
            for entry in self.browser_items:
                self.browser_list.insert("end", (entry["namespace"] + "." if entry["namespace"] else "") + entry["name"])
            self.browser_note.set(f"{len(entries):,} matches · showing first 500. Metadata declarations have stub bodies; they are not recovered game code.")
        else:
            base, extension = {"Native pseudocode": (core.ROOT / "local/ghidra/pseudocode", "*.c"),
                               "Recovered mod C#": (core.ROOT / "local/managed", "*.cs"),
                               "SDK documentation": (core.ROOT / "docs", "*.md")}[mode]
            self.browser_items = [p for p in sorted(base.rglob(extension)) if query in str(p.relative_to(base)).lower()][:500]
            for path in self.browser_items: self.browser_list.insert("end", str(path.relative_to(base)))
            note = "Approximate C-like native logic; original C# cannot be recovered automatically." if mode == "Native pseudocode" else "Readable local managed decompilation; not automatically a rebuildable project." if mode == "Recovered mod C#" else "Project plan and evidence."
            self.browser_note.set(f"{len(self.browser_items)} files · {note}")

    def show_browser_item(self, event=None):
        selection = self.browser_list.curselection()
        if not selection: return
        item = self.browser_items[selection[0]]
        try:
            text = core.read_declaration(self.catalog, item) if isinstance(item, dict) else item.read_text(encoding="utf-8", errors="replace")
            self.set_text(self.browser_text, text)
        except Exception as error: messagebox.showerror("Read failed", str(error))

    def index(self):
        default = self.game() / "Il2CppDumperOutput/dump.cs"
        path = filedialog.askopenfilename(initialdir=default.parent, title="Select Il2CppDumper dump.cs", filetypes=[("C# dump", "*.cs")])
        if path:
            self.run_task(lambda: core.index_dump(Path(path), core.ROOT / "local/catalog.json"), lambda result: self.load_catalog())

    def ghidra_export(self):
        home = filedialog.askdirectory(title="Select Ghidra home (contains support/analyzeHeadless.bat)", initialdir=core.ROOT.parent)
        if not home: return
        project = filedialog.askopenfilename(title="Select existing Ghidra project", initialdir=core.ROOT.parent / "ghidraOutput", filetypes=[("Ghidra project", "*.gpr")])
        if project:
            self.run_task(lambda: core.export_ghidra(Path(home), Path(project), self.report),
                          lambda result: (self.report(json.dumps(result, indent=2)), self.refresh_browser()))

    def managed_export(self):
        executable = core.ROOT.parent / "dnSpy-net-win64/dnSpy.Console.exe"
        if not executable.exists():
            chosen = filedialog.askopenfilename(title="Select dnSpy.Console.exe", filetypes=[("Executable", "*.exe")])
            if not chosen: return
            executable = Path(chosen)
        dll = filedialog.askopenfilename(title="Select managed mod DLL", initialdir=self.game() / "BepInEx/plugins", filetypes=[("Managed DLL", "*.dll")])
        game = self.game()
        if dll:
            self.run_task(lambda: core.decompile_managed(executable, Path(dll), game, self.report),
                          lambda result: (self.report(f"Recovered C# in {result}"), self.refresh_browser()))

    def build_editor(self):
        frame = self.tab_frames["C# editor"]
        bar = ttk.Frame(frame)
        bar.pack(fill="x")
        for label, command in (("New mod", self.new_mod), ("Open project", self.open_project),
                               ("Save C#", self.save_source), ("Build", self.build), ("Deploy build", self.deploy)):
            ttk.Button(bar, text=label, command=command).pack(side="left", padx=(0, 6))
        self.project_label = tk.StringVar(value=str(self.project))
        ttk.Label(frame, textvariable=self.project_label, wraplength=1080).pack(anchor="w", pady=8)
        self.source_combo = ttk.Combobox(frame, state="readonly")
        self.source_combo.pack(fill="x", pady=(0, 8))
        self.source_combo.bind("<<ComboboxSelected>>", lambda e: self.open_source())
        self.editor = ScrolledText(frame, wrap="none", undo=True, font=("Consolas", 11))
        self.editor.pack(fill="both", expand=True)
        ttk.Label(frame, text="Normal C# files · external editors use the same project · build output is in Logs & roadmap").pack(anchor="w", pady=8)

    def can_leave_editor(self):
        if not self.editor.edit_modified(): return True
        answer = messagebox.askyesnocancel("Unsaved C#", "Save the current source before switching?")
        if answer is None: return False
        if answer: return self.save_source()
        return True

    def refresh_sources(self):
        self.sources = [p for p in sorted(self.project.parent.rglob("*.cs")) if not any(x in p.parts for x in ("bin", "obj"))]
        self.sources.sort(key=lambda p: (p.name != "Plugin.cs", str(p)))
        self.source_combo["values"] = [str(p.relative_to(self.project.parent)) for p in self.sources]
        self.project_label.set(str(self.project))
        self.editor_path = None
        if self.sources:
            self.source_combo.current(0)
            self.load_source(self.sources[0])

    def load_source(self, path):
        self.editor_path = path
        self.set_text(self.editor, path.read_text(encoding="utf-8-sig"), readonly=False)

    def open_source(self):
        if not self.can_leave_editor():
            self.source_combo.set(str(self.editor_path.relative_to(self.project.parent)))
            return
        self.load_source(self.sources[self.source_combo.current()])

    def save_source(self):
        if self.busy:
            messagebox.showinfo("Build running", "Wait before saving source during a build.")
            return False
        if self.editor_path:
            try:
                core.atomic_write(self.editor_path, self.editor.get("1.0", "end-1c").encode("utf-8"))
                self.editor.edit_modified(False)
                self.status.set("Source saved")
                return True
            except Exception as error: messagebox.showerror("Save failed", str(error))
        return False

    def open_project(self):
        if self.busy or not self.can_leave_editor(): return
        path = filedialog.askopenfilename(title="Open C# mod project", initialdir=core.ROOT / "plugins", filetypes=[("C# project", "*.csproj")])
        if path:
            self.project = Path(path)
            self.artifact = core.ROOT / "artifacts" / self.project.stem
            self.refresh_sources()

    def new_mod(self):
        if self.busy or not self.can_leave_editor(): return
        name = simpledialog.askstring("Create mod", "Project name (letters, digits, underscore):")
        if name:
            try:
                self.project = core.create_mod(core.ROOT / "local/projects", name)
                self.artifact = core.ROOT / "artifacts" / self.project.stem
                self.refresh_sources()
            except Exception as error: messagebox.showerror("Create failed", str(error))

    def build(self):
        if self.busy or not self.save_source(): return
        game, project = self.game(), self.project
        def done(result):
            self.artifact = Path(result["artifact"])
            self.status.set("Build passed · deploy when ready · runtime not yet verified")
            self.tabs.select(self.tab_frames["Logs & roadmap"])
        self.run_task(lambda: core.build_plugin(game, project, self.report), done)

    def deploy(self):
        game, artifact = self.game(), self.artifact
        self.run_task(lambda: core.deploy_plugin(game, artifact),
                      lambda result: (self.report(f"Deployed; backup receipt: {result}"), self.refresh_mods()))

    def build_mods(self):
        frame = self.tab_frames["Mods & backups"]
        ttk.Label(frame, text="Plugin DLLs and dependency DLLs share this directory. Toggle a dependency only when its dependent mods are disabled.", wraplength=1060).pack(anchor="w", pady=(0, 8))
        bar = ttk.Frame(frame)
        bar.pack(fill="x")
        ttk.Button(bar, text="Refresh", command=self.refresh_mods).pack(side="left")
        ttk.Button(bar, text="Enable / disable selected", command=self.toggle).pack(side="left", padx=6)
        self.mods_tree = ttk.Treeview(frame, columns=("state", "size"), height=12)
        self.mods_tree.heading("#0", text="Relative DLL path")
        self.mods_tree.heading("state", text="State (restart required)")
        self.mods_tree.heading("size", text="Bytes")
        self.mods_tree.column("state", width=180)
        self.mods_tree.column("size", width=110)
        self.mods_tree.pack(fill="both", expand=True, pady=8)
        ttk.Label(frame, text="Backups of files written by Studio", font=("Segoe UI", 12, "bold")).pack(anchor="w")
        self.backup_combo = ttk.Combobox(frame, state="readonly")
        self.backup_combo.pack(fill="x", pady=8)
        ttk.Button(frame, text="Restore selected backup", command=self.restore).pack(anchor="w")

    def refresh_mods(self):
        self.mods_tree.delete(*self.mods_tree.get_children())
        for item in core.plugins(self.game()):
            self.mods_tree.insert("", "end", iid=item["path"], text=item["relative"], values=("enabled" if item["enabled"] else "disabled", item["bytes"]))
        self.backups = sorted((core.ROOT / "local/backups").glob("*/receipt.json"), reverse=True)
        labels = []
        for path in self.backups:
            receipt = json.loads(path.read_text())
            labels.append(path.parent.name + " · " + receipt["relative"])
        self.backup_combo["values"] = labels
        if labels: self.backup_combo.current(0)

    def toggle(self):
        selection = self.mods_tree.selection()
        if not selection: return
        game, path = self.game(), Path(selection[0])
        self.run_task(lambda: core.toggle_plugin(game, path), lambda result: self.refresh_mods())

    def restore(self):
        index = self.backup_combo.current()
        if index < 0: return
        game, transaction = self.game(), self.backups[index].parent
        self.run_task(lambda: core.restore_backup(game, transaction), lambda result: self.refresh_mods())

    def build_customization(self):
        frame = self.tab_frames["Customization"]
        ttk.Label(frame, text="Starter plugin settings", font=("Segoe UI", 14, "bold")).pack(anchor="w")
        ttk.Label(frame, text="Build and deploy TK2.Customization first. Settings reload about once per second while it runs.", wraplength=1040).pack(anchor="w", pady=8)
        self.setting_vars = {}
        grid = ttk.Frame(frame)
        grid.pack(fill="x")
        for row, (key, (kind, default, low, high)) in enumerate(core.SETTING_SCHEMA.items()):
            section, name = key
            ttk.Label(grid, text=f"{section} · {name}").grid(row=row, column=0, sticky="w", pady=5, padx=(0, 20))
            if kind == "bool":
                var = tk.BooleanVar(value=default)
                widget = ttk.Checkbutton(grid, variable=var, text="Enabled")
            else:
                var = tk.StringVar(value=str(default))
                widget = ttk.Entry(grid, textvariable=var, width=25)
            widget.grid(row=row, column=1, sticky="w")
            if kind == "float": ttk.Label(grid, text=f"{low:g} – {high:g}").grid(row=row, column=2, padx=12, sticky="w")
            self.setting_vars[key] = var
        controls = ttk.Frame(frame)
        controls.pack(fill="x", pady=16)
        ttk.Button(controls, text="Load from game", command=self.load_settings).pack(side="left")
        ttk.Button(controls, text="Save to game config", command=self.save_settings).pack(side="left", padx=8)
        ttk.Label(frame, text="UI scales matching root canvases; default filter HUD may need adjustment. FOV targets Camera.main.\n"
                  "Wwise volume affects levels observed through SetVolume; change an in-game volume slider once if no levels were captured.\n"
                  "Fast fall: hold Down Arrow in an offline race. Gameplay changes block the known upload path until game restart.\n"
                  "These starter features have compiled successfully; manual in-game validation is still required.", wraplength=1040).pack(anchor="w", pady=10)

    def load_settings(self):
        path = self.game() / "BepInEx/config" / core.CONFIG_NAME
        if not path.exists(): return
        parser = configparser.ConfigParser(interpolation=None)
        parser.optionxform = str
        try:
            parser.read(path, encoding="utf-8-sig")
            for key, var in self.setting_vars.items():
                if parser.has_option(*key):
                    value = parser.get(*key)
                    var.set(value.lower() == "true" if core.SETTING_SCHEMA[key][0] == "bool" else value)
            self.status.set("Starter settings loaded")
        except Exception as error: messagebox.showerror("Config read failed", str(error))

    def save_settings(self):
        if self.busy: return
        try:
            values = core.validate_settings({key: var.get() for key, var in self.setting_vars.items()})
            game = core.validate_game(self.game())
            path = core.contained(game / "BepInEx/config", game / "BepInEx/config" / core.CONFIG_NAME)
            original = path.read_bytes() if path.exists() else b""
            text = original.decode("utf-8-sig")
            updated = core.update_cfg(text, values).encode("utf-8")
            self.run_task(lambda: self.write_if_unchanged(game, path, original, updated),
                          lambda result: (self.report(f"Settings saved; backup {result}"), self.refresh_configs(), self.refresh_mods()))
        except Exception as error: messagebox.showerror("Config save failed", str(error))

    @staticmethod
    def write_if_unchanged(game, path, original, updated):
        if (path.read_bytes() if path.exists() else b"") != original:
            raise ValueError("Config changed since read. Reload before saving.")
        return core.backup_write(game, path, updated)

    def build_configs(self):
        frame = self.tab_frames["Configs"]
        ttk.Label(frame, text="Existing BepInEx settings · preserves content as edited · backups on every save").pack(anchor="w")
        bar = ttk.Frame(frame)
        bar.pack(fill="x", pady=8)
        self.cfg_combo = ttk.Combobox(bar, state="readonly", width=55)
        self.cfg_combo.pack(side="left", fill="x", expand=True)
        self.cfg_combo.bind("<<ComboboxSelected>>", lambda e: self.open_config())
        ttk.Button(bar, text="Refresh", command=self.refresh_configs).pack(side="left", padx=6)
        ttk.Button(bar, text="Save config", command=self.save_config).pack(side="left")
        self.cfg_text = ScrolledText(frame, wrap="none", undo=True, font=("Consolas", 11))
        self.cfg_text.pack(fill="both", expand=True)
        ttk.Label(frame, text="Only mods with config reload support apply changes live. Other mods require restart.").pack(anchor="w", pady=8)

    def refresh_configs(self):
        self.configs = sorted((self.game() / "BepInEx/config").glob("*.cfg"))
        self.cfg_combo["values"] = [p.name for p in self.configs]

    def open_config(self):
        if self.cfg_text.edit_modified() and not messagebox.askyesno("Unsaved config", "Discard unsaved config edits?"):
            if self.config_path: self.cfg_combo.set(self.config_path.name)
            return
        self.config_path = core.contained(self.game() / "BepInEx/config", self.configs[self.cfg_combo.current()])
        self.config_game = self.game()
        self.config_original = self.config_path.read_bytes()
        self.set_text(self.cfg_text, self.config_original.decode("utf-8-sig"), readonly=False)

    def save_config(self):
        if not self.config_path or self.busy: return
        if self.config_game != self.game():
            messagebox.showerror("Game folder changed", "Reopen the config from the selected installation before saving.")
            return
        game, path, original = self.game(), self.config_path, self.config_original
        updated = self.cfg_text.get("1.0", "end-1c").encode("utf-8")
        def done(result):
            self.config_original = updated
            self.cfg_text.edit_modified(False)
            self.report(f"Config saved; backup {result}")
            self.refresh_mods()
        self.run_task(lambda: self.write_if_unchanged(game, path, original, updated), done)

    def build_logs(self):
        frame = self.tab_frames["Logs & roadmap"]
        bar = ttk.Frame(frame)
        bar.pack(fill="x")
        ttk.Button(bar, text="Read game log", command=self.read_log).pack(side="left")
        ttk.Button(bar, text="Show roadmap", command=lambda: self.report((core.ROOT / "docs/ROADMAP.md").read_text())).pack(side="left", padx=6)
        ttk.Button(bar, text="Show possibilities", command=lambda: self.report((core.ROOT / "docs/CAPABILITIES.md").read_text())).pack(side="left")
        self.log_text = ScrolledText(frame, wrap="word", font=("Consolas", 10), state="disabled")
        self.log_text.pack(fill="both", expand=True, pady=8)

    def read_log(self):
        base = self.game() / "BepInEx"
        path = next((base / name for name in ("LogOutput.log", "LogOutput.txt") if (base / name).exists()), None)
        self.report(path.read_text(encoding="utf-8", errors="replace")[-100000:] if path else "No BepInEx log exists yet.")

    def refresh_projects(self):
        self.refresh_mods()
        self.refresh_configs()

    def save_active(self):
        active = self.tabs.select()
        if active == str(self.tab_frames["C# editor"]): self.save_source()
        elif active == str(self.tab_frames["Configs"]): self.save_config()

    def close(self):
        if self.busy:
            messagebox.showinfo("Task running", "Wait for the current operation before closing Studio.")
            return
        if not self.can_leave_editor(): return
        if self.cfg_text.edit_modified() and not messagebox.askyesno("Unsaved config", "Close and discard unsaved config edits?"): return
        core.write_json(core.ROOT / "local/studio.json", {"game": str(self.game())})
        self.destroy()
