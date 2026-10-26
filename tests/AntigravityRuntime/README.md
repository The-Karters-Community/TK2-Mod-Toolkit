# Antigravity runtime-adapter tests

Run `dotnet run --project tests/AntigravityRuntime/AntigravityRuntime.csproj` from the repository root.

This headless harness compiles the production adapter and policy against small Unity and game-interop fakes. It invokes the actual private callback handlers to verify early disabled exits, local/offline stable-road acquisition, excluded colliders, no airborne reacquisition, native jump grace without drift held, cleanup on online/pause/respawn/replay guards, current-heading release without position or velocity rewinds, and fault cleanup. The fakes do not emulate PhysX or prove in-game handling. Use `tests/RuntimeHooks` against the installed interop for the separate real-signature check; only live offline play can establish visible behavior.
