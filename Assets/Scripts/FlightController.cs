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

    [Header("Audio Configuration")]
    public AudioSource engineAudioSource;
    public float minEnginePitch = 0.5f;
    public float maxEnginePitch = 2.0f;
    public float minEngineVolume = 0.3f;
    public float maxEngineVolume = 1.0f;

    [Header("Maneuver Configuration")]
    public float immelmannCooldown = 10.0f;
    public float splitSCooldown = 10.0f;
    public float immelmannDuration = 3.0f; // Total time for Immelmann
    public float splitSDuration = 3.0f;    // Total time for Split-S
    public float maneuverPitchRate = 1.0f; // Normalized input for pitch during maneuvers
    public float maneuverRollRate = 1.0f;  // Normalized input for roll during maneuvers

    // Private variables
    private Rigidbody rb;
    private float currentThrottle = 0f;
    private float targetThrottle = 0f;
    
    // Input values
    private float pitchInput = 0f;
    private float rollInput = 0f;
    private float yawInput = 0f;
    private float throttleInput = 0f;

    // Maneuver state variables
    private bool isPerformingImmelmann = false;
    private bool isPerformingSplitS = false;
    private float lastImmelmannTime = -100f; // Initialize to allow immediate use
    private float lastSplitSTime = -100f;    // Initialize to allow immediate use
    private int currentManeuverPhase = 0;
    private float currentManeuverTime = 0f;
    
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
        
        // Initialize cooldowns to allow immediate use
        lastImmelmannTime = -immelmannCooldown;
        lastSplitSTime = -splitSCooldown;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateThrottle();
        UpdateEngineSound();
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
        // Get throttle input first - this remains player controlled
        throttleInput = Input.GetAxis("Throttle"); // Shift/Ctrl or custom axis
        if (Input.GetKey(KeyCode.LeftShift)) throttleInput += Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftControl)) throttleInput -= Time.deltaTime;
        throttleInput = Mathf.Clamp01(throttleInput);

        // Maneuver execution takes precedence over player input for pitch/roll/yaw
        if (isPerformingImmelmann)
        {
            ExecuteImmelmannStep();
            return; // Skip normal input processing for pitch/roll/yaw
        }

        if (isPerformingSplitS)
        {
            ExecuteSplitSStep();
            return; // Skip normal input processing for pitch/roll/yaw
        }

        // Get standard player input if no maneuver is active
        pitchInput = Input.GetAxis("Vertical");    // W/S or Up/Down
        rollInput = Input.GetAxis("Horizontal");   // A/D or Left/Right
        yawInput = Input.GetAxis("Yaw");           // Q/E (though typically not used with mouse controls)

        // Check for maneuver initiation
        if (Input.GetButtonDown("Immelmann") && !isPerformingSplitS && Time.time >= lastImmelmannTime + immelmannCooldown)
        {
            isPerformingImmelmann = true;
            lastImmelmannTime = Time.time;
            currentManeuverPhase = 0;
            currentManeuverTime = 0f;
            // Optionally, clear player inputs for this frame if maneuver starts
            // pitchInput = 0; rollInput = 0; yawInput = 0; 
            ExecuteImmelmannStep(); // Execute first step immediately
            return;
        }
        
        if (Input.GetButtonDown("SplitS") && !isPerformingImmelmann && Time.time >= lastSplitSTime + splitSCooldown)
        {
            isPerformingSplitS = true;
            lastSplitSTime = Time.time;
            currentManeuverPhase = 0;
            currentManeuverTime = 0f;
            // Optionally, clear player inputs for this frame
            // pitchInput = 0; rollInput = 0; yawInput = 0;
            ExecuteSplitSStep(); // Execute first step immediately
            return;
        }
    }

    private void ExecuteImmelmannStep()
    {
        // Placeholder for Immelmann logic
        // This will set pitchInput and rollInput based on phase and time
        Debug.Log("Executing Immelmann Step - Phase: " + currentManeuverPhase + " Time: " + currentManeuverTime);

        currentManeuverTime += Time.deltaTime;

        if (currentManeuverPhase == 0) // Phase 1: Half-loop up
        {
            pitchInput = maneuverPitchRate;
            rollInput = 0f;
            yawInput = 0f; // Maintain yaw

            if (currentManeuverTime >= immelmannDuration / 2f)
            {
                currentManeuverPhase = 1;
                currentManeuverTime = 0f; // Reset timer for next phase
            }
        }
        else if (currentManeuverPhase == 1) // Phase 2: Half-roll
        {
            pitchInput = 0f; // Neutral pitch during roll
            rollInput = maneuverRollRate;
            yawInput = 0f;

            if (currentManeuverTime >= immelmannDuration / 2f)
            {
                isPerformingImmelmann = false;
                pitchInput = 0f; // Reset controls
                rollInput = 0f;
                Debug.Log("Immelmann Complete");
            }
        }
    }

    private void ExecuteSplitSStep()
    {
        // Placeholder for Split-S logic
        // This will set pitchInput and rollInput based on phase and time
        Debug.Log("Executing Split S Step - Phase: " + currentManeuverPhase + " Time: " + currentManeuverTime);
        
        currentManeuverTime += Time.deltaTime;

        if (currentManeuverPhase == 0) // Phase 1: Half-roll
        {
            pitchInput = 0f;
            rollInput = maneuverRollRate;
            yawInput = 0f;

            if (currentManeuverTime >= splitSDuration / 2f)
            {
                currentManeuverPhase = 1;
                currentManeuverTime = 0f; // Reset timer for next phase
            }
        }
        else if (currentManeuverPhase == 1) // Phase 2: Half-loop down
        {
            pitchInput = -maneuverPitchRate; // Negative pitch for downward loop
            rollInput = 0f;
            yawInput = 0f;

            if (currentManeuverTime >= splitSDuration / 2f)
            {
                isPerformingSplitS = false;
                pitchInput = 0f; // Reset controls
                rollInput = 0f;
                Debug.Log("Split S Complete");
            }
        }
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

    private void UpdateEngineSound()
    {
        if (engineAudioSource != null)
        {
            // throttleInput is already clamped 0-1
            float throttlePercent = throttleInput; 

            engineAudioSource.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, throttlePercent);
            engineAudioSource.volume = Mathf.Lerp(minEngineVolume, maxEngineVolume, throttlePercent);
        }
    }
}
