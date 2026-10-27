using System.Collections.Generic;
using UnityEngine;

public class NPCCarController : MonoBehaviour
{
    #region Wheel Settings
    [Header("Wheel Settings")]
    [Tooltip("Коллайдер переднего левого колеса")]
    public WheelCollider frontLeftWheel;
    [Tooltip("Коллайдер переднего правого колеса")]
    public WheelCollider frontRightWheel;
    [Tooltip("Коллайдер заднего левого колеса")]
    public WheelCollider rearLeftWheel;
    [Tooltip("Коллайдер заднего правого колеса")]
    public WheelCollider rearRightWheel;

    [Header("Wheel Meshes")]
    [Tooltip("Модель переднего левого колеса")]
    public Transform frontLeftWheelMesh;
    [Tooltip("Модель переднего правого колеса")]
    public Transform frontRightWheelMesh;
    [Tooltip("Модель заднего левого колеса")]
    public Transform rearLeftWheelMesh;
    [Tooltip("Модель заднего правого колеса")]
    public Transform rearRightWheelMesh;
    #endregion

    #region Car Physics Settings
    [Header("Car Settings")]
    [Tooltip("Максимальный крутящий момент двигателя (Н·м)")]
    public float maxMotorTorque = 200f;
    [Tooltip("Максимальный угол поворота колес (градусы)")]
    public float maxSteeringAngle = 25f;
    [Tooltip("Максимальная скорость движения вперед (км/ч)")]
    public float maxSpeed = 20f;
    [Tooltip("Максимальная скорость движения назад (км/ч)")]
    public float reverseSpeed = 10f;
    [Tooltip("Множитель крутящего момента на низких скоростях")]
    public float lowSpeedTorqueMultiplier = 2f;
    [Tooltip("Множитель крутящего момента заднего хода")]
    [Range(0.5f, 2f)]
    public float reverseTorqueMultiplier = 1.3f;
    #endregion

    #region Braking System Settings
    [Header("Braking System Settings")]
    [Tooltip("Максимальное тормозное усилие (Н·м)")]
    public float maxBrakeTorque = 2000f;
    [Tooltip("Сила торможения двигателем (Н·м)")]
    public float engineBrakeTorque = 300f;
    [Tooltip("Время полного нарастания тормозного усилия (сек)")]
    public float brakeRampUpTime = 0.5f;
    [Tooltip("Время полного спада тормозного усилия (сек)")]
    public float brakeRampDownTime = 0.3f;
    [Tooltip("Скорость, при которой включается торможение двигателем (км/ч)")]
    public float engineBrakeThreshold = 10f;
    [Tooltip("Коэффициент распределения тормозного усилия на передние колеса")]
    [Range(0.5f, 0.8f)]
    public float frontBrakeBias = 0.7f;
    #endregion

    #region Path Settings
    [Header("Path Settings")]
    [Tooltip("Точки пути для следования")]
    public List<Transform> waypoints;
    [Tooltip("Дистанция до точки, при которой она считается достигнутой (метры)")]
    public float waypointThreshold = 3f;
    [Tooltip("Зацикливать путь?")]
    public bool loopPath = true;
    #endregion

    #region Slope Settings
    [Header("Slope Settings")]
    [Tooltip("Максимальный угол наклона для коррекции скорости")]
    public float MAX_SLOPE_ANGLE = 30f;
    [Tooltip("Множитель скорости на подъеме")]
    public float SLOPE_SPEED_BOOST = 1.5f;
    [Tooltip("Множитель скорости на спуске")]
    public float SLOPE_SPEED_REDUCTION = 0.7f;
    #endregion

    #region Private Variables
    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private bool isReversing = false;
    private float currentSpeed;
    private float currentSteeringAngle;
    private Vector3 currentTargetPosition;
    private float currentBrakeIntensity = 0f;
    private float targetBrakeIntensity = 0f;
    private bool isBraking = false;
    private float lastBrakeTime = 0f;
    private float lastReverseStartTime;
    private bool isStuck = false;
    private float stuckTimer = 0f;
    private float slopeAngle;
    private float slopeSpeedMultiplier = 1f;
    #endregion

    #region Constants
    private const float STUCK_TIME_THRESHOLD = 2f;
    private const float STUCK_SPEED_THRESHOLD = 0.5f;
    private const float SLOPE_CHECK_DISTANCE = 5f;
    #endregion

