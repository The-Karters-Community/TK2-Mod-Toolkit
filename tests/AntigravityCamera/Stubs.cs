using System;

namespace UnityEngine
{
    internal static class Mathf
    {
        internal const float Deg2Rad = MathF.PI / 180f;
        internal const float Rad2Deg = 180f / MathF.PI;
        internal static float Min(float a, float b) => MathF.Min(a, b);
        internal static float Max(float a, float b) => MathF.Max(a, b);
        internal static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
        internal static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max);
        internal static int CeilToInt(float v) => (int)MathF.Ceiling(v);
        internal static float Exp(float v) => MathF.Exp(v);
        internal static float Tan(float v) => MathF.Tan(v);
    }

    internal struct Vector3
    {
        internal float x, y, z;
        internal Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal static Vector3 zero => new(0, 0, 0);
        internal static Vector3 up => new(0, 1, 0);
        internal static Vector3 down => new(0, -1, 0);
        internal static Vector3 right => new(1, 0, 0);
        internal static Vector3 forward => new(0, 0, 1);
        internal float magnitude => MathF.Sqrt(sqrMagnitude);
        internal float sqrMagnitude => x * x + y * y + z * z;
        internal Vector3 normalized => magnitude > 1e-8f ? this / magnitude : zero;
        internal static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        internal static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        internal static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        internal static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b-a) * Math.Clamp(t, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x/b,a.y/b,a.z/b);
    }

    internal struct Quaternion
    {
        internal float x, y, z, w;
        internal Quaternion(float x,float y,float z,float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
        internal static Quaternion identity => new(0,0,0,1);
        internal static Quaternion AngleAxis(float degrees, Vector3 axis)
        {
            axis = axis.normalized; float h=degrees*Mathf.Deg2Rad*.5f, s=MathF.Sin(h);
            return new(axis.x*s,axis.y*s,axis.z*s,MathF.Cos(h));
        }
        internal static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            from=from.normalized; to=to.normalized; float dot=Vector3.Dot(from,to);
            if (dot < -.999999f) return AngleAxis(180, MathF.Abs(from.x)<.8f ? new Vector3(1,0,0) : new Vector3(0,0,1));
            var cross=Vector3.Cross(from,to); float s=MathF.Sqrt((1+dot)*2);
            return s < 1e-8f ? identity : new(cross.x/s,cross.y/s,cross.z/s,s*.5f);
        }
        internal static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t=Math.Clamp(t,0,1); float dot=a.x*b.x+a.y*b.y+a.z*b.z+a.w*b.w;
            if(dot<0){b=new(-b.x,-b.y,-b.z,-b.w);dot=-dot;}
            if(dot>.9995f) return Normalize(new(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t,a.w+(b.w-a.w)*t));
            float theta=MathF.Acos(Math.Clamp(dot,-1,1)), sin=MathF.Sin(theta);
            float aa=MathF.Sin((1-t)*theta)/sin, bb=MathF.Sin(t*theta)/sin;
            return new(a.x*aa+b.x*bb,a.y*aa+b.y*bb,a.z*aa+b.z*bb,a.w*aa+b.w*bb);
        }
        internal static Quaternion Normalize(Quaternion q) { float m=MathF.Sqrt(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w);return new(q.x/m,q.y/m,q.z/m,q.w/m); }
        internal static Quaternion Inverse(Quaternion q)=>new(-q.x,-q.y,-q.z,q.w);
        internal static float Angle(Quaternion a, Quaternion b)
        { float d=MathF.Abs(a.x*b.x+a.y*b.y+a.z*b.z+a.w*b.w);return MathF.Acos(Math.Clamp(d,-1,1))*2*Mathf.Rad2Deg; }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(
            a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,
            a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,
            a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,
            a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        { var u=new Vector3(q.x,q.y,q.z);return u*(2*Vector3.Dot(u,v))+v*(q.w*q.w-Vector3.Dot(u,u))+Vector3.Cross(u,v)*(2*q.w); }
    }

    internal class Camera { public float nearClipPlane=.1f, aspect=16f/9f; }
    internal class Collider
    {
        public bool isTrigger;
        public global::TK2.Customization.Ant_Player? Player;
        public global::TK2.Customization.PixelEasyCharMoveKartController? Kart;
        public T? GetComponentInParent<T>() where T:class
        { if(typeof(T)==typeof(global::TK2.Customization.Ant_Player))return Player as T;if(typeof(T)==typeof(global::TK2.Customization.PixelEasyCharMoveKartController))return Kart as T;return null; }
    }
    internal struct RaycastHit { public Collider? collider; public float distance; }
    internal enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    internal static class Physics
    {
        internal const int DefaultRaycastLayers = -5;
        internal static TK2.Customization.AntigravityCameraMath.ICollisionProbe? World;
        internal static Collider[]? OverlapFixture;
        internal static RaycastHit[]? CastFixture;
        internal static int OverlapCalls, CastCalls;
        internal static int OverlapBoxNonAlloc(Vector3 center,Vector3 half,Collider[] results,Quaternion rotation,int mask,QueryTriggerInteraction triggers)
        {
            OverlapCalls++;
            if(OverlapFixture!=null){Array.Copy(OverlapFixture,results,Math.Min(OverlapFixture.Length,results.Length));return OverlapFixture.Length;}
            if(World?.IsClear(center,half,rotation)!=false)return 0;results[0]=new Collider();return 1;
        }
        internal static int BoxCastNonAlloc(Vector3 origin,Vector3 half,Vector3 direction,RaycastHit[] results,Quaternion rotation,float distance,int mask,QueryTriggerInteraction triggers)
        {
            CastCalls++;
            if(CastFixture!=null){Array.Copy(CastFixture,results,Math.Min(CastFixture.Length,results.Length));return CastFixture.Length;}
            if(World!=null&&World.Cast(origin,direction,distance,half,rotation,out float hit)){results[0]=new RaycastHit{collider=new Collider(),distance=hit};return 1;}return 0;
        }
        internal static void ResetCounters(){OverlapCalls=CastCalls=0;OverlapFixture=null;CastFixture=null;}
    }
    internal static class Time { internal static float unscaledDeltaTime=1f/60f; }
}

