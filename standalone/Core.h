#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>

namespace race {
constexpr uint32_t Magic = 0x42525431, Version = 2, AutomaticPlayer = 6;
inline uint32_t choosePlayer(uint32_t requested,uint32_t available) {
 available&=63;
 if(requested<6&&(available&(1u<<requested)))return requested;
 for(uint32_t i=0;i<6;i++)if(available&(1u<<i))return i;
 return AutomaticPlayer;
}
struct LocalPlayers {
 std::array<uint64_t,6> seen{};
 void observe(uint32_t slot,uint64_t now){if(slot<6)seen[slot]=now;}
 uint32_t mask(uint64_t now) const {
  uint32_t result{};for(uint32_t i=0;i<6;i++)if(seen[i]&&now>=seen[i]&&now-seen[i]<1000)result|=1u<<i;
  return result;
 }
};
struct Vec3 { float x{}, y{}, z{}; };
struct Quat { float x{}, y{}, z{}, w{1}; };
inline Vec3 operator+(Vec3 a, Vec3 b) { return {a.x+b.x,a.y+b.y,a.z+b.z}; }
inline Vec3 operator-(Vec3 a, Vec3 b) { return {a.x-b.x,a.y-b.y,a.z-b.z}; }
inline Vec3 operator*(Vec3 a, float b) { return {a.x*b,a.y*b,a.z*b}; }
inline float dot(Vec3 a, Vec3 b) { return a.x*b.x+a.y*b.y+a.z*b.z; }
inline Vec3 cross(Vec3 a, Vec3 b) { return {a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x}; }
inline Vec3 normalized(Vec3 v) { float n=std::sqrt(dot(v,v)); return n>.00001f?v*(1/n):Vec3{}; }
inline Quat multiply(Quat a, Quat b) { return {
 a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y, a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,
 a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w, a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z}; }
inline Vec3 rotate(Quat q, Vec3 v) { auto t=cross({q.x,q.y,q.z},v)*2; return v+t*q.w+cross({q.x,q.y,q.z},t); }
inline Quat euler(float x,float y,float z) {
 constexpr float halfRad=3.14159265358979323846f/360;
 x*=halfRad;y*=halfRad;z*=halfRad;
 return multiply(multiply({0,std::sin(y),0,std::cos(y)},{std::sin(x),0,0,std::cos(x)}),{0,0,std::sin(z),std::cos(z)});
}
inline Quat look(Vec3 forward, Quat fallback) {
 forward=normalized(forward); if(dot(forward,forward)<.5f)return fallback;
 Vec3 right=normalized(cross({0,1,0},forward));if(dot(right,right)<.5f)return fallback;
 Vec3 up=cross(forward,right);float t=right.x+up.y+forward.z; Quat q;
 if(t>0){float s=std::sqrt(t+1)*2;q={(up.z-forward.y)/s,(forward.x-right.z)/s,(right.y-up.x)/s,s*.25f};}
 else if(right.x>up.y&&right.x>forward.z){float s=std::sqrt(1+right.x-up.y-forward.z)*2;q={s*.25f,(up.x+right.y)/s,(forward.x+right.z)/s,(up.z-forward.y)/s};}
 else if(up.y>forward.z){float s=std::sqrt(1+up.y-right.x-forward.z)*2;q={(up.x+right.y)/s,s*.25f,(forward.y+up.z)/s,(forward.x-right.z)/s};}
 else{float s=std::sqrt(1+forward.z-right.x-up.y)*2;q={(forward.x+right.z)/s,(forward.y+up.z)/s,s*.25f,(right.y-up.x)/s};}
 return q;
}
struct Settings {
 uint32_t camera{}, preserve{1}, aim{}, viewer{1}, ghosts{1}, player{AutomaticPlayer};
 float fov{65},distance{1},height{},side{},target{.5f},pitch{},yaw{},roll{},smoothing{.12f};
 float overlayX{50},overlayY{0},overlayScale{1},opacity{.92f};
};
inline float bound(float v,float lo,float hi,float fallback) {return std::isfinite(v)?std::clamp(v,lo,hi):fallback;}
inline Settings sanitize(Settings s) {
 s.camera=!!s.camera;s.preserve=!!s.preserve;s.aim=!!s.aim;s.viewer=!!s.viewer;s.ghosts=!!s.ghosts;s.player=std::min(s.player,AutomaticPlayer);
 s.fov=bound(s.fov,35,110,65);s.distance=bound(s.distance,.25f,4,1);s.height=bound(s.height,-5,8,0);s.side=bound(s.side,-4,4,0);
 s.target=bound(s.target,-1,3,.5f);s.pitch=bound(s.pitch,-45,45,0);s.yaw=bound(s.yaw,-90,90,0);s.roll=bound(s.roll,-30,30,0);
 s.smoothing=bound(s.smoothing,0,2,.12f);s.overlayX=bound(s.overlayX,0,100,50);s.overlayY=bound(s.overlayY,0,100,0);
 s.overlayScale=bound(s.overlayScale,.25f,2,1);s.opacity=bound(s.opacity,.45f,1,.92f);return s;
}
inline float blend(float dt,float seconds) {return seconds<=0?1:1-std::exp(-std::clamp(dt,0.f,.25f)/seconds);}
inline float distanceScale(float before,float after,bool preserve,float distance) {
 constexpr double rad=3.14159265358979323846/360;
 return preserve?std::clamp(float(std::tan(before*rad)/std::tan(after*rad))*distance,.2f,6.f):distance;
}
inline void framing(Settings s,Vec3 kart,Vec3& pos,Quat& rot,float& fov) {
 pos=kart+(pos-kart)*distanceScale(fov,s.fov,s.preserve!=0,s.distance)+Vec3{0,s.height,0}+rotate(rot,{1,0,0})*s.side;
 if(s.aim)rot=look(kart+Vec3{0,s.target,0}-pos,rot);
 rot=multiply(rot,euler(s.pitch,s.yaw,s.roll));fov=s.fov;
}
enum Source:uint32_t { Waiting,Live,Ghost };
enum Action:uint32_t { Accelerate=1,Brake=2,Reverse=4,Drift=8,Boost=16,Weapon=32 };
inline bool useGhost(bool hasLive,bool localReplay,bool playing,bool enabled) {return playing&&enabled&&(!hasLive||localReplay);}
struct Snapshot {
 uint64_t sampleTick{},cameraTick{};
 uint32_t source{},actions{},player{AutomaticPlayer},faults{},localPlayers{};
 float steer{},accel{},originalFov{},appliedFov{};
 Vec3 nativePosition{},appliedPosition{};
 uint32_t liveCalls{},ghostCalls{},cameraCalls{};
};
// Fixed-size protocol: no process pointers, strings with heap ownership, or game data.
struct Shared {
 uint32_t magic{Magic},version{Version},size{sizeof(Shared)},hostPid{};
 volatile int32_t state{},settingsSequence{},snapshotSequence{},capabilities{};
 alignas(8) volatile int64_t heartbeat{};
 Settings settings;
 Snapshot snapshot;
 wchar_t error[256]{};
};
static_assert(sizeof(float)==4&&sizeof(Vec3)==12&&sizeof(Quat)==16,"Unexpected native ABI");
}
