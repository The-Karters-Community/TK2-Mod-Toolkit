#include "Runtime.h"
#include "Core.h"
#include "Renderer.h"
#include "vendor/minhook/include/MinHook.h"
#include <array>

using namespace race;
namespace {
HMODULE self{}; Runtime rt;
Shared* shared{}; HANDLE mapping{};
DWORD unityThread{}; bool initialized{};
HWND gameWindow{};
bool cameraFault{},inputFault{},replayFault{};
Settings settings; Snapshot snapshot;
LocalPlayers players;
using CameraFn=void(*)(void*,float,Vec3*,Quat*,float*,const void*);
using InputFn=void(*)(void*,const void*);
using ReplayFn=void(*)(void*,void*,const void*);
CameraFn cameraOriginal{}; InputFn inputOriginal{}; ReplayFn replayOriginal{};
void *cameraTarget{},*inputTarget{},*replayTarget{};
void *replayInstance{},*controlledMethod{},*buttonMethod{};
using StaticRead=void(*)(void*,void*); StaticRead staticRead{};
using ControlledFn=bool(*)(void*,void*,const void*); ControlledFn controlled{};
using ButtonFn=bool(*)(void*,void*,const void*); ButtonFn button{};
using HandleTarget=void*(*)(uintptr_t); HandleTarget handleTarget{};
uintptr_t actions[4]{};
int localType{},playerType{},playerSlot{},inputPlayer{},inputData{},inputRewired{},steer{},accel{},brake{};
int camPlayer{},camSpectator{},camStrength{},visual{},position{};
int replayReady{},replaySteer{},replayAccel{},replayBrake{},replayJump{},replayDrift{},replayBoost{};
bool inputPlayerReplay{};uint64_t liveTick{},ghostSeen{},jumpUntil{},boostUntil{};void* ghostIdentity{};
uint32_t capabilities{};
struct Frame {void* camera{};void* player{};uint64_t seen{};Settings value;};
std::array<Frame,16> frames;
std::array<int,9> inputButtons{};
using GetLock=int(*)(const void*);using GetVisible=bool(*)(const void*);
using SetLock=void(*)(int,const void*);using SetVisible=void(*)(bool,const void*);
GetLock getLock{};GetVisible getVisible{};SetLock setLockOriginal{};SetVisible setVisibleOriginal{};
void *getLockMethod{},*getVisibleMethod{},*setLockMethod{},*setVisibleMethod{};
void *lockTarget{},*visibleTarget{};bool cursorManaged{},wantedVisible{};int wantedLock{};
bool uiCapturing(){
 if(!shared||shared->state!=2||shared->rendererState!=1||!shared->panelOpen||GetForegroundWindow()!=gameWindow)return false;
 auto heartbeat=InterlockedCompareExchange64(&shared->heartbeat,0,0);return heartbeat&&GetTickCount64()<static_cast<uint64_t>(heartbeat)+1500;
}
void uiTick(){
 if(GetCurrentThreadId()!=unityThread||!setLockOriginal||!setVisibleOriginal)return;
 bool capture=uiCapturing();
 if(capture&&!cursorManaged){wantedLock=getLock(getLockMethod);wantedVisible=getVisible(getVisibleMethod);cursorManaged=true;setLockOriginal(0,setLockMethod);setVisibleOriginal(false,setVisibleMethod);ClipCursor(nullptr);}
 else if(!capture&&cursorManaged){cursorManaged=false;setLockOriginal(wantedLock,setLockMethod);setVisibleOriginal(wantedVisible,setVisibleMethod);}
}
void safeUiTick(){__try{uiTick();}__except(EXCEPTION_EXECUTE_HANDLER){if(shared)InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->panelOpen),0);}}
void lockHook(int value,const void* method){wantedLock=value;setLockOriginal(cursorManaged?0:value,method);}
void visibleHook(bool value,const void* method){wantedVisible=value;setVisibleOriginal(cursorManaged?false:value,method);}
bool consumeInput(void* input){
 if(!uiCapturing()||!shared->editMode)return false;
 auto player=member<void*>(input,inputPlayer);if(!player||member<int>(player,playerType)!=localType)return false;
 auto data=member<void*>(input,inputData);if(!data)return false;
 *reinterpret_cast<float*>(static_cast<char*>(data)+steer)=0;*reinterpret_cast<float*>(static_cast<char*>(data)+accel)=0;
 for(int offset:inputButtons)*reinterpret_cast<bool*>(static_cast<char*>(data)+offset)=false;
 return true;
}
bool safeConsume(void* input){__try{return consumeInput(input);}__except(EXCEPTION_EXECUTE_HANDLER){if(shared)InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->panelOpen),0);return false;}}

