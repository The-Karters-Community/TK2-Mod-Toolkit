using System;
using System.Reflection;
using TK2.Customization;
using UnityEngine;
using UObject = UnityEngine.Object;

int assertions = 0;
void Check(bool condition, string name) { assertions++; if (!condition) throw new Exception(name); }
var config = new BepInEx.Configuration.ConfigFile();
var mirror = new MirrorRace(); mirror.Configure(config); config.Enabled.Value = true;
var camera = new GameObject().AddComponent<Camera>();
var player = new GameObject().AddComponent<Ant_Player>();
player.ePlayerType = Ant_Player.EPlayerType.E_HUMAN_LOCAL;
player.gameplayCamera = new CameraBrain { Data = new CameraData { unityCamera = camera } };
var kart = new GameObject().AddComponent<PixelEasyCharMoveKartController>(); kart.parentPlayer = player;
var steer = typeof(MirrorRace).GetMethod("BeforeSteerInput", BindingFlags.NonPublic | BindingFlags.Static)!;
float Steering(PixelEasyCharMoveKartController value) { object[] args = { value, .6f }; steer.Invoke(null, args); return (float)args[1]; }
void Tick(float at) { Time.unscaledTime = at; mirror.Tick(); }
void Idle(string reason, float time)
{
    int scans = UObject.Scans;
    for (int i = 0; i < 1000; i++) Tick(time + i * .016f);
    Check(UObject.Scans == scans, reason + ": zero scene scans");
    Check(camera.gameObject.GetComponent<MirrorImageEffect>() == null, reason + ": no retained render callback");
    Check(!MirrorRace.ShouldMirror(camera), reason + ": no image flip");
    Check(Steering(kart) == .6f, reason + ": native steering");
    Check(!camera.Destroyed && !camera.gameObject.Destroyed && camera.enabled, reason + ": camera preserved");
}
Ant_MapData.instance = new(); MenuManager.Instance = new();
Idle("menu with retained map/kart", 0);
MenuManager.Instance = null; Ant_MapData.instance = null;
Idle("no map/loading", 20);
Ant_MapData.instance = new(); Tick(40);
var effect = camera.gameObject.GetComponent<MirrorImageEffect>();
Check(effect != null && effect.enabled, "attach before running state/countdown");
Check(MirrorRace.ShouldMirror(camera) && Steering(kart) == -.6f, "camera and steering mirror during countdown");
var src = new RenderTexture(); var dst = new RenderTexture();
effect!.OnRenderImage(src, dst); Check(Graphics.Flips == 1 && Graphics.Copies == 0, "countdown image mirrors immediately");
Ant_CurrentGameConfiguration.eCurrentRaceState = Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
Check(MirrorRace.ShouldMirror(camera), "race mirrored");
Check(Steering(kart) == -.6f, "local steering reversed");
Check(UObject.Scans == 1, "first race camera scan only");
Tick(40.1f); Check(UObject.Scans == 1, "quarter-second scan cadence");
effect.OnRenderImage(src, dst); Check(Graphics.Flips == 2, "completed camera image flip");
MenuManager.Instance = new();
Check(!MirrorRace.ShouldMirror(camera) && Steering(kart) == .6f, "menu callbacks guarded before next Tick");
effect.OnRenderImage(src, dst); Check(Graphics.Flips == 2 && Graphics.Copies == 1, "pending callback passthrough in menu");
Idle("return to menu", 41); Check(effect.Destroyed, "previous effect destroyed");
MenuManager.Instance = null; Tick(60);
var second = camera.gameObject.GetComponent<MirrorImageEffect>();
Check(second != null && second != effect && MirrorRace.ShouldMirror(camera), "second race reacquires fresh callback");
config.Enabled.Value = false;
Check(!MirrorRace.ShouldMirror(camera) && Steering(kart) == .6f, "config reload guarded before Tick");
Idle("disabled", 61); Check(second!.Destroyed, "disable destroys callback");
config.Enabled.Value = true; Tick(80);
Plugin.OfflineLabAllowed = false;
Check(!MirrorRace.ShouldMirror(camera) && Steering(kart) == .6f, "online callbacks immediately guarded");
Idle("online", 81);
Plugin.OfflineLabAllowed = true; Tick(100);
Ant_MapData.instance!.Destroyed = true;
Idle("destroyed map", 101);
Ant_MapData.instance = new(); Tick(120);
var active = camera.gameObject.GetComponent<MirrorImageEffect>()!;
var markerObject = new GameObject();
var marker = markerObject.AddComponent<PTK_MarkerIndicator>();
var waypoint = markerObject.AddComponent<Waypoint_Indicator>();
marker.markerIndicatorImpl = waypoint;
var markerParent = new RectTransform { rect = new Rect(0, 0, 1000, 500), pivot = new Vector2(.5f, .5f) };
var markerRoot = new RectTransform { parent = markerParent, position = new Vector3(240, 30, 0) };
waypoint.SetMarkerRoot(markerRoot);
var markerCanvas = new GameObject().AddComponent<Canvas>();
var target = camera.gameObject; // Native distance reference, NOT the tracked racer.
waypoint.SetTracking(camera, markerCanvas, target);
var mirrorMarker = typeof(MirrorRace).GetMethod("AfterWaypointUpdate", BindingFlags.NonPublic | BindingFlags.Static)!;
mirrorMarker.Invoke(null, new object[] { waypoint });
Check(markerRoot.position.x == 960 && markerRoot.position.y == 30, "mirror native marker placement and preserve racer height");
var beforeMarker = typeof(MirrorRace).GetMethod("BeforeWaypointUpdate", BindingFlags.NonPublic | BindingFlags.Static)!;
beforeMarker.Invoke(null, new object[] { waypoint });
Check(markerRoot.position.x == 240, "restore native position before next update to avoid oscillation");
markerRoot.position = new Vector3(320, 35, 0); // Native follows the other racer.
target.transform.position = new Vector3(900, 900, 900); // Camera steering must not be used as a racer target.
mirrorMarker.Invoke(null, new object[] { waypoint });
Check(markerRoot.position.x == 880 && markerRoot.position.y == 35, "marker follows native racer placement independently of distance camera");
beforeMarker.Invoke(null, new object[] { waypoint });
mirrorMarker.Invoke(null, new object[] { waypoint });
Check(markerRoot.position.x == 880, "repeated/early-return native updates do not toggle marker position");
beforeMarker.Invoke(null, new object[] { waypoint });
camera.pixelRect = new Rect(600, 0, 600, 800);
markerRoot.position = new Vector3(700, 50, 0);
mirrorMarker.Invoke(null, new object[] { waypoint });
Check(markerRoot.position.x == 1100, "split-screen mirrors inside the correct camera viewport");
beforeMarker.Invoke(null, new object[] { waypoint });
camera.pixelRect = new Rect(0, 0, 1200, 800);

