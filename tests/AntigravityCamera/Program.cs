using TK2.Customization;
using UnityEngine;

int checks=0;
void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
bool Near(float a,float b,float epsilon=.02f)=>MathF.Abs(a-b)<=epsilon;

var up=Vector3.up;
var pivot=Vector3.zero;
var half=new Vector3(.35f,.3f,.2f);
var nativeRot=Quaternion.identity;
var nativePos=new Vector3(0,0,-5);

// Wall collision retracts by the camera volume's near-plane depth and skin.
var wall=new PlaneWorld(-2);
var safe=AntigravityCameraMath.ResolveObstruction(pivot,nativePos,nativeRot,half,wall,out var blocked);
Check(blocked && safe.z > -1.83f && safe.z < -1.7f,"wall clamp must leave the full camera volume outside geometry");

// A larger near-plane depth must move the camera farther inward.
var nearPlaneSafe=AntigravityCameraMath.ResolveObstruction(pivot,nativePos,nativeRot,
    new Vector3(.35f,.3f,.6f),wall,out _);
Check(nearPlaneSafe.z > safe.z+.35f,"near-plane volume depth must affect the wall clearance");

// The cast origin can start inside geometry. The bounded overlap walk finds a clear start,
// then the cast still prevents the camera from crossing the wall.
var insideOrigin=new PositivePlaneWorld(3,.2f);
var recovered=AntigravityCameraMath.ResolveObstruction(Vector3.zero,new Vector3(0,0,5),
    nativeRot,half,insideOrigin,out blocked);
Check(blocked && recovered.z < 2.9f && recovered.z > .2f,"initial overlap recovery must find a clear origin and clamp the endpoint");

// Two separate barriers represent a corner. The nearest valid cast wins without allowing
// camera-box corners to enter either wall.
var corner=new CornerWorld(-1.8f,.7f);
var cornerSafe=AntigravityCameraMath.ResolveObstruction(Vector3.zero,new Vector3(0,0,-5),
    nativeRot,half,corner,out blocked);
Check(blocked && cornerSafe.z > -1.7f,"corner cast must retract to the nearest obstructing surface");

// Surface alignment is eased from world-up. Repeated native outputs do not accumulate the
// gravity transform, and a gradual surface normal does not create a 180 degree flip.
var state=default(AntigravityCameraMath.State);
var pitchedNative=Quaternion.AngleAxis(18,Vector3.right);
var steepUp=new Vector3(0,MathF.Cos(60*Mathf.Deg2Rad),MathF.Sin(60*Mathf.Deg2Rad));
float previousAngle=0;
AntigravityCameraMath.Result aligned=default;
for(int i=0;i<18;i++)
{
    aligned=AntigravityCameraMath.Step(ref state,pivot,new Vector3(0,0,-5),nativeRot,
        steepUp,half,Vector3.zero,1f/60f,.18f,new PlaneWorld(float.NegativeInfinity));
    Vector3 currentUp=aligned.Rotation*Vector3.up;
    float angle=Vector3Angle(Vector3.up,currentUp);
    Check(angle+0.1f>=previousAngle,"camera up must change smoothly along a gradual curve");
    Check(angle<90,"camera orientation must never take a sudden half-turn");
    previousAngle=angle;
}
Check(previousAngle>40 && previousAngle<60,"camera up should approach the active surface normal");
Check(Near(Vector3.Distance(pivot,aligned.Position),5,.08f),"gravity alignment must rotate the native camera offset without accumulating distance");
var pitchedState=default(AntigravityCameraMath.State);
var pitchedResult=AntigravityCameraMath.Step(ref pitchedState,pivot,new Vector3(0,0,-5),pitchedNative,
    steepUp,half,Vector3.zero,1f/60f,.18f,new PlaneWorld(float.NegativeInfinity));
Check(Quaternion.Angle(Quaternion.Inverse(pitchedState.GravityFrame)*pitchedResult.Rotation,pitchedNative)<.1f,
    "gravity alignment must preserve the native camera pitch");

var loopState=default(AntigravityCameraMath.State);
var priorLoopRotation=pitchedNative;
float largestLoopStep=0;
for(int degrees=0;degrees<=360;degrees+=2)
{
    var normal=new Vector3(0,MathF.Cos(degrees*Mathf.Deg2Rad),MathF.Sin(degrees*Mathf.Deg2Rad));
    var loopFrame=AntigravityCameraMath.Step(ref loopState,pivot,new Vector3(0,0,-5),pitchedNative,
        normal,half,Vector3.zero,1f/30f,.04f,new PlaneWorld(float.NegativeInfinity));
    float stepAngle=Quaternion.Angle(priorLoopRotation,loopFrame.Rotation);
    largestLoopStep=MathF.Max(largestLoopStep,stepAngle);
    priorLoopRotation=loopFrame.Rotation;
}
Check(largestLoopStep<5,"transported camera frame must not flip heading at the loop apex");
Check(Quaternion.Angle(Quaternion.Inverse(loopState.GravityFrame)*priorLoopRotation,pitchedNative)<.1f,
    "transported loop alignment must preserve pitch through the full loop");

