using System.Collections.Generic;
using BepInEx.Configuration;
using KinematicCharacterController;
using UnityEngine;

namespace TK2.Customization;

// Rewind only the local motor. Race checkpoints, clock, items and AI stay current.
public sealed class EchoRewind : LocalKartMechanic
{
    public override string Name => "EchoRewind";
    private ConfigEntry<float> _seconds = null!, _history = null!, _cooldown = null!, _teleportDistance = null!;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _groundOnly = null!, _all = null!;
    private sealed class Sample { internal float Time; internal KinematicCharacterMotorState Motor = null!; }
    private sealed class State
    {
        internal readonly Queue<Sample> Samples = new();
        internal float NextSample, ReadyAt;
        internal Vector3 Position;
        internal bool HasPosition;
    }
    private readonly Dictionary<int, State> _states = new();

    public override void Configure(ConfigFile config)
    {
        _seconds = config.Bind("Recipe." + Name, "RewindSeconds", 2f, new ConfigDescription("How far back to restore the local kart motor; limited by the retained history.", new AcceptableValueRange<float>(.5f, 8)));
        _history = config.Bind("Recipe." + Name, "HistorySeconds", 6f, new ConfigDescription("Maximum history duration. Sampling is 20 Hz with a hard 201-state cap.", new AcceptableValueRange<float>(2, 10)));
        _cooldown = config.Bind("Recipe." + Name, "CooldownSeconds", 8f, new ConfigDescription("Time between rewinds; history starts fresh after each rewind.", new AcceptableValueRange<float>(1, 60)));
        _teleportDistance = config.Bind("Recipe." + Name, "TeleportResetDistance", 25f, new ConfigDescription("Clear history after a sudden position jump to avoid rewinding through portals or respawns.", new AcceptableValueRange<float>(5, 100)));
        _groundOnly = config.Bind("Recipe." + Name, "GroundedSnapshotsOnly", true, "Record grounded positions only for safer recovery; disabling also permits airborne snapshots.");
        _key = config.Bind("Recipe." + Name, "RewindKey", KeyCode.R, "Press to restore a recent motor position and velocity. Race progress is deliberately not rewound.");
        _all = config.Bind("Recipe." + Name, "AllLocalPlayers", false, "Rewind all local karts with the shared keyboard shortcut.");
    }

    protected override void OnTick(float delta)
    {
        MechanicsContext.Prune(_states);
        foreach (var kart in MechanicsContext.Local)
        {
            if (!MechanicsContext.Selected(kart, _all.Value)) continue;
            int id = kart.GetInstanceID();
            if (!_states.TryGetValue(id, out var state)) _states[id] = state = new State();
            var controller = kart.kartController;
            Vector3 position = controller.GetKartPos();
            if (state.HasPosition && (position - state.Position).sqrMagnitude > _teleportDistance.Value * _teleportDistance.Value)
                state.Samples.Clear();
            state.Position = position; state.HasPosition = true;
            while (state.Samples.Count > 0 && state.Samples.Peek().Time < Time.time - _history.Value) state.Samples.Dequeue();
            if (Input.GetKeyDown(_key.Value) && Time.time >= state.ReadyAt)
            {
                float target = Time.time - Mathf.Min(_seconds.Value, _history.Value);
                Sample? chosen = null;
                foreach (var sample in state.Samples) if (sample.Time <= target) chosen = sample; else break;
                if (chosen != null)
                {
                    controller.Motor.ApplyState(chosen.Motor, true);
                    kart.SetSteeringPhysicsRotation(chosen.Motor.Rotation, false);
                    state.Position = chosen.Motor.Position;
                    state.Samples.Clear(); state.ReadyAt = Time.time + _cooldown.Value;
                    continue;
                }
            }
            if (Time.time >= state.NextSample && (!_groundOnly.Value || kart.bWasGrounded))
            {
                state.NextSample = Time.time + .05f;
                state.Samples.Enqueue(new Sample { Time = Time.time, Motor = controller.Motor.GetState() });
                while (state.Samples.Count > 201) state.Samples.Dequeue();
            }
        }
    }
    protected override void ClearState() => _states.Clear();
}
