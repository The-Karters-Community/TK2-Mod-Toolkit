// Exercise the real panel's interaction paths. These stubs do not prove Unity rendering.
using System;
using System.Collections.Generic;

namespace BepInEx.Configuration {
 public class ConfigEntry<T> { public T Value; public object DefaultValue; public ConfigEntry(T value) { Value=value;DefaultValue=value!; } }
}
namespace UnityEngine {
 public struct Vector2 { }
 public struct Vector3 { public static Vector3 zero; public Vector3(float x,float y,float z) {} }
 public struct Quaternion { public static Quaternion identity; }
 public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 v,Quaternion q,Vector3 s) => new(); }
 public struct Color { public static Color white; public Color(float r,float g,float b,float a) {} }
 public struct Rect { public float height; public Rect(float x,float y,float w,float h) { height=h; } }
 public enum FontStyle { Normal,Bold }
 public enum TextAnchor { MiddleRight }
 public enum CursorLockMode { None,Locked }
 public enum KeyCode { None,F8,Escape,LeftShift,RightShift }
 public class GUIStyleState { public Color textColor; }
 public class GUIStyle {
  public int fontSize; public FontStyle fontStyle; public bool wordWrap; public TextAnchor alignment;
  public GUIStyleState normal=new(); public GUIStyle() {} public GUIStyle(GUIStyle other) {}
 }
 public class GUISkin { public GUIStyle label=new(); }
 public class GUIContent { public string text,tooltip; public GUIContent(string t,string h) {text=t;tooltip=h;} }
 public class Texture2D { public static Texture2D whiteTexture=new(); }
 public static class GUI {
  public static Matrix4x4 matrix; public static Color color,backgroundColor,contentColor;
  public static int depth; public static bool enabled=true; public static string tooltip="",Clicked="";
  public static GUISkin skin=new(); public static HashSet<string> Labels=new();
  public static float ScrollHeight,ContentHeight;
  public static void Label(Rect rect,string text,GUIStyle? style) { Labels.Add(text); }
  public static bool Button(Rect rect,string text) { if (!enabled || Clicked!=text) return false;Clicked="";return true; }
  public static bool Button(Rect rect,GUIContent content) => Button(rect,content.tooltip);
  public static void DrawTexture(Rect rect,Texture2D texture) {}
  public static float HorizontalSlider(Rect rect,float value,float low,float high) => value;
  public static Vector2 BeginScrollView(Rect rect,Vector2 scroll,Rect content) {ScrollHeight=rect.height;ContentHeight=content.height;return scroll;}
  public static void EndScrollView() {}
 }
 public static class Input {
  public static KeyCode Down;
  public static bool GetKeyDown(KeyCode key) { if (key!=Down || key==KeyCode.None) return false;Down=KeyCode.None;return true; }
  public static bool GetKey(KeyCode key) => false;
 }
 public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
 public static class Time { public static float unscaledTime; }
 public static class Screen { public static int width=1920,height=1080; }
 public static class Mathf { public static float Clamp(float v,float min,float max) => Math.Clamp(v,min,max); }
}
namespace TK2.Customization {
 using BepInEx.Configuration;
 internal class Plugin {
  public ConfigEntry<bool> CameraEnabled=new(false), CameraPanelEnabled=new(true), PreserveKartFraming=new(true), AimAtKart=new(false);
  public ConfigEntry<string> CameraPanelKey=new("F8");
  public ConfigEntry<float> CameraPanelScale=new(1), Fov=new(65), CameraDistance=new(1), CameraHeight=new(0), CameraLateral=new(0),
   CameraTargetHeight=new(.5f), CameraPitch=new(0), CameraYaw=new(0), CameraRoll=new(0), CameraSmoothing=new(.12f);
  public TestLog Log=new();
 }
 internal class TestLog { public void LogWarning(string text) => throw new Exception(text); }
 internal static class CameraFeature { public static bool RaceAvailable; }
 internal static class LiveConfig {
  public static int Flushes; public static string Status="Saved";
  public static void Change<T>(Plugin p,ConfigEntry<T> entry,T value,float time) {entry.Value=value;}
  public static void Flush(Plugin p) {Flushes++;}
 }
 internal static class StudioBehaviour {
  public static string Clicked="";
  public static bool DrawCheckBox(UnityEngine.Rect r,bool value,string text) {if(Clicked!=text)return value;Clicked="";return !value;}
 }
}