// An obstructed camera retracts immediately, then releases outward gradually after the wall
// clears; the post-smoothing collision check keeps every returned position safe.
var releaseState=default(AntigravityCameraMath.State);
var closingWall=new PlaneWorld(-2);
var retracted=AntigravityCameraMath.Step(ref releaseState,pivot,nativePos,nativeRot,up,
    half,Vector3.zero,1f/60f,.2f,closingWall);
Check(retracted.Obstructed && retracted.Position.z> -1.83f,"collision retraction must be immediate");
closingWall.WallZ=float.NegativeInfinity;
var released=AntigravityCameraMath.Step(ref releaseState,pivot,nativePos,nativeRot,up,
    half,Vector3.zero,1f/60f,.2f,closingWall);
Check(released.Position.z<retracted.Position.z && released.Position.z> -5,"release must ease outward");

// Exercise the full Unity-facing adapter with fake configuration, cameras and physics. A
// disabled setting performs no antigravity-state query or physics access and leaves outputs intact.
Plugin.Instance=new Plugin();
var gameCamera=new PixelGameKartCamera{Id=17};
var player=new Ant_Player();
AntigravityCamera.ResetAll();AntigravityFeature.Active=true;AntigravityFeature.Calls=0;
Physics.World=new PlaneWorld(float.NegativeInfinity);Physics.ResetCounters();
Plugin.Instance.AntigravityCameraEnabled.Value=false;
var inputPosition=new Vector3(0,0,-5);var inputRotation=Quaternion.identity;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(AntigravityFeature.Calls==0 && Physics.OverlapCalls==0 && Physics.CastCalls==0,
    "disabled adapter must avoid gravity-state and physics access");
Check(Near(inputPosition.z,-5) && Quaternion.Angle(inputRotation,Quaternion.identity)<.01f,
    "disabled adapter must preserve native outputs");

Plugin.Instance.AntigravityCameraEnabled.Value=true;
Physics.World=new PositivePlaneWorld(.1f,.2f);Physics.ResetCounters();
inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(Near(inputPosition.z,-5) && Quaternion.Angle(inputRotation,Quaternion.identity)<.01f,
    "adapter must fail closed when no clear near-plane origin exists");

Physics.World=new PlaneWorld(-2);Physics.ResetCounters();
AntigravityFeature.Calls=0;
inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(AntigravityFeature.Calls==1 && Physics.OverlapCalls>0 && Physics.CastCalls>0,
    "enabled local adapter must query active state and run bounded collision probes");
Check(inputPosition.z+.1f-.025f>-2,$"enabled adapter must keep the near-plane box outside the wall (camera z={inputPosition.z:0.###})");

// The runtime adapter must ignore only its own kart/player colliders and triggers in both
// overlap and cast results; those hits must not retract a clear native camera pose.
AntigravityCamera.ResetAll();Physics.World=null;Physics.ResetCounters();
var ownKart=new PixelEasyCharMoveKartController{parentPlayer=player};
var ownKartCollider=new Collider{Kart=ownKart};
var ownPlayerCollider=new Collider{Player=player};
var triggerCollider=new Collider{isTrigger=true};
Physics.OverlapFixture=new[]{ownKartCollider,ownPlayerCollider,triggerCollider};
Physics.CastFixture=new[]{
    new RaycastHit{collider=ownKartCollider,distance=.1f},
    new RaycastHit{collider=ownPlayerCollider,distance=.2f},
    new RaycastHit{collider=triggerCollider,distance=.3f}};
AntigravityFeature.Calls=0;inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(Physics.OverlapCalls>0 && Physics.CastCalls>0 &&
    Near(inputPosition.z,nativePos.z) && Quaternion.Angle(inputRotation,nativeRot)<.01f,
    "own kart/player and trigger colliders must be ignored by both camera probes");

// A saturated non-alloc overlap buffer may hide another wall, so the adapter treats it as
// blocked and leaves the native output unchanged instead of accepting incomplete evidence.
AntigravityCamera.ResetAll();Physics.ResetCounters();AntigravityFeature.Calls=0;
Physics.OverlapFixture=Enumerable.Range(0,32).Select(_=>new Collider()).ToArray();
inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(Physics.OverlapCalls>0 && Physics.CastCalls==0 && Near(inputPosition.z,nativePos.z) &&
    Quaternion.Angle(inputRotation,nativeRot)<.01f,
    "a full overlap buffer must conservatively block without moving the camera");

