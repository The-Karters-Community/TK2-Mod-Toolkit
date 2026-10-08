using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

internal static partial class LegacyMK
{
    private static ConfigEntry<bool> KartParameters = null!, BoostParameters = null!;
    private static readonly List<Parameter<PixelKartPhysics>> KartValues = new();
    private static readonly List<Parameter<Ant_BoostManager>> BoostValues = new();
    private static Parameter<PixelKartPhysics> _speedParameter = null!, _jumpParameter = null!;
    internal static bool OverridesSpeed => KartParameters != null && KartParameters.Value && _speedParameter.Enabled;
    internal static bool OverridesJump => KartParameters != null && KartParameters.Value && _jumpParameter.Enabled;
    // Called BEFORE advanced baselines are captured. The simple tuning module can
    // restore/release its own changed fields, avoiding multiplied baseline capture.
    internal static Action<PixelKartPhysics, bool, bool>? BeforeAdvancedTuning;

    // Every individual override starts OFF. Values are the documented legacy presets,
    // not assumed defaults of the current game. Untouched fields preserve the game.
    private sealed class Parameter<T> where T : Component
    {
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<float> _value;
        private readonly Func<T, float> _read;
        private readonly Action<T, float> _write;
        private readonly Dictionary<int, (T Target, float Value)> _originals = new();
        internal bool Enabled => _enabled.Value;
        internal Parameter(string section, string key, float preset, Func<T, float> read, Action<T, float> write)
        {
            _read = read; _write = write;
            _enabled = _plugin.Config.Bind(section, "Override" + key, false, "Override only " + key + ". Turn off to restore the captured game value.");
            float minimum = preset < 0 ? -1000 : 0;
            if (key == "Mass" || key == "ReachMaxSteerInXSeconds" || key == "SteerMinMaxTimePow" ||
                key == "MaxTimeForBestBoostXSeconds" || key == "ReservesValueForMaxExtraSpeed" || key == "MaxTimeInAirForMaxBoost") minimum = 0.01f;
            _value = Number(section, key, preset, minimum, 1000, "Legacy preset for " + key + "; applied only when its Override option is enabled.");
        }
        internal void Apply(T target, bool active)
        {
            int id = target.GetInstanceID();
            if (!active || !_enabled.Value)
            {
                if (_originals.Remove(id, out var old) && old.Target != null) _write(old.Target, old.Value);
                return;
            }
            if (!_originals.ContainsKey(id)) _originals[id] = (target, _read(target));
            _write(target, _value.Value);
        }
        internal void Restore()
        {
            foreach (var old in _originals.Values) if (old.Target != null) _write(old.Target, old.Value);
            _originals.Clear();
        }
    }

