#include "Runtime.h"
#include "Core.h"
#include <commctrl.h>
#include <objidl.h>
#include <gdiplus.h>
#include <shlobj.h>
#include <sddl.h>
#include <bcrypt.h>
#include <shellapi.h>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <filesystem>
#include <array>
#include <vector>

using namespace race;
namespace fs=std::filesystem;
namespace {
constexpr wchar_t ClassName[]=L"BobbyRaceTools.Window.v1";
HINSTANCE instance;HWND window{},body{},tabs{},titleLabel{},connectButton{},statusLabel{},resetButton{},panelButton{},footer{},preview{},playerCombo{},cameraKeyCombo{},inputKeyCombo{};
HFONT font{},headingFont{},titleFont{};HBRUSH background{},paper{};int dpi=96,page{},scrollY{},bodyHeight{},contentHeight{};
Settings settings;Snapshot snapshot;
HWND gameWindow{};DWORD gamePid{},gameThread{};HANDLE gameHandle{},mapHandle{};HMODULE bridge{};HHOOK hook{};Shared* shared{};uint64_t connectStarted{};
uint64_t nonce{},lastPaint{},lastSnapshot{},lastReport{};std::wstring message=L"Open The Karters 2, then connect.";fs::path iniPath,reportPath;
 bool probe{},offline{},closing{},syncing{},pendingSave{};uint64_t probeStart{};int cameraKey=VK_F8,inputKey=VK_F9;ULONG_PTR gdiplusToken{};
LONG lastUiRevision{};
uint32_t listedPlayers=~0u,listedSelection=~0u;
void resize();void setPanel(bool enabled);
std::unique_ptr<Gdiplus::Font> hudTitle,hudSmall,hudButton;
struct Field {const wchar_t* key;const wchar_t* label;float Settings::*member;float low,high;HWND labelWindow{},slider{},edit{},reset{};};
std::array<Field,13> fields{{
 {L"Fov",L"Field of view (\u00b0)",&Settings::fov,35,110},{L"Distance",L"Distance (\u00d7)",&Settings::distance,.25f,4},
 {L"Height",L"Height",&Settings::height,-5,8},{L"Side",L"Side offset",&Settings::side,-4,4},
 {L"Target",L"Target height",&Settings::target,-1,3},{L"Pitch",L"Pitch (\u00b0)",&Settings::pitch,-45,45},
 {L"Yaw",L"Yaw (\u00b0)",&Settings::yaw,-90,90},{L"Roll",L"Roll (\u00b0)",&Settings::roll,-30,30},
 {L"Smoothing",L"Smoothing (seconds)",&Settings::smoothing,0,2},
 {L"OverlayX",L"Horizontal position (%)",&Settings::overlayX,0,100},{L"OverlayY",L"Vertical position (%)",&Settings::overlayY,0,100},
 {L"OverlayScale",L"Size (\u00d7)",&Settings::overlayScale,.25f,2},{L"Opacity",L"Background opacity",&Settings::opacity,.45f,1}
}};
struct Flag {const wchar_t* key;const wchar_t* label;uint32_t Settings::*member;HWND control{};};
std::array<Flag,5> flags{{{L"Camera",L"Camera overrides",&Settings::camera},{L"Preserve",L"Keep kart size when FOV changes",&Settings::preserve},
 {L"Aim",L"Aim toward kart",&Settings::aim},{L"Viewer",L"Show input viewer",&Settings::viewer},{L"Ghosts",L"Show recorded inputs when watching replays",&Settings::ghosts}}};
HWND groupA{},groupB{},playerLabel{},shortcutsLabel{},cameraKeyLabel{},inputKeyLabel{},sourceNote{};
int px(int value){return MulDiv(value,dpi,96);}
std::wstring number(float value){wchar_t text[48];swprintf_s(text,L"%.3f",value);std::wstring s=text;while(s.back()==L'0')s.pop_back();if(s.back()==L'.')s.pop_back();return s;}
void setText(HWND h,const std::wstring& text){if(!h)return;int size=GetWindowTextLengthW(h);std::wstring current(size+1,L'\0');GetWindowTextW(h,current.data(),size+1);current.resize(size);if(current!=text)SetWindowTextW(h,text.c_str());}
HWND child(const wchar_t* type,const wchar_t* text,DWORD style,HWND parent,int id=0){if(!wcscmp(type,L"BUTTON")&&(style&BS_TYPEMASK)==BS_PUSHBUTTON)style=(style&~BS_TYPEMASK)|BS_OWNERDRAW;if(!wcscmp(type,L"STATIC"))style|=SS_NOPREFIX;auto h=CreateWindowExW(0,type,text,WS_CHILD|WS_VISIBLE|style,0,0,1,1,parent,reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),instance,nullptr);SendMessageW(h,WM_SETFONT,reinterpret_cast<WPARAM>(font),TRUE);return h;}
void show(HWND h,bool visible){ShowWindow(h,visible?SW_SHOW:SW_HIDE);}
void place(HWND h,int x,int y,int w,int hgt,bool inBody=true){MoveWindow(h,px(x),px(y-(inBody?scrollY:0)),px(w),px(hgt),TRUE);}
void status(const std::wstring& text){message=text;setText(statusLabel,message);}
void fonts(){
 auto old=font,oldHeading=headingFont,oldTitle=titleFont;
 font=CreateFontW(-px(13),0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
 headingFont=CreateFontW(-px(14),0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
 titleFont=CreateFontW(-px(21),0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
 if(window)EnumChildWindows(window,[](HWND h,LPARAM)->BOOL{SendMessageW(h,WM_SETFONT,reinterpret_cast<WPARAM>(font),TRUE);return TRUE;},0);
 for(auto h:{groupA,groupB})if(h)SendMessageW(h,WM_SETFONT,reinterpret_cast<WPARAM>(headingFont),TRUE);
 if(titleLabel)SendMessageW(titleLabel,WM_SETFONT,reinterpret_cast<WPARAM>(titleFont),TRUE);
 if(old)DeleteObject(old);if(oldHeading)DeleteObject(oldHeading);if(oldTitle)DeleteObject(oldTitle);
}
fs::path executable(){wchar_t value[32768];GetModuleFileNameW(nullptr,value,32768);return fs::path(value);}
fs::path localData(){PWSTR value{};if(FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData,0,nullptr,&value)))throw std::runtime_error("Cannot locate the user data folder.");fs::path p(value);CoTaskMemFree(value);return p/L"BobbyRaceTools";}
void loadSettings(){
 iniPath=executable().parent_path()/L"BobbyRaceTools.ini";
 // Prefer portable settings; fall back to the user folder if this directory is read-only.
 auto test=executable().parent_path()/(L".race-tools-write-"+std::to_wstring(GetCurrentProcessId()));
 HANDLE check=CreateFileW(test.c_str(),GENERIC_WRITE,0,nullptr,CREATE_NEW,FILE_ATTRIBUTE_TEMPORARY|FILE_FLAG_DELETE_ON_CLOSE,nullptr);
 if(check==INVALID_HANDLE_VALUE){iniPath=localData()/L"BobbyRaceTools.ini";fs::create_directories(iniPath.parent_path());}else CloseHandle(check);
 wchar_t text[80];for(auto& f:fields){GetPrivateProfileStringW(L"Settings",f.key,number(settings.*f.member).c_str(),text,80,iniPath.c_str());wchar_t* end{};float v=wcstof(text,&end);if(end!=text&&!*end)settings.*f.member=v;}
 for(auto& f:flags)settings.*f.member=GetPrivateProfileIntW(L"Settings",f.key,settings.*f.member,iniPath.c_str());
 settings.player=GetPrivateProfileIntW(L"Settings",L"Player",AutomaticPlayer,iniPath.c_str());settings=sanitize(settings);
 cameraKey=GetPrivateProfileIntW(L"Settings",L"CameraKey",VK_F8,iniPath.c_str());inputKey=GetPrivateProfileIntW(L"Settings",L"InputKey",VK_F9,iniPath.c_str());
 if(cameraKey&&(cameraKey<VK_F1||cameraKey>VK_F12))cameraKey=VK_F8;if(inputKey&&(inputKey<VK_F1||inputKey>VK_F12))inputKey=VK_F9;
}
bool saveSettings(){
 bool ok=true;
 for(auto& f:fields)ok=WritePrivateProfileStringW(L"Settings",f.key,number(settings.*f.member).c_str(),iniPath.c_str())&&ok;
 for(auto& f:flags)ok=WritePrivateProfileStringW(L"Settings",f.key,std::to_wstring(settings.*f.member).c_str(),iniPath.c_str())&&ok;
 ok=WritePrivateProfileStringW(L"Settings",L"Player",std::to_wstring(settings.player).c_str(),iniPath.c_str())&&ok;
 ok=WritePrivateProfileStringW(L"Settings",L"CameraKey",std::to_wstring(cameraKey).c_str(),iniPath.c_str())&&ok;
 return WritePrivateProfileStringW(L"Settings",L"InputKey",std::to_wstring(inputKey).c_str(),iniPath.c_str())&&ok;
}
void push(){settings=sanitize(settings);if(shared){InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->settingsSequence));MemoryBarrier();shared->settings=settings;MemoryBarrier();InterlockedIncrement(reinterpret_cast<volatile LONG*>(&shared->settingsSequence));}pendingSave=true;SetTimer(window,2,350,nullptr);if(preview)InvalidateRect(preview,nullptr,FALSE);}
void syncControls(bool all=true){
 syncing=true;
 for(auto& f:fields){SendMessageW(f.slider,TBM_SETPOS,TRUE,static_cast<LPARAM>((settings.*f.member-f.low)/(f.high-f.low)*1000));if(all||GetFocus()!=f.edit)setText(f.edit,number(settings.*f.member));}
 for(auto& f:flags)SendMessageW(f.control,BM_SETCHECK,settings.*f.member?BST_CHECKED:BST_UNCHECKED,0);
 int count=static_cast<int>(SendMessageW(playerCombo,CB_GETCOUNT,0,0));for(int i=0;i<count;i++)if(static_cast<uint32_t>(SendMessageW(playerCombo,CB_GETITEMDATA,i,0))==settings.player)SendMessageW(playerCombo,CB_SETCURSEL,i,0);
 for(auto h:{fields[4].slider,fields[4].edit,fields[4].reset})EnableWindow(h,settings.aim!=0);
 InvalidateRect(fields[4].labelWindow,nullptr,TRUE);
 syncing=false;
}
void refreshPlayers(){
 uint32_t mask=shared?snapshot.localPlayers:0;
 if(mask==listedPlayers&&settings.player==listedSelection)return;
 listedPlayers=mask;listedSelection=settings.player;syncing=true;
 SendMessageW(playerCombo,CB_RESETCONTENT,0,0);
 auto add=[&](const std::wstring& label,uint32_t value){auto index=SendMessageW(playerCombo,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(label.c_str()));SendMessageW(playerCombo,CB_SETITEMDATA,index,value);if(value==settings.player)SendMessageW(playerCombo,CB_SETCURSEL,index,0);};
 add(L"Automatic",AutomaticPlayer);
 for(uint32_t i=0;i<6;i++)if((mask&(1u<<i))||settings.player==i)add(L"Player "+std::to_wstring(i+1)+((mask&(1u<<i))?L"":L" (not active)"),i);
 syncing=false;
}
std::wstring panelButtonText(){std::wstring label=shared&&shared->panelOpen?L"Hide camera":L"In-game camera";return cameraKey?label+L" \u00b7 F"+std::to_wstring(cameraKey-VK_F1+1):label;}
void shortcuts(){
 setText(panelButton,panelButtonText());
 UnregisterHotKey(window,1);UnregisterHotKey(window,2);
 bool ok=true;if(cameraKey)ok=RegisterHotKey(window,1,MOD_NOREPEAT,cameraKey)!=0;
 if(inputKey)ok=(RegisterHotKey(window,2,MOD_NOREPEAT,inputKey)!=0)&&ok;
 if(!ok)status(L"A shortcut is already in use. Choose different keys in Input viewer.");
}
bool readSnapshot(){
 if(!shared)return false;
 for(int i=0;i<3;i++){LONG before=InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->snapshotSequence),0,0);if(before&1)continue;
  Snapshot value=shared->snapshot;MemoryBarrier();if(before==InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->snapshotSequence),0,0)){snapshot=value;return true;}}
 return false;
}
void disconnect(){
 if(shared)InterlockedExchange64(&shared->heartbeat,0);
 if(hook){UnhookWindowsHookEx(hook);hook=nullptr;}
 if(shared){UnmapViewOfFile(shared);shared=nullptr;}if(mapHandle){CloseHandle(mapHandle);mapHandle=nullptr;}
 if(bridge){FreeLibrary(bridge);bridge=nullptr;}if(gameHandle){CloseHandle(gameHandle);gameHandle=nullptr;}
 gameWindow=nullptr;snapshot={};lastSnapshot=0;lastUiRevision=0;setText(connectButton,L"Connect");
 refreshPlayers();if(panelButton)EnableWindow(panelButton,FALSE);
 if(window&&!closing)SetTimer(window,1,100,nullptr);
}
std::wstring sha256(const uint8_t* data,DWORD size){
 BCRYPT_ALG_HANDLE algorithm{};BCRYPT_HASH_HANDLE hash{};DWORD count{},objectSize{};uint8_t digest[32];
 if(BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,nullptr,0)<0)throw std::runtime_error("SHA-256 unavailable.");
 BCryptGetProperty(algorithm,BCRYPT_OBJECT_LENGTH,reinterpret_cast<PUCHAR>(&objectSize),sizeof(objectSize),&count,0);std::vector<uint8_t> object(objectSize);
 NTSTATUS result=BCryptCreateHash(algorithm,&hash,object.data(),objectSize,nullptr,0,0);
 if(result>=0)result=BCryptHashData(hash,const_cast<PUCHAR>(data),size,0);if(result>=0)result=BCryptFinishHash(hash,digest,sizeof(digest),0);
 if(hash)BCryptDestroyHash(hash);BCryptCloseAlgorithmProvider(algorithm,0);if(result<0)throw std::runtime_error("Cannot verify bridge data.");
 std::wostringstream value;for(auto byte:digest)value<<std::hex<<std::setw(2)<<std::setfill(L'0')<<int(byte);return value.str();
}
fs::path extractBridge(){
 auto resource=FindResourceW(instance,MAKEINTRESOURCEW(101),RT_RCDATA);if(!resource)throw std::runtime_error("Embedded native bridge is missing.");
 DWORD size=SizeofResource(instance,resource);auto data=static_cast<const uint8_t*>(LockResource(LoadResource(instance,resource)));auto hash=sha256(data,size);
 wchar_t temporaryRoot[32768];DWORD length=GetTempPathW(32768,temporaryRoot);if(!length||length>=32768)throw std::runtime_error("Cannot locate the temporary bridge cache.");
 auto folder=fs::path(temporaryRoot)/L"BobbyRaceTools"/hash;fs::create_directories(folder);auto path=folder/(L"Bridge-"+hash.substr(0,16)+L".dll");
 bool same=false;if(fs::exists(path)){std::ifstream file(path,std::ios::binary);std::vector<uint8_t> old((std::istreambuf_iterator<char>(file)),{});same=old.size()==size&&sha256(old.data(),static_cast<DWORD>(old.size()))==hash;}
 if(!same){auto temporary=folder/(L"bridge-"+std::to_wstring(GetCurrentProcessId())+L".tmp");std::ofstream file(temporary,std::ios::binary|std::ios::trunc);file.write(reinterpret_cast<const char*>(data),size);file.close();if(!file||!MoveFileExW(temporary.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH))throw std::runtime_error("Cannot write the native bridge cache.");}
 return path;
}
BOOL CALLBACK findGame(HWND h,LPARAM){
 if(!IsWindowVisible(h)||GetWindow(h,GW_OWNER))return TRUE;
 DWORD pid{};DWORD thread=GetWindowThreadProcessId(h,&pid);HANDLE process=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|SYNCHRONIZE,FALSE,pid);if(!process)return TRUE;
 wchar_t path[32768];DWORD length=32768;bool match=QueryFullProcessImageNameW(process,0,path,&length)&&_wcsicmp(fs::path(path).filename().c_str(),L"TheKarters2.exe")==0;
 if(match){if(gameWindow){CloseHandle(process);return TRUE;}gameWindow=h;gamePid=pid;gameThread=thread;gameHandle=process;}else CloseHandle(process);return TRUE;
}
void connect(){
 if(shared){disconnect();status(L"Disconnected. The game uses its native camera.");return;}
 try{
  EnumWindows(findGame,0);if(!gameWindow)throw std::runtime_error("Open The Karters 2 before connecting.");
  auto path=extractBridge();bridge=LoadLibraryW(path.c_str());if(!bridge)throw std::runtime_error("Cannot load the embedded x64 bridge.");
  if(BCryptGenRandom(nullptr,reinterpret_cast<PUCHAR>(&nonce),sizeof(nonce),BCRYPT_USE_SYSTEM_PREFERRED_RNG)<0)throw std::runtime_error("Cannot create a private connection.");
  wchar_t name[128];swprintf_s(name,L"Local\\BobbyRaceTools_%lu_%llx",gamePid,static_cast<unsigned long long>(nonce));
  PSECURITY_DESCRIPTOR sd{};if(!ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:P(A;;GA;;;OW)",SDDL_REVISION_1,&sd,nullptr))throw std::runtime_error("Cannot restrict connection access.");
  SECURITY_ATTRIBUTES security{sizeof(security),sd,FALSE};mapHandle=CreateFileMappingW(INVALID_HANDLE_VALUE,&security,PAGE_READWRITE,0,sizeof(Shared),name);LocalFree(sd);
  if(!mapHandle)throw std::runtime_error("Cannot create the game connection.");shared=static_cast<Shared*>(MapViewOfFile(mapHandle,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared)));if(!shared)throw std::runtime_error("Cannot map the game connection.");
  new(shared)Shared{};shared->hostPid=GetCurrentProcessId();shared->settings=settings;shared->heartbeat=GetTickCount64();
  hook=SetWindowsHookExW(WH_GETMESSAGE,reinterpret_cast<HOOKPROC>(GetProcAddress(bridge,"RaceToolsBootstrap")),bridge,gameThread);
  if(!hook)throw std::runtime_error("Cannot connect. Run both apps as the same Windows user and elevation level.");
  // Unity can filter out thread-only messages during a race. A window message
  // reaches the same main-thread hook in menus, races and modal input loops.
  if(!PostMessageW(gameWindow,RegisterWindowMessageW(L"Bobby.RaceTools.Connect.v3"),static_cast<WPARAM>(nonce),0))throw std::runtime_error("The game is not responding to the connection.");
  connectStarted=GetTickCount64();status(L"Connecting to the game\u2026");setText(connectButton,L"Disconnect");SetTimer(window,1,16,nullptr);
 }catch(const std::exception& error){std::string s=error.what();std::wstring w(s.begin(),s.end());disconnect();status(w);}
}
void text(Gdiplus::Graphics& g,const wchar_t* value,Gdiplus::Font& f,float x,float y,Gdiplus::Color color,float width=400){
 Gdiplus::SolidBrush brush(color);Gdiplus::StringFormat format;format.SetFormatFlags(Gdiplus::StringFormatFlagsNoWrap);g.DrawString(value,-1,&f,Gdiplus::RectF(x,y,width,24),&format,&brush);
}
void drawHud(Gdiplus::Graphics& g,bool layoutPreview){
 using namespace Gdiplus;g.SetSmoothingMode(SmoothingModeAntiAlias);g.SetTextRenderingHint(TextRenderingHintAntiAliasGridFit);
 if(!hudTitle){hudTitle=std::make_unique<Font>(L"Segoe UI",14.f,FontStyleBold,UnitPixel);hudSmall=std::make_unique<Font>(L"Segoe UI",11.f,FontStyleRegular,UnitPixel);hudButton=std::make_unique<Font>(L"Segoe UI",11.f,FontStyleBold,UnitPixel);}
 SolidBrush panel(Color(static_cast<BYTE>(settings.opacity*255),19,21,19));g.FillRectangle(&panel,0,0,400,126);
 bool fresh=!layoutPreview&&snapshot.sampleTick&&GetTickCount64()-snapshot.sampleTick<500;
 Color muted(255,179,186,172),accent(255,255,184,92),light(255,237,241,229);text(g,L"Inputs",*hudTitle,14,8,light);
 text(g,layoutPreview?L"Layout preview":!fresh?L"Waiting for race input":snapshot.source==Ghost?L"Recorded ghost":L"Your driving inputs",*hudSmall,79,11,muted,235);
 text(g,layoutPreview?L"":!fresh?L"WAIT":snapshot.source==Ghost?L"GHOST":L"LIVE",*hudButton,336,11,fresh?accent:muted,60);
 Pen line(Color(255,71,76,68));g.DrawLine(&line,14,36,386,36);text(g,L"Steer",*hudSmall,14,47,muted);
 wchar_t value[32];swprintf_s(value,L"%+.2f",fresh?snapshot.steer:0);text(g,value,*hudSmall,62,47,muted);
 Pen rail(Color(255,71,76,68),4);g.DrawLine(&rail,18,85,106,85);g.DrawLine(&line,62,79,62,91);
 SolidBrush dotBrush(fresh&&std::abs(snapshot.steer)>.08f?accent:light);float x=62+(fresh?snapshot.steer:0)*40;g.FillEllipse(&dotBrush,x-6,79.f,12.f,12.f);
 const wchar_t* labels[]={L"Accelerate",L"Brake",L"Reverse",L"Jump / drift",L"Boost",L"Weapon"};const uint32_t masks[]={Accelerate,Brake,Reverse,Drift,Boost,Weapon};
 for(int i=0;i<6;i++){if(i==5&&fresh&&snapshot.source==Ghost)continue;float bx=124+89.f*(i%3),by=45+36.f*(i/3);bool on=fresh&&(snapshot.actions&masks[i]);SolidBrush fill(on?accent:Color(255,43,47,40));g.FillRectangle(&fill,bx,by,82.f,28.f);
  SolidBrush ink(on?Color(255,36,31,22):light);StringFormat center;center.SetAlignment(StringAlignmentCenter);center.SetLineAlignment(StringAlignmentCenter);g.DrawString(labels[i],-1,hudButton.get(),RectF(bx,by,82,28),&center,&ink);}
}
bool appOrGameFocused(){DWORD foreground{};GetWindowThreadProcessId(GetForegroundWindow(),&foreground);return foreground==gamePid||foreground==GetCurrentProcessId();}
void setPanel(bool enabled){
 if(!shared||shared->state!=2){status(L"Connect to the game before opening Camera.");return;}
 if(enabled&&shared->rendererState!=1){status(shared->rendererState<0?L"The game renderer could not open camera controls.":L"Waiting for the game renderer. Return to the game and try again.");return;}
 InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->editMode),0);
 InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->panelOpen),enabled?1:0);
 setText(panelButton,panelButtonText());
 // F8 never changes, hides, minimizes, reparents, or restyles the desktop window.
 // An explicit desktop click may bring the game forward to reveal its own panel.
 if(enabled&&GetForegroundWindow()==window)SetForegroundWindow(gameWindow);
}
void pullGameEdits(){
 if(!shared)return;
 LONG revision=InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&shared->uiRevision),0,0);
 if(revision==lastUiRevision)return;
 for(int i=0;i<3;i++){
  LONG before=shared->uiSequence;if(before&1)continue;
  Settings next=shared->uiSettings;MemoryBarrier();
  if(before!=shared->uiSequence||revision!=shared->uiRevision)continue;
  settings=sanitize(next);lastUiRevision=revision;syncControls(false);refreshPlayers();push();
  InterlockedExchange(reinterpret_cast<volatile LONG*>(&shared->uiAck),revision);break;
 }
}
void layoutBody(){
 RECT rect{};GetClientRect(body,&rect);int width=MulDiv(rect.right,96,dpi),available=width-44;bool narrow=width<630;int col=narrow?available:(available-32)/2;int right=narrow?22:22+col+32;
 for(auto& f:fields)for(auto h:{f.labelWindow,f.slider,f.edit,f.reset})show(h,false);for(auto& f:flags)show(f.control,false);
 for(auto h:{groupA,groupB,preview,playerLabel,playerCombo,shortcutsLabel,cameraKeyLabel,cameraKeyCombo,inputKeyLabel,inputKeyCombo,sourceNote})show(h,false);
 show(playerLabel,true);show(playerCombo,true);place(playerLabel,22+available-238,11,74,24);place(playerCombo,22+available-160,8,160,180);
 auto field=[&](int i,int x,int y,int w){auto& f=fields[i];for(auto h:{f.labelWindow,f.slider,f.edit,f.reset})show(h,true);place(f.labelWindow,x,y,w-115,21);place(f.edit,x+w-106,y-3,70,25);place(f.reset,x+w-29,y-3,29,25);place(f.slider,x,y+27,w,23);};
 if(page==0){
  show(flags[0].control,true);place(flags[0].control,22,8,available-248,26);show(groupA,true);setText(groupA,L"Framing");place(groupA,22,51,col,25);
  for(int i=0;i<4;i++)field(i,22,86+i*62,col);show(flags[1].control,true);place(flags[1].control,22,338,col,28);
  int top=narrow?394:51;show(groupB,true);setText(groupB,L"Aim & rotation");place(groupB,right,top,col,25);show(flags[2].control,true);place(flags[2].control,right,top+32,col,27);
  for(int i=4;i<9;i++)field(i,right,top+70+(i-4)*62,col);contentHeight=top+70+5*62+15;
 }else{
  show(flags[3].control,true);place(flags[3].control,22,8,available-248,26);show(flags[4].control,true);place(flags[4].control,22,42,available,26);
  show(preview,true);place(preview,22,85,std::min(available,400),126);
  show(sourceNote,true);place(sourceNote,22,222,available,32);
  field(9,22,270,col);field(10,22,332,col);field(11,right,narrow?394:270,col);field(12,right,narrow?456:332,col);
  int top=narrow?538:420;show(shortcutsLabel,true);place(shortcutsLabel,22,top,available,24);show(cameraKeyLabel,true);show(cameraKeyCombo,true);show(inputKeyLabel,true);show(inputKeyCombo,true);
  place(cameraKeyLabel,22,top+34,100,24);place(cameraKeyCombo,126,top+31,90,180);place(inputKeyLabel,244,top+34,70,24);place(inputKeyCombo,318,top+31,90,180);contentHeight=top+75;
 }
 int maximum=std::max(0,contentHeight-bodyHeight);if(scrollY>maximum){scrollY=maximum;layoutBody();return;}
 SCROLLINFO info{sizeof(info),SIF_RANGE|SIF_PAGE|SIF_POS,0,contentHeight,static_cast<UINT>(std::max(1,bodyHeight)),scrollY,0};SetScrollInfo(body,SB_VERT,&info,TRUE);
}
void resize(){
 RECT r{};GetClientRect(window,&r);int w=MulDiv(r.right,96,dpi),h=MulDiv(r.bottom,96,dpi);
 place(titleLabel,24,15,w-180,33,false);
 MoveWindow(connectButton,px(w-126),px(20),px(102),px(32),TRUE);MoveWindow(statusLabel,px(24),px(55),px(w-48),px(26),TRUE);
 MoveWindow(tabs,px(22),px(91),px(w-44),px(29),TRUE);int top=129;bodyHeight=std::max(120,h-top-55);MoveWindow(body,0,px(top),r.right,px(bodyHeight),TRUE);
 MoveWindow(resetButton,px(24),px(h-42),px(132),px(29),TRUE);MoveWindow(panelButton,px(168),px(h-42),px(142),px(29),TRUE);MoveWindow(footer,px(326),px(h-40),px(w-350),px(29),TRUE);layoutBody();InvalidateRect(window,nullptr,FALSE);
}
void scroll(int value){scrollY=std::clamp(value,0,std::max(0,contentHeight-bodyHeight));layoutBody();}
void command(WPARAM w){
 if(syncing)return;
 int id=LOWORD(w),notification=HIWORD(w);
 if(id==10&&notification==BN_CLICKED){connect();return;}
 if(id==13&&notification==BN_CLICKED){setPanel(!(shared&&shared->panelOpen));return;}
 if(id==11&&notification==BN_CLICKED){Settings defaults;if(page==0){for(int i=0;i<9;i++)settings.*fields[i].member=defaults.*fields[i].member;settings.preserve=defaults.preserve;settings.aim=defaults.aim;}else{for(int i=9;i<13;i++)settings.*fields[i].member=defaults.*fields[i].member;}syncControls();push();return;}
 if(id>=200&&id<205&&notification==BN_CLICKED){auto& f=flags[id-200];settings.*f.member=SendMessageW(f.control,BM_GETCHECK,0,0)==BST_CHECKED;syncControls();push();return;}
 if(id==206&&notification==CBN_SELCHANGE){auto index=SendMessageW(playerCombo,CB_GETCURSEL,0,0);if(index!=CB_ERR){settings.player=static_cast<uint32_t>(SendMessageW(playerCombo,CB_GETITEMDATA,index,0));push();refreshPlayers();}return;}
 if((id==207||id==208)&&notification==CBN_SELCHANGE){int value=static_cast<int>(SendMessageW(id==207?cameraKeyCombo:inputKeyCombo,CB_GETCURSEL,0,0));(id==207?cameraKey:inputKey)=value?VK_F1+value-1:0;shortcuts();pendingSave=true;SetTimer(window,2,350,nullptr);return;}
 if(id>=1000&&id<1000+int(fields.size())*3){int index=(id-1000)/3,kind=(id-1000)%3;auto& f=fields[index];
  if(kind==2&&notification==BN_CLICKED){Settings defaults;settings.*f.member=defaults.*f.member;syncControls();push();}
  else if(kind==1&&(notification==EN_CHANGE||notification==EN_KILLFOCUS)){wchar_t text[64];GetWindowTextW(f.edit,text,64);wchar_t* end{};float value=wcstof(text,&end);if(end!=text&&!*end&&std::isfinite(value)){settings.*f.member=std::clamp(value,f.low,f.high);push();SendMessageW(f.slider,TBM_SETPOS,TRUE,static_cast<LPARAM>((settings.*f.member-f.low)/(f.high-f.low)*1000));}if(notification==EN_KILLFOCUS)setText(f.edit,number(settings.*f.member));}
 }
}
void report(){
 if(reportPath.empty())return;
 auto detail=shared&&shared->error[0]?std::wstring(shared->error):message;
 int bytes=WideCharToMultiByte(CP_UTF8,0,detail.c_str(),-1,nullptr,0,nullptr,nullptr);std::string utf8(bytes,'\0');WideCharToMultiByte(CP_UTF8,0,detail.c_str(),-1,utf8.data(),bytes,nullptr,nullptr);utf8.pop_back();
 // Status text contains no control characters; quoted escapes quotes and backslashes.
 std::ofstream out(reportPath);out<<"{\"connected\":"<<(shared&&shared->state==2?"true":"false")<<",\"state\":"<<(shared?shared->state:0)<<",\"status\":"<<std::quoted(utf8)<<",\"capabilities\":"<<(shared?shared->capabilities:0)<<",\"requestedPlayer\":"<<settings.player<<",\"activePlayer\":"<<snapshot.player<<",\"localPlayers\":"<<snapshot.localPlayers<<",\"renderer\":"<<(shared?shared->rendererState:0)<<",\"renderFrames\":"<<(shared?shared->renderFrames:0)<<",\"panelOpen\":"<<(shared?shared->panelOpen:0)<<",\"editing\":"<<(shared?shared->editMode:0)<<",\"fullscreen\":"<<(shared?shared->fullscreen:0)<<",\"gamepad\":"<<(shared?shared->controllerPresent:0)<<",\"source\":"<<snapshot.source<<",\"actions\":"<<snapshot.actions<<",\"steer\":"<<snapshot.steer<<",\"accel\":"<<snapshot.accel<<",\"originalFov\":"<<snapshot.originalFov<<",\"appliedFov\":"<<snapshot.appliedFov<<",\"liveCalls\":"<<snapshot.liveCalls<<",\"ghostCalls\":"<<snapshot.ghostCalls<<",\"cameraCalls\":"<<snapshot.cameraCalls<<",\"faults\":"<<snapshot.faults<<"}";
}
void tick(){
 if(shared){InterlockedExchange64(&shared->heartbeat,GetTickCount64());
  if(gameHandle&&WaitForSingleObject(gameHandle,0)==WAIT_OBJECT_0){report();disconnect();status(L"Game closed. Open it and connect again.");}
  else if(shared->state==-1){std::wstring error=shared->error;report();disconnect();status(error);}
  else if(shared->state==2){readSnapshot();pullGameEdits();refreshPlayers();EnableWindow(panelButton,shared->rendererState==1);setText(panelButton,panelButtonText());
   if(shared->error[0])status(shared->error);
   else if(snapshot.player<6){
    auto actual=L"Player "+std::to_wstring(snapshot.player+1);
    if(settings.player<6&&settings.player!=snapshot.player)status(L"Player "+std::to_wstring(settings.player+1)+L" is not active \u00b7 using "+actual);
    else status(L"Connected \u00b7 "+actual+(snapshot.cameraTick&&GetTickCount64()-snapshot.cameraTick<1000?L" \u00b7 Racing camera ready":L" \u00b7 Waiting for racing camera"));
   }else status(snapshot.source==Ghost?L"Connected \u00b7 Recorded ghost inputs":L"Connected \u00b7 Waiting for a local racer");
   if(preview&&lastSnapshot!=snapshot.sampleTick){lastSnapshot=snapshot.sampleTick;InvalidateRect(preview,nullptr,FALSE);}}
  else if(GetTickCount64()-connectStarted>10000){report();disconnect();status(L"The game did not respond. Return to its menu and reconnect.");}
 }
 if(!reportPath.empty()&&GetTickCount64()-lastReport>=1000){lastReport=GetTickCount64();report();}
 if(probe&&GetTickCount64()-probeStart>12000){report();DestroyWindow(window);}
}
LRESULT CALLBACK previewProc(HWND h,UINT m,WPARAM w,LPARAM l){if(m==WM_PAINT){PAINTSTRUCT p;HDC dc=BeginPaint(h,&p);Gdiplus::Graphics g(dc);RECT r;GetClientRect(h,&r);g.ScaleTransform(r.right/400.f,r.bottom/126.f);drawHud(g,true);EndPaint(h,&p);return 0;}return DefWindowProcW(h,m,w,l);}
LRESULT CALLBACK bodyProc(HWND h,UINT m,WPARAM w,LPARAM l){
 if(m==WM_COMMAND||m==WM_HSCROLL||m==WM_DRAWITEM||m==WM_CTLCOLORSTATIC||m==WM_CTLCOLOREDIT||m==WM_CTLCOLORBTN)return SendMessageW(window,m,w,l);
 if(m==WM_VSCROLL){int target=scrollY;switch(LOWORD(w)){case SB_LINEUP:target-=30;break;case SB_LINEDOWN:target+=30;break;case SB_PAGEUP:target-=bodyHeight;break;case SB_PAGEDOWN:target+=bodyHeight;break;case SB_THUMBTRACK:case SB_THUMBPOSITION:{SCROLLINFO i{sizeof(i),SIF_TRACKPOS};GetScrollInfo(h,SB_VERT,&i);target=i.nTrackPos;break;}}scroll(target);return 0;}
 if(m==WM_MOUSEWHEEL){scroll(scrollY-GET_WHEEL_DELTA_WPARAM(w)/WHEEL_DELTA*80);return 0;}
 if(m==WM_ERASEBKGND){RECT r;GetClientRect(h,&r);FillRect(reinterpret_cast<HDC>(w),&r,background);return 1;}return DefWindowProcW(h,m,w,l);
}
LRESULT CALLBACK windowProc(HWND h,UINT m,WPARAM w,LPARAM l){
 switch(m){
 case WM_CREATE:{window=h;dpi=GetDpiForWindow(h);fonts();
  titleLabel=child(L"STATIC",L"Bobby's Race Tools",0,h);SendMessageW(titleLabel,WM_SETFONT,reinterpret_cast<WPARAM>(titleFont),TRUE);
  statusLabel=child(L"STATIC",message.c_str(),0,h);connectButton=child(L"BUTTON",L"Connect",BS_PUSHBUTTON|WS_TABSTOP,h,10);tabs=child(WC_TABCONTROLW,L"",WS_TABSTOP,h,12);
  TCITEMW item{};item.mask=TCIF_TEXT;item.pszText=const_cast<wchar_t*>(L"  Camera  ");TabCtrl_InsertItem(tabs,0,&item);item.pszText=const_cast<wchar_t*>(L"  Input viewer  ");TabCtrl_InsertItem(tabs,1,&item);
  body=CreateWindowExW(WS_EX_CONTROLPARENT,L"BobbyRaceTools.Body",L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|WS_CLIPCHILDREN,0,0,1,1,h,nullptr,instance,nullptr);
  for(int i=0;i<int(fields.size());i++){auto& f=fields[i];f.labelWindow=child(L"STATIC",f.label,0,body);f.slider=child(TRACKBAR_CLASSW,L"",TBS_NOTICKS|WS_TABSTOP,body,1000+i*3);SendMessageW(f.slider,TBM_SETRANGE,TRUE,MAKELPARAM(0,1000));f.edit=child(L"EDIT",L"",WS_TABSTOP|WS_BORDER|ES_RIGHT|ES_AUTOHSCROLL,body,1001+i*3);SendMessageW(f.edit,EM_SETLIMITTEXT,24,0);f.reset=child(L"BUTTON",L"\u21ba",WS_TABSTOP|BS_PUSHBUTTON,body,1002+i*3);}
  for(int i=0;i<int(flags.size());i++){flags[i].control=child(L"BUTTON",flags[i].label,BS_AUTOCHECKBOX|WS_TABSTOP,body,200+i);}
  groupA=child(L"STATIC",L"",0,body);groupB=child(L"STATIC",L"",0,body);for(auto label:{groupA,groupB})SendMessageW(label,WM_SETFONT,reinterpret_cast<WPARAM>(headingFont),TRUE);
  preview=CreateWindowExW(0,L"BobbyRaceTools.Preview",L"",WS_CHILD|WS_VISIBLE,0,0,400,126,body,nullptr,instance,nullptr);
  playerLabel=child(L"STATIC",L"Local player",0,body);playerCombo=child(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,body,206);refreshPlayers();
  shortcutsLabel=child(L"STATIC",L"Shortcuts",0,body);cameraKeyLabel=child(L"STATIC",L"Camera window",0,body);inputKeyLabel=child(L"STATIC",L"Inputs",0,body);cameraKeyCombo=child(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,body,207);inputKeyCombo=child(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,body,208);
  for(auto combo:{cameraKeyCombo,inputKeyCombo}){SendMessageW(combo,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(L"None"));for(int i=1;i<=12;i++){std::wstring s=L"F"+std::to_wstring(i);SendMessageW(combo,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(s.c_str()));}}
  SendMessageW(cameraKeyCombo,CB_SETCURSEL,cameraKey?cameraKey-VK_F1+1:0,0);SendMessageW(inputKeyCombo,CB_SETCURSEL,inputKey?inputKey-VK_F1+1:0,0);
  sourceNote=child(L"STATIC",L"Your inputs take priority over opponent ghosts.",0,body);
  resetButton=child(L"BUTTON",L"Reset framing",WS_TABSTOP|BS_PUSHBUTTON,h,11);panelButton=child(L"BUTTON",L"In-game camera \u00b7 F8",WS_TABSTOP|BS_PUSHBUTTON,h,13);EnableWindow(panelButton,FALSE);footer=child(L"STATIC",L"Changes save automatically.",0,h);
  syncControls();shortcuts();SetTimer(h,1,100,nullptr);return 0;
 }
 case WM_SIZE:if(body)resize();return 0;
 case WM_GETMINMAXINFO:{auto i=reinterpret_cast<MINMAXINFO*>(l);i->ptMinTrackSize={px(510),px(430)};return 0;}
 case WM_DPICHANGED:{dpi=HIWORD(w);fonts();auto r=reinterpret_cast<RECT*>(l);SetWindowPos(h,nullptr,r->left,r->top,r->right-r->left,r->bottom-r->top,SWP_NOZORDER|SWP_NOACTIVATE);return 0;}
 case WM_COMMAND:command(w);return 0;
 case WM_HSCROLL:{HWND source=reinterpret_cast<HWND>(l);for(auto& f:fields)if(f.slider==source){settings.*f.member=f.low+(f.high-f.low)*static_cast<float>(SendMessageW(source,TBM_GETPOS,0,0))/1000;setText(f.edit,number(settings.*f.member));push();break;}return 0;}
 case WM_NOTIFY:if(reinterpret_cast<NMHDR*>(l)->hwndFrom==tabs&&reinterpret_cast<NMHDR*>(l)->code==TCN_SELCHANGE){page=TabCtrl_GetCurSel(tabs);scrollY=0;setText(resetButton,page?L"Reset layout":L"Reset framing");layoutBody();}return 0;
 case WM_CTLCOLORSTATIC:case WM_CTLCOLORBTN:SetTextColor(reinterpret_cast<HDC>(w),reinterpret_cast<HWND>(l)==fields[4].labelWindow&&!settings.aim?RGB(136,142,129):RGB(38,43,37));SetBkColor(reinterpret_cast<HDC>(w),RGB(247,246,242));return reinterpret_cast<LRESULT>(background);
 case WM_CTLCOLOREDIT:SetTextColor(reinterpret_cast<HDC>(w),RGB(38,43,37));SetBkColor(reinterpret_cast<HDC>(w),RGB(255,254,250));return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));
 case WM_DRAWITEM:{auto draw=reinterpret_cast<DRAWITEMSTRUCT*>(l);if(draw->CtlType!=ODT_BUTTON)break;
  bool disabled=(draw->itemState&ODS_DISABLED)!=0,pressed=(draw->itemState&ODS_SELECTED)!=0,primary=draw->CtlID==10&&!disabled;
  auto brush=CreateSolidBrush(primary?RGB(255,184,92):pressed?RGB(226,226,219):RGB(252,251,248));FillRect(draw->hDC,&draw->rcItem,brush);DeleteObject(brush);
  auto border=CreateSolidBrush((draw->itemState&ODS_FOCUS)?RGB(199,131,48):RGB(196,198,190));FrameRect(draw->hDC,&draw->rcItem,border);DeleteObject(border);
  SetBkMode(draw->hDC,TRANSPARENT);SetTextColor(draw->hDC,disabled?RGB(136,142,129):RGB(38,43,37));
  auto previous=SelectObject(draw->hDC,font);wchar_t text[128];GetWindowTextW(draw->hwndItem,text,128);DrawTextW(draw->hDC,text,-1,&draw->rcItem,DT_CENTER|DT_VCENTER|DT_SINGLELINE);SelectObject(draw->hDC,previous);return TRUE;}
 case WM_ERASEBKGND:{RECT r;GetClientRect(h,&r);FillRect(reinterpret_cast<HDC>(w),&r,background);return 1;}
 case WM_TIMER:if(w==1)tick();else if(w==2){KillTimer(h,2);pendingSave=false;setText(footer,saveSettings()?L"Saved":L"Could not save settings. Check folder permissions.");}return 0;
 case WM_HOTKEY:if(appOrGameFocused()){if(w==2){settings.viewer=!settings.viewer;syncControls();push();}else setPanel(!(shared&&shared->panelOpen));}return 0;
 case WM_CLOSE:DestroyWindow(h);return 0;
 case WM_DESTROY:closing=true;if(pendingSave)saveSettings();disconnect();UnregisterHotKey(h,1);UnregisterHotKey(h,2);PostQuitMessage(0);return 0;
 }
 return DefWindowProcW(h,m,w,l);
}
}

