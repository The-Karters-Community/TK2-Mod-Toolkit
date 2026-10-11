# Build and release checklist

The in-app updater is configured for `The-Karters-Community/TK2-Mod-Toolkit`. It checks the latest non-draft, non-prerelease GitHub Release. A packaged copy can install an update only if the release contains the exact versioned ZIP, the GitHub asset API provides a SHA-256 digest, and the archive contains a matching `portable-manifest.json`, executable and `tools/update_portable.ps1`. The updater verifies the digest, validates archive paths/size/version, preserves the app's `local/` user data and uses a rollback-capable folder swap. Source checkouts only offer the release page.

## Prerequisites

- Windows x64 build host with Python 3.10+ x64, Node.js 20.19+ with npm, and a .NET SDK.
- A supported, initialized game/BepInEx installation for plugin compilation.
- GitHub CLI (`gh`) authenticated as a repository maintainer.
- A clean Git worktree with the intended release commit already pushed to `origin/main`.

## Prepare and test

1. Check `APP_VERSION` in `studio/version.py`: it is the next unreleased version (currently 0.6.24). Source UI adds `-dev`; packaged builds show the numeric release version. Use `.\tools\set_version.ps1 -Version X.Y.Z` only to choose a different release number. Builds never increment versions.
2. Run relevant tests and inspect the final diff. Compile the plugin with the supported game references when plugin code changed.
3. Close the Toolkit executable, then build the desired package:

   ```powershell
   .\tools\build_toolkit.ps1 -RebuildPlugin
   # Add -IncludeLoader only when the larger loader-bundled package is intended.
   ```

4. Test the unpacked package in a relocated folder. Verify startup, installation discovery, module/source catalogs and update metadata. Do not test against the production game folder if the test would modify its files; use a disposable copy where possible.
5. Commit and push the exact tested source commit. The release script refuses a dirty worktree or a local HEAD different from `origin/main`.

The build runs `npm ci` at the repository root, compiles `studio/ui/` into `studio/web-dist/`, and packages the built files. The Windows app uses pywebview with the system WebView2 runtime when available; it falls back to Edge app mode. The normal archive is `artifacts/portable/TK2-Mod-Toolkit-<version>-win-x64.zip`. The default package bundles the application runtime but not BepInEx. `-IncludeLoader` adds the validated loader distribution while preserving the same archive name, so choose one package variant for a release asset.

Only compiled UI assets are staged into the player package. React source, npm dependencies, source maps and frontend tooling stay out of it. The ZIP still contains the Python/desktop runtime and C# authoring material needed by Workshop; changing frontend frameworks does not remove those costs.

## Create the GitHub release

Run from the repository root:

```powershell
# Create a draft for review
.\tools\publish_release.ps1

# Publish directly (skip draft review)
.\tools\publish_release.ps1 -Publish
```

The script validates the versioned archive, Git status and remote branch head, then creates tag `v<version>` on the exact commit and uploads the ZIP. By default, it creates a draft; review notes and asset before publishing the draft on GitHub. Publishing a stable release is what makes it eligible for startup update checks. The GitHub release asset API must report its SHA-256 digest for automatic installation to be offered.

After a successful release creation (draft or published), the script increments the patch version in the checkout and commits the desktop/plugin version files. The release tag and ZIP keep the released version. Push that development commit before further contributions; creation failure does not advance the version. The next source build shows the new version with `-dev`.

## Updater compatibility and current limits

The updater currently accepts one asset with the exact name `TK2-Mod-Toolkit-<version>-win-x64.zip`. It replaces the app and renames standard versioned folders to the installed release: `TK2-Mod-Toolkit-0.6.23-win-x64` becomes `TK2-Mod-Toolkit-0.6.24-win-x64`; older `TK2 Mod Toolkit 0.6.23` folders become `TK2 Mod Toolkit 0.6.24`. Custom folder names without a version stay unchanged. The app relaunches from the new path. A pre-existing destination is never overwritten; failed startup restores the old folder and name. Shortcuts pointing to the old versioned path need retargeting. The displayed version and manifest also identify the installed release. The downloaded updater accepts calls from older releases. Old files remain only in temporary staging until the new process passes its authenticated health check; successful startup removes staging and leaves no permanent `.backup-*` folder. Failed startup restores the old app and keeps diagnostics in staging. Existing backups made by older updates are not removed automatically. **Compact/delta packages and dual full-plus-compact update releases are not implemented.** If both the slim app ZIP and the loader-included ZIP are uploaded, the current updater cannot distinguish them; do not publish both with the same required name.

Before publishing each update, test it against an older packaged copy. Verify the update prompt, digest, embedded manifest, preserved `local/` folder, relaunch and rollback path.
