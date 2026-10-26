# Antigravity policy tests

Run `dotnet run --project tests/Antigravity/Antigravity.csproj` from the repository root.

This harness links the production pure transition/frame policy and a Unity value-type stub. It covers stable-road-only acquisition, eligibility and mode guards, normal transport through a full loop, curvature limits, steep-surface speed thresholds, bounded drift-hop continuity, finite-input handling, jump/gravity delta magnitude preservation, and upright release using the current heading and velocity. It does not load interop, invoke game methods, or establish in-game physics behavior. `tests/RuntimeHooks` separately verifies resolved hook signatures against the installed interop when Antigravity's hook list is available.