var board = new GameObject().AddComponent<PTK_CounterBoardController>();
board.numbersTileMatInstance = new Material();
var beforeBoard = typeof(MirrorRace).GetMethod("BeforeCountdownUpdate", BindingFlags.NonPublic | BindingFlags.Static)!;
var afterBoard = typeof(MirrorRace).GetMethod("AfterCountdownUpdate", BindingFlags.NonPublic | BindingFlags.Static)!;
foreach (float tileX in new[] { 0f, .5f })
{
    beforeBoard.Invoke(null, new object[] { board });
    board.numbersTileMatInstance.mainTextureOffset = new Vector2(tileX, .5f);
    afterBoard.Invoke(null, new object[] { board });
    Check(board.numbersTileMatInstance.mainTextureScale.x == -.5f, "countdown glyph reflected before camera flip");
    Check(board.numbersTileMatInstance.mainTextureOffset.x == tileX + .5f && board.numbersTileMatInstance.mainTextureOffset.y == .5f, "countdown stays within selected atlas tile");
    afterBoard.Invoke(null, new object[] { board });
    Check(board.numbersTileMatInstance.mainTextureScale.x == -.5f, "nested countdown update does not flip twice");
    beforeBoard.Invoke(null, new object[] { board });
    Check(board.numbersTileMatInstance.mainTextureScale.x == .5f && board.numbersTileMatInstance.mainTextureOffset.x == tileX, "native countdown scale and offset restored before next update");
}
afterBoard.Invoke(null, new object[] { board });
mirror.Restore();
Check(board.numbersTileMatInstance.mainTextureScale.x == .5f, "disable restores countdown material");
Tick(120); active = camera.gameObject.GetComponent<MirrorImageEffect>()!;
var unrelatedWaypoint = new GameObject().AddComponent<Waypoint_Indicator>();
var unrelatedRoot = new RectTransform { parent = markerParent, localPosition = new Vector3(-240, 0, 0) };
unrelatedWaypoint.SetMarkerRoot(unrelatedRoot);
unrelatedWaypoint.SetTracking(camera, markerCanvas, target);
mirrorMarker.Invoke(null, new object[] { unrelatedWaypoint });
Check(unrelatedRoot.localPosition.x == -240, "non-opponent waypoint UI remains unchanged");
camera.enabled = false;
Check(!MirrorRace.ShouldMirror(camera), "inactive camera never mirrored");
Tick(120.3f); Check(active.Destroyed, "inactive camera callback removed");
camera.enabled = true; player.gameplayCamera!.bIsSpectatorCamera = true; Tick(120.6f);
Check(camera.gameObject.GetComponent<MirrorImageEffect>() == null && Steering(kart) == .6f, "spectator excluded");
player.gameplayCamera.bIsSpectatorCamera = false;
player.ePlayerType = Ant_Player.EPlayerType.E_AI_LOCAL; Tick(121);
Check(camera.gameObject.GetComponent<MirrorImageEffect>() == null && Steering(kart) == .6f, "AI excluded");
player.ePlayerType = Ant_Player.EPlayerType.E_HUMAN_LOCAL; kart.gameObject.activeInHierarchy = false; Tick(121.3f);
Check(camera.gameObject.GetComponent<MirrorImageEffect>() == null && Steering(kart) == .6f, "inactive kart excluded");
kart.gameObject.activeInHierarchy = true; Tick(122);
mirror.Restore(); mirror.Restore();
Check(camera.gameObject.GetComponent<MirrorImageEffect>() == null && Steering(kart) == .6f, "repeated cleanup safe");
Console.WriteLine($"Mirror Race: {assertions} production-source assertions passed.");
