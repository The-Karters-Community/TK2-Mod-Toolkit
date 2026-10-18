#include "Core.h"
#include <cassert>
#include <cstring>
#include <iostream>
#include <limits>
using namespace race;
bool near(float a,float b){return std::abs(a-b)<.0001f;}
int main(){
 Settings s;assert(!s.camera&&s.viewer&&s.preserve&&s.ghosts);assert(sizeof(Shared)<2048);
 s.fov=std::numeric_limits<float>::quiet_NaN();s.distance=-10;s.player=99;s.opacity=42;s=sanitize(s);
 assert(s.fov==65&&s.distance==.25f&&s.player==AutomaticPlayer&&s.opacity==1);
 assert(Settings{}.player==AutomaticPlayer);
 assert(choosePlayer(2,1)==0); // A saved, inactive slot must not silence both features.
 assert(choosePlayer(2,5)==2&&choosePlayer(AutomaticPlayer,5)==0);
 assert(choosePlayer(0,0)==AutomaticPlayer&&choosePlayer(1,4)==2);
 LocalPlayers players;players.observe(0,100);players.observe(2,200);players.observe(6,200);
 assert(players.mask(250)==5&&players.mask(1150)==4&&players.mask(1200)==0);
 assert(near(distanceScale(65,65,true,1),1));assert(near(distanceScale(65,100,false,2),2));
 assert(distanceScale(65,35,true,1)>1&&distanceScale(65,100,true,1)<1);
 assert(blend(.016f,0)==1&&blend(0,.1f)==0&&blend(.016f,.1f)>0&&blend(.016f,.1f)<1);
 Quat identity;auto v=rotate(identity,{1,2,3});assert(near(v.x,1)&&near(v.y,2)&&near(v.z,3));
 auto yaw=euler(0,90,0);v=rotate(yaw,{0,0,1});assert(near(v.x,1)&&near(v.z,0));
 auto lookForward=look({0,0,1},identity);assert(near(lookForward.w,1));
 for(Vec3 forward: {Vec3{1,0,0},Vec3{-1,0,0},Vec3{0,0,-1},Vec3{1,1,1}}){auto q=look(forward,identity);auto actual=rotate(q,{0,0,1});auto expected=normalized(forward);assert(near(actual.x,expected.x)&&near(actual.y,expected.y)&&near(actual.z,expected.z));}
 Vec3 pos{0,2,-4};Quat rot;float fov=65;s=Settings{};framing(s,{},pos,rot,fov);assert(near(pos.y,2)&&near(pos.z,-4)&&fov==65);
 s.height=2;s.side=1;framing(s,{},pos,rot,fov);assert(near(pos.x,1)&&near(pos.y,4));
 assert(!useGhost(true,false,true,true));assert(useGhost(true,true,true,true));assert(useGhost(false,false,true,true));assert(!useGhost(true,true,true,false));assert(!useGhost(false,false,false,true));
 Shared shared;assert(shared.magic==Magic&&shared.version==Version&&shared.size==sizeof(Shared));
 std::cout<<"Standalone core: camera geometry, quaternions, settings bounds, protocol and source priority passed. Shared bytes="<<sizeof(Shared)<<"\n";
}
