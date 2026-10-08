namespace BepInEx.Configuration {
 public sealed class ConfigEntry<T> { public T Value; public ConfigEntry(T value) { Value = value; } }
 public sealed class ConfigDescription { public ConfigDescription(string text, object acceptable) {} }
 public sealed class AcceptableValueRange<T> { public AcceptableValueRange(T min,T max) {} }
 public sealed class ConfigFile { public ConfigEntry<T> Bind<T>(string section,string name,T value,object description) => new(value); }
}
namespace UnityEngine {
 public enum KeyCode { V,B,G,LeftArrow,RightArrow,R,P,L }
 public enum FindObjectsSortMode { None }
 public static class Time { public static float time,unscaledTime,deltaTime=.05f,timeScale=1; public static int frameCount; }
 public static class Input {
  public static readonly HashSet<KeyCode> Held = new(), Down = new();
  public static bool GetKey(KeyCode key) => Held.Contains(key);
  public static bool GetKeyDown(KeyCode key) => Down.Contains(key);
 }
 public static class Object { public static PixelKartPhysics[] Karts=Array.Empty<PixelKartPhysics>(); public static int Scans; public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) { Scans++; return Karts.Cast<T>().ToArray(); } }
 public struct Vector3 {
  public float x,y,z; public Vector3(float X,float Y,float Z){x=X;y=Y;z=Z;}
  public static Vector3 zero=>new(0,0,0); public static Vector3 up=>new(0,1,0); public static Vector3 forward=>new(0,0,1);
  public float sqrMagnitude=>x*x+y*y+z*z; public float magnitude=>MathF.Sqrt(sqrMagnitude); public Vector3 normalized=>magnitude>.00001f?this/magnitude:zero;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator -(Vector3 a)=>new(-a.x,-a.y,-a.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
  public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 }
 public struct Quaternion { public static Vector3 operator *(Quaternion q,Vector3 v)=>v; }
 public static class Mathf {
  public const float Deg2Rad=MathF.PI/180; public static float Clamp(float v,float lo,float hi)=>Math.Clamp(v,lo,hi); public static float Clamp01(float v)=>Clamp(v,0,1);
  public static float Min(float a,float b)=>MathF.Min(a,b); public static int Min(int a,int b)=>Math.Min(a,b); public static float Max(float a,float b)=>MathF.Max(a,b);
  public static float Cos(float a)=>MathF.Cos(a); public static float Abs(float a)=>MathF.Abs(a); public static float Lerp(float a,float b,float c)=>a+(b-a)*Clamp01(c);
 }
}
namespace UnityEngine.SceneManagement { public struct Scene { public int handle; } public static class SceneManager { public static int Scene; public static Scene GetActiveScene()=>new(){handle=Scene}; } }
namespace KinematicCharacterController {
 public sealed class KinematicCharacterMotorState { public UnityEngine.Vector3 Position,BaseVelocity; public UnityEngine.Quaternion Rotation; }
 public sealed class KinematicCharacterMotor {
  public UnityEngine.Vector3 Position,Velocity;
  public KinematicCharacterMotorState GetState()=>new(){Position=Position,BaseVelocity=Velocity};
  public void ApplyState(KinematicCharacterMotorState state,bool stable){Position=state.Position;Velocity=state.BaseVelocity;}
 }
}
public static class Ant_CurrentGameConfiguration { public enum ERaceState { E_RACE_RUNNING, Stopped } public static ERaceState eCurrentRaceState=ERaceState.E_RACE_RUNNING; }
public sealed class HpBarController { public int currentHp=100; }
public sealed class Ant_Player { public enum EPlayerType { E_HUMAN_LOCAL, E_AI_LOCAL, E_NETWORK } public EPlayerType ePlayerType; public int eAntLocalPlayerNr; public HpBarController hpBarController=new(); }
public sealed class PTK_VehicleOutsideMapValidator { public bool bIsPlayerRespawning; }
public sealed class PixelEasyCharMoveKartController {
 public Ant_Player parentPlayer=new(); public KinematicCharacterController.KinematicCharacterMotor Motor=new(); public PTK_VehicleOutsideMapValidator outsideMapValidator=new(); public bool Replay;
 public bool IsPlayingReplayData()=>Replay; public UnityEngine.Vector3 GetKartPos()=>Motor.Position; public UnityEngine.Vector3 GetKartVelocity()=>Motor.Velocity; public UnityEngine.Quaternion GetKartRotation()=>new();
}
public sealed class PixelKartPhysics {
 private static int _next; public int Id=++_next; public bool isActiveAndEnabled=true,bWasGrounded=true,bIsDrifting; public float fTimeInAir; public UnityEngine.Vector3 Added; public PixelEasyCharMoveKartController kartController=new();
 public int GetInstanceID()=>Id; public void SetSteeringPhysicsRotation(UnityEngine.Quaternion rotation,bool force) {}
}
namespace TK2.Customization {
 public interface IModRecipe { string Name{get;} bool ChangesGameplay{get;} void Configure(BepInEx.Configuration.ConfigFile config); void Tick(); void Restore(); }
 public static class Plugin { public static bool OfflineLabAllowed=true; }
 public static class ReadableGame { public static void AddVelocity(PixelKartPhysics kart,UnityEngine.Vector3 extra)=>kart.Added+=extra; }
}
