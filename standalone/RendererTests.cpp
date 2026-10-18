#include "Renderer.h"
#include "vendor/minhook/include/MinHook.h"
#include <d3d11.h>
#include <xinput.h>
#include <wrl/client.h>
#include <cassert>
#include <iostream>
using Microsoft::WRL::ComPtr;
using namespace race;
namespace {
HWND testWindow{};XINPUT_GAMEPAD simulatedPad{};
HWND WINAPI foreground(){return testWindow;}
DWORD WINAPI controller(DWORD index,XINPUT_STATE* state){if(index)return ERROR_DEVICE_NOT_CONNECTED;*state={};state->Gamepad=simulatedPad;return ERROR_SUCCESS;}
void tick(){}
LRESULT CALLBACK procedure(HWND h,UINT m,WPARAM w,LPARAM l){return DefWindowProcW(h,m,w,l);}
}
int main(){
 WNDCLASSW cls{};cls.lpfnWndProc=procedure;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"RaceTools.RendererSmoke";RegisterClassW(&cls);
 HWND window=CreateWindowW(cls.lpszClassName,L"Renderer validation",WS_OVERLAPPEDWINDOW,0,0,1024,768,nullptr,nullptr,cls.hInstance,nullptr);assert(window);testWindow=window;
 HANDLE mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Shared),nullptr);assert(mapping);
 auto shared=static_cast<Shared*>(MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared)));assert(shared);new(shared)Shared{};shared->state=2;shared->heartbeat=GetTickCount64();
 assert(MH_Initialize()==MH_OK);
 // Mock only this test process's OS input queries, never send keys or steal focus.
 assert(MH_CreateHook(reinterpret_cast<void*>(GetForegroundWindow),reinterpret_cast<void*>(foreground),nullptr)==MH_OK);
 assert(MH_QueueEnableHook(reinterpret_cast<void*>(GetForegroundWindow))==MH_OK);
 assert(MH_CreateHook(reinterpret_cast<void*>(XInputGetState),reinterpret_cast<void*>(controller),nullptr)==MH_OK);
 assert(MH_QueueEnableHook(reinterpret_cast<void*>(XInputGetState))==MH_OK);
 gameui::install(window,tick);assert(MH_ApplyQueued()==MH_OK);gameui::bind(mapping);
 ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;ComPtr<IDXGISwapChain> swap;
 auto create=[&](){DXGI_SWAP_CHAIN_DESC desc{};desc.BufferCount=1;desc.BufferDesc.Width=1024;desc.BufferDesc.Height=768;desc.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;desc.OutputWindow=window;desc.SampleDesc.Count=1;desc.Windowed=TRUE;
  auto result=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&swap,&device,nullptr,&context);
  if(FAILED(result))result=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&swap,&device,nullptr,&context);
  assert(SUCCEEDED(result));
 };
 auto present=[&](){auto before=shared->renderFrames;shared->heartbeat=GetTickCount64();MSG message{};while(PeekMessageW(&message,nullptr,0,0,PM_REMOVE)){TranslateMessage(&message);DispatchMessageW(&message);}swap->Present(0,0);assert(shared->rendererState==1&&shared->renderFrames==before+1);};
 create();for(int i=0;i<12;i++){shared->panelOpen=i%2;present();}
 assert(shared->renderFrames==12);assert(SUCCEEDED(swap->ResizeBuffers(1,800,600,DXGI_FORMAT_UNKNOWN,0)));present();
 // A new game device/swap chain must not strand the renderer after a mode change.
 context->ClearState();swap.Reset();context.Reset();device.Reset();create();present();
 gameui::bind(mapping);present();assert(shared->renderFrames==15);
 // A held driving stick must not adjust a formerly focused slider in passive mode.
 shared->panelOpen=1;shared->editMode=1;present();present();present();
 simulatedPad.wButtons=XINPUT_GAMEPAD_DPAD_DOWN;present();simulatedPad.wButtons=0;present();
 simulatedPad.wButtons=XINPUT_GAMEPAD_A;present();simulatedPad.wButtons=0;present();simulatedPad.sThumbLX=32767;simulatedPad.wButtons=XINPUT_GAMEPAD_DPAD_RIGHT;
 for(int i=0;i<35;i++){Sleep(16);present();}
 assert(shared->uiRevision>0);auto edited=shared->uiSettings.fov;
 shared->editMode=0;auto revision=shared->uiRevision;
 for(int i=0;i<35;i++){Sleep(16);present();}
 assert(shared->uiRevision==revision&&shared->uiSettings.fov==edited&&!gameui::capturing());
 auto frames=shared->renderFrames;
 // A dead desktop heartbeat must immediately stop captures and rendering.
 shared->heartbeat=0;swap->Present(0,0);assert(!gameui::capturing()&&shared->renderFrames==frames);
 std::cout<<"D3D11 renderer: Present, ResizeBuffers, swap-chain/device replacement, reconnect, controller editing, passive driving and heartbeat passed.\n";
 DestroyWindow(window);return 0;
}
