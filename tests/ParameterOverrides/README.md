# Parameter interop regression

Run `dotnet run --project tests/ParameterOverrides/ParameterOverrides.csproj -c Release`.

The harness links production `LegacyMK.Parameters.cs`. A stub component rejects identity reads when forbidden, so the original implementation fails the disabled-path test. It also checks baseline capture, active edits and restoration of individual, master-disabled and multiple-target overrides. Stub game field declarations exist only to compile the actual binding source; they contain no game implementation. This does not prove native Harmony dispatch or race performance; those were checked with bounded in-game allocation observations.