    private static void BindParameters()
    {
        KartParameters = Toggle("KartParameters", "Override selected kart physics fields. Each individual field has its own override switch.");
        BoostParameters = Toggle("BoostParameters", "Override selected boost physics fields. Each individual field has its own override switch.");
        KartValues.Add(new("KartParameters", "Mass", 15f, kart => kart.fMass, (kart, value) => kart.fMass = value));
        KartValues.Add(_jumpParameter = new("KartParameters", "JumpStrength", 21f, kart => kart.fJumpStrength, (kart, value) => kart.fJumpStrength = value));
        KartValues.Add(_speedParameter = new("KartParameters", "MaxAccelForward", 81f, kart => kart.max_speed_accel_forward, (kart, value) => kart.max_speed_accel_forward = value));
        KartValues.Add(new("KartParameters", "MaxAccelReverse", 25f, kart => kart.max_speed_accel_backward, (kart, value) => kart.max_speed_accel_backward = value));
        KartValues.Add(new("KartParameters", "MaxSpeedAdditionFromSlopes", 30f, kart => kart.add_max_speed_from_slope, (kart, value) => kart.add_max_speed_from_slope = value));
        KartValues.Add(new("KartParameters", "MaxSpeedToSlopeNoInputAccel", 20f, kart => kart.max_speed_to_slope_no_input_accel, (kart, value) => kart.max_speed_to_slope_no_input_accel = value));
        KartValues.Add(new("KartParameters", "MaxSpeedFromSlopeNoInputAccel", 45f, kart => kart.max_speed_from_slope_no_input_accel, (kart, value) => kart.max_speed_from_slope_no_input_accel = value));
        KartValues.Add(new("KartParameters", "MaxVerticalVelocity", 60f, kart => kart.fKartMaximum_Y_Velocity, (kart, value) => kart.fKartMaximum_Y_Velocity = value));
        KartValues.Add(new("KartParameters", "OnGroundAccelerationFactor", 91f, kart => kart.acceleration_factor, (kart, value) => kart.acceleration_factor = value));
        KartValues.Add(new("KartParameters", "InAirAccelerationFactor", 100f, kart => kart.inAirAcceleration_factor, (kart, value) => kart.inAirAcceleration_factor = value));
        KartValues.Add(new("KartParameters", "AccelerationBackPercent", 0.7f, kart => kart.acceleration_back_percent, (kart, value) => kart.acceleration_back_percent = value));
        KartValues.Add(new("KartParameters", "FromSlopeAccelerationForceFactor", 80f, kart => kart.fromSlopeAccelForceFactor, (kart, value) => kart.fromSlopeAccelForceFactor = value));
        KartValues.Add(new("KartParameters", "ToSlopeBackForceFactor", 80f, kart => kart.toSlopeBackForceFactor, (kart, value) => kart.toSlopeBackForceFactor = value));
        KartValues.Add(new("KartParameters", "GroundFrictionFactor", 0.008f, kart => kart.ground_friction_factor, (kart, value) => kart.ground_friction_factor = value));
        KartValues.Add(new("KartParameters", "MinimumDragVelocity", 15f, kart => kart.fMinimumDragVelicty, (kart, value) => kart.fMinimumDragVelicty = value));
        KartValues.Add(new("KartParameters", "DrivingOnGroundSteerFactor", 90f, kart => kart.driving_steer_factor, (kart, value) => kart.driving_steer_factor = value));
        KartValues.Add(new("KartParameters", "DrivingInAirSteerFactor", 90f, kart => kart.driving_InAir_steer_factor, (kart, value) => kart.driving_InAir_steer_factor = value));
        KartValues.Add(new("KartParameters", "DriftingNeutralSteerFactor", -90f, kart => kart.driftinNeutral_steer_factor, (kart, value) => kart.driftinNeutral_steer_factor = value));
        KartValues.Add(new("KartParameters", "DriftingMaxSteerAddFactor", -90f, kart => kart.driftingMax_steer_Addfactor, (kart, value) => kart.driftingMax_steer_Addfactor = value));
        KartValues.Add(new("KartParameters", "DriftingMinCounterSteerFactor", -90f, kart => kart.driftingMinCounter_steer_factor, (kart, value) => kart.driftingMinCounter_steer_factor = value));
        KartValues.Add(new("KartParameters", "DrivingSteerSideVelocityMin", 2.2f, kart => kart.driving_steer_SideVelocity_Min, (kart, value) => kart.driving_steer_SideVelocity_Min = value));
        KartValues.Add(new("KartParameters", "DrivingSteerSideVelocityMax", 2.2f, kart => kart.driving_steer_SideVelocity_Max, (kart, value) => kart.driving_steer_SideVelocity_Max = value));
        KartValues.Add(new("KartParameters", "ReachMaxSteerInXSeconds", 1f, kart => kart.achiveMaxSteerInTime, (kart, value) => kart.achiveMaxSteerInTime = value));
        KartValues.Add(new("KartParameters", "SteerMinMaxTimePow", 1.25f, kart => kart.fSteerMinMaxTimePow, (kart, value) => kart.fSteerMinMaxTimePow = value));
        KartValues.Add(new("KartParameters", "DrivingSteerSideVelocityInDriftOrAir", 3.3f, kart => kart.driving_steer_SideVelocity_duringDriftOrInAir, (kart, value) => kart.driving_steer_SideVelocity_duringDriftOrInAir = value));
        KartValues.Add(new("KartParameters", "DriftingNeutralSideVelocity", 3.3f, kart => kart.driftinNeutral_SideVelocity, (kart, value) => kart.driftinNeutral_SideVelocity = value));
        KartValues.Add(new("KartParameters", "DriftingMaxSteerSideVelocity", 3.7f, kart => kart.driftingMax_steer_SideVelocity, (kart, value) => kart.driftingMax_steer_SideVelocity = value));
        KartValues.Add(new("KartParameters", "DriftingMinCounterSideVelocity", 4.3f, kart => kart.driftingMinCounter_SideVelocity, (kart, value) => kart.driftingMinCounter_SideVelocity = value));
        KartValues.Add(new("KartParameters", "InAirMinToMaxSideVelocityInXSeconds", 0.8f, kart => kart.fInAirMinToMaxSideVelocityInTime, (kart, value) => kart.fInAirMinToMaxSideVelocityInTime = value));
        KartValues.Add(new("KartParameters", "AirBreakingSteerVelocity", 5.5f, kart => kart.fAirBreaking_SteerVelocity, (kart, value) => kart.fAirBreaking_SteerVelocity = value));
        KartValues.Add(new("KartParameters", "BreakingDriftingSteerVelocity", 6f, kart => kart.fBreaking_DriftingSteerVelocity, (kart, value) => kart.fBreaking_DriftingSteerVelocity = value));
        KartValues.Add(new("KartParameters", "BreakingInputZeroVelocityLerpSpeed", 3f, kart => kart.fBreakingInputZeroVelocityLerpSpeed, (kart, value) => kart.fBreakingInputZeroVelocityLerpSpeed = value));
        KartValues.Add(new("KartParameters", "MinSpeedForDrifting", 15f, kart => kart.fMinSpeedForDrifting, (kart, value) => kart.fMinSpeedForDrifting = value));
        KartValues.Add(new("KartParameters", "InstantBoostAddFullInSeconds", 0.25f, kart => kart.fInstantBoostAddFullInSeconds, (kart, value) => kart.fInstantBoostAddFullInSeconds = value));
        KartValues.Add(new("KartParameters", "InstantBoostAddFullInSecondsRemoveMultiplier", 1f, kart => kart.fInstantBoostAddFullInSecondsRemoveMultiplier, (kart, value) => kart.fInstantBoostAddFullInSecondsRemoveMultiplier = value));
        BoostValues.Add(new("BoostParameters", "MaxTimeForBestBoostXSeconds", 1f, kart => kart.fMaximumTimeForBestBoost, (kart, value) => kart.fMaximumTimeForBestBoost = value));
        BoostValues.Add(new("BoostParameters", "MinTimeForBoostXSeconds", 0.5f, kart => kart.fMinimumTimeForBoost, (kart, value) => kart.fMinimumTimeForBoost = value));
        BoostValues.Add(new("BoostParameters", "ReservesValueForMaxExtraSpeed", 5f, kart => kart.fReservesVal_ForMaxExtraSpeed_NotClamped, (kart, value) => kart.fReservesVal_ForMaxExtraSpeed_NotClamped = value));
        BoostValues.Add(new("BoostParameters", "MaxSpeedIncreaseForBoostPad", 20f, kart => kart.fMaxSpeedIncreaseForBoostPad, (kart, value) => kart.fMaxSpeedIncreaseForBoostPad = value));
        BoostValues.Add(new("BoostParameters", "BoostLengthFor1", 1.45f, kart => kart.fBoostLengthFor1, (kart, value) => kart.fBoostLengthFor1 = value));
        BoostValues.Add(new("BoostParameters", "BoostLengthFor2", 1.45f, kart => kart.fBoostLengthFor2, (kart, value) => kart.fBoostLengthFor2 = value));
        BoostValues.Add(new("BoostParameters", "BoostLengthFor3", 1.45f, kart => kart.fBoostLengthFor3, (kart, value) => kart.fBoostLengthFor3 = value));
        BoostValues.Add(new("BoostParameters", "BoostInstantVelocityFor1", 15f, kart => kart.fBoostInstantVelFor1, (kart, value) => kart.fBoostInstantVelFor1 = value));
        BoostValues.Add(new("BoostParameters", "BoostInstantVelocityFor2", 25f, kart => kart.fBoostInstantVelFor2, (kart, value) => kart.fBoostInstantVelFor2 = value));
        BoostValues.Add(new("BoostParameters", "BoostInstantVelocityFor3", 35f, kart => kart.fBoostInstantVelFor3, (kart, value) => kart.fBoostInstantVelFor3 = value));
        BoostValues.Add(new("BoostParameters", "TripleBoostSpeedReservesLoweringSpeed", 0.1f, kart => kart.fTripleBoostSpeedReserves_LoweringSpeed, (kart, value) => kart.fTripleBoostSpeedReserves_LoweringSpeed = value));
        BoostValues.Add(new("BoostParameters", "MaxSpeedIncreaseForTripleBoostReserves", 11f, kart => kart.fMaxSpeedIncreaseForTripleBoostReserves, (kart, value) => kart.fMaxSpeedIncreaseForTripleBoostReserves = value));
        BoostValues.Add(new("BoostParameters", "TripleBoostSpeedIncrease1", 0.4f, kart => kart.fTripleBoostSpeedIncrease_AddBoost1, (kart, value) => kart.fTripleBoostSpeedIncrease_AddBoost1 = value));
        BoostValues.Add(new("BoostParameters", "TripleBoostSpeedIncrease2", 0.4f, kart => kart.fTripleBoostSpeedIncrease_AddBoost2, (kart, value) => kart.fTripleBoostSpeedIncrease_AddBoost2 = value));
        BoostValues.Add(new("BoostParameters", "TripleBoostSpeedIncrease3", 0.4f, kart => kart.fTripleBoostSpeedIncrease_AddBoost3, (kart, value) => kart.fTripleBoostSpeedIncrease_AddBoost3 = value));
        BoostValues.Add(new("BoostParameters", "DefaultBoostPadInstantVelocity", 50f, kart => kart.fDefaultBoostPadInstantVelAfterEntering, (kart, value) => kart.fDefaultBoostPadInstantVelAfterEntering = value));
        BoostValues.Add(new("BoostParameters", "DefaultBoostPadLengthXSeconds", 2f, kart => kart.fDefaultBoostPadLengthTime, (kart, value) => kart.fDefaultBoostPadLengthTime = value));
        BoostValues.Add(new("BoostParameters", "BoostPadAccelerationMultiplier", 1.25f, kart => kart.fAccelerationMultiplierAfterEnteringBoostpad, (kart, value) => kart.fAccelerationMultiplierAfterEnteringBoostpad = value));
        BoostValues.Add(new("BoostParameters", "BoostPadForcedMinimumSpeed", 100f, kart => kart.fMinimumSpeedForcedAfterEnteringBoostpad, (kart, value) => kart.fMinimumSpeedForcedAfterEnteringBoostpad = value));
        BoostValues.Add(new("BoostParameters", "BoostInstantVelocityOnLanding", 1.2f, kart => kart.fBoostInstantVelForLand, (kart, value) => kart.fBoostInstantVelForLand = value));
        BoostValues.Add(new("BoostParameters", "MaxTimeInAirForMaxBoost", 4f, kart => kart.fMaxTimeInAirForMaxBoost, (kart, value) => kart.fMaxTimeInAirForMaxBoost = value));
        BoostValues.Add(new("BoostParameters", "ContinuousBoostMaxSpeedAdd", 5f, kart => kart.fContinuousBoostMaxSpeedAdd, (kart, value) => kart.fContinuousBoostMaxSpeedAdd = value));
    }

