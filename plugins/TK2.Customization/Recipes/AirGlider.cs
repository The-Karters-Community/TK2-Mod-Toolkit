using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Hold a shortcut in the air for limited lift and left/right glide correction.
public sealed class AirGlider : LocalKartMechanic
{
    public override string Name => "AirGlider";
    private ConfigEntry<float> _fuel = null!, _lift = null!, _steer = null!, _fallLimit = null!, _airDelay = null!, _recharge = null!;
    private ConfigEntry<KeyCode> _key = null!, _left = null!, _right = null!;
    private ConfigEntry<bool> _all = null!;
    private readonly Dictionary<int, float> _remaining = new();

    public override void Configure(ConfigFile config)
    {
        _fuel = config.Bind("Recipe." + Name, "FuelSeconds", 2f, new ConfigDescription("Maximum glide duration before ground recharge is needed.", new AcceptableValueRange<float>(.2f, 8)));
        _lift = config.Bind("Recipe." + Name, "LiftAcceleration", 18f, new ConfigDescription("Upward acceleration while gliding; does not replace native gravity.", new AcceptableValueRange<float>(0, 50)));
        _steer = config.Bind("Recipe." + Name, "SteeringAcceleration", 8f, new ConfigDescription("Left/right air correction acceleration.", new AcceptableValueRange<float>(0, 25)));
        _fallLimit = config.Bind("Recipe." + Name, "MaximumLiftVerticalSpeed", 2f, new ConfigDescription("Stop adding lift above this upward velocity.", new AcceptableValueRange<float>(-10, 10)));
        _airDelay = config.Bind("Recipe." + Name, "MinimumAirTime", .2f, new ConfigDescription("Delay before gliding can start after leaving the ground.", new AcceptableValueRange<float>(0, 2)));
        _recharge = config.Bind("Recipe." + Name, "GroundRechargeRate", 2f, new ConfigDescription("Fuel seconds restored per grounded second.", new AcceptableValueRange<float>(.1f, 10)));
        _key = config.Bind("Recipe." + Name, "GlideKey", KeyCode.G, "Hold while airborne to glide; no visual wings are attached.");
        _left = config.Bind("Recipe." + Name, "SteerLeftKey", KeyCode.LeftArrow, "Glide correction to the left.");
        _right = config.Bind("Recipe." + Name, "SteerRightKey", KeyCode.RightArrow, "Glide correction to the right.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Apply shared keyboard glide inputs to all local players.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_remaining);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            if (!_remaining.TryGetValue(id, out float fuel)) fuel = _fuel.Value;
            if (kart.bWasGrounded) fuel = Mathf.Min(_fuel.Value, fuel + delta * _recharge.Value);
            else if (fuel > 0 && kart.fTimeInAir >= _airDelay.Value && Input.GetKey(_key.Value))
            {
                float used = Mathf.Min(delta, fuel); fuel -= used;
                Vector3 force = Vector3.zero;
                float vertical = kart.kartController.GetKartVelocity().y;
                if (vertical < _fallLimit.Value) force.y = Mathf.Min(_lift.Value * used, _fallLimit.Value - vertical);
                int direction = (Input.GetKey(_right.Value) ? 1 : 0) - (Input.GetKey(_left.Value) ? 1 : 0);
                force += Vector3.Cross(Vector3.up, MechanicsContext.Forward(kart)) * (_steer.Value * direction * used);
                ReadableGame.AddVelocity(kart, force);
            }
            _remaining[id] = Mathf.Clamp(fuel, 0, _fuel.Value);
        }
    }
    protected override void ClearState() => _remaining.Clear();
}
