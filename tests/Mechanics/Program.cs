using TK2.Customization;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx.Configuration;
using Object = UnityEngine.Object;

int checks=0;
void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
PixelKartPhysics Kart(bool ai=false,int number=0) { var kart=new PixelKartPhysics(); kart.kartController.parentPlayer.ePlayerType=ai?Ant_Player.EPlayerType.E_AI_LOCAL:Ant_Player.EPlayerType.E_HUMAN_LOCAL; kart.kartController.parentPlayer.eAntLocalPlayerNr=number; kart.kartController.Motor.Velocity=new(0,0,10); return kart; }
T Prepare<T>(params PixelKartPhysics[] karts) where T:LocalKartMechanic,new() {
 SceneManager.Scene++; Object.Karts=karts; Input.Down.Clear(); Input.Held.Clear(); Plugin.OfflineLabAllowed=true; Time.timeScale=1; Ant_CurrentGameConfiguration.eCurrentRaceState=Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
 var recipe=new T(); recipe.Configure(new ConfigFile()); return recipe;
}
void Step(LocalKartMechanic recipe,int frames=1) { for(int i=0;i<frames;i++) { Time.time+=.05f;Time.unscaledTime+=.05f;Time.frameCount++;recipe.Tick();Input.Down.Clear(); } }

var player=Kart(); var leader=Kart(true); leader.kartController.Motor.Position=new(0,0,8);
var sling=Prepare<SlipstreamSling>(player,leader); int beforeScans=Object.Scans;
Step(sling,42); Input.Down.Add(KeyCode.V);Step(sling);Check(player.Added.z>11.8f,"Draft produces a full passing dash");
Check(Object.Scans-beforeScans<=3,"Racer scans are cached rather than performed every frame");
player.Added=Vector3.zero;Input.Down.Add(KeyCode.V);Step(sling);Check(player.Added.sqrMagnitude==0,"Sling cooldown blocks repeat shortcuts");
sling.Restore();leader.kartController.Motor.Position=new(0,20,8);Step(sling,50);Input.Down.Add(KeyCode.V);Step(sling);Check(player.Added.sqrMagnitude==0,"No drafting from another track level");

player=Kart();var drift=Prepare<DriftCapacitor>(player);player.bIsDrifting=true;Step(drift,65);player.bIsDrifting=false;Input.Down.Add(KeyCode.B);Step(drift);Check(player.Added.z>13.8f,"A charged drift releases on a chosen straight");
player.Added=Vector3.zero;Input.Down.Add(KeyCode.B);Step(drift);Check(player.Added.sqrMagnitude==0,"Capacitor release consumes its charge");

player=Kart();var glide=Prepare<AirGlider>(player);player.bWasGrounded=false;player.fTimeInAir=.5f;player.kartController.Motor.Velocity=new(0,-10,10);Input.Held.Add(KeyCode.G);Step(glide);Check(player.Added.y>.89f,"Glide supplies lift while falling");
Step(glide,45);player.Added=Vector3.zero;Step(glide);Check(player.Added.sqrMagnitude==0,"Glide fuel is limited");
player.bWasGrounded=true;Step(glide,10);player.bWasGrounded=false;Step(glide);Check(player.Added.y>.89f,"Landing recharges glider fuel");

player=Kart();var rewind=Prepare<EchoRewind>(player);
for(int i=0;i<80;i++){player.kartController.Motor.Position=new(0,0,i*.2f);Step(rewind);}
float recent=player.kartController.Motor.Position.z;Input.Down.Add(KeyCode.R);Step(rewind);Check(player.kartController.Motor.Position.z<recent-6,"Echo restores a motor position from two seconds ago");
float restored=player.kartController.Motor.Position.z;player.kartController.Motor.Position=new(0,0,restored+1);Input.Down.Add(KeyCode.R);Step(rewind);Check(player.kartController.Motor.Position.z==restored+1,"Echo cooldown prevents another rewind");
rewind.Restore();for(int i=0;i<60;i++){player.kartController.Motor.Position=new(0,0,i*.2f);Step(rewind);}player.kartController.Motor.Position=new(0,0,100);Input.Down.Add(KeyCode.R);Step(rewind);Check(player.kartController.Motor.Position.z==100,"Teleport invalidates rewind history");

