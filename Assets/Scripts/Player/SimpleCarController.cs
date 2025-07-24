using UnityEngine;

public class WheelController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Steering Settings")]
    public float maxSteerAngle = 30f;
    public float steeringSensitivity = 1.5f;
    [Range(0, 1)] public float steeringHelper = 0.75f; // �������� �������
    [Range(0, 1)] public float tractionControl = 0.75f; // �������� ���������
    public bool speedDependentSteering = false; // Отключаем замедление на скорости
    public float steeringResponseCurve = 2.0f; // Кривая отклика руля


    [Header("Advanced Steering")]
    public float steeringAssist = 0.3f; // Помощь в поворотах
    public float counterSteerFactor = 0.5f; // Автоматический контрруль

    [Header("Engine Settings")]
    public float maxMotorTorque = 1000f;
    public float maxBrakeTorque = 2000f;
    public AnimationCurve torqueCurve; // ������ ��������� ������� � ����������� �� ��������

    [Header("Suspension Settings")]
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    public float suspensionDistance = 0.2f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0);

    [Header("Input Settings")]
    public string steeringAxis = "Horizontal";
    public string throttleAxis = "Vertical";
    public string brakeAxis = "Brake";
    public bool invertSteering = false;

    [Header("Acceleration Boost")]
    [Tooltip("Множитель крутящего момента на низких скоростях")]
    public float lowSpeedBoost = 1.5f;
    [Tooltip("Скорость (км/ч), до которой действует ускорение")]
    public float boostCutoffSpeed = 30f;
    [Tooltip("Кривая плавного уменьшения boost'а")]
    public AnimationCurve boostCurve = new AnimationCurve(
        new Keyframe(0, 1),
        new Keyframe(0.5f, 0.5f),
        new Keyframe(1, 0)
    );

    private float steeringInput;
    private float throttleInput;
    private float brakeInput;
    private Rigidbody carRigidbody;
    private float currentSpeed;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass += centerOfMassOffset;

        // ��������� �������� ��� ���� �����
        ConfigureSuspension();

        // ������������� ������ ��������� �������
        torqueCurve = new AnimationCurve(
            new Keyframe(0, 1),
            new Keyframe(50, 0.8f),
            new Keyframe(100, 0.5f),
            new Keyframe(150, 0.3f),
            new Keyframe(200, 0.1f)
        );
    }

    void Update()
    {
        GetInput();
        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f; // ��/�
    }

    void FixedUpdate()
    {
        ApplySteering();
        ApplyThrottle();
        ApplyBrakes();
        ApplySteeringHelper();
        ApplyTractionControl();
    }

    void ConfigureSuspension()
    {
        JointSpring spring = new JointSpring
        {
            spring = suspensionSpring,
            damper = suspensionDamper
        };

        WheelFrictionCurve forwardFriction = frontLeftWheel.forwardFriction;
        forwardFriction.asymptoteValue = 0.5f;
        forwardFriction.extremumSlip = 0.4f;
        forwardFriction.stiffness = 1f;

        WheelFrictionCurve sidewaysFriction = frontLeftWheel.sidewaysFriction;
        sidewaysFriction.asymptoteValue = 0.75f;
        sidewaysFriction.extremumSlip = 0.2f;
        sidewaysFriction.stiffness = 1f;

        frontLeftWheel.forwardFriction = forwardFriction;
        frontLeftWheel.sidewaysFriction = sidewaysFriction;
        frontRightWheel.forwardFriction = forwardFriction;
        frontRightWheel.sidewaysFriction = sidewaysFriction;

        rearLeftWheel.forwardFriction = forwardFriction;
        rearLeftWheel.sidewaysFriction = sidewaysFriction;
        rearRightWheel.forwardFriction = forwardFriction;
        rearRightWheel.sidewaysFriction = sidewaysFriction;

        frontLeftWheel.suspensionSpring = spring;
        frontRightWheel.suspensionSpring = spring;
        rearLeftWheel.suspensionSpring = spring;
        rearRightWheel.suspensionSpring = spring;

        frontLeftWheel.suspensionDistance = suspensionDistance;
        frontRightWheel.suspensionDistance = suspensionDistance;
        rearLeftWheel.suspensionDistance = suspensionDistance;
        rearRightWheel.suspensionDistance = suspensionDistance;
    }

    void GetInput()
    {
        steeringInput = Input.GetAxis(steeringAxis) * steeringSensitivity;
        if (invertSteering) steeringInput *= -1;
        steeringInput = Mathf.Clamp(steeringInput, -1f, 1f);

        throttleInput = Input.GetAxis(throttleAxis);
        brakeInput = Input.GetAxis(brakeAxis);
    }

    
