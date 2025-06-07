using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Handles all input for the flight simulator including flight controls, weapons, and UI
/// Supports both keyboard/mouse and gamepad input with customizable sensitivity
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float gamepadSensitivity = 1.5f;
    [SerializeField] private bool invertPitch = false;
    [SerializeField] private bool invertYaw = false;
    [SerializeField] private float deadzone = 0.1f;
    
    [Header("Component References")]
    [SerializeField] private FlightController flightController;
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private FlightCamera flightCamera;
    
    // Input values
    private Vector2 pitchRollInput;
    private float yawInput;
    private float thrustInput;
    private bool fireInput;
    private bool secondaryFireInput;
    private bool cycleWeaponInput; // Existing, likely for switching between multiple weapons
    private bool pauseInput;
    private bool cameraToggleInput;

    // New inputs for ammunition type cycling
    private bool cycleNextAmmoInput;
    private bool cyclePrevAmmoInput;
    
    // Input smoothing
    private Vector2 smoothedPitchRoll;
    private float smoothedYaw;
    private float smoothedThrust;
    
    [Header("Input Smoothing")]
    [SerializeField] private float inputSmoothTime = 0.1f;
    [SerializeField] private float thrustSmoothTime = 0.2f;
    
    // Input velocity for smoothing
    private Vector2 pitchRollVelocity;
    private float yawVelocity;
    private float thrustVelocity;
    
    // Input state tracking
    private bool wasFirePressed;
    private bool wasSecondaryFirePressed;
    private bool wasCycleWeaponPressed;
    private bool wasPausePressed;
    private bool wasCameraTogglePressed;

    // New state tracking for ammo cycling
    private bool wasCycleNextAmmoPressed;
    private bool wasCyclePrevAmmoPressed;
    
    // Events
    public static event Action OnPauseToggle;
    public static event Action OnCameraToggle;
    public static event Action OnWeaponCycle;
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (flightController == null)
            flightController = GetComponent<FlightController>();
        if (weaponSystem == null)
            weaponSystem = GetComponent<WeaponSystem>();
        if (flightCamera == null)
            flightCamera = FindObjectOfType<FlightCamera>();
    }
    
    private void Update()
    {
        HandleInput();
        SmoothInput();
        ApplyInput();
        HandleButtonEvents();
    }
    
    /// <summary>
    /// Reads input from all sources and processes them
    /// </summary>
    private void HandleInput()
    {
        // Flight control inputs
        HandleFlightControlInput();
        
        // Weapon inputs (including new ammo switching)
        HandleWeaponInput(); // This will be modified or a new method will be called
        
        // UI and camera inputs
        HandleUIInput();
    }

    // It might be cleaner to separate ammo switching into its own method
    // For now, modifying HandleWeaponInput as per instructions.
    // private void HandleAmmoSwitchInput() { ... }
    
    /// <summary>
    /// Handles flight control input from keyboard/mouse and gamepad
    /// </summary>
    private void HandleFlightControlInput()
    {
        Vector2 rawPitchRoll = Vector2.zero;
        float rawYaw = 0f;
        float rawThrust = 0f;
        
        // Keyboard input
        if (Input.GetKey(KeyCode.W)) rawPitchRoll.x += 1f; // Pitch up
        if (Input.GetKey(KeyCode.S)) rawPitchRoll.x -= 1f; // Pitch down
        if (Input.GetKey(KeyCode.A)) rawPitchRoll.y -= 1f; // Roll left
        if (Input.GetKey(KeyCode.D)) rawPitchRoll.y += 1f; // Roll right
        if (Input.GetKey(KeyCode.Q)) rawYaw -= 1f; // Yaw left
        if (Input.GetKey(KeyCode.E)) rawYaw += 1f; // Yaw right
        
        // Thrust control
        if (Input.GetKey(KeyCode.LeftShift)) rawThrust += 1f; // Increase thrust
        if (Input.GetKey(KeyCode.LeftControl)) rawThrust -= 1f; // Decrease thrust
        
        // Mouse input for pitch and roll
        if (Input.GetMouseButton(1)) // Right mouse button for mouse look
        {
            Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            rawPitchRoll.x += mouseDelta.y * mouseSensitivity * (invertPitch ? 1f : -1f);
            rawPitchRoll.y += mouseDelta.x * mouseSensitivity;
        }
        
        // Gamepad input
        rawPitchRoll.x += Input.GetAxis("Vertical") * gamepadSensitivity * (invertPitch ? -1f : 1f);
        rawPitchRoll.y += Input.GetAxis("Horizontal") * gamepadSensitivity;
        rawYaw += Input.GetAxis("RightStickHorizontal") * gamepadSensitivity * (invertYaw ? -1f : 1f);
        rawThrust += Input.GetAxis("RightTrigger") - Input.GetAxis("LeftTrigger");
        
        // Apply deadzone
        if (Mathf.Abs(rawPitchRoll.x) < deadzone) rawPitchRoll.x = 0f;
        if (Mathf.Abs(rawPitchRoll.y) < deadzone) rawPitchRoll.y = 0f;
        if (Mathf.Abs(rawYaw) < deadzone) rawYaw = 0f;
        
        // Clamp values
        pitchRollInput = Vector2.ClampMagnitude(rawPitchRoll, 1f);
        yawInput = Mathf.Clamp(rawYaw, -1f, 1f);
        thrustInput = Mathf.Clamp(thrustInput + rawThrust * Time.deltaTime, 0f, 1f);
    }
    
    /// <summary>
    /// Handles weapon input
    /// </summary>
    private void HandleWeaponInput()
    {
        fireInput = Input.GetMouseButton(0) || Input.GetButton("Fire1"); // Standard fire
        secondaryFireInput = Input.GetMouseButton(2) || Input.GetButton("Fire2"); // Could be for missiles or other weapon systems

        // Existing weapon cycling (if multiple weapons are equipped on the player)
        cycleWeaponInput = Input.GetKeyDown(KeyCode.Tab) || Input.GetButtonDown("CycleWeapon");

        // New Ammunition Type Cycling Inputs
        // Using GetKeyDown to ensure it triggers once per press
        cycleNextAmmoInput = Input.GetKeyDown(KeyCode.G); // Example key for next ammo type
        cyclePrevAmmoInput = Input.GetKeyDown(KeyCode.H); // Example key for previous ammo type
    }
    
    /// <summary>
    /// Handles UI and camera input
    /// </summary>
    private void HandleUIInput()
    {
        pauseInput = Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Pause");
        cameraToggleInput = Input.GetKeyDown(KeyCode.C) || Input.GetButtonDown("CameraToggle");
    }
    
    /// <summary>
    /// Applies smoothing to input values for better feel
    /// </summary>
    private void SmoothInput()
    {
        smoothedPitchRoll = Vector2.SmoothDamp(smoothedPitchRoll, pitchRollInput, ref pitchRollVelocity, inputSmoothTime);
        smoothedYaw = Mathf.SmoothDamp(smoothedYaw, yawInput, ref yawVelocity, inputSmoothTime);
        smoothedThrust = Mathf.SmoothDamp(smoothedThrust, thrustInput, ref thrustVelocity, thrustSmoothTime);
    }
    
    /// <summary>
    /// Applies processed input to game systems
    /// </summary>
    private void ApplyInput()
    {
        if (flightController != null)
        {
            flightController.SetPitchInput(smoothedPitchRoll.x);
            flightController.SetRollInput(smoothedPitchRoll.y);
            flightController.SetYawInput(smoothedYaw);
            flightController.SetThrustInput(smoothedThrust);
        }
        
        if (weaponSystem != null)
        {
            // Assuming FirePrimary maps to the main Fire method in WeaponSystem
            if (fireInput)
                weaponSystem.Fire();

            // Assuming FireSecondary might be for a different weapon or mode not yet fully implemented
            // if (secondaryFireInput)
            //     weaponSystem.FireSecondary(); // Or some other method
        }
    }
    
    /// <summary>
    /// Handles button press events that should only trigger once per press
    /// </summary>
    private void HandleButtonEvents()
    {
        // Weapon cycling (existing, for switching between different weapons if applicable)
        if (cycleWeaponInput && !wasCycleWeaponPressed)
        {
            OnWeaponCycle?.Invoke();
            // if (weaponSystem != null) // Assuming weaponSystem might have a method like CycleEquippedWeapon()
            //     weaponSystem.CycleEquippedWeapon(); // Placeholder for actual multi-weapon cycling
            Debug.Log("Cycle Weapon Input Pressed (TAB)"); // Placeholder action
        }
        
        // Ammunition Type Cycling (New)
        if (cycleNextAmmoInput && !wasCycleNextAmmoPressed)
        {
            if (weaponSystem != null)
            {
                weaponSystem.CycleNextAmmunitionType();
                Debug.Log("Cycle Next Ammo Type Input Pressed (G)"); // For testing
            }
        }
        if (cyclePrevAmmoInput && !wasCyclePrevAmmoPressed)
        {
            if (weaponSystem != null)
            {
                weaponSystem.CyclePreviousAmmunitionType();
                Debug.Log("Cycle Previous Ammo Type Input Pressed (H)"); // For testing
            }
        }

        // Pause toggle
        if (pauseInput && !wasPausePressed)
        {
            OnPauseToggle?.Invoke();
        }
        
        // Camera toggle
        if (cameraToggleInput && !wasCameraTogglePressed)
        {
            OnCameraToggle?.Invoke();
            if (flightCamera != null)
                flightCamera.ToggleCameraMode();
        }
        
        // Update previous frame state
        wasCycleWeaponPressed = cycleWeaponInput;
        wasPausePressed = pauseInput;
        wasCameraTogglePressed = cameraToggleInput;
        // Update previous frame state for new inputs
        wasCycleNextAmmoPressed = cycleNextAmmoInput;
        wasCyclePrevAmmoPressed = cyclePrevAmmoInput;
    }
    
    /// <summary>
    /// Sets input sensitivity for mouse control
    /// </summary>
    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 10f);
    }
    
    /// <summary>
    /// Sets input sensitivity for gamepad control
    /// </summary>
    public void SetGamepadSensitivity(float sensitivity)
    {
        gamepadSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
    }
    
    /// <summary>
    /// Toggles pitch inversion
    /// </summary>
    public void SetInvertPitch(bool invert)
    {
        invertPitch = invert;
    }
    
    /// <summary>
    /// Toggles yaw inversion
    /// </summary>
    public void SetInvertYaw(bool invert)
    {
        invertYaw = invert;
    }
    
    /// <summary>
    /// Sets the input deadzone for analog controls
    /// </summary>
    public void SetDeadzone(float deadzone)
    {
        this.deadzone = Mathf.Clamp01(deadzone);
    }
    
    /// <summary>
    /// Gets current thrust input value
    /// </summary>
    public float GetThrustInput()
    {
        return smoothedThrust;
    }
    
    /// <summary>
    /// Gets current pitch and roll input values
    /// </summary>
    public Vector2 GetPitchRollInput()
    {
        return smoothedPitchRoll;
    }
    
    /// <summary>
    /// Gets current yaw input value
    /// </summary>
    public float GetYawInput()
    {
        return smoothedYaw;
    }
    
    /// <summary>
    /// Enables or disables input processing
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            // Reset all inputs when disabled
            pitchRollInput = Vector2.zero;
            yawInput = 0f;
            fireInput = false;
            secondaryFireInput = false;
        }
    }
    
    private void OnValidate()
    {
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 10f);
        gamepadSensitivity = Mathf.Clamp(gamepadSensitivity, 0.1f, 5f);
        deadzone = Mathf.Clamp01(deadzone);
        inputSmoothTime = Mathf.Clamp(inputSmoothTime, 0.01f, 1f);
        thrustSmoothTime = Mathf.Clamp(thrustSmoothTime, 0.01f, 1f);
    }
}