namespace TK2.Customization
{
    using UnityEngine;
    internal sealed class ConfigEntry<T> { internal T Value; internal ConfigEntry(T value)=>Value=value; }
    internal sealed class Plugin { internal static Plugin? Instance; internal static bool OfflineLabAllowed=true; internal ConfigEntry<bool> AntigravityCameraEnabled=new(true); internal Logger Log=new(); }
    internal sealed class Logger { internal void LogError(string message){} }
    internal static class AntigravityFeature
    {
        internal static bool Active; internal static Vector3 Up=Vector3.up; internal static int Calls;
        internal static bool TryGetActiveFrame(Ant_Player player,out Vector3 up,out Vector3 forward)
        {Calls++;up=Up;forward=Vector3.forward;return Active;}
    }
    internal sealed class PixelSDK_Camera { internal Camera unityCamera=new(); }
    internal static class ReadableGame { internal static PixelSDK_Camera? GetCamera(PixelGameKartCamera camera)=>camera.SdkCamera; }
    internal sealed class PixelGameKartCamera
    { internal int Id; internal bool bIsSpectatorCamera;internal float fRacingCameraStrength=1;internal PixelSDK_Camera? SdkCamera=new();internal int GetInstanceID()=>Id; }
    internal sealed class Ant_Player
    {
        internal enum EPlayerType { E_HUMAN_LOCAL, E_AI }
        internal EPlayerType ePlayerType=EPlayerType.E_HUMAN_LOCAL;
        internal VisualInstanceSyncedParams? visualInstanceSyncedParams=new();
    }
    internal sealed class VisualInstanceSyncedParams { internal Vector3 vCurPos=Vector3.zero; }
    internal sealed class PixelEasyCharMoveKartController { internal Ant_Player? parentPlayer; }
}
