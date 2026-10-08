using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Time a shortcut at touchdown to chain increasingly strong landing bursts.
public sealed class LandingCombo : LocalKartMechanic
{
    public override string Name => "LandingCombo";
    private ConfigEntry<float> _window = null!, _minimumAir = null!, _impulse = null!, _step = null!, _timeout = null!;
    private ConfigEntry<int> _maximum = null!;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _all = null!;
    private sealed class State
    {
        internal bool Initialized, WasGrounded, LandingPending;
        internal float AirTime, LastPress = -100, LandedAt = -100, LastSuccess = -100;
        internal int Combo;
    }
    private readonly Dictionary<int, State> _states = new();

    public override void Configure(ConfigFile config)
    {
        _window = config.Bind("Recipe." + Name, "TimingWindowSeconds", .16f, new ConfigDescription("Press window before or after landing for a perfect landing burst.", new AcceptableValueRange<float>(.05f, .6f)));
        _minimumAir = config.Bind("Recipe." + Name, "MinimumAirTime", .4f, new ConfigDescription("Minimum airborne duration before touchdown can score.", new AcceptableValueRange<float>(.1f, 3)));
        _impulse = config.Bind("Recipe." + Name, "BaseBurstVelocity", 5f, new ConfigDescription("Forward velocity added by the first perfect landing.", new AcceptableValueRange<float>(1, 15)));
        _step = config.Bind("Recipe." + Name, "ExtraVelocityPerCombo", 1.5f, new ConfigDescription("Additional forward velocity for each chained landing.", new AcceptableValueRange<float>(0, 5)));
        _maximum = config.Bind("Recipe." + Name, "MaximumCombo", 4, new ConfigDescription("Maximum number of landings counted for burst scaling.", new AcceptableValueRange<int>(1, 10)));
        _timeout = config.Bind("Recipe." + Name, "ComboTimeoutSeconds", 8f, new ConfigDescription("Time allowed between successful perfect landings before the combo resets.", new AcceptableValueRange<float>(1, 30)));
        _key = config.Bind("Recipe." + Name, "LandingKey", KeyCode.L, "Press just before or just after touchdown to trigger a landing burst.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Use the shared landing timing shortcut for every local player.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_states);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            if (!_states.TryGetValue(id, out var state)) _states[id] = state = new State();
            bool grounded = kart.bWasGrounded;
            if (!state.Initialized) { state.Initialized = true; state.WasGrounded = grounded; }
            if (Input.GetKeyDown(_key.Value)) state.LastPress = Time.time;
            if (Time.time - state.LastSuccess > _timeout.Value) state.Combo = 0;
            if (!grounded) state.AirTime += delta;
            if (grounded && !state.WasGrounded)
            {
                state.LandingPending = state.AirTime >= _minimumAir.Value;
                state.LandedAt = Time.time; state.AirTime = 0;
            }
            if (state.LandingPending && !grounded) { state.LandingPending = false; state.Combo = 0; }
            if (state.LandingPending && Time.time - state.LandedAt > _window.Value)
            { state.LandingPending = false; state.Combo = 0; }
            if (state.LandingPending && grounded && Mathf.Abs(state.LastPress - state.LandedAt) <= _window.Value)
            {
                state.Combo = Mathf.Min(_maximum.Value, state.Combo + 1);
                ReadableGame.AddVelocity(kart, MechanicsContext.Forward(kart) * (_impulse.Value + (state.Combo - 1) * _step.Value));
                state.LastSuccess = Time.time; state.LastPress = -100; state.LandingPending = false;
            }
            state.WasGrounded = grounded;
        }
    }
    protected override void ClearState() => _states.Clear();
}
