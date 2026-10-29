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
Console.WriteLine("CTR fire state assertions passed");