// Spectator/cinematic cameras are rejected before attached-state or physics access.
AntigravityCamera.ResetAll();Physics.ResetCounters();AntigravityFeature.Calls=0;
gameCamera.bIsSpectatorCamera=true;inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(AntigravityFeature.Calls==0 && Physics.OverlapCalls==0 && Physics.CastCalls==0 &&
    Near(inputPosition.z,nativePos.z) && Quaternion.Angle(inputRotation,nativeRot)<.01f,
    "spectator cameras must bypass antigravity and collision probes");
gameCamera.bIsSpectatorCamera=false;

Physics.World=new PlaneWorld(float.NegativeInfinity);
AntigravityFeature.Up=steepUp;Time.unscaledDeltaTime=1f/30f;
float tiltedAngle=0;
for(int i=0;i<24;i++)
{
    inputPosition=nativePos;inputRotation=nativeRot;
    AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
    tiltedAngle=Quaternion.Angle(nativeRot,inputRotation);
}
Check(tiltedAngle>45 && tiltedAngle<65,"camera adapter must ease into the engaged slope frame from fresh native poses");
AntigravityFeature.Active=false;inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(Quaternion.Angle(nativeRot,inputRotation)<tiltedAngle,
    "camera adapter must ease back toward world-up when antigravity releases");
Time.unscaledDeltaTime=1f/60f;AntigravityFeature.Active=true;

// Turning the setting off clears the transition state and restores future native frames.
Plugin.Instance.AntigravityCameraEnabled.Value=false;
inputPosition=nativePos;inputRotation=nativeRot;
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(Near(inputPosition.z,-5) && Quaternion.Angle(inputRotation,Quaternion.identity)<.01f,
    "disabling antigravity camera must immediately restore native framing");
Plugin.Instance.AntigravityCameraEnabled.Value=true;
Plugin.OfflineLabAllowed=false;AntigravityFeature.Calls=0;Physics.ResetCounters();
AntigravityCamera.Apply(gameCamera,player,ref inputPosition,ref inputRotation,65);
Check(AntigravityFeature.Calls==0 && Physics.OverlapCalls==0 && Physics.CastCalls==0,
    "online or restoring modes must not query antigravity state or camera collision");
Plugin.OfflineLabAllowed=true;

Console.WriteLine($"AntigravityCamera: {checks} checks passed");

static float Vector3Angle(Vector3 a,Vector3 b)=>MathF.Acos(Math.Clamp(Vector3.Dot(a.normalized,b.normalized),-1,1))*Mathf.Rad2Deg;

sealed class PlaneWorld(float wallZ) : AntigravityCameraMath.ICollisionProbe
{
    internal float WallZ=wallZ;
    internal float EmbeddedUntilZ=float.NegativeInfinity;
    public bool IsClear(Vector3 center,Vector3 half,Quaternion orientation) =>
        center.z-half.z > WallZ && center.z > EmbeddedUntilZ;
    public bool Cast(Vector3 origin,Vector3 direction,float distance,Vector3 half,
        Quaternion orientation,out float hitDistance)
    {
        hitDistance=float.PositiveInfinity;
        if(direction.z>=-.0001f)return false;
        float distanceToWall=(origin.z-half.z-WallZ)/-direction.z;
        if(distanceToWall<0)distanceToWall=0;
        if(distanceToWall>distance)return false;
        hitDistance=distanceToWall;return true;
    }
}

sealed class PositivePlaneWorld(float wallZ,float embeddedUntil):AntigravityCameraMath.ICollisionProbe
{
    public bool IsClear(Vector3 c,Vector3 h,Quaternion r)=>c.z+h.z<wallZ && c.z>embeddedUntil;
    public bool Cast(Vector3 o,Vector3 d,float distance,Vector3 h,Quaternion r,out float hit)
    {
        hit=float.PositiveInfinity;if(d.z<=.0001f)return false;
        float at=(wallZ-o.z-h.z)/d.z;if(at<0)at=0;if(at>distance)return false;hit=at;return true;
    }
}

sealed class CornerWorld(float wallZ,float wallX):AntigravityCameraMath.ICollisionProbe
{
    public bool IsClear(Vector3 c,Vector3 h,Quaternion r)=>c.z-h.z>wallZ && c.x+h.x<wallX;
    public bool Cast(Vector3 o,Vector3 d,float distance,Vector3 h,Quaternion r,out float hit)
    {
        hit=float.PositiveInfinity;bool found=false;
        if(d.z<-.0001f){float z=(o.z-h.z-wallZ)/-d.z;if(z>=0&&z<=distance){hit=z;found=true;}}
        if(d.x>.0001f){float x=(wallX-(o.x+h.x))/d.x;if(x>=0&&x<=distance&&x<hit){hit=x;found=true;}}
        return found;
    }
}
