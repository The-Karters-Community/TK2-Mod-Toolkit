using TK2.Customization;
static void Check(bool value) { if (!value) throw new Exception("CTR model assertion failed"); }
var m = new CtrFireModel();
m.Add(2, 16, false); m.Add(1, 8, false); Check(m.Reserves == 3 && m.FireCap == 16);
m.Tick(1, true); Check(m.Reserves == 3); m.Tick(1, false); Check(m.Reserves == 2);
m.Add(1, 30, true); Check(m.FireCap == 30); m.Add(1, 10, false); Check(m.FireCap == 10);
m.Clear(); m.Add(1, 16, false, true); m.Add(1, 16, false, true); Check(m.Reserves == 1);
m.Tick(.5f, true); m.Add(1, 16, false, true); Check(m.Reserves == 1);
m.Tick(.5f, false); m.Add(1, 16, false, true); Check(m.Reserves == 1);
m.Tick(2, false); Check(m.Reserves == 0 && m.FireCap == 0);
Check(CtrFireModel.DriftGain(.5f,.5f,1) == 0 && CtrFireModel.DriftGain(1,.5f,1) == 2);
var thirty = new CtrFireModel(); var twoHundred = new CtrFireModel();
thirty.Add(10, 16, false); twoHundred.Add(10, 16, false);
for (int i = 0; i < 30; i++) thirty.Tick(1f / 30f, false);
for (int i = 0; i < 200; i++) twoHundred.Tick(1f / 200f, false);
Check(Math.Abs(thirty.Reserves - twoHundred.Reserves) < .001f);
Check(CtrSpeedGhostMath.RetainedSpeed(20, 19.8f, 15, .6f, .02f) > 19.98f);
Check(CtrSpeedGhostMath.RetainedSpeed(14, 13, 15, .6f, .02f) == 13);
Check(CtrSpeedGhostMath.RetainedSpeed(20, 15, 12, .6f, .02f) == 15);
Check(CtrSpeedGhostMath.RetainedSpeed(20, 21, 15, .6f, .02f) == 21);
static float Sim(int rate) {
    float speed = 25;
    for (int i = 0; i < rate; i++) speed = CtrSpeedGhostMath.RetainedSpeed(speed, speed - 4f / rate, 15, .6f, 1f / rate);
    return speed;
}
Check(Math.Abs(Sim(30) - Sim(200)) < .001f);
Console.WriteLine("CTR fire and speedghost assertions passed");
