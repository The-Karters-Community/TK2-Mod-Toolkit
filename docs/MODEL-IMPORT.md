# Import a creator's model

The Toolkit imports a **static kart cosmetic shell**. The original kart still supplies collision, wheels, handling and the seated driver's animation. This importer does not replace a driver's skeleton or retarget animations. Use a model whose creator permits use and redistribution; keep their attribution with any shared pack.

## FBX in the app

Open **Workshop → Models**, choose the creator's FBX and the detected Blender executable (or browse to blender.exe). Blender 3.5+ is required; 3.5.1 was verified locally. Import produces a Toolkit-library OBJ with supported diffuse material textures; it does not require rebuilding the plugin for every model. Select the imported model, choose **Use on kart** to copy its assets and stage its selection, then **Save changes** and enter a local race. If prompted, update the Toolkit plugin once with the game closed so the importer itself is available.

Open **Mod packs → Custom models → Imported kart model**. Adjust **Scale**, the three offsets and the three rotation values to align the shell around your kart. Keep **Hide original kart** off until alignment is correct, then enable it to hide original static kart meshes while retaining the driver. The change applies while racing. Geometry is visual only: a large imported shell retains the original hitbox.

FBX conversion flattens the model into static meshes. Skeletons, shape keys, animations, physics and most PBR shader settings are not reproduced. The OBJ route reads `Kd` diffuse colours and simple `map_Kd` PNG/JPEG textures. Missing textures fall back to diffuse colour. Normal, roughness and metallic maps are not interpreted by the OBJ loader. Complex concave polygons should be triangulated in the creator's editor before export.

## OBJ directly

Import the OBJ together with its `.mtl` and PNG/JPEG texture files. Keep their relative folder structure; references must stay within the imported model's folder. The loader accepts positions, UVs, normals, negative indices and triangulated polygon faces. **Mirror X** converts normal right-handed OBJ coordinates into Unity coordinates. Disable it if the converter already changed handedness. If the shell is backward, adjust yaw to 180 degrees; if it is too small, adjust scale.

Installed model data lives at:

```text
<game>/BepInEx/models/<model-id>/model.obj
<game>/BepInEx/models/<model-id>/model.mtl
<game>/BepInEx/models/<model-id>/<textures>
```

`Recipe.CosmeticModel.ModelPath` is relative to `BepInEx/models`, for example `my-kart/model.obj`. A changed selected file reloads within about a second during a local race. If only its material or textures changed, increase **Reload token** to reload those files too. Transforms and tint reload through the ordinary live config path. Disabling the module removes the shell and restores captured renderer visibility.

## Unity AssetBundle for materials

The installed game reports **Unity 6000.0.75f1** in `BepInEx/LogOutput.log` (observed 2026-10-08). Use that exact editor and Windows x64 when possible. Unity does not support loading bundles built with a newer editor into an older player; matching the game build avoids that boundary. See [Unity's AssetBundle compatibility documentation](https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/assets-managing-runtime/assetbundles-section/asset-bundles-intro).

1. Import the creator's FBX/model and textures into a Unity project. Assemble a static prefab using `MeshFilter` and `MeshRenderer` components. Use shaders compatible with the game's render pipeline.
2. Copy `templates/ModelBundleExporter.cs.txt` from this repository to `Assets/Editor/TK2ModelBundleExporter.cs` in that Unity project.
3. Select the prefab asset. Choose **TK2 → Export selected static model prefab**, then choose an output folder.
4. Import `model.bundle` in the Toolkit. Leave **Asset name** empty to pick its first prefab, or set its full asset path (for example `Assets/Models/MyKart.prefab`).

The runtime copies static mesh/material transforms from the prefab. It does not instantiate the supplied prefab's scripts, colliders, lights, cameras or animator. Dependencies must be included in the same bundle; this importer does not fetch external bundles. Skinned-only prefabs are rejected with a diagnostic.

## Limits and verification

Models are loaded only for human local karts in an offline race. The importer performs player discovery and selected-file checks at most once a second, bounds OBJ/material/texture/bundle sizes, rejects paths outside the imported folder and rejects junctions. Loader failures appear in BepInEx diagnostics; change selection or increase Reload token after correcting an asset.

Limits: OBJ 32 MB; 250,000 source vertices; 500,000 generated triangles; 64 materials; material file 1 MB; individual diffuse texture 16 MB, at most 8192 pixels per side and 16 megapixels, with 32 megapixels combined per OBJ; bundle 256 MB; copied bundle hierarchy 512 nodes and 64 static meshes, each at most 250,000 vertices, with at most 1.5 million vertices and indices combined across the imported hierarchy. PNG/JPEG dimensions are checked before decoding.

Current IL2CPP signatures were compiled against the installed game's interop. Visual placement, material fidelity and renderer separation still need an in-game check with the chosen model. The game was not launched automatically.

Evidence: offline PE metadata confirms `Ant_Player.visualInstanceSyncedParams`, `Ant_VisualKart.GetVisualKartRoot`, `GetCharacterSocket`, and `IsKartLoaded`. REA AssetBundle module evidence `ev_67e86eeb764b0e7d392276a51bba05e305c48295e9d4145a855c81a6a19fc3f6` confirms loading/unloading and prefab access signatures; these wrappers establish available APIs, not recovered original game bodies. The full Assembly-CSharp REA inventory exceeded the provider's string limit, so the focused game-member inventory used read-only `System.Reflection.Metadata` instead.
