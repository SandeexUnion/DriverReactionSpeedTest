
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

    [Tooltip("Множитель крутящего момента заднего хода")]
    [Range(0.5f, 2f)]
    public float reverseTorqueMultiplier = 1.3f;

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
    private float lastReverseStartTime;
    #endregion
    #region Private Variables
    private bool isStuck = false;
    private float stuckTimer = 0f;
    private const float STUCK_TIME_THRESHOLD = 2f;
    private const float STUCK_SPEED_THRESHOLD = 0.5f;
    private float slopeAngle;
    private float slopeSpeedMultiplier = 1f;
    private const float SLOPE_CHECK_DISTANCE = 5f;
    public  float MAX_SLOPE_ANGLE = 30f;
    public  float SLOPE_SPEED_BOOST = 1.5f; // Множитель скорости на подъеме
    public  float SLOPE_SPEED_REDUCTION = 0.7f; // Множитель скорости на спуске
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
        CheckSlope(); // Проверяем наклон поверхности
        currentSpeed = rb.linearVelocity.magnitude;
        // Ограничение угловой скорости при реверсе
        if (isReversing && rb.angularVelocity.magnitude > 2f)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * 2f;
        }
        // Сбрасываем состояние застревания при достаточной скорости
        if (currentSpeed > 2f && isStuck)
        {
            isStuck = false;
            stuckTimer = 0f;
        }
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

            // Более точное определение направления наклона
            Vector3 cross = Vector3.Cross(hit.normal, Vector3.up);
            float slopeDirection = Vector3.Dot(cross, transform.right);

            // Плавное изменение множителя с учетом текущей скорости
            float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed / 3.6f));

            if (slopeAngle > 5f)
            {
                // Подъем в гору (увеличиваем мощность)
                if (slopeDirection < -0.05f)
                {
                    float angleFactor = Mathf.Clamp01(slopeAngle / MAX_SLOPE_ANGLE);
                    slopeSpeedMultiplier = Mathf.Lerp(1f, SLOPE_SPEED_BOOST, angleFactor * speedFactor);
                }
                // Спуск с горы (уменьшаем мощность)
                else if (slopeDirection > 0.05f)
                {
                    float angleFactor = Mathf.Clamp01(slopeAngle / MAX_SLOPE_ANGLE);
                    slopeSpeedMultiplier = Mathf.Lerp(SLOPE_SPEED_REDUCTION, 1f, 1 - (angleFactor * speedFactor));
                }
                else // Боковой наклон
                {
                    slopeSpeedMultiplier = 1f;
                }
            }
            else
            {
                slopeSpeedMultiplier = 1f;
            }

            // Дополнительная регулировка при движении назад
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
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }
    #endregion

    #region Steering Calculation
    float CalculateSteering(Vector3 directionToTarget, float distanceToTarget)
    {
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);

        // Усиленный руль в режиме застревания или при заднем ходе
        float steeringMultiplier = 1f;
        

        if (isReversing)
        {
            angleToTarget = -angleToTarget * steeringMultiplier;
        }
        else
        {
            angleToTarget *= steeringMultiplier;
        }

        // Плавное предсказание
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

        // Проверка на застревание
        CheckIfStuck(distanceToTarget, angleToTarget);

        // Специальный режим при застревании
        if (isStuck)
        {
            return HandleStuckSituation(distanceToTarget, angleToTarget);
        }

        // Обычная логика движения
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
        // Дополнительные условия для определения застревания:
        // 1. Машина не движется (или почти не движется)
        // 2. Большой угол к цели
        // 3. Находится далеко от точки
        // 4. Не в режиме заднего хода
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
                // Принудительно активируем задний ход
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

        if (cycleTime < 2f) // Режим заднего хода (2 секунды)
        {
            isReversing = true;
            // УБРАНО искусственное увеличение угла поворота
            return -maxMotorTorque * 0.9f;
        }
        else // Режим переднего хода (1 секунда)
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
        // Плавное увеличение мощности при начале движения назад
        float reversePower = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(Time.time - lastReverseStartTime));

        // Компенсация бокового скольжения
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        if (Mathf.Abs(localVel.x) > 0.5f)
        {
            float compensation = -localVel.x * 20f * rb.mass * Time.fixedDeltaTime;
            rb.AddRelativeTorque(Vector3.up * compensation, ForceMode.VelocityChange);
        }

        // Условия для прекращения заднего хода
        bool shouldStopReversing = Mathf.Abs(angleToTarget) < 90f
                                && Vector3.Dot(transform.forward, (currentTargetPosition - transform.position).normalized) > 0.7f;

        if (shouldStopReversing)
        {
            isReversing = false;
            return 0f;
        }

        // Динамическая мощность в зависимости от угла
        float angleFactor = Mathf.Clamp01(Mathf.Abs(angleToTarget) / 90f);

        // Учитываем наклон при движении назад (уменьшаем мощность при спуске)
        float slopeEffect = slopeSpeedMultiplier < 1f ? 0.7f : 1f;

        return -maxMotorTorque * reverseTorqueMultiplier * reversePower * (0.7f + angleFactor * 0.3f) * slopeEffect;
    }

    float HandleForwardMovement(float distanceToTarget, float absAngle)
    {
        // Учитываем влияние наклона только при достаточной скорости
        float effectiveSlopeMultiplier = currentSpeed > 1f ? slopeSpeedMultiplier : 1f;

        float speedReduction = Mathf.Clamp01(distanceToTarget / (waypointThreshold * 3f));
        float targetSpeed = maxSpeed * speedReduction * effectiveSlopeMultiplier;

        if (currentSpeed > targetSpeed / 3.6f)
            return 0f;

        float angleFactor = Mathf.Clamp01(1f - absAngle / 120f);
        float torqueMultiplier = currentSpeed < 3f ? lowSpeedTorqueMultiplier : 1f;
        float accelerationSmoothing = Mathf.Clamp01(currentSpeed / 2f);

        // Усиливаем эффект наклона при низких скоростях
        float slopeEffect = Mathf.Lerp(1.5f, 1f, currentSpeed / 5f);

        return maxMotorTorque * angleFactor * torqueMultiplier * speedReduction *
               accelerationSmoothing * Mathf.Pow(effectiveSlopeMultiplier, slopeEffect);
    }

    bool ShouldReverse(float distanceToTarget, float angleToTarget)
    {
        // Если машина почти остановилась и сильно отклонена от цели
        if (currentSpeed < 1.5f && Mathf.Abs(angleToTarget) > 90f)
            return true;

        // Если точка позади и достаточно близко
        Vector3 toTarget = (currentTargetPosition - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, toTarget);

        // Более агрессивное определение необходимости заднего хода
        if (dot < -0.3f && distanceToTarget < waypointThreshold * 3f)
            return true;

        // Если машина проехала точку
        if (distanceToTarget < waypointThreshold * 1.5f && dot < 0.4f)
            return true;

        return false;
    }
    #endregion

    #region New Braking System
    void UpdateBrakingSystem()
    {
        targetBrakeIntensity = CalculateTargetBrakeIntensity();

        // Более плавное изменение интенсивности торможения
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

        // Динамическое распределение тормозного усилия
        float dynamicBias = frontBrakeBias;

        // Учитываем поворот при торможении
        if (Mathf.Abs(currentSteeringAngle) > 5f)
        {
            float turnFactor = Mathf.Clamp01(Mathf.Abs(currentSteeringAngle) / maxSteeringAngle);
            dynamicBias = Mathf.Lerp(frontBrakeBias, 0.55f, turnFactor);

            // Компенсация увода в сторону
            if (currentSteeringAngle > 0) // Поворот вправо
            {
                frontLeftWheel.brakeTorque = brakeTorque * dynamicBias * 1.1f;
                frontRightWheel.brakeTorque = brakeTorque * dynamicBias * 0.9f;
            }
            else // Поворот влево
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

        // Торможение двигателем (более плавное)
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
        // Плавное изменение угла поворота с разными параметрами для переднего/заднего хода
        float steerSpeed = isReversing ? 3f : 5f;
        float maxAngle = isReversing ? maxSteeringAngle * 1.3f : maxSteeringAngle;

        currentSteeringAngle = Mathf.Lerp(
            currentSteeringAngle,
            steeringAngle,
            Time.fixedDeltaTime * steerSpeed
        );

        // Ограничиваем угол поворота
        currentSteeringAngle = Mathf.Clamp(currentSteeringAngle, -maxAngle, maxAngle);

        // Применяем угол только если есть движение
        if (Mathf.Abs(motorTorque) > 0.1f || currentSpeed > 0.5f)
        {
            frontLeftWheel.steerAngle = currentSteeringAngle;
            frontRightWheel.steerAngle = currentSteeringAngle;
        }
        else
        {
            // Плавный сброс угла при остановке
            frontLeftWheel.steerAngle = Mathf.Lerp(frontLeftWheel.steerAngle, 0f, Time.fixedDeltaTime * 5f);
            frontRightWheel.steerAngle = Mathf.Lerp(frontRightWheel.steerAngle, 0f, Time.fixedDeltaTime * 5f);
        }

        // Распределение мощности для заднего хода
        if (isReversing)
        {
            // 30% на передние, 70% на задние колеса
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

        // Управление тормозами
        if (Mathf.Abs(motorTorque) > 0.1f)
        {
            ReleaseBrakes();
        }
        else
        {
            UpdateBrakingSystem();
        }

        UpdateWheelVisuals();
        ApplyAntiRoll();
        StabilizeCar();
    }

    void ConfigureWheelsForReverse()
    {
        WheelFrictionCurve forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.3f, // было 0.4f
            extremumValue = 1f,   // было 0.8f
            asymptoteSlip = 0.5f, // было 0.6f
            asymptoteValue = 0.75f, // было 0.5f
            stiffness = 1.1f     // было 0.9f
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
        // Режим застревания - уменьшаем стабилизацию для более резких движений
        if (!isStuck)
        {
            // Обычная стабилизация
            if (Vector3.Angle(transform.up, Vector3.up) > 30f)
            {
                Vector3 torque = Vector3.Cross(transform.up, Vector3.up) * rb.mass * 0.05f;
                rb.AddTorque(torque, ForceMode.Impulse);
            }
        }

        // Увеличенное демпфирование при заднем ходе
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