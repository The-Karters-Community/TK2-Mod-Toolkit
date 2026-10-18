#include "Renderer.h"
#include "vendor/minhook/include/MinHook.h"
#include "vendor/imgui/imgui.h"
#include "vendor/imgui/backends/imgui_impl_dx11.h"
#include "vendor/imgui/backends/imgui_impl_win32.h"
#include <d3d11.h>
#include <dxgi.h>
#include <wrl/client.h>
#include <array>
#include <atomic>
#include <mutex>

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND,UINT,WPARAM,LPARAM);
using namespace race;
using Microsoft::WRL::ComPtr;
namespace gameui {
namespace {
HWND targetWindow{};WNDPROC originalWindow{};void (*tickMain)(){};
UINT tickMessage{};std::atomic<bool> tickPending{},captureInput{};
using Present=HRESULT(WINAPI*)(IDXGISwapChain*,UINT,UINT);
using Resize=HRESULT(WINAPI*)(IDXGISwapChain*,UINT,UINT,UINT,DXGI_FORMAT,UINT);
Present originalPresent{};Resize originalResize{};
SRWLOCK bindingLock=SRWLOCK_INIT,queueLock=SRWLOCK_INIT;
Shared* shared{};HANDLE mapping{};
std::mutex graphicsLock;
ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;ComPtr<ID3D11RenderTargetView> target;
IDXGISwapChain* currentChain{};ImGuiContext* gui{};ImGuiStyle baseStyle;
struct Event{UINT message;WPARAM w;LPARAM l;};
std::array<Event,512> events;size_t eventCount{};bool overflow{};
Settings values;LONG hostSequence=-1,ownRevision{};bool previouslyOpen{};
ImVec2 panelPosition{-1,-1};

struct BindingRead{BindingRead(){AcquireSRWLockShared(&bindingLock);}~BindingRead(){ReleaseSRWLockShared(&bindingLock);}};
bool alive(){if(!shared)return false;auto heartbeat=InterlockedCompareExchange64(&shared->heartbeat,0,0);return heartbeat&&GetTickCount64()<static_cast<uint64_t>(heartbeat)+1500&&shared->state==2;}
void postTick(){if(!tickPending.exchange(true))if(!PostMessageW(targetWindow,tickMessage,0,0))tickPending=false;}
void queue(UINT message,WPARAM w,LPARAM l){
 AcquireSRWLockExclusive(&queueLock);
 if(eventCount<events.size())events[eventCount++]={message,w,l};else overflow=true;
 ReleaseSRWLockExclusive(&queueLock);
}
LRESULT CALLBACK windowProc(HWND h,UINT message,WPARAM w,LPARAM l){
 if(message==tickMessage){tickPending=false;if(tickMain)tickMain();return 0;}
 // Dear ImGui is owned by the render thread. Never call its backend from this
 // window thread or hold a graphics lock while forwarding Windows messages.
 if(message>=WM_MOUSEFIRST&&message<=WM_MOUSELAST)queue(message,w,l);
 else if(message>=WM_KEYFIRST&&message<=WM_KEYLAST)queue(message,w,l);
 else if(message==WM_SETFOCUS||message==WM_KILLFOCUS||message==WM_DEVICECHANGE||message==WM_MOUSELEAVE)queue(message,w,l);
 if(captureInput.load()){
  if(message==WM_INPUT)return DefWindowProcW(h,message,w,l); // Release the raw-input packet.
  if((message>=WM_MOUSEFIRST&&message<=WM_MOUSELAST)||(message>=WM_KEYFIRST&&message<=WM_KEYLAST))return 0;
  if(message==WM_SETCURSOR){SetCursor(nullptr);return TRUE;}
 }
 return CallWindowProcW(originalWindow,h,message,w,l);
}
void inputEvents(){
 std::array<Event,512> pending;size_t count{};bool dropped{};
 AcquireSRWLockExclusive(&queueLock);count=eventCount;std::copy_n(events.begin(),count,pending.begin());eventCount=0;dropped=overflow;overflow=false;ReleaseSRWLockExclusive(&queueLock);
 if(dropped)ImGui::GetIO().ClearInputKeys();
 for(size_t i=0;i<count;i++)ImGui_ImplWin32_WndProcHandler(targetWindow,pending[i].message,pending[i].w,pending[i].l);
}
void initializeGui(IDXGISwapChain* chain){
 if(gui)return;
 if(FAILED(chain->GetDevice(IID_PPV_ARGS(&device))))throw std::runtime_error("The game swap chain is not Direct3D 11.");
 device->GetImmediateContext(&context);gui=ImGui::CreateContext();ImGui::SetCurrentContext(gui);
 auto& io=ImGui::GetIO();io.IniFilename=nullptr;io.LogFilename=nullptr;
 io.ConfigFlags|=ImGuiConfigFlags_NavEnableKeyboard|ImGuiConfigFlags_NavEnableGamepad;
 wchar_t windows[MAX_PATH];GetWindowsDirectoryW(windows,MAX_PATH);
 std::wstring path=std::wstring(windows)+L"\\Fonts\\segoeui.ttf";
 char utf8[MAX_PATH*3];WideCharToMultiByte(CP_UTF8,0,path.c_str(),-1,utf8,sizeof(utf8),nullptr,nullptr);
 if(!io.Fonts->AddFontFromFileTTF(utf8,18.f))io.Fonts->AddFontDefault();
 ImGui::StyleColorsDark();auto& style=ImGui::GetStyle();
 style.WindowRounding=6;style.FrameRounding=3;style.ScrollbarRounding=3;
 style.WindowPadding={18,16};style.FramePadding={8,5};style.ItemSpacing={10,9};style.WindowBorderSize=1;
 auto& colors=style.Colors;
 colors[ImGuiCol_WindowBg]={.075f,.086f,.075f,.98f};colors[ImGuiCol_TitleBg]={.11f,.125f,.105f,1};colors[ImGuiCol_TitleBgActive]=colors[ImGuiCol_TitleBg];
 colors[ImGuiCol_FrameBg]={.16f,.18f,.145f,1};colors[ImGuiCol_FrameBgHovered]={.23f,.26f,.20f,1};colors[ImGuiCol_FrameBgActive]=colors[ImGuiCol_FrameBgHovered];
 colors[ImGuiCol_Button]=colors[ImGuiCol_FrameBg];colors[ImGuiCol_ButtonHovered]={.30f,.34f,.25f,1};colors[ImGuiCol_ButtonActive]=colors[ImGuiCol_ButtonHovered];
 colors[ImGuiCol_CheckMark]={1,.72f,.36f,1};colors[ImGuiCol_SliderGrab]=colors[ImGuiCol_CheckMark];colors[ImGuiCol_SliderGrabActive]={1,.82f,.55f,1};
 colors[ImGuiCol_NavHighlight]=colors[ImGuiCol_CheckMark];colors[ImGuiCol_Header]=colors[ImGuiCol_FrameBg];colors[ImGuiCol_HeaderHovered]=colors[ImGuiCol_ButtonHovered];colors[ImGuiCol_HeaderActive]=colors[ImGuiCol_ButtonHovered];
 baseStyle=style;
 if(!ImGui_ImplWin32_Init(targetWindow)||!ImGui_ImplDX11_Init(device.Get(),context.Get()))throw std::runtime_error("Cannot initialize the in-game controls.");
 currentChain=chain;
}
bool readHost(){
 LONG sequence=InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->settingsSequence),0,0);
 if(sequence==hostSequence||(sequence&1))return false;
 Settings next=shared->settings;MemoryBarrier();if(sequence!=shared->settingsSequence)return false;
 if(shared->uiAck>=ownRevision){values=sanitize(next);hostSequence=sequence;return true;}return false;
}
void commit(){
 values=sanitize(values);InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->uiSequence));MemoryBarrier();shared->uiSettings=values;MemoryBarrier();InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->uiSequence));
 ownRevision=InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->uiRevision));
}
bool setting(const char* label,float Settings::*field,float low,float high,const char* format,float scale){
 ImGui::PushID(label);ImGui::TextUnformatted(label);ImGui::SetNextItemWidth(std::max(80.f,ImGui::GetContentRegionAvail().x-70*scale));
 bool changed=ImGui::SliderFloat("##value",&(values.*field),low,high,format,ImGuiSliderFlags_AlwaysClamp);
 if(ImGui::IsItemFocused()&&ImGui::IsKeyPressed(ImGuiKey_GamepadFaceUp)){values.*field=Settings{}.*field;changed=true;}
 ImGui::SameLine();if(ImGui::Button("Reset",{60*scale,0})){values.*field=Settings{}.*field;changed=true;}
 ImGui::PopID();return changed;
}
void camera(const Snapshot& state,float scale){
 auto& io=ImGui::GetIO();float width=std::min(570*scale,io.DisplaySize.x-24),height=io.DisplaySize.y-24;
 if(panelPosition.x<0)panelPosition={std::max(12.f,io.DisplaySize.x-width-22),22};
 panelPosition.x=std::clamp(panelPosition.x,0.f,std::max(0.f,io.DisplaySize.x-width));panelPosition.y=std::clamp(panelPosition.y,0.f,std::max(0.f,io.DisplaySize.y-200*scale));
 ImGui::SetNextWindowPos(panelPosition);ImGui::SetNextWindowSize({width,0},ImGuiCond_Appearing);ImGui::SetNextWindowSizeConstraints({std::min(350*scale,width),0},{std::max(width,350*scale),height});
 if(!previouslyOpen)ImGui::SetNextWindowFocus();
 bool open=true,changed=false;
 if(ImGui::Begin("Camera",&open,ImGuiWindowFlags_AlwaysAutoResize|ImGuiWindowFlags_NoCollapse|ImGuiWindowFlags_NoSavedSettings)){
  bool enabled=values.camera!=0;if(ImGui::Checkbox("Camera overrides",&enabled)){values.camera=enabled;changed=true;}
  ImGui::SameLine();if(state.player<6)ImGui::TextDisabled("Player %u",state.player+1);else ImGui::TextDisabled("Applies in your next race");
  if(!previouslyOpen)ImGui::SetKeyboardFocusHere();
  changed|=setting("Field of view",&Settings::fov,35,110,"%.1f deg",scale);
  changed|=setting("Distance",&Settings::distance,.25f,4,"%.2fx",scale);
  changed|=setting("Height",&Settings::height,-5,8,"%.2f",scale);
  changed|=setting("Side offset",&Settings::side,-4,4,"%.2f",scale);
  bool preserve=values.preserve!=0;if(ImGui::Checkbox("Keep kart size when FOV changes",&preserve)){values.preserve=preserve;changed=true;}
  if(ImGui::CollapsingHeader("Aim & rotation")){
   bool aim=values.aim!=0;if(ImGui::Checkbox("Aim toward kart",&aim)){values.aim=aim;changed=true;}
   ImGui::BeginDisabled(!aim);changed|=setting("Target height",&Settings::target,-1,3,"%.2f",scale);ImGui::EndDisabled();
   changed|=setting("Pitch",&Settings::pitch,-45,45,"%.1f deg",scale);changed|=setting("Yaw",&Settings::yaw,-90,90,"%.1f deg",scale);changed|=setting("Roll",&Settings::roll,-30,30,"%.1f deg",scale);
  }
  changed|=setting("Smoothing",&Settings::smoothing,0,2,"%.2f sec",scale);
  ImGui::Separator();
  if(ImGui::Button("Reset framing")){Settings defaults;values.fov=defaults.fov;values.distance=1;values.height=values.side=values.pitch=values.yaw=values.roll=0;values.target=.5f;values.smoothing=.12f;values.preserve=1;values.aim=0;changed=true;}
  ImGui::SameLine();if(ImGui::Button("Back to race"))open=false;
  ImGui::TextDisabled("Changes save automatically");
  ImGui::TextDisabled((io.BackendFlags&ImGuiBackendFlags_HasGamepad)?"D-pad / stick: move   A: adjust   B: back   Y: reset":"Tab: move   Arrows: adjust   Esc: back");
  if(!ImGui::IsAnyItemActive()&&(ImGui::IsKeyPressed(ImGuiKey_Escape)||ImGui::IsKeyPressed(ImGuiKey_GamepadFaceRight)))open=false;
  panelPosition=ImGui::GetWindowPos();
 }
 ImGui::End();if(changed)commit();if(!open)InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->panelOpen),0);
}
void hud(const Snapshot& state){
 auto& io=ImGui::GetIO();float scale=std::min(values.overlayScale,std::min((io.DisplaySize.x-24)/400,(io.DisplaySize.y-24)/126));if(scale<=0)return;
 ImVec2 pos{12+(io.DisplaySize.x-400*scale-24)*values.overlayX/100,12+(io.DisplaySize.y-126*scale-24)*values.overlayY/100};
 ImGui::SetNextWindowPos(pos);ImGui::SetNextWindowSize({400*scale,126*scale});
 if(ImGui::Begin("##DrivingInputs",nullptr,ImGuiWindowFlags_NoDecoration|ImGuiWindowFlags_NoBackground|ImGuiWindowFlags_NoInputs|ImGuiWindowFlags_NoNav|ImGuiWindowFlags_NoSavedSettings)){
  auto draw=ImGui::GetWindowDrawList();auto point=[&](float x,float y){return ImVec2{pos.x+x*scale,pos.y+y*scale};};
  auto text=[&](float x,float y,const char* value,ImU32 color,float size=13){draw->AddText(ImGui::GetFont(),size*scale,point(x,y),color,value);};
  const ImU32 light=IM_COL32(237,241,229,255),muted=IM_COL32(179,186,172,255),accent=IM_COL32(255,184,92,255);
  bool fresh=state.sampleTick&&GetTickCount64()-state.sampleTick<500;
  draw->AddRectFilled(pos,point(400,126),IM_COL32(19,21,19,static_cast<int>(values.opacity*255)),4*scale);
  text(14,9,"Inputs",light,16);text(80,12,!fresh?"Waiting for race input":state.source==Ghost?"Recorded ghost":"Your driving inputs",muted);text(337,12,!fresh?"WAIT":state.source==Ghost?"GHOST":"LIVE",accent);
  draw->AddLine(point(14,36),point(386,36),IM_COL32(71,76,68,255));text(14,48,"Steer",muted);
  char value[24];snprintf(value,sizeof(value),"%+.2f",fresh?state.steer:0);text(63,48,value,muted);
  draw->AddLine(point(18,85),point(106,85),IM_COL32(71,76,68,255),4*scale);draw->AddCircleFilled(point(62+(fresh?state.steer:0)*40,85),6*scale,light);
  const char* names[]={"Accelerate","Brake","Reverse","Jump / drift","Boost","Weapon"};const uint32_t bits[]={Accelerate,Brake,Reverse,Drift,Boost,Weapon};
  for(int i=0;i<6;i++){if(i==5&&fresh&&state.source==Ghost)continue;float x=124+89.f*(i%3),y=45+36.f*(i/3);bool on=fresh&&(state.actions&bits[i]);draw->AddRectFilled(point(x,y),point(x+82,y+28),on?accent:IM_COL32(43,47,40,255),2*scale);text(x+5,y+7,names[i],on?IM_COL32(36,31,22,255):light,12);}
 }ImGui::End();
}
void frame(IDXGISwapChain* chain){
 BindingRead lease;if(!alive()){bool was=captureInput.exchange(false);if(was)postTick();previouslyOpen=false;return;}
 DXGI_SWAP_CHAIN_DESC desc{};if(FAILED(chain->GetDesc(&desc))||desc.OutputWindow!=targetWindow)return;
 bool foreground=GetForegroundWindow()==targetWindow;bool open=shared->panelOpen&&foreground;
 bool was=captureInput.exchange(open);if(open||was)postTick();
 try{initializeGui(chain);}catch(const std::exception&){InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->rendererState),-1);return;}
 if(chain!=currentChain)return;ImGui::SetCurrentContext(gui);
 if(!target){ComPtr<ID3D11Texture2D> buffer;if(FAILED(chain->GetBuffer(0,IID_PPV_ARGS(&buffer)))||FAILED(device->CreateRenderTargetView(buffer.Get(),nullptr,&target)))return;}
 readHost();Snapshot state{};
 for(int i=0;i<3;i++){LONG before=shared->snapshotSequence;if(before&1)continue;state=shared->snapshot;MemoryBarrier();if(before==shared->snapshotSequence)break;}
 ImGui_ImplDX11_NewFrame();ImGui_ImplWin32_NewFrame();inputEvents();
 auto& io=ImGui::GetIO();io.MouseDrawCursor=open;
 float scale=std::clamp(io.DisplaySize.y/900.f,.85f,1.5f);ImGui::GetStyle()=baseStyle;ImGui::GetStyle().ScaleAllSizes(scale);io.FontGlobalScale=scale;
 ImGui::NewFrame();
 if(io.DisplaySize.x>50&&io.DisplaySize.y>50){if(values.viewer)hud(state);if(open)camera(state,scale);}
 ImGui::Render();
 ComPtr<ID3D11RenderTargetView> oldTarget;ComPtr<ID3D11DepthStencilView> oldDepth;context->OMGetRenderTargets(1,&oldTarget,&oldDepth);
 auto view=target.Get();context->OMSetRenderTargets(1,&view,nullptr);ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
 auto previous=oldTarget.Get();context->OMSetRenderTargets(1,&previous,oldDepth.Get());
 InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->rendererState),1);InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->renderFrames));
 InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->controllerPresent),(io.BackendFlags&ImGuiBackendFlags_HasGamepad)?1:0);
 previouslyOpen=open;
}
HRESULT WINAPI presentHook(IDXGISwapChain* chain,UINT interval,UINT flags){
 if(!(flags&DXGI_PRESENT_TEST)){std::lock_guard<std::mutex> lock(graphicsLock);frame(chain);}
 return originalPresent(chain,interval,flags);
}
HRESULT WINAPI resizeHook(IDXGISwapChain* chain,UINT count,UINT width,UINT height,DXGI_FORMAT format,UINT flags){
 std::lock_guard<std::mutex> lock(graphicsLock);if(gui&&chain==currentChain){ImGui::SetCurrentContext(gui);ImGui_ImplDX11_InvalidateDeviceObjects();target.Reset();}
 return originalResize(chain,count,width,height,format,flags);
}
void hook(void* targetAddress,void* replacement,void** original){
 auto result=MH_CreateHook(targetAddress,replacement,original);if(result!=MH_OK)throw std::runtime_error(MH_StatusToString(result));
 if(MH_QueueEnableHook(targetAddress)!=MH_OK)throw std::runtime_error("Cannot enable the game renderer hook.");
}
}
void install(HWND window,void (*mainThreadTick)()){
 targetWindow=window;tickMain=mainThreadTick;tickMessage=RegisterWindowMessageW(L"Bobby.RaceTools.UiTick.v3");
 WNDCLASSW cls{};cls.lpfnWndProc=DefWindowProcW;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"Bobby.RaceTools.DeviceProbe.v3";RegisterClassW(&cls);
 HWND probe=CreateWindowW(cls.lpszClassName,L"",WS_OVERLAPPED,0,0,32,32,nullptr,nullptr,cls.hInstance,nullptr);
 DXGI_SWAP_CHAIN_DESC desc{};desc.BufferCount=1;desc.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;desc.OutputWindow=probe;desc.SampleDesc.Count=1;desc.Windowed=TRUE;desc.SwapEffect=DXGI_SWAP_EFFECT_DISCARD;
 ComPtr<IDXGISwapChain> swap;ComPtr<ID3D11Device> temporary;ComPtr<ID3D11DeviceContext> temporaryContext;
 HRESULT result=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&swap,&temporary,nullptr,&temporaryContext);
 if(FAILED(result))result=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&swap,&temporary,nullptr,&temporaryContext);
 if(FAILED(result)){DestroyWindow(probe);throw std::runtime_error("Cannot discover the Direct3D 11 swap chain.");}
 auto vtable=*reinterpret_cast<void***>(swap.Get());void* present=vtable[8];void* resize=vtable[13];
 swap.Reset();temporaryContext.Reset();temporary.Reset();DestroyWindow(probe);
 hook(present,reinterpret_cast<void*>(presentHook),reinterpret_cast<void**>(&originalPresent));hook(resize,reinterpret_cast<void*>(resizeHook),reinterpret_cast<void**>(&originalResize));
 SetLastError(0);originalWindow=reinterpret_cast<WNDPROC>(SetWindowLongPtrW(window,GWLP_WNDPROC,reinterpret_cast<LONG_PTR>(windowProc)));
 if(!originalWindow)throw std::runtime_error("Cannot route the in-game controls.");
}
void bind(HANDLE source){
 HANDLE next{};if(!DuplicateHandle(GetCurrentProcess(),source,GetCurrentProcess(),&next,0,FALSE,DUPLICATE_SAME_ACCESS))throw std::runtime_error("Cannot bind the game renderer.");
 auto view=static_cast<Shared*>(MapViewOfFile(next,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared)));if(!view){CloseHandle(next);throw std::runtime_error("Cannot map renderer settings.");}
 AcquireSRWLockExclusive(&bindingLock);if(shared)UnmapViewOfFile(shared);if(mapping)CloseHandle(mapping);shared=view;mapping=next;hostSequence=-1;ownRevision=0;values=sanitize(shared->settings);previouslyOpen=false;ReleaseSRWLockExclusive(&bindingLock);
}
bool capturing(){return captureInput.load();}
}