int WINAPI wWinMain(HINSTANCE app,HINSTANCE,PWSTR,int showCommand){
 instance=app;
 int argc{};auto argv=CommandLineToArgvW(GetCommandLineW(),&argc);
 for(int i=1;i<argc;i++){if(!wcscmp(argv[i],L"--no-connect"))offline=true;else if(!wcscmp(argv[i],L"--probe"))probe=true;else if(!wcscmp(argv[i],L"--report")&&i+1<argc)reportPath=argv[++i];}
 LocalFree(argv);
 HANDLE single=CreateMutexW(nullptr,FALSE,L"Local\\BobbyRaceTools.Desktop.v1");if(GetLastError()==ERROR_ALREADY_EXISTS&&!probe){auto old=FindWindowW(ClassName,nullptr);if(old){ShowWindow(old,SW_RESTORE);SetForegroundWindow(old);}if(single)CloseHandle(single);return 0;}
 try{
  loadSettings();INITCOMMONCONTROLSEX controls{sizeof(controls),ICC_STANDARD_CLASSES|ICC_BAR_CLASSES|ICC_TAB_CLASSES};InitCommonControlsEx(&controls);
  Gdiplus::GdiplusStartupInput input;Gdiplus::GdiplusStartup(&gdiplusToken,&input,nullptr);paper=CreateSolidBrush(RGB(247,246,242));background=paper;
  WNDCLASSEXW cls{sizeof(cls)};cls.hInstance=instance;cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);cls.hIcon=LoadIconW(nullptr,IDI_APPLICATION);cls.hbrBackground=background;cls.lpfnWndProc=windowProc;cls.lpszClassName=ClassName;RegisterClassExW(&cls);
  cls.lpfnWndProc=bodyProc;cls.lpszClassName=L"BobbyRaceTools.Body";RegisterClassExW(&cls);cls.lpfnWndProc=previewProc;cls.lpszClassName=L"BobbyRaceTools.Preview";RegisterClassExW(&cls);
  window=CreateWindowExW(WS_EX_CONTROLPARENT,ClassName,L"Bobby's Race Tools",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,780,710,nullptr,nullptr,instance,nullptr);
  if(!window)throw std::runtime_error("Cannot create the application window.");
  if(!probe)ShowWindow(window,offline?SW_SHOWNOACTIVATE:showCommand);else{settings.camera=0;settings.viewer=1;probeStart=GetTickCount64();connect();}
  MSG msg;while(GetMessageW(&msg,nullptr,0,0)>0){if(!IsDialogMessageW(window,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}
  hudTitle.reset();hudSmall.reset();hudButton.reset();Gdiplus::GdiplusShutdown(gdiplusToken);DeleteObject(font);DeleteObject(headingFont);DeleteObject(titleFont);DeleteObject(paper);if(single)CloseHandle(single);return 0;
 }catch(const std::exception& e){std::string s=e.what();std::wstring w(s.begin(),s.end());MessageBoxW(nullptr,w.c_str(),L"Bobby's Race Tools",MB_ICONERROR);if(single)CloseHandle(single);return 1;}
}
