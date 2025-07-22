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
    [Tooltip("Сила торможения (Н·м)")]
    public float brakeTorque = 500f;
    [Tooltip("Максимальная скорость движения вперед (км/ч)")]
    public float maxSpeed = 20f;
    [Tooltip("Максимальная скорость движения назад (км/ч)")]
    public float reverseSpeed = 10f;
    [Tooltip("Множитель крутящего момента на низких скоростях")]
    public float lowSpeedTorqueMultiplier = 2f;
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
    // Текущий индекс целевой точки пути
    private int currentWaypointIndex = 0;
    // Ссылка на Rigidbody машины
    private Rigidbody rb;
    // Флаг движения задним ходом
    private bool isReversing = false;
    // Таймер для системы уклонения
    private float avoidanceTimer = 0f;
    // Текущая скорость машины (м/с)
    private float currentSpeed;
    // Текущий угол поворота колес
    private float currentSteeringAngle;
    // Позиция текущей цели
    private Vector3 currentTargetPosition;
    #endregion

    #region Initialization
    void Start()
    {
        // Получаем компонент Rigidbody
        rb = GetComponent<Rigidbody>();

        // Проверяем наличие точек пути
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned to NPCCarController!");
            enabled = false;
            return;
        }

        // Настраиваем коллайдеры колес
        ConfigureWheelColliders();

        // Устанавливаем первую цель
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }

    /// <summary>
    /// Настройка параметров коллайдеров колес
    /// </summary>
    void ConfigureWheelColliders()
    {
        WheelCollider[] wheels = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        foreach (var wheel in wheels)
        {
            // Настраиваем параметры физики колес
            wheel.ConfigureVehicleSubsteps(5, 12, 15);
            wheel.wheelDampingRate = 0.5f;
        }
    }
    #endregion

    #region Main Update Loop
    void FixedUpdate()
    {
        // Если нет точек пути - выходим
        if (waypoints.Count == 0) return;

        // Обновляем текущую скорость
        currentSpeed = rb.linearVelocity.magnitude;

        // Получаем позицию текущей цели (игнорируем высоту)
        currentTargetPosition = waypoints[currentWaypointIndex].position;
        currentTargetPosition.y = transform.position.y;

        // Вычисляем вектор к цели и его длину
        Vector3 toTarget = currentTargetPosition - transform.position;
        float distanceToTarget = toTarget.magnitude;
        Vector3 directionToTarget = toTarget.normalized;

        // Проверяем, достигли ли текущую точку
        if (HasReachedWaypoint(distanceToTarget, directionToTarget))
        {
            SelectNextWaypoint();
            return; // Пропускаем остальные вычисления для этого кадра
        }

        // Вычисляем угол поворота колес
        float steeringAngle = CalculateSteering(directionToTarget, distanceToTarget);

        // Вычисляем крутящий момент двигателя
        float motorTorque = CalculateMotorTorque(distanceToTarget, directionToTarget);

        // Применяем управление к машине
        MoveCar(steeringAngle, motorTorque);

        // Обновляем визуальное представление колес
        UpdateWheelVisuals();
    }
    #endregion

    #region Waypoint Logic
    /// <summary>
    /// Проверяет, достигнута ли текущая точка пути
    /// </summary>
    bool HasReachedWaypoint(float distance, Vector3 direction)
    {
        // Проверяем близость к точке
        bool isCloseEnough = distance < waypointThreshold;
        // Проверяем, что машина направлена примерно в сторону точки (или почти стоит)
        bool isFacingWaypoint = Vector3.Dot(transform.forward, direction) > 0.7f || currentSpeed < 0.5f;

        return isCloseEnough && isFacingWaypoint;
    }

    /// <summary>
    /// Выбирает следующую точку пути
    /// </summary>
    void SelectNextWaypoint()
    {
        currentWaypointIndex++;

        // Проверяем, нужно ли зацикливать путь
        if (currentWaypointIndex >= waypoints.Count)
        {
            if (loopPath)
            {
                currentWaypointIndex = 0;
            }
            else
            {
                // Останавливаем машину и отключаем скрипт
                ApplyBrakes();
                enabled = false;
                return;
            }
        }

        // Сбрасываем состояние движения задним ходом
        isReversing = false;
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }
    #endregion

    #region Steering Calculation
    /// <summary>
    /// Вычисляет угол поворота колес на основе направления к цели
    /// </summary>
    float CalculateSteering(Vector3 directionToTarget, float distanceToTarget)
    {
        // Вычисляем угол между текущим направлением и направлением к цели
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);

        // Если движемся задним ходом - инвертируем угол
        if (isReversing)
        {
            angleToTarget = -angleToTarget;
        }

        // Ограничиваем угол максимальным значением
        float targetSteering = Mathf.Clamp(angleToTarget, -maxSteeringAngle, maxSteeringAngle);

        // Плавное уменьшение угла при приближении к точке
        float steeringSmoothing = Mathf.Clamp(distanceToTarget / waypointThreshold, 0.3f, 1f);
        return targetSteering * steeringSmoothing;
    }
    #endregion

    #region Motor Logic
    /// <summary>
    /// Вычисляет необходимый крутящий момент двигателя
    /// </summary>
    float CalculateMotorTorque(float distanceToTarget, Vector3 directionToTarget)
    {
        // Вычисляем угол к цели
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        float absAngle = Mathf.Abs(angleToTarget);

        // Проверяем, нужно ли включить задний ход
        if (!isReversing && absAngle > 100f && currentSpeed < 2f)
        {
            isReversing = true;
            return -maxMotorTorque * 0.5f; // Начинаем движение назад с половинной мощностью
        }

        // Логика движения задним ходом
        if (isReversing)
        {
            // Проверяем, можно ли прекратить движение назад
            if (absAngle < 60f || distanceToTarget < waypointThreshold * 1.5f)
            {
                isReversing = false;
                return 0f; // Тормозим
            }

            // Ограничиваем скорость заднего хода
            if (currentSpeed > reverseSpeed / 3.6f) // Конвертируем км/ч в м/с
                return 0f;

            return -maxMotorTorque * 0.6f; // Двигаемся назад с 60% мощности
        }

        // Ограничиваем максимальную скорость вперед
        if (currentSpeed > maxSpeed / 3.6f) // Конвертируем км/ч в м/с
            return 0f;

        // Уменьшаем мощность на резких поворотах
        float angleFactor = Mathf.Clamp01(1f - absAngle / 90f);

        // Увеличиваем мощность на низких скоростях
        float torqueMultiplier = currentSpeed < 5f ? lowSpeedTorqueMultiplier : 1f;

        return maxMotorTorque * angleFactor * torqueMultiplier;
    }
    #endregion

    #region Car Control
    /// <summary>
    /// Применяет вычисленные параметры управления к машине
    /// </summary>
    void MoveCar(float steeringAngle, float motorTorque)
    {
        // Плавно изменяем угол поворота
        currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, steeringAngle, Time.fixedDeltaTime * 1f);
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;

        // Распределяем крутящий момент в зависимости от режима движения
        if (isReversing)
        {
            // При движении назад - крутящий момент на передние колеса
            frontLeftWheel.motorTorque = motorTorque;
            frontRightWheel.motorTorque = motorTorque;
            rearLeftWheel.motorTorque = 0f;
            rearRightWheel.motorTorque = 0f;
        }
        else
        {
            // При движении вперед - крутящий момент на задние колеса
            rearLeftWheel.motorTorque = motorTorque;
            rearRightWheel.motorTorque = motorTorque;
            frontLeftWheel.motorTorque = 0f;
            frontRightWheel.motorTorque = 0f;
        }

        // Применяем тормоза при необходимости
        if (motorTorque == 0f && currentSpeed > 0.1f)
        {
            ApplyBrakes();
        }
        else
        {
            ReleaseBrakes();
        }
    }

    /// <summary>
    /// Включает тормоза на всех колесах
    /// </summary>
    void ApplyBrakes()
    {
        frontLeftWheel.brakeTorque = brakeTorque;
        frontRightWheel.brakeTorque = brakeTorque;
        rearLeftWheel.brakeTorque = brakeTorque;
        rearRightWheel.brakeTorque = brakeTorque;
    }

    /// <summary>
    /// Отключает тормоза на всех колесах
    /// </summary>
    void ReleaseBrakes()
    {
        frontLeftWheel.brakeTorque = 0f;
        frontRightWheel.brakeTorque = 0f;
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;
    }
    #endregion

    #region Wheel Visualization
    /// <summary>
    /// Обновляет визуальное представление всех колес
    /// </summary>
    void UpdateWheelVisuals()
    {
        UpdateWheelVisual(frontLeftWheel);
        UpdateWheelVisual(frontRightWheel);
        UpdateWheelVisual(rearLeftWheel);
        UpdateWheelVisual(rearRightWheel);
    }

    /// <summary>
    /// Синхронизирует визуальное представление колеса с его физическим состоянием
    /// </summary>
    void UpdateWheelVisual(WheelCollider collider)
    {
        if (collider.transform.childCount == 0) return;

        // Получаем визуальное представление колеса
        Transform visualWheel = collider.transform.GetChild(0);

        // Получаем актуальную позицию и поворот колеса из коллайдера
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);

        // Плавно обновляем позицию и поворот визуального объекта
        visualWheel.transform.position = Vector3.Lerp(visualWheel.transform.position, position, Time.deltaTime * 20f);
        visualWheel.transform.rotation = rotation;
    }
    #endregion

    #region Debug Visualization
    /// <summary>
    /// Отрисовывает Gizmos для визуализации пути в редакторе
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        // Отрисовываем точки пути зеленым цветом
        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.5f);

            // Отрисовываем линии между точками
            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
            else if (loopPath && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
            }
        }

        // В режиме игры отрисовываем текущую цель красным цветом
        if (Application.isPlaying && waypoints.Count > currentWaypointIndex)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(waypoints[currentWaypointIndex].position, 0.7f);
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex].position);
        }
    }
    #endregion
}