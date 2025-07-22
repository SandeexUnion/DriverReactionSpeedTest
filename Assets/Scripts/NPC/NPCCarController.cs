using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Контроллер для управления NPC-машиной, следующей по заданным точкам пути
/// </summary>
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
    [Tooltip("Дистанция предвидения для плавного поворота (метры)")]
    public float lookAheadDistance = 4f;
    #endregion

    #region AI Settings
    [Header("AI Settings")]
    [Tooltip("Дистанция обнаружения препятствий (метры)")]
    public float safeDistance = 5f;
    [Tooltip("Сила уклонения от препятствий")]
    public float avoidanceStrength = 1f;
    [Tooltip("Слои для обнаружения препятствий")]
    public LayerMask obstacleLayers;
    #endregion

    #region Private Variables
    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private bool isReversing = false;
    private float avoidanceTimer = 0f;
    private float currentSpeed;
    private float currentSteeringAngle;
    private Vector3 currentTargetPosition;
    private float currentBrakeIntensity = 0f;
    private float targetBrakeIntensity = 0f;
    private bool isBraking = false;
    private float lastBrakeTime = 0f;
    #endregion

    #region Initialization
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned to NPCCarController!");
            enabled = false;
            return;
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
        if (waypoints.Count == 0) return;

        currentSpeed = rb.linearVelocity.magnitude;
        currentTargetPosition = waypoints[currentWaypointIndex].position;
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

        MoveCar(steeringAngle, motorTorque);
        UpdateWheelVisuals();
        ApplyAntiRoll();
        StabilizeCar();
    }
    #endregion

    #region Waypoint Logic
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
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }
    #endregion

    #region Steering Calculation
    float CalculateSteering(Vector3 directionToTarget, float distanceToTarget)
    {
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);

        if (isReversing) angleToTarget = -angleToTarget;

        float predictionTime = Mathf.Clamp(distanceToTarget / (currentSpeed + 0.1f), 0.5f, 2f);
        Vector3 predictedPosition = transform.position + rb.linearVelocity * predictionTime;
        Vector3 predictedDirection = (currentTargetPosition - predictedPosition).normalized;
        float predictedAngle = Vector3.SignedAngle(transform.forward, predictedDirection, Vector3.up);

        float combinedAngle = Mathf.Lerp(angleToTarget, predictedAngle, Mathf.Clamp01(currentSpeed / 5f));

        float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));
        float maxAllowedAngle = maxSteeringAngle * (1 - speedFactor * 0.8f);

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

        if (!isReversing && absAngle > 100f && currentSpeed < 2f)
        {
            isReversing = true;
            return -maxMotorTorque * 0.5f;
        }

        if (isReversing)
        {
            if (absAngle < 60f || distanceToTarget < waypointThreshold * 1.5f)
            {
                isReversing = false;
                return 0f;
            }

            if (currentSpeed > reverseSpeed / 3.6f)
                return 0f;

            return -maxMotorTorque * 0.6f;
        }

        float speedReduction = Mathf.Clamp01(distanceToTarget / (waypointThreshold * 3f));
        float targetSpeed = maxSpeed * speedReduction;

        if (currentSpeed > targetSpeed / 3.6f)
            return 0f;

        float angleFactor = Mathf.Clamp01(1f - absAngle / 120f);
        float torqueMultiplier = currentSpeed < 3f ? lowSpeedTorqueMultiplier : 1f;
        float accelerationSmoothing = Mathf.Clamp01(currentSpeed / 2f);

        return maxMotorTorque * angleFactor * torqueMultiplier * speedReduction * accelerationSmoothing;
    }
    #endregion

    #region New Braking System
    void UpdateBrakingSystem()
    {
        targetBrakeIntensity = CalculateTargetBrakeIntensity();

        float rampSpeed = isBraking ?
            (1f / brakeRampUpTime) * Time.fixedDeltaTime :
            (1f / brakeRampDownTime) * Time.fixedDeltaTime;

        currentBrakeIntensity = Mathf.MoveTowards(
            currentBrakeIntensity,
            targetBrakeIntensity,
            rampSpeed
        );

        ApplyBrakingForces();
    }

    float CalculateTargetBrakeIntensity()
    {
        // Получаем текущее направление и расстояние до цели
        Vector3 toTarget = (currentTargetPosition - transform.position);
        float distanceToWaypoint = toTarget.magnitude;
        Vector3 directionToTarget = toTarget.normalized;

        // 1. Торможение при превышении скорости
        float currentMaxSpeed = isReversing ? reverseSpeed : maxSpeed;
        float overspeedRatio = Mathf.Clamp01(
            (currentSpeed * 3.6f - currentMaxSpeed) / (currentMaxSpeed * 0.2f)
        );

        // 2. Торможение перед поворотом
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
        float turnBrakeRatio = Mathf.Clamp01((angleToTarget - 30f) / 60f);

        // Учитываем расстояние до точки и скорость
        float distanceFactor = Mathf.Clamp01(
            1f - (distanceToWaypoint / (waypointThreshold * 3f))
        );
        float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));

        float turnBrakeIntensity = turnBrakeRatio * distanceFactor * speedFactor;

        // 3. Комбинируем факторы торможения
        float combinedIntensity = Mathf.Max(overspeedRatio, turnBrakeIntensity);

        // Обновляем состояние торможения
        isBraking = combinedIntensity > 0.05f;
        if (isBraking) lastBrakeTime = Time.time;

        return Mathf.Clamp01(combinedIntensity);
    }

    void ApplyBrakingForces()
    {
        float brakeTorque = currentBrakeIntensity * maxBrakeTorque;

        // Применяем больше тормозного усилия на передние колеса
        frontLeftWheel.brakeTorque = brakeTorque * frontBrakeBias;
        frontRightWheel.brakeTorque = brakeTorque * frontBrakeBias;
        rearLeftWheel.brakeTorque = brakeTorque * (1f - frontBrakeBias);
        rearRightWheel.brakeTorque = brakeTorque * (1f - frontBrakeBias);

        // Торможение двигателем
        if (currentBrakeIntensity > 0.1f &&
            currentSpeed * 3.6f > engineBrakeThreshold &&
            !isReversing)
        {
            float engineBrake = currentBrakeIntensity * engineBrakeTorque;
            rearLeftWheel.brakeTorque += engineBrake;
            rearRightWheel.brakeTorque += engineBrake;
        }

        if (currentBrakeIntensity < 0.01f)
        {
            ReleaseBrakes();
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
        currentSteeringAngle = Mathf.Lerp(
            currentSteeringAngle,
            steeringAngle,
            Time.fixedDeltaTime * 2f
        );
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;

        if (isReversing)
        {
            frontLeftWheel.motorTorque = motorTorque;
            frontRightWheel.motorTorque = motorTorque;
            rearLeftWheel.motorTorque = 0f;
            rearRightWheel.motorTorque = 0f;
        }
        else
        {
            rearLeftWheel.motorTorque = motorTorque;
            rearRightWheel.motorTorque = motorTorque;
            frontLeftWheel.motorTorque = 0f;
            frontRightWheel.motorTorque = 0f;
        }

        UpdateBrakingSystem();
        UpdateWheelVisuals();
        ApplyAntiRoll();
        StabilizeCar();
    }

    void StabilizeCar()
    {
        if (Vector3.Angle(transform.up, Vector3.up) > 45f)
        {
            Vector3 torque = Vector3.Cross(transform.up, Vector3.up) * rb.mass * 0.1f;
            rb.AddTorque(torque, ForceMode.Impulse);
        }

        float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));
        rb.angularDamping = 0.5f + speedFactor * 2f;
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
        UpdateWheelVisual(frontLeftWheel);
        UpdateWheelVisual(frontRightWheel);
        UpdateWheelVisual(rearLeftWheel);
        UpdateWheelVisual(rearRightWheel);
    }

    void UpdateWheelVisual(WheelCollider collider)
    {
        if (collider.transform.childCount == 0) return;

        Transform visualWheel = collider.transform.GetChild(0);
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);

        visualWheel.transform.position = Vector3.Lerp(
            visualWheel.transform.position,
            position,
            Time.deltaTime * 20f
        );
        visualWheel.transform.rotation = rotation;
    }
    #endregion

    #region Debug Visualization
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.5f);

            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
            else if (loopPath && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
            }
        }

        if (Application.isPlaying && waypoints.Count > currentWaypointIndex)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(waypoints[currentWaypointIndex].position, 0.7f);
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex].position);
        }
    }
    #endregion
}