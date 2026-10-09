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
Check(MirrorRace.ShouldMirror(camera), "race mirrored");
Check(Steering(kart) == -.6f, "local steering reversed");
Check(UObject.Scans == 1, "first race camera scan only");
Tick(40.1f); Check(UObject.Scans == 1, "quarter-second scan cadence");
var src = new RenderTexture(); var dst = new RenderTexture();
effect!.OnRenderImage(src, dst); Check(Graphics.Flips == 1, "completed camera image flip");
MenuManager.Instance = new();
Check(!MirrorRace.ShouldMirror(camera) && Steering(kart) == .6f, "menu callbacks guarded before next Tick");
effect.OnRenderImage(src, dst); Check(Graphics.Flips == 1 && Graphics.Copies == 1, "pending callback passthrough in menu");
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
