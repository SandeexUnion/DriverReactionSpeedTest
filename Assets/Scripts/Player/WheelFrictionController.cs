using UnityEngine;

public class WheelFrictionController : MonoBehaviour
{
    [Header("Wheel References")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Friction Settings")]
    public float defaultExtremumSlip = 0.2f;
    public float defaultExtremumValue = 1f;
    public float driftExtremumSlipMultiplier = 3f;
    public float driftExtremumValueMultiplier = 0.7f;

    private WheelFrictionCurve flSidewaysFriction;
    private WheelFrictionCurve frSidewaysFriction;
    private WheelFrictionCurve rlSidewaysFriction;
    private WheelFrictionCurve rrSidewaysFriction;

    void Start()
    {
        InitializeFrictionCurves();
        ApplyDefaultFriction();
    }

    void InitializeFrictionCurves()
    {
        // Сохраняем оригинальные настройки трения
        flSidewaysFriction = CreateFrictionCurveCopy(frontLeftWheel.sidewaysFriction);
        frSidewaysFriction = CreateFrictionCurveCopy(frontRightWheel.sidewaysFriction);
        rlSidewaysFriction = CreateFrictionCurveCopy(rearLeftWheel.sidewaysFriction);
        rrSidewaysFriction = CreateFrictionCurveCopy(rearRightWheel.sidewaysFriction);
    }

    WheelFrictionCurve CreateFrictionCurveCopy(WheelFrictionCurve original)
    {
        return new WheelFrictionCurve
        {
            extremumSlip = original.extremumSlip,
            extremumValue = original.extremumValue,
            asymptoteSlip = original.asymptoteSlip,
            asymptoteValue = original.asymptoteValue,
            stiffness = original.stiffness
        };
    }

    public void ApplyDefaultFriction()
    {
        frontLeftWheel.sidewaysFriction = flSidewaysFriction;
        frontRightWheel.sidewaysFriction = frSidewaysFriction;
        rearLeftWheel.sidewaysFriction = rlSidewaysFriction;
        rearRightWheel.sidewaysFriction = rrSidewaysFriction;
    }

    public void StartDrifting(float driftIntensity)
    {
        // Настраиваем трение для дрифта только на задних колесах
        WheelFrictionCurve driftRLFriction = CreateFrictionCurveCopy(rlSidewaysFriction);
        WheelFrictionCurve driftRRFriction = CreateFrictionCurveCopy(rrSidewaysFriction);

        driftRLFriction.extremumSlip = defaultExtremumSlip * driftExtremumSlipMultiplier * driftIntensity;
        driftRRFriction.extremumSlip = defaultExtremumSlip * driftExtremumSlipMultiplier * driftIntensity;

        driftRLFriction.extremumValue = defaultExtremumValue * driftExtremumValueMultiplier * driftIntensity;
        driftRRFriction.extremumValue = defaultExtremumValue * driftExtremumValueMultiplier * driftIntensity;

        rearLeftWheel.sidewaysFriction = driftRLFriction;
        rearRightWheel.sidewaysFriction = driftRRFriction;
    }

    public void StopDrifting()
    {
        ApplyDefaultFriction();
    }
}