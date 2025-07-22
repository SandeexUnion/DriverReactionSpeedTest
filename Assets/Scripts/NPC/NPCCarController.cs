using System.Collections.Generic;
using UnityEngine;

public class NPCCarController : MonoBehaviour
{
    [Header("Wheel Settings")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Car Settings")]
    [Tooltip("Maximum engine torque")]
    public float maxMotorTorque = 200f;
    [Tooltip("Maximum wheel turn angle")]
    public float maxSteeringAngle = 25f;
    [Tooltip("Braking force")]
    public float brakeTorque = 500f;
    [Tooltip("Maximum forward speed (km/h)")]
    public float maxSpeed = 20f;
    [Tooltip("Maximum reverse speed (km/h)")]
    public float reverseSpeed = 10f;
    [Tooltip("Engine power at low speeds")]
    public float lowSpeedTorqueMultiplier = 2f;

    [Header("Path Settings")]
    [Tooltip("Path waypoints")]
    public List<Transform> waypoints;
    [Tooltip("Distance to waypoint to consider it reached")]
    public float waypointThreshold = 3f;
    [Tooltip("Loop the path?")]
    public bool loopPath = true;
    [Tooltip("Look ahead distance for smooth turning")]
    public float lookAheadDistance = 4f;

    [Header("AI Settings")]
    [Tooltip("Obstacle detection distance")]
    public float safeDistance = 5f;
    [Tooltip("Obstacle avoidance strength")]
    public float avoidanceStrength = 1f;
    [Tooltip("Layers for obstacle detection")]
    public LayerMask obstacleLayers;

    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private bool isReversing = false;
    private float avoidanceTimer = 0f;
    private float currentSpeed;
    private float currentSteeringAngle;
    private Vector3 currentTargetPosition;

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

    void FixedUpdate()
    {
        if (waypoints.Count == 0) return;

        currentSpeed = rb.linearVelocity.magnitude;
        currentTargetPosition = waypoints[currentWaypointIndex].position;
        currentTargetPosition.y = transform.position.y;

        // Calculate distances and directions
        Vector3 toTarget = currentTargetPosition - transform.position;
        float distanceToTarget = toTarget.magnitude;
        Vector3 directionToTarget = toTarget.normalized;

        // Check if we've reached the current waypoint
        if (HasReachedWaypoint(distanceToTarget, directionToTarget))
        {
            SelectNextWaypoint();
            return; // Skip the rest of the frame to avoid calculations with new waypoint
        }

        // Calculate steering
        float steeringAngle = CalculateSteering(directionToTarget, distanceToTarget);

        // Calculate motor torque with speed control
        float motorTorque = CalculateMotorTorque(distanceToTarget, directionToTarget);

        // Apply controls
        MoveCar(steeringAngle, motorTorque);
        UpdateWheelVisuals();
    }

    bool HasReachedWaypoint(float distance, Vector3 direction)
    {
        // Check if we're close enough and facing roughly the right direction
        bool isCloseEnough = distance < waypointThreshold;
        bool isFacingWaypoint = Vector3.Dot(transform.forward, direction) > 0.7f || currentSpeed < 0.5f;

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
                ApplyBrakes();
                enabled = false;
                return;
            }
        }

        // Reset reversing state when selecting new waypoint
        isReversing = false;
        currentTargetPosition = waypoints[currentWaypointIndex].position;
    }

    float CalculateSteering(Vector3 directionToTarget, float distanceToTarget)
    {
        // Calculate angle to target
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);

        // If reversing, invert the angle
        if (isReversing)
        {
            angleToTarget = -angleToTarget;
        }

        // Scale the angle to steering range
        float targetSteering = Mathf.Clamp(angleToTarget, -maxSteeringAngle, maxSteeringAngle);

        // Apply additional smoothing when close to waypoint
        float steeringSmoothing = Mathf.Clamp(distanceToTarget / waypointThreshold, 0.3f, 1f);
        return targetSteering * steeringSmoothing;
    }

    float CalculateMotorTorque(float distanceToTarget, Vector3 directionToTarget)
    {
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        float absAngle = Mathf.Abs(angleToTarget);

        // Check if we need to reverse
        if (!isReversing && absAngle > 100f && currentSpeed < 2f)
        {
            isReversing = true;
            return -maxMotorTorque * 0.5f;
        }

        // If we're reversing
        if (isReversing)
        {
            // Check if we should stop reversing
            if (absAngle < 60f || distanceToTarget < waypointThreshold * 1.5f)
            {
                isReversing = false;
                return 0f; // Brake
            }

            // Limit reverse speed
            if (currentSpeed > reverseSpeed / 3.6f)
                return 0f;

            return -maxMotorTorque * 0.6f;
        }

        // Limit forward speed
        if (currentSpeed > maxSpeed / 3.6f)
            return 0f;

        // Calculate speed factor based on angle only (removed distance-based slowing)
        float angleFactor = Mathf.Clamp01(1f - absAngle / 90f);

        // Boost torque at low speeds
        float torqueMultiplier = currentSpeed < 5f ? lowSpeedTorqueMultiplier : 1f;

        return maxMotorTorque * angleFactor * torqueMultiplier;
    }

    void MoveCar(float steeringAngle, float motorTorque)
    {
        // Apply steering with smoothing
        currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, steeringAngle, Time.fixedDeltaTime * 5f);
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;

        // Apply motor torque
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

        // Apply brakes when needed
        if (motorTorque == 0f && currentSpeed > 0.1f)
        {
            ApplyBrakes();
        }
        else
        {
            ReleaseBrakes();
        }
    }

    void ApplyBrakes()
    {
        frontLeftWheel.brakeTorque = brakeTorque;
        frontRightWheel.brakeTorque = brakeTorque;
        rearLeftWheel.brakeTorque = brakeTorque;
        rearRightWheel.brakeTorque = brakeTorque;
    }

    void ReleaseBrakes()
    {
        frontLeftWheel.brakeTorque = 0f;
        frontRightWheel.brakeTorque = 0f;
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;
    }

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

        visualWheel.transform.position = Vector3.Lerp(visualWheel.transform.position, position, Time.deltaTime * 20f);
        visualWheel.transform.rotation = rotation;
    }

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
}