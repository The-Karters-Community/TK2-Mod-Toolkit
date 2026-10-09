# Mirror Race regressions

Run `dotnet run --project tests/MirrorRace/MirrorRace.csproj -c Release` from the SDK root.

The harness links production MirrorRace and MirrorImageEffect code with stub Unity/Harmony/config APIs. It checks zero scene scans and no retained render callback in menus/loading/offline-denied/disabled states, transition guards before the next update, countdown attachment, steering/image reversal, quarter-second discovery, and cleanup without destroying native cameras. It does not test native image-effect scheduling, shader output, actual menu FPS or IL2CPP injection.
