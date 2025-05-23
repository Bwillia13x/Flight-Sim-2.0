using UnityEngine;

/// <summary>
/// Core flight physics controller with realistic aerodynamic simulation
/// Handles pitch, roll, yaw, thrust, and drag calculations
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FlightController : MonoBehaviour
{
    [Header("Aircraft Configuration")]
    [SerializeField] private float wingArea = 25f;          // Wing surface area in m²
    [SerializeField] private float maxThrust = 50000f;      // Maximum engine thrust in Newtons
    [SerializeField] private float mass = 5000f;            // Aircraft mass in kg
    [SerializeField] private float liftCoefficient = 1.2f;  // Lift coefficient
    [SerializeField] private float dragCoefficient = 0.3f;  // Drag coefficient
    
    [Header("Control Sensitivity")]
    [SerializeField] private float pitchSensitivity = 2f;
    [SerializeField] private float rollSensitivity = 3f;
    [SerializeField] private float yawSensitivity = 1.5f;
    [SerializeField] private float throttleResponse = 2f;
    
    [Header("Flight Limits")]
    [SerializeField] private float maxSpeed = 300f;         // m/s
    [SerializeField] private float stallSpeed = 40f;        // m/s
    [SerializeField] private float maxAltitude = 15000f;    // meters
    
    // Private variables
    private Rigidbody rb;
    private float currentThrottle = 0f;
    private float targetThrottle = 0f;
    
    // Input values
    private float pitchInput = 0f;
    private float rollInput = 0f;
    private float yawInput = 0f;
    private float throttleInput = 0f;
    
    // Physics constants
    private const float AIR_DENSITY = 1.225f; // kg/m³ at sea level
    
    // Public properties for other systems
    public float CurrentSpeed => rb.velocity.magnitude;
    public float CurrentThrottle => currentThrottle;
    public float Altitude => transform.position.y;
    public bool IsStalling => CurrentSpeed < stallSpeed;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateThrottle();
    }
    
    private void FixedUpdate()
    {
        ApplyThrust();
        ApplyAerodynamics();
        ApplyControlSurfaces();
        LimitAltitude();
    }
    
    private void HandleInput()
    {
        // Get input from InputManager or Input System
        pitchInput = Input.GetAxis("Vertical");    // W/S or Up/Down
        rollInput = Input.GetAxis("Horizontal");   // A/D or Left/Right
        yawInput = Input.GetAxis("Yaw");           // Q/E
        throttleInput = Input.GetAxis("Throttle"); // Shift/Ctrl or custom axis
        
        // Alternative keyboard controls
        if (Input.GetKey(KeyCode.LeftShift)) throttleInput += Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftControl)) throttleInput -= Time.deltaTime;
        throttleInput = Mathf.Clamp01(throttleInput);
    }
    
    private void UpdateThrottle()
    {
        targetThrottle = throttleInput;
        currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle, 
            throttleResponse * Time.deltaTime);
    }
    
    private void ApplyThrust()
    {
        float thrustForce = maxThrust * currentThrottle;
        Vector3 thrust = transform.forward * thrustForce;
        rb.AddForce(thrust);
    }
    
    private void ApplyAerodynamics()
    {
        Vector3 velocity = rb.velocity;
        float speed = velocity.magnitude;
        
        if (speed < 0.1f) return;
        
        // Calculate angle of attack
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        float angleOfAttack = Mathf.Atan2(-localVelocity.y, localVelocity.z) * Mathf.Rad2Deg;
        
        // Calculate lift
        float dynamicPressure = 0.5f * AIR_DENSITY * speed * speed;
        float liftMagnitude = dynamicPressure * wingArea * liftCoefficient * 
            Mathf.Sin(angleOfAttack * Mathf.Deg2Rad);
        Vector3 lift = transform.up * liftMagnitude;
        
        // Calculate drag
        float dragMagnitude = dynamicPressure * wingArea * dragCoefficient;
        Vector3 drag = -velocity.normalized * dragMagnitude;
        
        // Apply forces
        rb.AddForce(lift);
        rb.AddForce(drag);
    }
    
    private void ApplyControlSurfaces()
    {
        float speed = rb.velocity.magnitude;
        float controlEffectiveness = Mathf.Clamp01(speed / stallSpeed);
        
        // Apply pitch (elevator)
        Vector3 pitchTorque = transform.right * (pitchInput * pitchSensitivity * controlEffectiveness);
        rb.AddTorque(pitchTorque, ForceMode.Force);
        
        // Apply roll (ailerons)
        Vector3 rollTorque = transform.forward * (rollInput * rollSensitivity * controlEffectiveness);
        rb.AddTorque(rollTorque, ForceMode.Force);
        
        // Apply yaw (rudder)
        Vector3 yawTorque = transform.up * (yawInput * yawSensitivity * controlEffectiveness);
        rb.AddTorque(yawTorque, ForceMode.Force);
    }
    
    private void LimitAltitude()
    {
        if (transform.position.y > maxAltitude)
        {
            Vector3 pos = transform.position;
            pos.y = maxAltitude;
            transform.position = pos;
            
            if (rb.velocity.y > 0)
            {
                Vector3 vel = rb.velocity;
                vel.y = 0;
                rb.velocity = vel;
            }
        }
    }
    
    // Public methods for external systems
    public void SetThrottle(float throttle)
    {
        throttleInput = Mathf.Clamp01(throttle);
    }
    
    // AI input methods
    public void SetFlightInputs(float pitch, float roll, float yaw, float throttle)
    {
        pitchInput = Mathf.Clamp(pitch, -1f, 1f);
        rollInput = Mathf.Clamp(roll, -1f, 1f);
        yawInput = Mathf.Clamp(yaw, -1f, 1f);
        throttleInput = Mathf.Clamp01(throttle);
    }
    
    public void SetPitchInput(float pitch)
    {
        pitchInput = Mathf.Clamp(pitch, -1f, 1f);
    }
    
    public void SetRollInput(float roll)
    {
        rollInput = Mathf.Clamp(roll, -1f, 1f);
    }
    
    public void SetYawInput(float yaw)
    {
        yawInput = Mathf.Clamp(yaw, -1f, 1f);
    }
    
    public void AddExternalForce(Vector3 force)
    {
        rb.AddForce(force);
    }
    
    // TODO: Add engine sound effects based on throttle
    // TODO: Add visual effects for engine exhaust
    // TODO: Add stall warning systems
    // TODO: Add fuel consumption mechanics
}