    #region Initialization
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned to NPCCarController!");
            //enabled = false;
            //return;
        }

        ConfigureWheelColliders();
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }

    void ConfigureWheelColliders()
    {
        WheelCollider[] wheels = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        foreach (var wheel in wheels)
        {
            wheel.ConfigureVehicleSubsteps(5, 12, 15);
            wheel.wheelDampingRate = 0.5f;
        }
    }
    #endregion

    #region Main Update Loop
    void FixedUpdate()
    {
        if (waypoints.Count == 0 || waypoints == null) return;

        CheckSlope();
        currentSpeed = rb.linearVelocity.magnitude;

        if (isReversing && rb.angularVelocity.magnitude > 2f)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * 2f;
        }

        if (currentSpeed > 2f && isStuck)
        {
            isStuck = false;
            stuckTimer = 0f;
        }
        try
        {
            currentTargetPosition = waypoints[currentWaypointIndex].position;
        }
        catch (System.Exception)
        {
            Debug.LogError("Нет точек");
            
        }
        
        currentTargetPosition.y = transform.position.y;

        Vector3 toTarget = currentTargetPosition - transform.position;
        float distanceToTarget = toTarget.magnitude;
        Vector3 directionToTarget = toTarget.normalized;

        if (HasReachedWaypoint(distanceToTarget, directionToTarget))
        {
            SelectNextWaypoint();
            return;
        }

        float steeringAngle = CalculateSteering(directionToTarget, distanceToTarget);
        float motorTorque = CalculateMotorTorque(distanceToTarget, directionToTarget);
        ConfigureWheelsForReverse();
        MoveCar(steeringAngle, motorTorque);
        UpdateWheelVisuals();
        ApplyAntiRoll();
        StabilizeCar();
    }
    #endregion

    #region Waypoint Logic
    void CheckSlope()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, SLOPE_CHECK_DISTANCE))
        {
            slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            Vector3 cross = Vector3.Cross(hit.normal, Vector3.up);
            float slopeDirection = Vector3.Dot(cross, transform.right);

            float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));

            if (slopeAngle > 5f)
            {
                if (slopeDirection < -0.05f)
                {
                    float angleFactor = Mathf.Clamp01(slopeAngle / MAX_SLOPE_ANGLE);
                    slopeSpeedMultiplier = Mathf.Lerp(1f, SLOPE_SPEED_BOOST, angleFactor * speedFactor);
                }
                else if (slopeDirection > 0.05f)
                {
                    float angleFactor = Mathf.Clamp01(slopeAngle / MAX_SLOPE_ANGLE);
                    slopeSpeedMultiplier = Mathf.Lerp(SLOPE_SPEED_REDUCTION, 1f, 1 - (angleFactor * speedFactor));
                }
                else
                {
                    slopeSpeedMultiplier = 1f;
                }
            }
            else
            {
                slopeSpeedMultiplier = 1f;
            }

            if (isReversing)
            {
                slopeSpeedMultiplier = Mathf.Clamp(slopeSpeedMultiplier, 0.7f, 1.2f);
            }
        }
        else
        {
            slopeSpeedMultiplier = 1f;
        }
    }

    bool HasReachedWaypoint(float distance, Vector3 direction)
    {
        float dynamicThreshold = waypointThreshold;
        if (Vector3.Dot(transform.forward, direction) > 0.95f)
        {
            dynamicThreshold *= 1.5f;
        }

        bool isCloseEnough = distance < dynamicThreshold;
        bool isFacingWaypoint = currentSpeed < 2f || Vector3.Dot(transform.forward, direction) > 0.5f;

        return isCloseEnough && isFacingWaypoint;
    }

    void SelectNextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Count)
        {
            if (loopPath)
            {
                currentWaypointIndex = 0;
            }
            else
            {
                enabled = false;
                return;
            }
        }

        isReversing = false;
        if(currentWaypointIndex >= waypoints.Count-1)
        {
            return;
        }
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }
    #endregion

    #region Steering Calculation
    float CalculateSteering(Vector3 directionToTarget, float distanceToTarget)
    {
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        float steeringMultiplier = 1f;

        if (isReversing)
        {
            angleToTarget = -angleToTarget * steeringMultiplier;
        }
        else
        {
            angleToTarget *= steeringMultiplier;
        }

        float predictionTime = Mathf.Clamp(distanceToTarget / (currentSpeed + 0.1f), 0.5f, 2f);
        Vector3 predictedPosition = transform.position + rb.linearVelocity * predictionTime;
        Vector3 predictedDirection = (currentTargetPosition - predictedPosition).normalized;
        float predictedAngle = Vector3.SignedAngle(transform.forward, predictedDirection, Vector3.up) * steeringMultiplier;

        if (isReversing) predictedAngle = -predictedAngle;

        float combinedAngle = Mathf.Lerp(angleToTarget, predictedAngle, Mathf.Clamp01(currentSpeed / 5f));

        float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));
        float maxAllowedAngle = maxSteeringAngle * (1 - speedFactor * 0.5f) * steeringMultiplier;

        float targetSteering = Mathf.Clamp(combinedAngle, -maxAllowedAngle, maxAllowedAngle);
        float approachFactor = Mathf.Clamp(distanceToTarget / (waypointThreshold * 2f), 0.2f, 1f);

        return targetSteering * approachFactor;
    }
    #endregion

    #region Motor Logic
    float CalculateMotorTorque(float distanceToTarget, Vector3 directionToTarget)
    {
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        float absAngle = Mathf.Abs(angleToTarget);

        CheckIfStuck(distanceToTarget, angleToTarget);

        if (isStuck)
        {
            return HandleStuckSituation(distanceToTarget, angleToTarget);
        }

        if (!isReversing && ShouldReverse(distanceToTarget, angleToTarget))
        {
            isReversing = true;
            lastReverseStartTime = Time.time;
            return -maxMotorTorque * 0.5f;
        }

        if (isReversing)
        {
            return HandleReversing(distanceToTarget, angleToTarget);
        }

        return HandleForwardMovement(distanceToTarget, absAngle);
    }

    void CheckIfStuck(float distanceToTarget, float angleToTarget)
    {
        bool isNotMoving = currentSpeed < STUCK_SPEED_THRESHOLD;
        bool isBadAngle = Mathf.Abs(angleToTarget) > 70f;
        bool isFarFromTarget = distanceToTarget > waypointThreshold;
        bool isNotReversing = !isReversing;

        if (isNotMoving && isBadAngle && isFarFromTarget && isNotReversing)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_TIME_THRESHOLD)
            {
                isStuck = true;
                stuckTimer = 0f;
                isReversing = true;
            }
        }
        else
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime * 0.5f);
        }
    }

    float HandleStuckSituation(float distanceToTarget, float angleToTarget)
    {
        float cycleTime = Mathf.PingPong(Time.time * 1.5f, 3f);

        if (cycleTime < 2f)
        {
            isReversing = true;
            return -maxMotorTorque * 0.9f;
        }
        else
        {
            isReversing = false;
            if (currentSpeed > 1.5f && Mathf.Abs(angleToTarget) < 50f)
            {
                isStuck = false;
            }
            return maxMotorTorque * 0.6f;
        }
    }

    float HandleReversing(float distanceToTarget, float angleToTarget)
    {
        float reversePower = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(Time.time - lastReverseStartTime));

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        if (Mathf.Abs(localVel.x) > 0.5f)
        {
            float compensation = -localVel.x * 20f * rb.mass * Time.fixedDeltaTime;
            rb.AddRelativeTorque(Vector3.up * compensation, ForceMode.VelocityChange);
        }

        bool shouldStopReversing = Mathf.Abs(angleToTarget) < 90f
                                && Vector3.Dot(transform.forward, (currentTargetPosition - transform.position).normalized) > 0.7f;

        if (shouldStopReversing)
        {
            isReversing = false;
            return 0f;
        }

        float angleFactor = Mathf.Clamp01(Mathf.Abs(angleToTarget) / 90f);
        float slopeEffect = slopeSpeedMultiplier < 1f ? 0.7f : 1f;

        return -maxMotorTorque * reverseTorqueMultiplier * reversePower * (0.7f + angleFactor * 0.3f) * slopeEffect;
    }

    float HandleForwardMovement(float distanceToTarget, float absAngle)
    {
        float effectiveSlopeMultiplier = currentSpeed > 1f ? slopeSpeedMultiplier : 1f;
        float speedReduction = Mathf.Clamp01(distanceToTarget / (waypointThreshold * 3f));
        float targetSpeed = maxSpeed * speedReduction * effectiveSlopeMultiplier;

        if (currentSpeed > targetSpeed / 3.6f)
            return 0f;

        float angleFactor = Mathf.Clamp01(1f - absAngle / 120f);
        float torqueMultiplier = currentSpeed < 3f ? lowSpeedTorqueMultiplier : 1f;
        float accelerationSmoothing = Mathf.Clamp01(currentSpeed / 2f);
        float slopeEffect = Mathf.Lerp(1.5f, 1f, currentSpeed / 5f);

        return maxMotorTorque * angleFactor * torqueMultiplier * speedReduction *
               accelerationSmoothing * Mathf.Pow(effectiveSlopeMultiplier, slopeEffect);
    }

    bool ShouldReverse(float distanceToTarget, float angleToTarget)
    {
        if (currentSpeed < 1.5f && Mathf.Abs(angleToTarget) > 90f)
            return true;

        Vector3 toTarget = (currentTargetPosition - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, toTarget);

        if (dot < -0.3f && distanceToTarget < waypointThreshold * 3f)
            return true;

        if (distanceToTarget < waypointThreshold * 1.5f && dot < 0.4f)
            return true;

        return false;
    }
    #endregion

    #region Braking System
    void UpdateBrakingSystem()
    {
        targetBrakeIntensity = CalculateTargetBrakeIntensity();

        float rampSpeed = isBraking ?
            (1f / Mathf.Lerp(brakeRampUpTime, brakeRampUpTime * 0.5f, currentSpeed / (maxSpeed / 3.6f))) * Time.fixedDeltaTime :
            (1f / Mathf.Lerp(brakeRampDownTime, brakeRampDownTime * 2f, currentBrakeIntensity)) * Time.fixedDeltaTime;

        currentBrakeIntensity = Mathf.MoveTowards(
            currentBrakeIntensity,
            targetBrakeIntensity,
            rampSpeed
        );

        ApplyBrakingForces();
    }

    float CalculateTargetBrakeIntensity()
    {
        Vector3 toTarget = (currentTargetPosition - transform.position);
        float distanceToWaypoint = toTarget.magnitude;
        Vector3 directionToTarget = toTarget.normalized;

        float currentMaxSpeed = isReversing ? reverseSpeed : maxSpeed;
        float overspeedRatio = Mathf.Clamp01(
            (currentSpeed * 3.6f - currentMaxSpeed) / (currentMaxSpeed * 0.2f)
        );

        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
        float turnBrakeRatio = Mathf.Clamp01((angleToTarget - 30f) / 60f);

        float distanceFactor = Mathf.Clamp01(1f - (distanceToWaypoint / (waypointThreshold * 3f)));
        float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));

        float turnBrakeIntensity = turnBrakeRatio * distanceFactor * speedFactor;
        float combinedIntensity = Mathf.Max(overspeedRatio, turnBrakeIntensity);

        isBraking = combinedIntensity > 0.05f;
        if (isBraking) lastBrakeTime = Time.time;

        return Mathf.Clamp01(combinedIntensity);
    }

    void ApplyBrakingForces()
    {
        float brakeTorque = currentBrakeIntensity * maxBrakeTorque;
        float dynamicBias = frontBrakeBias;

        if (Mathf.Abs(currentSteeringAngle) > 5f)
        {
            float turnFactor = Mathf.Clamp01(Mathf.Abs(currentSteeringAngle) / maxSteeringAngle);
            dynamicBias = Mathf.Lerp(frontBrakeBias, 0.55f, turnFactor);

            if (currentSteeringAngle > 0)
            {
                frontLeftWheel.brakeTorque = brakeTorque * dynamicBias * 1.1f;
                frontRightWheel.brakeTorque = brakeTorque * dynamicBias * 0.9f;
            }
            else
            {
                frontLeftWheel.brakeTorque = brakeTorque * dynamicBias * 0.9f;
                frontRightWheel.brakeTorque = brakeTorque * dynamicBias * 1.1f;
            }
        }
        else
        {
            frontLeftWheel.brakeTorque = brakeTorque * dynamicBias;
            frontRightWheel.brakeTorque = brakeTorque * dynamicBias;
        }

        rearLeftWheel.brakeTorque = brakeTorque * (1f - dynamicBias);
        rearRightWheel.brakeTorque = brakeTorque * (1f - dynamicBias);

        if (currentBrakeIntensity > 0.1f && currentSpeed * 3.6f > engineBrakeThreshold && !isReversing)
        {
            float engineBrake = Mathf.Lerp(0, engineBrakeTorque, currentBrakeIntensity * 0.5f);
            rearLeftWheel.brakeTorque += engineBrake;
            rearRightWheel.brakeTorque += engineBrake;
        }
    }

    void ReleaseBrakes()
    {
        frontLeftWheel.brakeTorque = 0f;
        frontRightWheel.brakeTorque = 0f;
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;
        currentBrakeIntensity = 0f;
        targetBrakeIntensity = 0f;
        isBraking = false;
    }
    #endregion

    #region Car Control
    void MoveCar(float steeringAngle, float motorTorque)
    {
        float steerSpeed = isReversing ? 3f : 5f;
        float maxAngle = isReversing ? maxSteeringAngle * 1.3f : maxSteeringAngle;

        currentSteeringAngle = Mathf.Lerp(
            currentSteeringAngle,
            steeringAngle,
            Time.fixedDeltaTime * steerSpeed
        );

        currentSteeringAngle = Mathf.Clamp(currentSteeringAngle, -maxAngle, maxAngle);

        if (Mathf.Abs(motorTorque) > 0.1f || currentSpeed > 0.5f)
        {
            frontLeftWheel.steerAngle = currentSteeringAngle;
            frontRightWheel.steerAngle = currentSteeringAngle;
        }
        else
        {
            frontLeftWheel.steerAngle = Mathf.Lerp(frontLeftWheel.steerAngle, 0f, Time.fixedDeltaTime * 5f);
            frontRightWheel.steerAngle = Mathf.Lerp(frontRightWheel.steerAngle, 0f, Time.fixedDeltaTime * 5f);
        }

        if (isReversing)
        {
            float frontTorque = motorTorque * 0.3f;
            float rearTorque = motorTorque * 0.7f;

            frontLeftWheel.motorTorque = frontTorque;
            frontRightWheel.motorTorque = frontTorque;
            rearLeftWheel.motorTorque = rearTorque;
            rearRightWheel.motorTorque = rearTorque;
        }
        else
        {
            rearLeftWheel.motorTorque = motorTorque;
            rearRightWheel.motorTorque = motorTorque;
            frontLeftWheel.motorTorque = 0f;
            frontRightWheel.motorTorque = 0f;
        }

        if (Mathf.Abs(motorTorque) > 0.1f)
        {
            ReleaseBrakes();
        }
        else
        {
            UpdateBrakingSystem();
        }
    }

    void ConfigureWheelsForReverse()
    {
        WheelFrictionCurve forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.3f,
            extremumValue = 1f,
            asymptoteSlip = 0.5f,
            asymptoteValue = 0.75f,
            stiffness = 1.1f
        };

        foreach (var wheel in new[] { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel })
        {
            wheel.forwardFriction = forwardFriction;
            wheel.sidewaysFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.2f,
                extremumValue = 1f,
                asymptoteSlip = 0.5f,
                asymptoteValue = 0.5f,
                stiffness = isReversing ? 1f : 1.5f
            };
        }
    }

    void StabilizeCar()
    {
        if (!isStuck && Vector3.Angle(transform.up, Vector3.up) > 30f)
        {
            Vector3 torque = Vector3.Cross(transform.up, Vector3.up) * rb.mass * 0.05f;
            rb.AddTorque(torque, ForceMode.Impulse);
        }

        float angularDamping = isReversing ? 2f : 0.5f;
        rb.angularDamping = angularDamping + currentSpeed;
        rb.linearDamping = Mathf.Lerp(0.1f, 0.01f, currentSpeed / (maxSpeed / 3.6f));
    }

    void ApplyAntiRoll()
    {
        WheelHit hit;
        bool groundedFL = frontLeftWheel.GetGroundHit(out hit);
        bool groundedFR = frontRightWheel.GetGroundHit(out hit);

        if (groundedFL != groundedFR)
        {
            rb.AddForceAtPosition(
                Vector3.down * rb.mass * 0.1f,
                transform.position + transform.up * 0.5f,
                ForceMode.Impulse
            );
        }
    }
    #endregion

    #region Wheel Visualization
    void UpdateWheelVisuals()
    {
        UpdateWheelVisual(frontLeftWheel, frontLeftWheelMesh);
        UpdateWheelVisual(frontRightWheel, frontRightWheelMesh);
        UpdateWheelVisual(rearLeftWheel, rearLeftWheelMesh);
        UpdateWheelVisual(rearRightWheel, rearRightWheelMesh);
    }

    void UpdateWheelVisual(WheelCollider collider, Transform visualWheel)
    {
        if (visualWheel == null) return;

        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);

        visualWheel.position = Vector3.Lerp(
            visualWheel.position,
            position,
            Time.fixedDeltaTime * 15f
        );

        visualWheel.rotation = Quaternion.Lerp(
            visualWheel.rotation,
            rotation,
            Time.fixedDeltaTime * 15f
        );

        if (collider == frontLeftWheel || collider == frontRightWheel)
        {
            float wheelRPM = collider.rpm;
            float rotationAngle = wheelRPM * 6f * Time.fixedDeltaTime;
            visualWheel.Rotate(Vector3.right, rotationAngle, Space.Self);
        }
    }
    #endregion
}