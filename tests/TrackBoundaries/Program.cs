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
bool Close(float a, float b) => Math.Abs(a-b) < .001f;
bool Same(Vector3 a, Vector3 b) => Close(a.x,b.x) && Close(a.y,b.y) && Close(a.z,b.z);
void Next(float seconds = 1.1f) { Time.unscaledTime += seconds; TrackBoundaries.Tick(); }
MeshRenderer? View(Collider source) => Object.Registry.OfType<MeshRenderer>().FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy && x.gameObject.name == "TK2 boundary view" &&
    (x.transform.parent == source.transform || ((source.TryCast<SphereCollider>() != null || source.TryCast<CapsuleCollider>() != null) && Same(x.transform.position, source.transform.TransformPoint(source.TryCast<SphereCollider>()?.center ?? source.TryCast<CapsuleCollider>()?.center ?? Vector3.zero)))));
int ViewCount() => Object.Registry.OfType<MeshRenderer>().Count(x => x != null && x.gameObject.activeInHierarchy && x.gameObject.name == "TK2 boundary view");
Plugin Reset()
{
    TrackBoundaries.Restore(); Object.Registry.Clear(); Object.Finds.Clear(); Time.unscaledTime = 0;
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
MeshCollider Boundary(int layer, float distance = 0, GameObject? parent = null)
{
    var go = new GameObject { layer = layer }; go.transform.parent = parent?.transform;
    var collider = go.Add<MeshCollider>(); collider.bounds = new Bounds { center = new Vector3(distance, 0, 0) };
    collider.sharedMesh = new Mesh { ThrowOnVertexRead = true }; return collider;
}
Collider NativeWrapper(Collider native)
{
    Object.Registry.Remove(native);
    var wrapper = new Collider { gameObject = native.gameObject, NativeSubtype = native };
    return wrapper;
}
Mesh? DrawnMesh(MeshRenderer renderer) => renderer.GetComponent<MeshFilter>()!.sharedMesh;

Check(BoundarySelection.Classify(-1, -1, -1, -1) == BoundaryKind.None, "negative layer rejected");
Check(BoundarySelection.Classify(32, -1, -1, -1) == BoundaryKind.None, "out of range layer rejected");
Check(BoundarySelection.Classify(31, int.MinValue, 0, 0) == BoundaryKind.Wall, "signed bit31 works");
Check(BoundarySelection.Classify(8, 1 << 8, 1 << 8, 0) == BoundaryKind.Respawn, "respawn takes priority over wall");
Check(BoundarySelection.Classify(10, 0, 0, 1 << 10) == BoundaryKind.Respawn, "always-respawn selected");

var p = Reset(); var nativeWall = Boundary(8); nativeWall.sharedMesh!.subMeshCount = 3; var wall = NativeWrapper(nativeWall); var respawn = Boundary(9); var other = Boundary(11); var far = Boundary(8, 201);
var camera = new GameObject().Add<Camera>(); camera.cullingMask = 1 << 3;
Next();
var wallView = View(wall) ?? throw new Exception("Expected wall overlay"); var respawnView = View(respawn) ?? throw new Exception("Expected respawn overlay");
Check(wall.GetType() == typeof(Collider) && wall.TryCast<MeshCollider>() == nativeWall, "fixture uses native downcast from a base Collider wrapper");
Check(!wallView.enabled && !respawnView.enabled, "rendererless colliders get outlines without automatic filled-surface rendering");
Check(View(other) == null && View(far) == null, "unrelated layers and out-of-range colliders excluded");
Check(wallView!.shadowCastingMode == ShadowCastingMode.Off && !wallView.receiveShadows, "overlay suppresses shadows");
Check(wallView.sharedMaterial!.Color.b > wallView.sharedMaterial.Color.r && respawnView.sharedMaterial!.Color.r > respawnView.sharedMaterial.Color.b, "cyan wall and orange respawn distinct");
Check(DrawnMesh(wallView) == nativeWall.sharedMesh && nativeWall.sharedMesh!.ThrowOnVertexRead, "native unreadable mesh reference reused without reading vertices");
Check(wallView.sharedMaterials.Length == 3 && wallView.sharedMaterials.All(x => x == wallView.sharedMaterial), "every native submesh receives tint");
Check(wallView.sharedMaterial!.Ints["_ZTest"] == (int)CompareFunction.LessEqual && wallView.sharedMaterial.Ints["_ZWrite"] == 0,
    "outline respects existing scene depth without writing depth");
Check(wallView.sharedMaterial.Color.a >= .8f, "edges use readable color rather than faint filled surfaces");
Camera.current = camera; camera.cullingMask |= 1;
Graphics.Draws = 0; Graphics.Submeshes.Clear(); GL.wireframe = false;
TrackBoundaries.Render();
Check(Graphics.Draws == 4 && Graphics.Submeshes.SequenceEqual(new[] {0,1,2,0}), "only outline draws include all native submeshes without CPU vertex access");
Check(!GL.wireframe, "native filled rendering state restored after outline pass");
GL.wireframe = true; TrackBoundaries.Render();
Check(GL.wireframe, "preexisting wireframe state preserved"); GL.wireframe = false;
int draws = Graphics.Draws; Camera.current = null; TrackBoundaries.Render();
Check(Graphics.Draws == draws, "no camera means no immediate draws");
Camera.current = camera; camera.cameraType = CameraType.Reflection; TrackBoundaries.Render();
Check(Graphics.Draws == draws, "reflection/auxiliary camera excluded");
camera.cameraType = CameraType.Game; camera.cullingMask &= ~1; TrackBoundaries.Render();
Check(Graphics.Draws == draws, "camera Default-layer exclusion preserved");
camera.cullingMask |= 1;
MenuManager.Instance = new GameObject().Add<MenuManager>(); TrackBoundaries.Render();
Check(Graphics.Draws == draws && !GL.wireframe, "menu callback draws nothing before next update"); MenuManager.Instance = null;
Plugin.OfflineLabAllowed = false; TrackBoundaries.Render();
Check(Graphics.Draws == draws && !GL.wireframe, "online transition draws nothing before next update"); Plugin.OfflineLabAllowed = true;
p.Config.Entry<bool>("Enabled").Value = false; TrackBoundaries.Render();
Check(Graphics.Draws == draws && !GL.wireframe, "disabled callback draws nothing before next update"); p.Config.Entry<bool>("Enabled").Value = true;
camera.cullingMask = 1 << 3;
Check(wallView.GetComponent<Collider>() == null && respawnView.GetComponent<Collider>() == null && wallView.gameObject.layer == 0, "overlay adds no physics and uses render-only layer");
Check(wall.enabled && wall.gameObject.layer == 8 && camera.cullingMask == 1 << 3, "native collider enabled/layer and camera mask preserved");
var tint = wallView.sharedMaterial!; Next();
Check(View(wall) == wallView && wallView.sharedMaterial == tint, "rescan reuses overlay and palette");
Check(ViewCount() == 2 && Object.Registry.OfType<Collider>().Count() == 4, "no recursive overlay selection or new colliders");
Check(Object.Finds[typeof(Collider)] == 1, "inventory does not rescan colliders every second");
var late = Boundary(10); Next();
Check(View(late) == null, "new collider waits for bounded inventory refresh");
Next(3.1f); Check(View(late) != null && Object.Finds[typeof(Collider)] == 2, "inventory discovers new colliders after five seconds");
var replacement = new Mesh { ThrowOnVertexRead = true }; nativeWall.sharedMesh = replacement; Next();
Check(DrawnMesh(wallView) == replacement, "changing native sharedMesh refreshes overlay mesh reference");
camera.cullingMask |= 1 << 20; p.Config.Entry<bool>("ShowWalls").Value = false; Next();
Check(View(wall) == null && wallView.gameObject.Destroyed && wall.enabled, "wall switch destroys its overlay without disabling collider");
Check(View(respawn) == respawnView && !respawnView.enabled, "respawn outline survives wall switch with filled renderer disabled");
Check(camera.cullingMask == ((1 << 3) | (1 << 20)), "runtime camera change preserved");
p.Config.Entry<bool>("Enabled").Value = false; Next();
Check(ViewCount() == 0 && respawnView.gameObject.Destroyed && !TrackBoundaries.Visible, "disable clears all overlays and diagnostic flag");
Check(tint.Destroyed && !replacement.Destroyed, "owned material destroyed but native mesh retained");

p = Reset(); var hotWall = Boundary(8); Input.Pressed = true; Next(); Check(ViewCount() == 0, "F10 can start hidden");
Input.Pressed = true; Next(); Check(View(hotWall) != null, "F10 enables overlays");
Application.isFocused = false; Input.Pressed = true; Next(); Check(View(hotWall) != null, "unfocused keyboard ignored");
Input.Pressed = false; Plugin.OfflineLabAllowed = false; Next(); Check(ViewCount() == 0, "online transition removes overlays");
Plugin.OfflineLabAllowed = true; Next(); Check(View(hotWall) != null, "offline running race rearms");
MenuManager.Instance = new GameObject().Add<MenuManager>(); Next(); Check(ViewCount() == 0, "menu removes overlays");
int menuScans = Object.Finds[typeof(Collider)]; Next(); Check(Object.Finds[typeof(Collider)] == menuScans, "menu does not scan colliders");
MenuManager.Instance = null; Next(); Check(View(hotWall) != null, "return to race rearms");
Plugin.Instance!.GameplayReady = false; Next(); Check(ViewCount() == 0, "unready gameplay clears overlays");
Plugin.Instance.GameplayReady = true; Next();
Ant_CurrentGameConfiguration.eCurrentRaceState = Ant_CurrentGameConfiguration.ERaceState.E_RACE_FINISHED; Next(); Check(ViewCount() == 0, "finished race removes overlays");

p = Reset(); var active = Boundary(8); var disabled = Boundary(8); disabled.enabled = false;
var inactive = Boundary(8); inactive.gameObject.activeInHierarchy = false;
var playerRoot = new GameObject(); playerRoot.Add<Ant_Player>(); var playerMesh = Boundary(8, 0, playerRoot);
var additive = Boundary(8); additive.gameObject.scene = new Scene(7);
var visible = Boundary(8); var visibleRenderer = visible.gameObject.Add<MeshRenderer>(); visibleRenderer.enabled = true;
visible.gameObject.Add<MeshFilter>().sharedMesh = visible.sharedMesh;
var originals = new[] { new Material(new Shader()), new Material(new Shader()) }; visibleRenderer.sharedMaterials = originals;
var hidden = Boundary(8); var hiddenRenderer = hidden.gameObject.Add<MeshRenderer>(); hiddenRenderer.enabled = true; hiddenRenderer.forceRenderingOff = true;
hidden.gameObject.Add<MeshFilter>().sharedMesh = hidden.sharedMesh;
var unsupported = new GameObject { layer = 8 }.Add<Collider>();
var second = active.gameObject.Add<MeshCollider>(); second.sharedMesh = new Mesh();
Next();
Check(View(active) != null && View(disabled) == null && View(inactive) == null && View(playerMesh) == null, "active selection excludes disabled/inactive/player colliders");
Check(View(additive) != null, "boundary physics from additive scenes included");
Check(View(visible) == null && View(hidden) != null, "matching visible wall omitted while forced-hidden wall included");
Check(visibleRenderer.enabled && visibleRenderer.sharedMaterials[0] == originals[0] && visibleRenderer.sharedMaterials[1] == originals[1] && visibleRenderer.receiveShadows && visibleRenderer.shadowCastingMode == ShadowCastingMode.On, "visible native renderer state/materials unchanged");
Check(hiddenRenderer.enabled && hiddenRenderer.forceRenderingOff, "forced-hidden native renderer unchanged");
Check(Object.Registry.OfType<MeshRenderer>().Count(x => x != null && x.gameObject.name == "TK2 boundary view" && x.transform.parent == active.transform) == 2, "distinct colliders on same object keep distinct views");
Check(p.Log.Infos.Any(x => x.Contains("1 unsupported") && x.Contains("wall=0x80000100")), "unsupported shape and exact native masks recorded");
var firstView = View(active)!; var firstTint = firstView.sharedMaterial!;
Ant_MapData.instance = new GameObject().Add<Ant_MapData>(); Next();
Check(firstView.gameObject.Destroyed && firstTint.Destroyed && View(active) != null, "map change destroys old objects/palette before rebuilding");
Object.Destroy(active); Next(.01f); Check(firstView.gameObject.Destroyed && !p.Log.Warnings.Any(), "destroyed source collider cleaned without fault");
TrackBoundaries.Restore(); Check(ViewCount() == 0 && !visible.sharedMesh!.Destroyed, "unload cleans views without native mesh destruction");

p = Reset();
var box = new GameObject { layer = 8 }.Add<BoxCollider>(); box.center = new(2,3,4); box.size = new(6,8,10);
box.transform.localScale = new(2,3,4); var boxGeo = BoundaryGeometry.Create(NativeWrapper(box))!; var boxTarget = new GameObject().transform; boxGeo.Attach(boxTarget);
Check(boxTarget.parent == box.transform && Same(boxTarget.localPosition, box.center) && Same(boxTarget.localScale, box.size), "box honors native center/dimensions and parent transform");
Check(boxGeo.Mesh.vertices.Length == 8 && boxGeo.Mesh.triangles.Length == 36 && Close(boxGeo.Mesh.vertices.Max(v => v.x), .5f), "box mesh unit extents and indexed faces");
var boxMesh = boxGeo.Mesh; boxGeo.Dispose(); Check(boxMesh.Destroyed, "generated box mesh disposed");

var sphere = new GameObject { layer = 9 }.Add<SphereCollider>(); sphere.radius = 2; sphere.center = new(1,2,3);
sphere.transform.position = new(10,20,30); sphere.transform.localScale = new(-2,3,4);
var sphereGeo = BoundaryGeometry.Create(NativeWrapper(sphere))!; var sphereTarget = new GameObject().transform; sphereGeo.Attach(sphereTarget);
Check(sphereTarget.parent == null && Same(sphereTarget.position, new(8,26,42)) && Same(sphereTarget.localScale, new(8,8,8)), "nonuniform sphere draws uniform radius using largest absolute scale and transformed center");
Check(Close(sphereGeo.Mesh.vertices.Max(v => v.y), 1) && Close(sphereGeo.Mesh.vertices.Min(v => v.y), -1), "sphere mesh diameter normalized");
sphere.radius = 3; sphereGeo.Refresh(); sphereGeo.Apply(sphereTarget);
Check(Same(sphereTarget.localScale, new(12,12,12)), "sphere dimension changes update scale");
var sphereMesh = sphereGeo.Mesh; sphereGeo.Dispose(); Check(sphereMesh.Destroyed, "generated sphere mesh disposed");

p = Reset();
var liveBox = new GameObject { layer = 8 }.Add<BoxCollider>(); liveBox.size = new(3,4,5);
var liveSphere = new GameObject { layer = 9 }.Add<SphereCollider>(); liveSphere.center = new(7,8,9); liveSphere.radius = 2;
var liveCapsule = new GameObject { layer = 10 }.Add<CapsuleCollider>(); liveCapsule.center = new(15,16,17);
Next();
var liveBoxView = View(liveBox)!; var liveSphereView = View(liveSphere)!; var liveCapsuleView = View(liveCapsule)!;
Check(ViewCount() == 3 && liveBoxView != null && liveSphereView != null && liveCapsuleView != null, "all supported primitive collider types receive live overlays");
var ownedPrimitiveMeshes = new[] { DrawnMesh(liveBoxView!), DrawnMesh(liveSphereView!), DrawnMesh(liveCapsuleView!) };
liveSphere.transform.position = new(20,30,40); Next(.01f);
Check(Same(liveSphereView!.transform.position, new(27,38,49)), "moving primitive overlay follows source each tick before a selection rescan");
liveBox.enabled = false; Next(.01f);
Check(liveBoxView!.gameObject.Destroyed && ownedPrimitiveMeshes[0]!.Destroyed, "disabling a primitive immediately removes view and generated mesh");
MenuManager.Instance = new GameObject().Add<MenuManager>(); Next(.01f);
Check(ownedPrimitiveMeshes.All(x => x!.Destroyed) && liveSphere.enabled && liveCapsule.enabled, "menu cleans primitive meshes without changing source physics");

foreach (int direction in new[] { 0, 1, 2 })
{
    var capsule = new GameObject().Add<CapsuleCollider>(); capsule.direction = direction; capsule.radius = 1; capsule.height = 8;
    capsule.center = new(1,2,3); capsule.transform.localScale = new(2,3,4);
    var geo = BoundaryGeometry.Create(NativeWrapper(capsule))!; var target = new GameObject().transform; geo.Attach(target);
    float axis = direction == 0 ? 2 : direction == 1 ? 3 : 4, radial = direction == 2 ? 3 : 4;
    Check(target.parent == null && Same(target.position, new(2,6,12)) && Same(target.localScale, Vector3.one*radial), $"capsule {direction} world center and perpendicular radius");
    Check(Close(geo.Mesh.vertices.Max(v => v.y)*radial, 8*axis/2), $"capsule {direction} scaled total height");
    var orientation = target.rotation.Rotate(new Vector3(0,1,0));
    Check(direction == 0 ? Close(Math.Abs(orientation.x),1) : direction == 1 ? Close(Math.Abs(orientation.y),1) : Close(Math.Abs(orientation.z),1), $"capsule {direction} axis orientation");
    var oldMesh = geo.Mesh; capsule.height = .5f; geo.Refresh(); geo.Apply(target);
    Check(oldMesh.Destroyed && Close(geo.Mesh.vertices.Max(v => v.y),1), $"capsule {direction} clamps short height to diameter and replaces owned mesh");
    var finalMesh = geo.Mesh; geo.Dispose(); Check(finalMesh.Destroyed, $"capsule {direction} final mesh disposed");
}
p = Reset(); Shader.Internal = null; var noMaterial = Boundary(8); Next();
Check(ViewCount() == 0 && noMaterial.enabled && !TrackBoundaries.Visible && p.Log.Warnings.Count == 0, "missing shader/material fails cleanly without changing physics");
Check(p.Log.Infos.Any(x => x.Contains("mask matches 1")), "zero result contains mask-match diagnostics");
TrackBoundaries.Restore();
p = Reset(); var faultWall = Boundary(8); Next();
Camera.current = new GameObject().Add<Camera>(); Camera.current.cullingMask = 1;
Graphics.ThrowOnDraw = true; GL.wireframe = false; TrackBoundaries.Render();
Check(!GL.wireframe, "draw exception restores native raster state");
Check(!TrackBoundaries.Visible && ViewCount() == 0 && faultWall.enabled && !faultWall.sharedMesh!.Destroyed,
    "render failure removes only owned views and retains native collider/mesh");
Check(p.Log.Warnings.Count == 1 && p.Log.Warnings[0].Contains("outline rendering"), "render failure logged once");
Graphics.ThrowOnDraw = false; TrackBoundaries.Render();
Check(p.Log.Warnings.Count == 1 && !GL.wireframe, "faulted module cannot reenter draw loop");
TrackBoundaries.Restore(); Camera.current = null;
Console.WriteLine($"Track boundaries: {assertions} assertions passed.");
