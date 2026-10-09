Run `dotnet run --project tests/TrackBoundaries/TrackBoundaries.csproj -c Release`.

These tests link the production classifier, collider geometry, and view lifecycle with Unity and BepInEx stubs. They reproduce a base Collider wrapper whose native subtype is a rendererless MeshCollider, including an unreadable mesh with multiple submeshes. They check native layer masks and respawn priority, additive scenes, exclusions, scan cadence, mesh replacement, palette reuse, F10 and focus, unchanged source physics/renderers/cameras, and cleanup during disable/menu/online/map changes. Primitive checks cover box dimensions, nonuniform sphere scales, all capsule axes, generated-mesh ownership, and movement between scans.

The harness models native TryCast and Unity destroyed-object equality explicitly. It does not run Il2CppInterop, native rendering or PhysX, and cannot establish the appearance or performance of an in-game track. Round meshes approximate curved collider surfaces; native collision meshes are referenced without reading their vertices.

Outline regressions check that automatic filled drawing is disabled, lines respect scene depth without writing depth, every submesh is drawn in wireframe, native raster state is restored on success/failure, and menu/online/disabled/auxiliary-camera callbacks do not draw. Actual line appearance still needs a player run.
