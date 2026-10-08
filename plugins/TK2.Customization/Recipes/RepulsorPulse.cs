using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// A close-range pulse throws nearby offline karts away without changing health.
public sealed class RepulsorPulse : LocalKartMechanic
{
    public override string Name => "RepulsorPulse";
    private ConfigEntry<float> _radius = null!, _force = null!, _lift = null!, _cooldown = null!, _falloff = null!;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _aiOnly = null!, _grounded = null!, _all = null!;
    private readonly Dictionary<int, float> _readyAt = new();

    public override void Configure(ConfigFile config)
    {
        _radius = config.Bind("Recipe." + Name, "Radius", 9f, new ConfigDescription("Maximum world-space radius of the outward pulse.", new AcceptableValueRange<float>(2, 25)));
        _force = config.Bind("Recipe." + Name, "PushVelocity", 10f, new ConfigDescription("Horizontal velocity added to a nearby target at the center of the pulse.", new AcceptableValueRange<float>(1, 25)));
        _lift = config.Bind("Recipe." + Name, "LiftVelocity", 2f, new ConfigDescription("Small upward velocity added to affected targets.", new AcceptableValueRange<float>(0, 8)));
        _cooldown = config.Bind("Recipe." + Name, "CooldownSeconds", 6f, new ConfigDescription("Time between pulses per local player.", new AcceptableValueRange<float>(1, 30)));
        _falloff = config.Bind("Recipe." + Name, "EdgeStrength", .2f, new ConfigDescription("Fraction of pulse strength retained at the outer radius.", new AcceptableValueRange<float>(0, 1)));
        _aiOnly = config.Bind("Recipe." + Name, "AffectAiOnly", true, "Only push offline AI; disabling also permits other local human karts.");
        _grounded = config.Bind("Recipe." + Name, "RequireGrounded", true, "Only emit the pulse while your kart is grounded.");
        _key = config.Bind("Recipe." + Name, "PulseKey", KeyCode.P, "Press to push nearby offline racers without consuming or awarding an item.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Shared shortcut emits a pulse from every local kart.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_readyAt);
        if (!Input.GetKeyDown(_key.Value)) return;
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value) || (_grounded.Value && !kart.bWasGrounded)) continue;
            int id = kart.GetInstanceID();
            if (_readyAt.TryGetValue(id, out float ready) && Time.time < ready) continue;
            Vector3 center = kart.kartController.GetKartPos();
            bool hit = false;
            foreach (var target in MechanicsContext.All)
            {
                if (target == kart || !MechanicsContext.Driveable(target) ||
                    (_aiOnly.Value && target.kartController.parentPlayer.ePlayerType != Ant_Player.EPlayerType.E_AI_LOCAL)) continue;
                Vector3 direction = target.kartController.GetKartPos() - center;
                float distance = direction.magnitude;
                if (distance > _radius.Value) continue;
                direction.y = 0;
                direction = direction.sqrMagnitude < .001f ? -MechanicsContext.Forward(kart) : direction.normalized;
                float strength = Mathf.Lerp(1, _falloff.Value, distance / _radius.Value);
                ReadableGame.AddVelocity(target, (direction * _force.Value + Vector3.up * _lift.Value) * strength);
                hit = true;
            }
            if (hit) _readyAt[id] = Time.time + _cooldown.Value;
        }
    }
    protected override void ClearState() => _readyAt.Clear();
}
