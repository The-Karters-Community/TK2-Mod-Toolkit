using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// An independent drift bank: save a long drift for a straight-line release.
public sealed class DriftCapacitor : LocalKartMechanic
{
    public override string Name => "DriftCapacitor";
    private ConfigEntry<float> _chargeTime = null!, _velocity = null!, _leak = null!, _cooldown = null!;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _auto = null!, _all = null!;
    private sealed class State { internal float Charge, ReadyAt; internal bool WasDrifting; }
    private readonly Dictionary<int, State> _states = new();

    public override void Configure(ConfigFile config)
    {
        _chargeTime = config.Bind("Recipe." + Name, "FullChargeSeconds", 3f, new ConfigDescription("Grounded drift duration for a full capacitor.", new AcceptableValueRange<float>(.5f, 10)));
        _velocity = config.Bind("Recipe." + Name, "ReleaseVelocity", 14f, new ConfigDescription("Forward velocity added at full stored charge.", new AcceptableValueRange<float>(1, 35)));
        _leak = config.Bind("Recipe." + Name, "ChargeLeakPerSecond", .08f, new ConfigDescription("Fraction of a full capacitor lost per second when not drifting.", new AcceptableValueRange<float>(0, 1)));
        _cooldown = config.Bind("Recipe." + Name, "CooldownSeconds", 2f, new ConfigDescription("Minimum time between capacitor releases.", new AcceptableValueRange<float>(.25f, 20)));
        _auto = config.Bind("Recipe." + Name, "ReleaseWhenDriftEnds", false, "Release automatically when a grounded drift ends; otherwise save charge for the shortcut.");
        _key = config.Bind("Recipe." + Name, "ReleaseKey", KeyCode.B, "Press on the ground to release at least 20% stored drift charge.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Apply to every local kart; keyboard release is shared.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_states);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            if (!_states.TryGetValue(id, out var state)) _states[id] = state = new State();
            bool drift = kart.bWasGrounded && kart.bIsDrifting;
            state.Charge = Mathf.Clamp01(state.Charge + delta * (drift ? 1 / _chargeTime.Value : -_leak.Value));
            bool release = Input.GetKeyDown(_key.Value) || (_auto.Value && state.WasDrifting && !drift);
            if (release && kart.bWasGrounded && state.Charge >= .2f && Time.time >= state.ReadyAt)
            {
                ReadableGame.AddVelocity(kart, MechanicsContext.Forward(kart) * _velocity.Value * state.Charge);
                state.Charge = 0; state.ReadyAt = Time.time + _cooldown.Value;
            }
            state.WasDrifting = drift;
        }
    }
    protected override void ClearState() => _states.Clear();
}