void publish() {
 if(!shared)return;
 InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->snapshotSequence));
 MemoryBarrier();shared->snapshot=snapshot;MemoryBarrier();
 InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->snapshotSequence));
}
bool active() {
 if(!shared||GetCurrentThreadId()!=unityThread)return false;
 auto now=GetTickCount64();auto beat=InterlockedCompareExchange64(&shared->heartbeat,0,0);
 if(!beat||now>static_cast<uint64_t>(beat)+1500)return false;
 LONG before=InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->settingsSequence),0,0);
 if(!(before&1)){Settings value=shared->settings;MemoryBarrier();
  if(before==InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->settingsSequence),0,0))settings=sanitize(value);}
 return true;
}
void fault(const wchar_t* text) {
 ++snapshot.faults;
 if(shared){wcsncpy_s(shared->error,text,_TRUNCATE);publish();}
}
bool local(void* player) {
 if(!player||member<int>(player,playerType)!=localType)return false;
 int slot=member<int>(player,playerSlot);if(slot<0||slot>=6)return false;
 players.observe(static_cast<uint32_t>(slot),GetTickCount64());
 snapshot.localPlayers=players.mask(GetTickCount64());snapshot.player=choosePlayer(settings.player,snapshot.localPlayers);
 return static_cast<uint32_t>(slot)==snapshot.player;
}
void* manager() {void* value{};if(replayInstance)staticRead(replayInstance,&value);return value;}
uint32_t bits(float a,bool breaking,bool drift,bool boost,bool weapon) {
 return (a>.05f?Accelerate:0)|(a<-.05f?Reverse:0)|(breaking?Brake:0)|(drift?Drift:0)|(boost?Boost:0)|(weapon?Weapon:0);
}
bool pressed(void* player,int action) {return button(player,handleTarget(actions[action]),buttonMethod);}
void sampleLive(void* input) {
 if(!active()||!settings.viewer)return;
 void* player=member<void*>(input,inputPlayer);if(!local(player))return;
 auto replay=manager();inputPlayerReplay=replay&&controlled(replay,player,controlledMethod);
 if(inputPlayerReplay)return;
 auto data=member<void*>(input,inputData);auto rewired=member<void*>(input,inputRewired);if(!data||!rewired)return;
 float s=member<float>(data,steer),a=member<float>(data,accel);if(!std::isfinite(s)||!std::isfinite(a))return;
 liveTick=GetTickCount64();snapshot.sampleTick=liveTick;snapshot.source=Live;
 snapshot.steer=std::clamp(s,-1.f,1.f);snapshot.accel=std::clamp(a,-1.f,1.f);
 snapshot.actions=bits(a,member<bool>(data,brake),pressed(rewired,0)||pressed(rewired,1),pressed(rewired,2),pressed(rewired,3));
 ++snapshot.liveCalls;ghostIdentity=nullptr;jumpUntil=boostUntil=0;publish();
}
void sampleReplay(void* replay,void*) {
 if(!active()||!settings.viewer||!settings.ghosts)return;
 uint64_t now=GetTickCount64();
 // The live driver always wins while racing an opponent ghost. Source switching
 // also clears held pulse state instead of mixing two players in one card.
 if(!useGhost(liveTick&&now-liveTick<350,inputPlayerReplay,true,settings.ghosts!=0))return;
 if(ghostIdentity&&ghostIdentity!=replay&&now-ghostSeen<1000)return;
 if(!member<bool>(replay,replayReady))return;
 if(ghostIdentity!=replay){ghostIdentity=replay;jumpUntil=boostUntil=0;}
 ghostSeen=now;
 float s=member<float>(replay,replaySteer),a=member<float>(replay,replayAccel);if(!std::isfinite(s)||!std::isfinite(a))return;
 if(member<bool>(replay,replayJump))jumpUntil=now+180;
 if(member<bool>(replay,replayBoost))boostUntil=now+180;
 snapshot.sampleTick=now;snapshot.source=Ghost;
 snapshot.steer=std::clamp(s,-1.f,1.f);snapshot.accel=std::clamp(a,-1.f,1.f);
 snapshot.actions=bits(a,member<bool>(replay,replayBrake),now<jumpUntil||member<bool>(replay,replayDrift),now<boostUntil,false);
 ++snapshot.ghostCalls;publish();
}
void cameraFrame(void* camera,float dt,Vec3* pos,Quat* rot,float* fov) {
 if(!active()||!pos||!rot||!fov)return;
 if(member<bool>(camera,camSpectator)||member<float>(camera,camStrength)<.95f)return;
 auto player=member<void*>(camera,camPlayer);if(!local(player))return;
 auto data=member<void*>(player,visual);if(!data||!std::isfinite(*fov)||*fov<1||*fov>175)return;
 uint64_t now=GetTickCount64();snapshot.cameraTick=now;snapshot.originalFov=*fov;snapshot.nativePosition=*pos;++snapshot.cameraCalls;
 if(!settings.camera){for(auto& f:frames)if(f.camera==camera)f.camera=nullptr;snapshot.appliedFov=*fov;snapshot.appliedPosition=*pos;publish();return;}
 Frame* state{};
 for(auto& f:frames)if(f.camera==camera){state=&f;break;}
 if(!state){for(auto& f:frames)if(!f.camera||now-f.seen>1000){state=&f;break;}}
 if(!state)state=&frames[0];
 if(state->camera!=camera||state->player!=player||now-state->seen>1000){state->camera=camera;state->player=player;state->value=settings;}
 else{
  float b=blend(dt,settings.smoothing);
  auto& v=state->value;
  float* values[]={&v.fov,&v.distance,&v.height,&v.side,&v.target,&v.pitch,&v.yaw,&v.roll};
  const float wanted[]={settings.fov,settings.distance,settings.height,settings.side,settings.target,settings.pitch,settings.yaw,settings.roll};
  for(int i=0;i<8;i++)*values[i]+=(wanted[i]-*values[i])*b;
  v.preserve=settings.preserve;v.aim=settings.aim;
 }
 state->seen=now;Vec3 nextPos=*pos;Quat nextRot=*rot;float nextFov=*fov;
 framing(state->value,member<Vec3>(data,position),nextPos,nextRot,nextFov);
 if(!std::isfinite(nextPos.x)||!std::isfinite(nextPos.y)||!std::isfinite(nextPos.z)||!std::isfinite(nextFov))return;
 *pos=nextPos;*rot=nextRot;*fov=nextFov;snapshot.appliedFov=nextFov;snapshot.appliedPosition=nextPos;publish();
}
void safeInput(void* o) {if(inputFault)return;__try{sampleLive(o);}__except(EXCEPTION_EXECUTE_HANDLER){inputFault=true;fault(L"Input reading stopped. Disconnect and reconnect before retrying.");}}
void safeReplay(void* o,void* input) {if(replayFault)return;__try{sampleReplay(o,input);}__except(EXCEPTION_EXECUTE_HANDLER){replayFault=true;fault(L"Replay input reading stopped. Reconnect before retrying.");}}
void safeCamera(void* o,float dt,Vec3* p,Quat* q,float* f) {if(cameraFault)return;__try{cameraFrame(o,dt,p,q,f);}__except(EXCEPTION_EXECUTE_HANDLER){cameraFault=true;fault(L"Camera adjustment stopped. Disconnect and reconnect before retrying.");}}
void cameraHook(void* o,float dt,Vec3* p,Quat* q,float* f,const void* m) {cameraOriginal(o,dt,p,q,f,m);safeUiTick();safeCamera(o,dt,p,q,f);}
void inputHook(void* o,const void* m) {safeUiTick();if(!safeConsume(o))inputOriginal(o,m);safeInput(o);}
// The native replay method clears click pulses after applying them to the kart.
// Capture those recorded values before calling it; never consume or modify them.
void replayHook(void* o,void* input,const void* m) {safeReplay(o,input);replayOriginal(o,input,m);}
void hook(void* method,void* replacement,void** original,void** target) {
 *target=rt.pointer(method);auto result=MH_CreateHook(*target,replacement,original);
 if(result!=MH_OK)throw std::runtime_error(std::string("Cannot prepare native hook: ")+MH_StatusToString(result));
 result=MH_QueueEnableHook(*target);if(result!=MH_OK)throw std::runtime_error("Cannot enable native hook.");
}
void initialize() {
 rt.module=GetModuleHandleW(L"GameAssembly.dll");if(!rt.module)throw std::runtime_error("Open The Karters 2 before connecting.");
 auto player=rt.type("","Ant_Player");playerType=rt.offset(player,"ePlayerType",8);playerSlot=rt.offset(player,"eAntLocalPlayerNr",8);
 localType=rt.enumMember(rt.fieldClass(player,"ePlayerType"),"E_HUMAN_LOCAL");
 auto status=MH_Initialize();if(status!=MH_OK&&status!=MH_ERROR_ALREADY_INITIALIZED)throw std::runtime_error("Native hook initialization failed.");
 std::string errors;
 try{
  auto camera=rt.type("","PixelGameKartCamera");camPlayer=rt.offset(camera,"antPlayer",0);camSpectator=rt.offset(camera,"bIsSpectatorCamera",2);camStrength=rt.offset(camera,"fRacingCameraStrength",12);
  visual=rt.offset(player,"visualInstanceSyncedParams",0);auto visualClass=rt.fieldClass(player,"visualInstanceSyncedParams");position=rt.offset(visualClass,"vCurPos",17);
  if(rt.typeName(rt.api<const void*(*)(void*)>("il2cpp_field_get_type")(rt.field(visualClass,"vCurPos")))!="UnityEngine.Vector3")throw std::runtime_error("Camera position type changed.");
  auto method=rt.method(camera,"GetRacingCamPositionAndRotation",{"System.Single","UnityEngine.Vector3&","UnityEngine.Quaternion&","System.Single&"},"System.Void");
  hook(method,reinterpret_cast<void*>(cameraHook),reinterpret_cast<void**>(&cameraOriginal),&cameraTarget);capabilities|=1;
 }catch(const std::exception& e){errors="Camera: "+std::string(e.what());}
 try{
  auto input=rt.type("","Ant_KartInput");inputPlayer=rt.offset(input,"antPlayer",0);inputData=rt.offset(input,"inputData",0);inputRewired=rt.offset(input,"player",0);
  auto data=rt.fieldClass(input,"inputData");steer=rt.offset(data,"fTurningInput",12);accel=rt.offset(data,"fAccelInput",12);brake=rt.offset(data,"bIsBreakingInput",2);
  const char* inputNames[]={"bJumpButton_ClickedDown","bDriftInput","bBoostInput_ClickedDown","bUseWeaponInput_ClickDown","bIsBreakingInput","bIsTargetLockUp_ClickUp","bIsTargetLockDown_ClickDown","bBackDirInputButtonDown","bWeaponPickupButtomClicked_ClickedDown"};
  for(size_t i=0;i<inputButtons.size();i++)inputButtons[i]=rt.offset(data,inputNames[i],2);
  auto rm=rt.type("","PTK_ReplayManager");replayInstance=rt.field(rm,"Instance");staticRead=rt.api<StaticRead>("il2cpp_field_static_get_value");
  controlledMethod=rt.method(rm,"IsPlayerControlledByReplayInput",{"Ant_Player"},"System.Boolean");controlled=reinterpret_cast<ControlledFn>(rt.pointer(controlledMethod));
  auto rewired=rt.fieldClass(input,"player");buttonMethod=rt.method(rewired,"GetButton",{"System.String"},"System.Boolean");button=reinterpret_cast<ButtonFn>(rt.pointer(buttonMethod));
  handleTarget=rt.api<HandleTarget>("il2cpp_gchandle_get_target");const char* names[]={"Jump","Drift_left_right","Boost","UseWeapon"};
  for(int i=0;i<4;i++)actions[i]=rt.api<uintptr_t(*)(void*,bool)>("il2cpp_gchandle_new")(rt.api<void*(*)(const char*)>("il2cpp_string_new")(names[i]),false);
  hook(rt.method(input,"ProcessRacingInput",{},"System.Void"),reinterpret_cast<void*>(inputHook),reinterpret_cast<void**>(&inputOriginal),&inputTarget);capabilities|=2;
 }catch(const std::exception& e){errors+=(errors.empty()?"":"; ")+std::string("Input: ")+e.what();}
 try{
  auto replay=rt.type("","PTK_PlayerReplayInput_ReplaySerializer");replayReady=rt.offset(replay,"bReplayDataReceived",2);replaySteer=rt.offset(replay,"fTurningInput_Get",12);replayAccel=rt.offset(replay,"fAccelInput_Get",12);
  replayBrake=rt.offset(replay,"bIsBreakingInput_Get",2);replayJump=rt.offset(replay,"bJumpButton_Get_Click",2);replayDrift=rt.offset(replay,"bDriftInput_Get",2);replayBoost=rt.offset(replay,"bBoostInput_Get_Click",2);
  hook(rt.method(replay,"ProcessAndSetPlayerInputFromReplay",{"PTK_KartInputBase"},"System.Void"),reinterpret_cast<void*>(replayHook),reinterpret_cast<void**>(&replayOriginal),&replayTarget);capabilities|=4;
 }catch(const std::exception& e){errors+=(errors.empty()?"":"; ")+std::string("Replay: ")+e.what();}
 try{
  if(!(capabilities&2))throw std::runtime_error("Local input routing is unavailable.");
  auto cursor=rt.type("UnityEngine","Cursor");getLockMethod=rt.method(cursor,"get_lockState",{},"UnityEngine.CursorLockMode");getLock=reinterpret_cast<GetLock>(rt.pointer(getLockMethod));
  getVisibleMethod=rt.method(cursor,"get_visible",{},"System.Boolean");getVisible=reinterpret_cast<GetVisible>(rt.pointer(getVisibleMethod));
  setLockMethod=rt.method(cursor,"set_lockState",{"UnityEngine.CursorLockMode"},"System.Void");setVisibleMethod=rt.method(cursor,"set_visible",{"System.Boolean"},"System.Void");
  hook(setLockMethod,reinterpret_cast<void*>(lockHook),reinterpret_cast<void**>(&setLockOriginal),&lockTarget);hook(setVisibleMethod,reinterpret_cast<void*>(visibleHook),reinterpret_cast<void**>(&setVisibleOriginal),&visibleTarget);
  gameui::install(gameWindow,safeUiTick);capabilities|=8;
 }catch(const std::exception& e){errors+=(errors.empty()?"":"; ")+std::string("Game controls: ")+e.what();}
 if(!capabilities)throw std::runtime_error(errors);
 auto enabled=MH_ApplyQueued();if(enabled!=MH_OK)throw std::runtime_error(std::string("Native hooks could not start: ")+MH_StatusToString(enabled));
 initialized=true;
 if(!errors.empty())MultiByteToWideChar(CP_UTF8,0,errors.c_str(),-1,shared->error,256);
}
void connect(Shared* request,HANDLE handle,HWND window) {
 if(request->magic!=Magic||request->version!=Version||request->size!=sizeof(Shared)){UnmapViewOfFile(request);CloseHandle(handle);return;}
 if(shared&&shared->hostPid!=request->hostPid&&shared->heartbeat&&GetTickCount64()<static_cast<uint64_t>(shared->heartbeat)+1500){wcscpy_s(request->error,L"Another Race Tools window is already connected.");InterlockedExchange(reinterpret_cast<volatile LONG*>(&request->state),-1);UnmapViewOfFile(request);CloseHandle(handle);return;}
 HMODULE pinned{};GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&connect),&pinned);
 if(cursorManaged){cursorManaged=false;setLockOriginal(wantedLock,setLockMethod);setVisibleOriginal(wantedVisible,setVisibleMethod);}
 if(shared){UnmapViewOfFile(shared);CloseHandle(mapping);}
 cursorManaged=false;shared=request;mapping=handle;gameWindow=window;unityThread=GetCurrentThreadId();settings=sanitize(shared->settings);snapshot={};cameraFault=inputFault=replayFault=false;
 players={};for(auto& frame:frames)frame={};liveTick=ghostSeen=jumpUntil=boostUntil=0;ghostIdentity=nullptr;inputPlayerReplay=false;
 try{if(!initialized)initialize();if(capabilities&8)gameui::bind(handle);shared->capabilities=capabilities;InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->state),2);}
 catch(const std::exception& e){MultiByteToWideChar(CP_UTF8,0,e.what(),-1,shared->error,256);InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->state),-1);}
}
void safeConnect(Shared* request,HANDLE handle,HWND window) {__try{connect(request,handle,window);}__except(EXCEPTION_EXECUTE_HANDLER){wcscpy_s(request->error,L"The runtime contract could not be initialized.");InterlockedExchange(reinterpret_cast<volatile LONG*>(&request->state),-1);}}
}

extern "C" __declspec(dllexport) LRESULT CALLBACK RaceToolsBootstrap(int code,WPARAM w,LPARAM l) {
 if(code>=0&&w==PM_REMOVE){auto msg=reinterpret_cast<MSG*>(l);
  if(msg&&msg->message==RegisterWindowMessageW(L"Bobby.RaceTools.Connect.v3")){
   wchar_t name[128];swprintf_s(name,L"Local\\BobbyRaceTools_%lu_%llx",GetCurrentProcessId(),static_cast<unsigned long long>(msg->wParam));
   HANDLE h=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,name);
   if(h){auto view=static_cast<Shared*>(MapViewOfFile(h,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared)));
    if(view&&InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&view->state),1,0)==0)safeConnect(view,h,msg->hwnd);
    else{if(view)UnmapViewOfFile(view);CloseHandle(h);}}}
 }
 return CallNextHookEx(nullptr,code,w,l);
}
BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,LPVOID) {if(reason==DLL_PROCESS_ATTACH){self=module;DisableThreadLibraryCalls(module);}return TRUE;}