void ApplySteering()
{
    // Новая система поворота без зависимости от скорости
    float steerAngle;
    
    if(speedDependentSteering)
    {
        // Старая система с зависимостью от скорости
        float speedFactor = Mathf.Clamp(1 - (currentSpeed / 150f), 0.1f, 1f);
        steerAngle = Mathf.Pow(Mathf.Abs(steeringInput), steeringResponseCurve) * 
                   Mathf.Sign(steeringInput) * maxSteerAngle * speedFactor;
    }
    else
    {
        // Новая система - полный угол всегда
        steerAngle = Mathf.Pow(Mathf.Abs(steeringInput), steeringResponseCurve) * 
                   Mathf.Sign(steeringInput) * maxSteerAngle;
    }
    
    frontLeftWheel.steerAngle = steerAngle;
    frontRightWheel.steerAngle = steerAngle;
    
    ApplySteeringAssist();
}

void ApplySteeringAssist()
{
    // Помощь в поворотах - делает авто более отзывчивым
    if(steeringInput != 0)
    {
        float turnAssist = carRigidbody.velocity.magnitude * steeringAssist * steeringInput;
        carRigidbody.AddTorque(transform.up * turnAssist, ForceMode.Acceleration);
    }
    
    // Автоматический контрруль для стабилизации
    float counterSteer = -carRigidbody.angularVelocity.y * counterSteerFactor;
    carRigidbody.AddTorque(transform.up * counterSteer, ForceMode.Acceleration);
}

    void ApplyThrottle()
    {
        // Расчет коэффициента boost'а
        float boostFactor = boostCurve.Evaluate(currentSpeed / boostCutoffSpeed);
        float finalBoost = Mathf.Lerp(1f, lowSpeedBoost, boostFactor);

        // Учет кривой крутящего момента и boost'а
        float torqueFactor = torqueCurve.Evaluate(currentSpeed);
        float motorTorque = throttleInput * maxMotorTorque * torqueFactor * finalBoost;

        // Применение крутящего момента в зависимости от привода
        rearLeftWheel.motorTorque = motorTorque;
        rearRightWheel.motorTorque = motorTorque;
    }

    void ApplyBrakes()
    {
        float brakeTorque = brakeInput * maxBrakeTorque;
        frontLeftWheel.brakeTorque = brakeTorque;
        frontRightWheel.brakeTorque = brakeTorque;
        rearLeftWheel.brakeTorque = brakeTorque;
        rearRightWheel.brakeTorque = brakeTorque;

        // ���������� ���������� ��� �������� �����
        if (throttleInput < 0)
        {
            rearLeftWheel.brakeTorque += Mathf.Abs(throttleInput) * maxBrakeTorque * 0.5f;
            rearRightWheel.brakeTorque += Mathf.Abs(throttleInput) * maxBrakeTorque * 0.5f;
        }
    }

    void ApplySteeringHelper()
    {
        // �������� ������� - ��������� ���������� ����������������
        if (steeringInput == 0 || carRigidbody.angularVelocity.magnitude > 0.1f)
        {
            Vector3 angularVelocity = carRigidbody.angularVelocity;
            angularVelocity.y *= 0.95f * (1 - steeringHelper);
            carRigidbody.angularVelocity = angularVelocity;
        }
    }

    void ApplyTractionControl()
    {
        // �������� ��������� - ������������� ����������� �����
        WheelHit hit;
        rearLeftWheel.GetGroundHit(out hit);
        AdjustTorque(hit.forwardSlip);

        rearRightWheel.GetGroundHit(out hit);
        AdjustTorque(hit.forwardSlip);
    }

    void AdjustTorque(float forwardSlip)
    {
        if (forwardSlip > 0.5f)
        {
            rearLeftWheel.motorTorque *= 1 - tractionControl;
            rearRightWheel.motorTorque *= 1 - tractionControl;
        }
    }
}