// Declarations only for compiling the real parameter source against stub targets.
public sealed class PixelKartPhysics : UnityEngine.Component {
    public float acceleration_back_percent;
    public float acceleration_factor;
    public float achiveMaxSteerInTime;
    public float add_max_speed_from_slope;
    public float driftinNeutral_SideVelocity;
    public float driftinNeutral_steer_factor;
    public float driftingMax_steer_Addfactor;
    public float driftingMax_steer_SideVelocity;
    public float driftingMinCounter_SideVelocity;
    public float driftingMinCounter_steer_factor;
    public float driving_InAir_steer_factor;
    public float driving_steer_SideVelocity_Max;
    public float driving_steer_SideVelocity_Min;
    public float driving_steer_SideVelocity_duringDriftOrInAir;
    public float driving_steer_factor;
    public float fAirBreaking_SteerVelocity;
    public float fBreakingInputZeroVelocityLerpSpeed;
    public float fBreaking_DriftingSteerVelocity;
    public float fInAirMinToMaxSideVelocityInTime;
    public float fInstantBoostAddFullInSeconds;
    public float fInstantBoostAddFullInSecondsRemoveMultiplier;
    public float fJumpStrength;
    public float fKartMaximum_Y_Velocity;
    public float fMass;
    public float fMinSpeedForDrifting;
    public float fMinimumDragVelicty;
    public float fSteerMinMaxTimePow;
    public float fromSlopeAccelForceFactor;
    public float ground_friction_factor;
    public float inAirAcceleration_factor;
    public float max_speed_accel_backward;
    public float max_speed_accel_forward;
    public float max_speed_from_slope_no_input_accel;
    public float max_speed_to_slope_no_input_accel;
    public float toSlopeBackForceFactor;
    public PixelEasyCharMoveKartController? kartController;
}

public sealed class Ant_BoostManager : UnityEngine.Component {
    public float fAccelerationMultiplierAfterEnteringBoostpad;
    public float fBoostInstantVelFor1;
    public float fBoostInstantVelFor2;
    public float fBoostInstantVelFor3;
    public float fBoostInstantVelForLand;
    public float fBoostLengthFor1;
    public float fBoostLengthFor2;
    public float fBoostLengthFor3;
    public float fContinuousBoostMaxSpeedAdd;
    public float fDefaultBoostPadInstantVelAfterEntering;
    public float fDefaultBoostPadLengthTime;
    public float fMaxSpeedIncreaseForBoostPad;
    public float fMaxSpeedIncreaseForTripleBoostReserves;
    public float fMaxTimeInAirForMaxBoost;
    public float fMaximumTimeForBestBoost;
    public float fMinimumSpeedForcedAfterEnteringBoostpad;
    public float fMinimumTimeForBoost;
    public float fReservesVal_ForMaxExtraSpeed_NotClamped;
    public float fTripleBoostSpeedIncrease_AddBoost1;
    public float fTripleBoostSpeedIncrease_AddBoost2;
    public float fTripleBoostSpeedIncrease_AddBoost3;
    public float fTripleBoostSpeedReserves_LoweringSpeed;
    public PixelEasyCharMoveKartController? kartController;
}
