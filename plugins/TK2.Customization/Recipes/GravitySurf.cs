using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Bank downhill movement, then spend that energy climbing the next hill.
public sealed class GravitySurf : LocalKartMechanic
{
    public override string Name => "GravitySurf";
    private ConfigEntry<float> _threshold = null!, _charge = null!, _assist = null!, _spend = null!, _surf = null!, _decay = null!, _minimumSpeed = null!;
    private ConfigEntry<bool> _all = null!;
    private readonly Dictionary<int, float> _energy = new();

    public override void Configure(ConfigFile config)
    {
        _threshold = config.Bind("Recipe." + Name, "MinimumSlopeRatio", .08f, new ConfigDescription("Minimum vertical speed divided by total speed to identify a hill; uses movement rather than ground material.", new AcceptableValueRange<float>(.02f, .5f)));
        _charge = config.Bind("Recipe." + Name, "DownhillChargePerSecond", .4f, new ConfigDescription("Fraction of full energy banked per second while moving downhill on the ground.", new AcceptableValueRange<float>(.05f, 2)));
        _assist = config.Bind("Recipe." + Name, "UphillAcceleration", 10f, new ConfigDescription("Extra forward acceleration while climbing with stored energy.", new AcceptableValueRange<float>(0, 25)));
        _spend = config.Bind("Recipe." + Name, "UphillEnergyUsePerSecond", .35f, new ConfigDescription("Fraction of full energy spent per uphill second.", new AcceptableValueRange<float>(.05f, 2)));
        _surf = config.Bind("Recipe." + Name, "DownhillAcceleration", 3f, new ConfigDescription("Extra forward acceleration while surfing downhill.", new AcceptableValueRange<float>(0, 12)));
        _decay = config.Bind("Recipe." + Name, "FlatEnergyDecayPerSecond", .03f, new ConfigDescription("Energy bank lost on flat ground or in the air per second.", new AcceptableValueRange<float>(0, 1)));
        _minimumSpeed = config.Bind("Recipe." + Name, "MinimumSpeed", 5f, new ConfigDescription("Ignore low-speed slope jitter below this speed.", new AcceptableValueRange<float>(1, 30)));
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Enable the hill energy mechanic on all local karts.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_energy);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            _energy.TryGetValue(id, out float energy);
            Vector3 velocity = kart.kartController.GetKartVelocity();
            float slope = Vector3.Dot(velocity, MechanicsContext.Forward(kart)) >= _minimumSpeed.Value ? velocity.y / velocity.magnitude : 0;
            if (kart.bWasGrounded && slope < -_threshold.Value)
            {
                energy = Mathf.Min(1, energy + delta * _charge.Value);
                ReadableGame.AddVelocity(kart, MechanicsContext.Forward(kart) * (_surf.Value * delta));
            }
            else if (kart.bWasGrounded && slope > _threshold.Value && energy > 0)
            {
                float used = Mathf.Min(energy, delta * _spend.Value);
                ReadableGame.AddVelocity(kart, MechanicsContext.Forward(kart) * (_assist.Value * used / _spend.Value));
                energy -= used;
            }
            else energy = Mathf.Max(0, energy - delta * _decay.Value);
            _energy[id] = energy;
        }
    }
    protected override void ClearState() => _energy.Clear();
}