    private static void InstallParameters()
    {
        Capability("all kart parameters", () => Hook(typeof(PixelKartPhysics), "FixedUpdate", Type.EmptyTypes, nameof(KartParametersPrefix), true));
        Capability("all boost parameters", () => Hook(typeof(Ant_BoostManager), "FixedUpdate", Type.EmptyTypes, nameof(BoostParametersPrefix), true));
    }
    private static void KartParametersPrefix(PixelKartPhysics __instance)
    {
        try
        {
            bool active = KartParameters.Value && Allowed(__instance.kartController?.parentPlayer);
            if (active) BeforeAdvancedTuning?.Invoke(__instance, OverridesSpeed, OverridesJump);
            foreach (var parameter in KartValues) parameter.Apply(__instance, active);
        }
        catch (Exception ex) { Fault(KartParameters, ex); }
    }
    private static void BoostParametersPrefix(Ant_BoostManager __instance)
    {
        try
        {
            bool active = BoostParameters.Value && Allowed(__instance.kartController?.parentPlayer);
            foreach (var parameter in BoostValues) parameter.Apply(__instance, active);
        }
        catch (Exception ex) { Fault(BoostParameters, ex); }
    }
    private static void RestoreParameters()
    {
        foreach (var parameter in KartValues) parameter.Restore();
        foreach (var parameter in BoostValues) parameter.Restore();
    }
}
