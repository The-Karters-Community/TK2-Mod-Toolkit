using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Follow another racer, store their wake, then spend it on a passing dash.
public sealed class SlipstreamSling : LocalKartMechanic
{
    public override string Name => "SlipstreamSling";
    private ConfigEntry<float> _distance = null!, _angle = null!, _chargeTime = null!, _impulse = null!, _decay = null!, _cooldown = null!, _minimumSpeed = null!, _height = null!;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _all = null!;
    private sealed class State { internal float Charge, ReadyAt; }
    private readonly Dictionary<int, State> _states = new();

    public override void Configure(ConfigFile config)
    {
        _distance = config.Bind("Recipe." + Name, "DraftDistance", 18f, new ConfigDescription("Maximum distance behind a racer in world units.", new AcceptableValueRange<float>(3, 50)));
        _angle = config.Bind("Recipe." + Name, "WakeConeDegrees", 25f, new ConfigDescription("Half-angle of the draft cone behind another kart.", new AcceptableValueRange<float>(5, 70)));
        _chargeTime = config.Bind("Recipe." + Name, "FullChargeSeconds", 2f, new ConfigDescription("Time spent drafting to fill the passing dash.", new AcceptableValueRange<float>(.25f, 8)));
        _impulse = config.Bind("Recipe." + Name, "DashVelocity", 12f, new ConfigDescription("Forward velocity added at full charge.", new AcceptableValueRange<float>(1, 30)));
        _decay = config.Bind("Recipe." + Name, "ChargeDecayPerSecond", .15f, new ConfigDescription("Fraction of a full charge lost per second outside a wake.", new AcceptableValueRange<float>(0, 1)));
        _cooldown = config.Bind("Recipe." + Name, "CooldownSeconds", 3f, new ConfigDescription("Time between passing dashes.", new AcceptableValueRange<float>(.25f, 20)));
        _minimumSpeed = config.Bind("Recipe." + Name, "MinimumSpeed", 5f, new ConfigDescription("Minimum kart speed needed to charge a wake.", new AcceptableValueRange<float>(0, 30)));
        _height = config.Bind("Recipe." + Name, "VerticalTolerance", 3f, new ConfigDescription("Maximum vertical separation from the leading kart; avoids drafting through stacked track levels.", new AcceptableValueRange<float>(.5f, 10)));
        _key = config.Bind("Recipe." + Name, "DashKey", KeyCode.V, "Press to spend at least 25% stored slipstream charge.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Shared keyboard applies to all local players; otherwise only the first local player.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_states);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            if (!_states.TryGetValue(id, out var state)) _states[id] = state = new State();
            bool drafting = false;
            Vector3 position = kart.kartController.GetKartPos(), forward = MechanicsContext.Forward(kart);
            if (kart.bWasGrounded && kart.kartController.GetKartVelocity().magnitude >= _minimumSpeed.Value)
            foreach (var other in MechanicsContext.All)
            {
                if (other == kart || !MechanicsContext.Driveable(other)) continue;
                Vector3 offset = position - other.kartController.GetKartPos();
                if (Mathf.Abs(offset.y) > _height.Value || other.kartController.GetKartVelocity().magnitude < _minimumSpeed.Value) continue;
                offset.y = 0;
                if (offset.sqrMagnitude < 1 || offset.sqrMagnitude > _distance.Value * _distance.Value) continue;
                Vector3 otherForward = MechanicsContext.Forward(other);
                if (Vector3.Dot(-offset.normalized, otherForward) >= Mathf.Cos(_angle.Value * Mathf.Deg2Rad) &&
                    Vector3.Dot(forward, otherForward) > .65f) { drafting = true; break; }
            }
            state.Charge = Mathf.Clamp01(state.Charge + delta * (drafting ? 1 / _chargeTime.Value : -_decay.Value));
            if (kart.bWasGrounded && Input.GetKeyDown(_key.Value) && state.Charge >= .25f && Time.time >= state.ReadyAt)
            {
                ReadableGame.AddVelocity(kart, forward * (_impulse.Value * state.Charge));
                state.Charge = 0; state.ReadyAt = Time.time + _cooldown.Value;
            }
        }
    }
    protected override void ClearState() => _states.Clear();
}
