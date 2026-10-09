using System;
using System.Reflection;
using System.Linq;
using TK2.Customization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Next() { Time.unscaledTime += 1.1f; TrackBoundaries.Tick(); }
MeshRenderer? View(MeshRenderer source) => Object.Registry.OfType<MeshRenderer>().FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy && x.transform.parent == source.transform);
int ViewCount() => Object.Registry.OfType<MeshRenderer>().Count(x => x != null && x.gameObject.activeInHierarchy && x.gameObject.name == "TK2 boundary view");
Plugin Reset()
{
    TrackBoundaries.Restore(); Object.Registry.Clear(); Time.unscaledTime = 0;
    typeof(TrackBoundaries).GetField("_wasEnabled", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
    typeof(TrackBoundaries).GetField("_faulted", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
    Application.isFocused = true; Input.Pressed = false; MenuManager.Instance = null;
    SceneManager.Active = new(1); Plugin.OfflineLabAllowed = true;
    Ant_CurrentGameConfiguration.eCurrentRaceState = Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
    Shader.Internal = new Shader();
    Ant_MapData.instance = new GameObject().Add<Ant_MapData>();
    var player = new GameObject().Add<Ant_Player>();
    var kart = player.gameObject.Add<PixelEasyCharMoveKartController>(); kart.parentPlayer = player;
    kart.wallLayerToCollDetect.value = (1 << 8) | (1 << 31);
    kart.wallRespawn_Flat_ColliderLayer.value = 1 << 9;
    kart.wallRespawn_Always_ColliderLayer.value = 1 << 10;
    var plugin = new Plugin(); Plugin.Instance = plugin; TrackBoundaries.Install(plugin);
    plugin.Config.Entry<bool>("Enabled").Value = true;
    return plugin;
}
MeshRenderer Boundary(int layer, float distance = 0, GameObject? parent = null)
{
    var go = new GameObject { layer = layer }; go.transform.parent = parent?.transform;
    go.Add<PTK_HideMeshRendererInPlayMode>(); var collider = go.Add<Collider>(); collider.bounds = new Bounds { center = new Vector3(distance, 0, 0) };
    var renderer = go.Add<MeshRenderer>(); renderer.sharedMaterials = new[] { new Material(new Shader()), new Material(new Shader()) };
    go.Add<MeshFilter>().sharedMesh = new Mesh();
    renderer.forceRenderingOff = true; return renderer;
}

Check(BoundarySelection.Classify(-1, -1, -1, -1) == BoundaryKind.None, "negative layer rejected");
Check(BoundarySelection.Classify(32, -1, -1, -1) == BoundaryKind.None, "out of range layer rejected");
Check(BoundarySelection.Classify(31, int.MinValue, 0, 0) == BoundaryKind.Wall, "signed bit31 works");
Check(BoundarySelection.Classify(8, 1 << 8, 1 << 8, 0) == BoundaryKind.Respawn, "respawn takes priority over wall");
Check(BoundarySelection.Classify(10, 0, 0, 1 << 10) == BoundaryKind.Respawn, "always-respawn selected");

var p = Reset(); var wall = Boundary(8); var respawn = Boundary(9); var other = Boundary(11); var far = Boundary(8, 201);
var originalWall = wall.sharedMaterials; var camera = new GameObject().Add<Camera>(); camera.cullingMask = 1 << 3;
Next();
var wallView = View(wall)!; var respawnView = View(respawn)!;
Check(wallView.enabled && respawnView.enabled, "selected authored meshes receive enabled visual overlays");
Check(View(other) == null && View(far) == null && !other.enabled && !far.enabled, "unrelated layer and out of range remain hidden");
Check(wallView.shadowCastingMode == ShadowCastingMode.Off && !wallView.receiveShadows, "overlay suppresses shadows");
Check(wallView.sharedMaterials.Length == 2 && wallView.sharedMaterials[0] == wallView.sharedMaterials[1], "all authored material slots receive tint");
Check(wallView.sharedMaterials[0].Color.b > wallView.sharedMaterials[0].Color.r && respawnView.sharedMaterials[0].Color.r > respawnView.sharedMaterials[0].Color.b, "cyan walls and orange respawn are distinct");
Check(wallView.GetComponent<MeshFilter>()!.sharedMesh == wall.GetComponent<MeshFilter>()!.sharedMesh, "overlay uses identical authored mesh asset");
Check(wallView.GetComponent<Collider>() == null && respawnView.GetComponent<Collider>() == null && wallView.gameObject.layer == 0, "overlay contains no colliders and uses default render layer");
Check(wall.sharedMaterials == originalWall && !wall.enabled && wall.forceRenderingOff && wall.shadowCastingMode == ShadowCastingMode.On && wall.receiveShadows && wall.gameObject.layer == 8, "native renderer and collider layer stay unchanged while visible");
Check(camera.cullingMask == (1 << 3), "native camera masks never expand");
var tint = wallView.sharedMaterials[0]; Next(); Check(View(wall) == wallView && wallView.sharedMaterials[0] == tint, "rescan reuses overlay and palette");
Check(ViewCount() == 2 && Object.Registry.OfType<Collider>().Count() == 4, "rescans do not recurse into owned overlays or create colliders");
camera.cullingMask |= 1 << 20; p.Config.Entry<bool>("ShowWalls").Value = false; Next();
Check(View(wall) == null && wallView.gameObject.Destroyed && !wall.enabled && wall.forceRenderingOff && wall.sharedMaterials == originalWall, "wall selection destroys overlay and leaves originals intact");
Check(wall.shadowCastingMode == ShadowCastingMode.On && wall.receiveShadows, "native shadow flags stay unchanged");
Check(camera.cullingMask == ((1 << 3) | (1 << 20)), "camera runtime changes remain untouched");
Check(View(respawn) == respawnView && respawnView.enabled, "respawn overlay survives wall selection toggle");
p.Config.Entry<bool>("Enabled").Value = false; Next();
Check(ViewCount() == 0 && respawnView.gameObject.Destroyed && !respawn.enabled && camera.cullingMask == ((1 << 3) | (1 << 20)), "disable destroys overlays and leaves native mesh and camera untouched");
Check(tint.Destroyed && !TrackBoundaries.Visible, "disable destroys owned palette and clears diagnostic visibility");

p = Reset(); wall = Boundary(8); Input.Pressed = true; Next(); Check(ViewCount() == 0, "hotkey can begin hidden");
Input.Pressed = true; Next(); Check(View(wall) != null, "hotkey re-enables");
Application.isFocused = false; Input.Pressed = true; Next(); Check(View(wall) != null, "unfocused keyboard event ignored");
Input.Pressed = false; Plugin.OfflineLabAllowed = false; Next(); Check(ViewCount() == 0 && !TrackBoundaries.Visible, "online transition destroys overlays");
Plugin.OfflineLabAllowed = true; Next(); Check(View(wall) != null, "offline scene rearms");
MenuManager.Instance = new GameObject().Add<MenuManager>(); Next(); Check(ViewCount() == 0, "menu destroys overlays");
MenuManager.Instance = null; Next(); Check(View(wall) != null, "return to running race rearms");
Ant_CurrentGameConfiguration.eCurrentRaceState = Ant_CurrentGameConfiguration.ERaceState.E_RACE_FINISHED; Next(); Check(ViewCount() == 0, "finished race destroys overlays");

p = Reset(); wall = Boundary(8); var disabled = Boundary(8); disabled.GetComponent<Collider>()!.enabled = false;
var inactive = Boundary(8); inactive.gameObject.activeInHierarchy = false;
var unhidden = new GameObject { layer = 8 }; unhidden.Add<Collider>(); var unhiddenRenderer = unhidden.Add<MeshRenderer>();
var playerRoot = new GameObject(); playerRoot.Add<Ant_Player>(); var playerMesh = Boundary(8, 0, playerRoot);
var wrongScene = Boundary(8); wrongScene.gameObject.scene = new Scene(7);
var root = new GameObject { layer = 8 }; root.Add<PTK_HideMeshRendererInPlayMode>().bIncludeChildrenMeshRenderers = false; root.Add<Collider>();
var child = new GameObject { layer = 8 }; child.transform.parent = root.transform; var childRenderer = child.Add<MeshRenderer>(); childRenderer.sharedMaterials = new[] { new Material(new Shader()) };
child.Add<MeshFilter>().sharedMesh = new Mesh();
Next();
Check(View(wall) != null && View(disabled) == null && View(inactive) == null, "disabled and inactive colliders excluded");
Check(View(unhiddenRenderer) == null && View(playerMesh) == null && View(wrongScene) == null, "non-authored, player and unrelated-scene geometry excluded");
Check(View(childRenderer) == null, "native no-children flag respected");
root.GetComponent<PTK_HideMeshRendererInPlayMode>()!.bIncludeChildrenMeshRenderers = true; Next();
Check(View(childRenderer) != null, "child debug mesh uses parent collider within authored subtree");
var firstView = View(wall)!; var firstTint = firstView.sharedMaterials[0]; Ant_MapData.instance = new GameObject().Add<Ant_MapData>(); Next();
Check(firstView.gameObject.Destroyed && firstTint.Destroyed && View(wall) != null, "new map destroys then rebuilds overlays and materials");
Object.Destroy(wall); Next(); Check(!p.Log.Warnings.Exists(x => x.StartsWith("Track boundaries unavailable")), "destroyed renderer does not fault scan");
TrackBoundaries.Restore(); Check(ViewCount() == 0 && !childRenderer.enabled, "unload destroys remaining overlays");

p = Reset(); Shader.Internal = null; wall = Boundary(8); wall.sharedMaterials = new[] { new Material(new Shader { Colored = false }) }; Next();
Check(ViewCount() == 0 && !wall.enabled && !TrackBoundaries.Visible && p.Log.Warnings.Count == 0, "unsupported material leaves renderer untouched");
TrackBoundaries.Restore();
Console.WriteLine($"Track boundaries: {assertions} assertions passed.");