player=Kart();var bot=Kart(true);bot.kartController.Motor.Position=new(0,0,5);var second=Kart(false,1);second.kartController.Motor.Position=new(0,0,4);var pulse=Prepare<RepulsorPulse>(player,bot,second);Input.Down.Add(KeyCode.P);Step(pulse);Check(bot.Added.z>0 && bot.Added.y>0,"Pulse pushes nearby AI outward and upward");Check(second.Added.sqrMagnitude==0,"Pulse default protects other local human racers");
bot.Added=Vector3.zero;Input.Down.Add(KeyCode.P);Step(pulse);Check(bot.Added.sqrMagnitude==0,"Pulse cooldown prevents repeat hits");

player=Kart();var surf=Prepare<GravitySurf>(player);player.kartController.Motor.Velocity=new(0,-5,10);Step(surf,10);Check(player.Added.z>1.4f,"Surf accelerates on downhill movement");player.Added=Vector3.zero;player.kartController.Motor.Velocity=new(0,5,10);Step(surf);Check(player.Added.z>.49f,"Downhill bank assists a later climb");player.Added=Vector3.zero;player.kartController.Motor.Velocity=new(0,0,10);Step(surf);Check(player.Added.sqrMagnitude==0,"Surf adds no force on flat ground");player.kartController.Motor.Velocity=new(0,-5,-10);Step(surf);Check(player.Added.sqrMagnitude==0,"Reverse hill movement cannot charge or surf");

player=Kart();var landing=Prepare<LandingCombo>(player);Step(landing);player.bWasGrounded=false;Step(landing,10);Input.Down.Add(KeyCode.L);Step(landing);player.bWasGrounded=true;Step(landing);Check(Math.Abs(player.Added.z-5)<.001f,"Timed first landing produces the base burst");player.Added=Vector3.zero;Input.Down.Add(KeyCode.L);Step(landing);Check(player.Added.sqrMagnitude==0,"No duplicate burst for the same landing");player.bWasGrounded=false;Step(landing,10);Input.Down.Add(KeyCode.L);Step(landing);player.bWasGrounded=true;Step(landing);Check(Math.Abs(player.Added.z-6.5f)<.001f,"A second perfect landing increases the combo burst");
player.Added=Vector3.zero;player.bWasGrounded=false;Step(landing,10);player.bWasGrounded=true;Step(landing,6);player.bWasGrounded=false;Step(landing,10);Input.Down.Add(KeyCode.L);Step(landing);player.bWasGrounded=true;Step(landing);Check(Math.Abs(player.Added.z-5)<.001f,"Missing the timing window resets the combo");

foreach(var guard in new[]{"online","pause","race","respawn","replay","death"}) {
 player=Kart();var capacitor=Prepare<DriftCapacitor>(player);player.bIsDrifting=true;Step(capacitor,65);player.bIsDrifting=false;
 switch(guard){case "online":Plugin.OfflineLabAllowed=false;break;case "pause":Time.timeScale=0;break;case "race":Ant_CurrentGameConfiguration.eCurrentRaceState=Ant_CurrentGameConfiguration.ERaceState.Stopped;break;case "respawn":player.kartController.outsideMapValidator.bIsPlayerRespawning=true;break;case "replay":player.kartController.Replay=true;break;case "death":player.kartController.parentPlayer.hpBarController.currentHp=0;break;}
 Input.Down.Add(KeyCode.B);Step(capacitor);Check(player.Added.sqrMagnitude==0,"Guard blocks impulses during "+guard);
}
Console.WriteLine($"Passed {checks} mechanic transition and guard checks; stub simulation, no game launch.");
