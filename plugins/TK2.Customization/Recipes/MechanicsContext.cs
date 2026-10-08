using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

// Managed-only helper. Recipes add impulses through the existing reconstructed
// adapter; no new native hooks, base-stat replacements or worker-thread Unity calls.
public abstract class LocalKartMechanic : IModRecipe
{
    public abstract string Name { get; }
    public bool ChangesGameplay => true;
    public abstract void Configure(ConfigFile config);
    private int _epoch = -1;

    public void Tick()
    {
        if (!MechanicsContext.Ready()) { Restore(); return; }
        if (_epoch != MechanicsContext.Epoch) { Restore(); _epoch = MechanicsContext.Epoch; }
        OnTick(Mathf.Clamp(Time.deltaTime, 0, .05f));
    }
    protected abstract void OnTick(float delta);
    protected abstract void ClearState();
    public void Restore() { ClearState(); _epoch = -1; }
}

internal static class MechanicsContext
{
    internal static int Epoch { get; private set; }
    private static int _scene = int.MinValue, _frame = -1;
    private static float _scanAt, _lastTime;
    private static readonly List<PixelKartPhysics> Racers = new();
    private static readonly List<PixelKartPhysics> Locals = new();
    internal static IReadOnlyList<PixelKartPhysics> All => Racers;
    internal static IReadOnlyList<PixelKartPhysics> Local => Locals;

    internal static bool Ready()
    {
        if (!Plugin.OfflineLabAllowed || Time.timeScale <= 0 ||
            Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
            return false;
        if (_frame == Time.frameCount) return true;
        _frame = Time.frameCount;
        int scene = SceneManager.GetActiveScene().handle;
        if (_scene != scene || Time.time < _lastTime)
        {
            _scene = scene; Epoch++; Racers.Clear(); Locals.Clear(); _scanAt = 0;
        }
        _lastTime = Time.time;
        if (Time.unscaledTime >= _scanAt)
        {
            _scanAt = Time.unscaledTime + 1;
            Racers.Clear(); Locals.Clear();
            foreach (var kart in UnityEngine.Object.FindObjectsByType<PixelKartPhysics>(FindObjectsSortMode.None))
            {
                if (Racers.Count >= 32) break; // Bound caches even on a malformed/custom scene.
                var player = kart.kartController?.parentPlayer;
                if (player == null || (player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL &&
                    player.ePlayerType != Ant_Player.EPlayerType.E_AI_LOCAL)) continue;
                Racers.Add(kart);
                if (player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL) Locals.Add(kart);
            }
            Locals.Sort((a, b) => ((int)a.kartController.parentPlayer.eAntLocalPlayerNr)
                .CompareTo((int)b.kartController.parentPlayer.eAntLocalPlayerNr));
        }
        return true;
    }

    internal static bool Driveable(PixelKartPhysics kart)
    {
        if (kart == null || !kart.isActiveAndEnabled) return false;
        var controller = kart.kartController;
        if (controller?.parentPlayer?.hpBarController != null && controller.parentPlayer.hpBarController.currentHp <= 0) return false;
        return controller != null && controller.Motor != null && !controller.IsPlayingReplayData() &&
            controller.outsideMapValidator != null && !controller.outsideMapValidator.bIsPlayerRespawning;
    }

    internal static bool Selected(PixelKartPhysics kart, bool allLocals) => Driveable(kart) &&
        kart.kartController.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL &&
        (allLocals || (Locals.Count > 0 && kart.GetInstanceID() == Locals[0].GetInstanceID()));

    internal static Vector3 Forward(PixelKartPhysics kart)
    {
        Vector3 direction = kart.kartController.GetKartRotation() * Vector3.forward;
        direction.y = 0;
        return direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
    }

    internal static void Prune<T>(Dictionary<int, T> states)
    {
        // Karts can disappear and instance IDs can be recycled. Do not retain
        // state for despawned, replay or respawning objects across a race.
        if (states.Count == 0) return;
        var valid = new HashSet<int>();
        foreach (var kart in Locals) if (Driveable(kart)) valid.Add(kart.GetInstanceID());
        foreach (int id in new List<int>(states.Keys)) if (!valid.Contains(id)) states.Remove(id);
    }
}
