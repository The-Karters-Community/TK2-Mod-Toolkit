# Mirror Race regressions

Run `dotnet run --project tests/MirrorRace/MirrorRace.csproj -c Release` from the SDK root.

The harness links production MirrorRace, its presentation companion and MirrorImageEffect with stub Unity/Harmony/config APIs. It checks menu/loading/online guards, mirroring and reversed steering during the countdown, discovery throttling, native nameplate placement reflected within each camera viewport, repeated updates without oscillation, atlas-tile UV compensation and restoration, and cleanup without destroying native cameras.

The waypoint and counter members are CLR properties, matching the installed interop signatures. The harness deliberately keeps the distance-reference camera separate from the native marker placement: that camera is not the opponent tracking target.

It does not test native image-effect scheduling, shader output, actual in-game nameplate placement, menu FPS or IL2CPP injection. Those still require visual validation in the game.
