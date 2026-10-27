using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCarController : MonoBehaviour
{
    [Header("Wheel References")]
    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;

    [Header("Wheel Meshes")]
    public Transform frontLeftWheelMesh;
    public Transform frontRightWheelMesh;
    public Transform rearLeftWheelMesh;
    public Transform rearRightWheelMesh;

    [Header("Physics References")]
    public Rigidbody vehicleRB;
    public Transform steeringWheel;

    [Header("Car Settings")]
    public float maxForwardSpeed = 100f;
    public float maxReverseSpeed = 30f;
    public float horsePower = 1000f;
    public float brakePower = 2000f;
    public float handbrakeForce = 3000f;
    public float maxSteerAngle = 30f;
    public float steeringSpeed = 5f;
    public float decelerationSpeed = 0.5f;
    public float maxSteeringWheelRotation = 360f;

    [Header("Drive Type")]
    public DriveTypes driveType = DriveTypes.RWD;

    [Header("Lights")]
    [SerializeField] private Light[] lights; 


    
    private float currentAccelerationValue = 0f;
    public float currentBrakeValue { get; private set; } = 0f;
    private float currentHandbrakeValue = 0f;
    private float currentSteerAngle = 0f;
    private float targetSteerAngle = 0f;
    private float speedFactor = 0f;

    
    private WheelCollider[] wheels;
    private Transform[] wheelMeshes;

    
    [SerializeField] private float currentSpeed = 0f;

    private void Awake()
    {
        wheels = new WheelCollider[]
        {
            frontLeftWheelCollider,
            frontRightWheelCollider,
            rearLeftWheelCollider,
            rearRightWheelCollider
        };

        wheelMeshes = new Transform[]
        {
            frontLeftWheelMesh,
            frontRightWheelMesh,
            rearLeftWheelMesh,
            rearRightWheelMesh
        };

        if (vehicleRB == null)
        {
            vehicleRB = GetComponent<Rigidbody>();
        }
    }

    
    void OnAccelerate(InputValue accelerationValue)
    {
        currentAccelerationValue = accelerationValue.Get<float>();
    }

    void OnBrake(InputValue brakeValue)
    {
        currentBrakeValue = brakeValue.Get<float>();
    }

    void OnHandbrake(InputValue handbrakeValue)
    {
        currentHandbrakeValue = handbrakeValue.Get<float>();
    }

    void OnSteer(InputValue turnValue)
    {
        targetSteerAngle = turnValue.Get<float>() * maxSteerAngle;
    }

    
    void Acceleration()
    {
       
        foreach (var wheel in wheels)
        {
            wheel.motorTorque = 0;
        }

        
        if (currentAccelerationValue > 0.1f) 
        {
            speedFactor = Mathf.InverseLerp(0, maxForwardSpeed, currentSpeed);
            float currentMotorTorque = Mathf.Lerp(horsePower, 0, speedFactor);

            if (currentSpeed < maxForwardSpeed)
            {
                ApplyMotorTorque(currentMotorTorque * currentAccelerationValue);
            }
        }
        else if (currentAccelerationValue < -0.1f) 
        {
            speedFactor = Mathf.InverseLerp(0, maxReverseSpeed, Mathf.Abs(currentSpeed));
            float currentMotorTorque = Mathf.Lerp(horsePower, 0, speedFactor);

            if (currentSpeed > -maxReverseSpeed)
            {
                ApplyMotorTorque(currentMotorTorque * currentAccelerationValue);
            }
        }
    }

    void ApplyMotorTorque(float torque)
    {
        switch (driveType)
        {
            case DriveTypes.RWD:
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
                break;
            case DriveTypes.FWD:
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
                break;
            case DriveTypes.AWD:
                foreach (var wheel in wheels)
                {
                    wheel.motorTorque = torque;
                }
                break;
        }
    }

    void Braking()
    {
        if (currentBrakeValue > 0.1f)
        {
            frontLeftWheelCollider.brakeTorque = currentBrakeValue * brakePower;
            frontRightWheelCollider.brakeTorque = currentBrakeValue * brakePower;
        }
        else
        {
            frontLeftWheelCollider.brakeTorque = 0;
            frontRightWheelCollider.brakeTorque = 0;
        }
    }

    void Handbraking()
    {
        if (currentHandbrakeValue > 0.1f)
        {
            rearLeftWheelCollider.motorTorque = 0;
            rearRightWheelCollider.motorTorque = 0;
            rearLeftWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;
            rearRightWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;
        }
        else
        {
            rearLeftWheelCollider.brakeTorque = 0;
            rearRightWheelCollider.brakeTorque = 0;
        }
    }

    void Steering()
    {
        float adjustedspeedFactor = Mathf.InverseLerp(20, maxForwardSpeed, Mathf.Abs(currentSpeed));
        float adjustedTurnAngle = targetSteerAngle * (1 - adjustedspeedFactor);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, adjustedTurnAngle, Time.deltaTime * steeringSpeed);

        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;

        UpdateWheelVisuals();
    }

    void Slowdown()
    {
        if (Mathf.Abs(currentAccelerationValue) < 0.1f && currentBrakeValue < 0.1f && currentHandbrakeValue < 0.1f)
        {
            
#if UNITY_6000_0_OR_NEWER
            vehicleRB.linearVelocity = Vector3.Lerp(vehicleRB.linearVelocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#else
            vehicleRB.velocity = Vector3.Lerp(vehicleRB.velocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#endif
        }
    }

    void UpdateWheelVisuals()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheelMeshes[i] != null)
            {
                wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheelMeshes[i].SetPositionAndRotation(position, rotation);
            }
        }
    }

    void RotateSteeringWheel()
    {
        if (steeringWheel != null)
        {
            float normalizedSteerAngle = Mathf.Clamp(frontLeftWheelCollider.steerAngle, -maxSteerAngle, maxSteerAngle);

            
            float rotation = Mathf.Lerp(-maxSteeringWheelRotation, maxSteeringWheelRotation,
                (normalizedSteerAngle + maxSteerAngle) / (2 * maxSteerAngle));

            steeringWheel.localRotation = Quaternion.Euler(
                steeringWheel.localEulerAngles.x,
                180f,     
                rotation-180
            );
        }
    }
    void OnLightsToggle(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            ToggleLights();
        }
    }

    public void ToggleLights()
    {
        if (lights != null && lights.Length > 0)
        {
            foreach (Light light in lights)
            {
                if (light != null)
                {
                    light.enabled = !light.enabled;
                }
            }
            Debug.Log($"All lights toggled. State: {(lights[0].enabled ? "ON" : "OFF")}");
        }
        else
        {
            Debug.LogWarning("Lights array is not set or empty in the inspector!");
        }
    }


    void UpdateSpeed()
    {
        
#if UNITY_6000_0_OR_NEWER
        currentSpeed = Vector3.Dot(transform.forward, vehicleRB.linearVelocity) * 3.6f;
#else
        currentSpeed = Vector3.Dot(transform.forward, vehicleRB.velocity) * 3.6f;
#endif
    }

    // Main update loop
    private void FixedUpdate()
    {
        Acceleration();
        Braking();
        Handbraking();
        Steering();
        Slowdown();
        RotateSteeringWheel();

        UpdateSpeed();
        UpdateWheelVisuals();
    }

    public bool InAir()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.GetGroundHit(out _))
            {
                return false;
            }
        }
        return true;
    }

    
    public float GetCurrentSpeed() => currentSpeed;
    public float GetAccelerationInput() => currentAccelerationValue;
    public float GetBrakeInput() => currentBrakeValue;
    public float GetSteeringInput() => targetSteerAngle / maxSteerAngle;

    
    public enum DriveTypes { RWD, FWD, AWD }
}