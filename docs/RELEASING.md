# Build and release checklist

The in-app updater is configured for `The-Karters-Community/TK2-Mod-Toolkit`. It checks the latest non-draft, non-prerelease GitHub Release. A packaged copy can install an update only if the release contains the exact versioned ZIP, the GitHub asset API provides a SHA-256 digest, and the archive contains a matching `portable-manifest.json`, executable and `tools/update_portable.ps1`. The updater verifies the digest, validates archive paths/size/version, preserves the app's `local/` user data and uses a rollback-capable folder swap. Source checkouts only offer the release page.

## Prerequisites

- Windows x64 build host with Python 3.10+ x64 and a .NET SDK.
- A supported, initialized game/BepInEx installation for plugin compilation.
- GitHub CLI (`gh`) authenticated as a repository maintainer.
- A clean Git worktree with the intended release commit already pushed to `origin/main`.

## Prepare and test

1. Update `APP_VERSION` in `studio/version.py`. The release script uses it for the tag and archive name.
2. Run relevant tests and inspect the final diff. Compile the plugin with the supported game references when plugin code changed.
3. Close the Toolkit executable, then build the desired package:

   ```powershell
   .\tools\build_toolkit.ps1 -RebuildPlugin
   # Add -IncludeLoader only when the larger loader-bundled package is intended.
   ```

4. Test the unpacked package in a relocated folder. Verify startup, installation discovery, module/source catalogs and update metadata. Do not test against the production game folder if the test would modify its files; use a disposable copy where possible.
5. Commit and push the exact tested source commit. The release script refuses a dirty worktree or a local HEAD different from `origin/main`.

The normal archive is `artifacts/portable/TK2-Mod-Toolkit-<version>-win-x64.zip`. The default package bundles the application runtime but not BepInEx. `-IncludeLoader` adds the validated loader distribution while preserving the same archive name, so choose one package variant for a release asset.

## Create the GitHub release

Run from the repository root:

```powershell
# Create a draft for review
.\tools\publish_release.ps1

# Publish directly (skip draft review)
.\tools\publish_release.ps1 -Publish
```

The script validates the versioned archive, Git status and remote branch head, then creates tag `v<version>` on the exact commit and uploads the ZIP. By default, it creates a draft; review notes and asset before publishing the draft on GitHub. Publishing a stable release is what makes it eligible for startup update checks. The GitHub release asset API must report its SHA-256 digest for automatic installation to be offered.

## Updater compatibility and current limits

The updater currently accepts one asset with the exact name `TK2-Mod-Toolkit-<version>-win-x64.zip`. It installs the whole application folder. **Compact/delta packages and dual full-plus-compact update releases are not implemented.** If both the slim app ZIP and the loader-included ZIP are uploaded, the current updater cannot distinguish them; do not publish both with the same required name.

As of October 10, 2026, the public repository has no published Releases, so the latest-release API returns no stable asset and in-app automatic update installation has not yet been exercised against a live release. Before announcing the first release, publish a test version and update an older packaged copy. Verify the update prompt, digest, embedded manifest, preserved `local/` folder, relaunch and rollback path.
