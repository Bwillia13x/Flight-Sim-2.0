using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Heads-Up Display for flight simulator showing speed, altitude, ammo, health, and radar
/// Provides real-time flight information and combat status
/// </summary>
public class FlightHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlightController playerFlight;
    [SerializeField] private WeaponSystem playerWeapons;
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private Transform playerTransform;
    
    [Header("Speed Indicator")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Slider speedBar;
    [SerializeField] private float maxSpeedDisplay = 400f;
    
    [Header("Altimeter")]
    [SerializeField] private TextMeshProUGUI altitudeText;
    [SerializeField] private Image altitudeBar;
    [SerializeField] private float maxAltitudeDisplay = 15000f;
    
    [Header("Health")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Color healthColorHigh = Color.green;
    [SerializeField] private Color healthColorMid = Color.yellow;
    [SerializeField] private Color healthColorLow = Color.red;
    
    [Header("Ammo")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Slider ammoBar;
    [SerializeField] private Image ammoBarFill;
    [SerializeField] private Color ammoColorHigh = Color.cyan;
    [SerializeField] private Color ammoColorLow = Color.red;
    [SerializeField] private TextMeshProUGUI currentAmmoTypeText; // New field for ammo type name
    
    [Header("Throttle")]
    [SerializeField] private Slider throttleBar;
    [SerializeField] private TextMeshProUGUI throttleText;
    
    [Header("Crosshair")]
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private Color crosshairNormal = Color.white;
    [SerializeField] private Color crosshairTarget = Color.red;
    [SerializeField] private Image crosshairImage;
    
    [Header("Radar")]
    [SerializeField] private MiniRadar miniRadar;
    
    [Header("Warning Systems")]
    [SerializeField] private GameObject stallWarning;
    [SerializeField] private GameObject lowHealthWarning;
    [SerializeField] private GameObject lowAmmoWarning;
    [SerializeField] private TextMeshProUGUI warningText;
    
    [Header("Combat Info")]
    [SerializeField] private TextMeshProUGUI targetInfoText;
    [SerializeField] private GameObject reloadIndicator;
    [SerializeField] private Slider reloadProgress;

    [Header("Weapon Heat")]
    [SerializeField] private Slider weaponHeatBar;      // Slider to display heat level (0-1 range)
    [SerializeField] private TextMeshProUGUI weaponHeatText; // Optional: Text for heat percentage
    [SerializeField] private Image weaponHeatBarFill;    // Optional: Image component of the Slider's Fill area for color changing
    [SerializeField] private Color heatColorLow = Color.blue; // Color for low/normal heat (e.g., blue or green)
    [SerializeField] private Color heatColorHigh = Color.red;   // Color for high heat / overheated
    [SerializeField] private GameObject overheatWarningUI;    // Dedicated UI GameObject to show/hide for overheat warning (e.g., a flashing icon or text)
    
    // Update rates
    private float updateInterval = 0.1f;
    private float lastUpdateTime;
    
    // Warning blink timers
    private float warningBlinkTimer;
    private bool warningVisible = true;
    
    private void Start()
    {
        InitializeHUD();
        FindPlayerComponents();
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateHUD();
            lastUpdateTime = Time.time;
        }
        
        UpdateWarnings();
        UpdateCrosshair();
    }
    
    private void InitializeHUD()
    {
        // Initialize bars and text
        if (speedBar != null) speedBar.maxValue = maxSpeedDisplay;
        if (altitudeBar != null) altitudeBar.fillAmount = 0f;
        if (healthBar != null) healthBar.maxValue = 1f;
        if (ammoBar != null) ammoBar.maxValue = 1f;
        if (throttleBar != null) throttleBar.maxValue = 1f;
        if (weaponHeatBar != null) weaponHeatBar.maxValue = 1f; // Normalized value
        if (overheatWarningUI != null) overheatWarningUI.SetActive(false);
        
        // Hide warnings initially
        SetWarningVisibility(stallWarning, false);
        SetWarningVisibility(lowHealthWarning, false);
        SetWarningVisibility(lowAmmoWarning, false);
        SetWarningVisibility(reloadIndicator, false);
    }
    
    private void FindPlayerComponents()
    {
        // Auto-find player components if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                if (playerFlight == null) playerFlight = player.GetComponent<FlightController>();
                if (playerWeapons == null) playerWeapons = player.GetComponent<WeaponSystem>();
                if (playerHealth == null) playerHealth = player.GetComponent<HealthSystem>();
            }
        }
    }
    
    private void UpdateHUD()
    {
        UpdateSpeedDisplay();
        UpdateAltitudeDisplay();
        UpdateHealthDisplay();
        UpdateAmmoDisplay();
        UpdateThrottleDisplay();
        UpdateReloadDisplay();
        UpdateTargetInfo();
        UpdateWeaponHeatDisplay();
    }
    
    private void UpdateWeaponHeatDisplay()
    {
        if (playerWeapons == null)
        {
            // Optionally disable heat UI elements if no playerWeapons
            if (weaponHeatBar != null) weaponHeatBar.gameObject.SetActive(false);
            if (weaponHeatText != null) weaponHeatText.gameObject.SetActive(false);
            if (overheatWarningUI != null) overheatWarningUI.SetActive(false);
            return;
        }

        // Ensure UI elements are active if playerWeapons is valid
        if (weaponHeatBar != null && !weaponHeatBar.gameObject.activeSelf) weaponHeatBar.gameObject.SetActive(true);
        // Check if the TextMeshPro component itself is null before trying to access its gameObject
        if (weaponHeatText != null && !weaponHeatText.gameObject.activeSelf) weaponHeatText.gameObject.SetActive(true);


        float heatPercentage = 0f;
        if (playerWeapons.maxHeat > 0) // Avoid division by zero
        {
            heatPercentage = Mathf.Clamp01(playerWeapons.CurrentHeat / playerWeapons.maxHeat);
        }

        if (weaponHeatBar != null)
        {
            weaponHeatBar.value = heatPercentage;
        }

        if (weaponHeatText != null)
        {
            weaponHeatText.text = $"HEAT: {heatPercentage * 100f:F0}%";
        }

        if (weaponHeatBarFill != null)
        {
            weaponHeatBarFill.color = Color.Lerp(heatColorLow, heatColorHigh, heatPercentage * heatPercentage); // Square percentage for more pronounced color change at high heat
        }

        bool isOverheated = playerWeapons.CurrentHeat >= playerWeapons.maxHeat && playerWeapons.maxHeat > 0;
        if (overheatWarningUI != null)
        {
            // Use the existing 'warningVisible' from UpdateWarnings for blinking effect
            overheatWarningUI.SetActive(isOverheated && warningVisible);
        }
    }

    private void UpdateSpeedDisplay()
    {
        if (playerFlight == null) return;
        
        float speed = playerFlight.CurrentSpeed;
        
        // Update speed text (convert to km/h for readability)
        if (speedText != null)
        {
            speedText.text = $"{speed * 3.6f:F0} km/h";
        }
        
        // Update speed bar
        if (speedBar != null)
        {
            speedBar.value = speed;
        }
    }
    
    private void UpdateAltitudeDisplay()
    {
        if (playerFlight == null) return;
        
        float altitude = playerFlight.Altitude;
        
        // Update altitude text
        if (altitudeText != null)
        {
            altitudeText.text = $"{altitude:F0}m";
        }
        
        // Update altitude bar
        if (altitudeBar != null)
        {
            altitudeBar.fillAmount = Mathf.Clamp01(altitude / maxAltitudeDisplay);
        }
    }
    
    private void UpdateHealthDisplay()
    {
        if (playerHealth == null) return;
        
        float healthPercentage = playerHealth.HealthPercentage;
        
        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{playerHealth.CurrentHealth:F0}/{playerHealth.MaxHealth:F0}";
        }
        
        // Update health bar
        if (healthBar != null)
        {
            healthBar.value = healthPercentage;
        }
        
        // Update health bar color
        if (healthBarFill != null)
        {
            Color healthColor;
            if (healthPercentage > 0.6f)
                healthColor = healthColorHigh;
            else if (healthPercentage > 0.3f)
                healthColor = Color.Lerp(healthColorMid, healthColorHigh, (healthPercentage - 0.3f) / 0.3f);
            else
                healthColor = Color.Lerp(healthColorLow, healthColorMid, healthPercentage / 0.3f);
                
            healthBarFill.color = healthColor;
        }
    }
    
    private void UpdateAmmoDisplay()
    {
        if (playerWeapons == null) return;
        
        float ammoPercentage = playerWeapons.AmmoPercentage;
        
        // Update ammo text
        if (ammoText != null)
        {
            ammoText.text = $"{playerWeapons.CurrentAmmo}/{playerWeapons.MaxAmmo}";
        }
        
        // Update ammo bar
        if (ammoBar != null)
        {
            ammoBar.value = ammoPercentage;
        }
        
        // Update ammo bar color
        if (ammoBarFill != null)
        {
            Color ammoColor = Color.Lerp(ammoColorLow, ammoColorHigh, ammoPercentage);
            ammoBarFill.color = ammoColor;
        }

        // Update current ammo type text
        if (currentAmmoTypeText != null && playerWeapons != null)
        {
            currentAmmoTypeText.text = playerWeapons.CurrentSelectedAmmoType.ToString().ToUpper(); // Display type name
        }
    }
    
    private void UpdateThrottleDisplay()
    {
        if (playerFlight == null) return;
        
        float throttle = playerFlight.CurrentThrottle;
        
        // Update throttle text
        if (throttleText != null)
        {
            throttleText.text = $"{throttle * 100f:F0}%";
        }
        
        // Update throttle bar
        if (throttleBar != null)
        {
            throttleBar.value = throttle;
        }
    }
    
    private void UpdateReloadDisplay()
    {
        if (playerWeapons == null) return;
        
        bool isReloading = playerWeapons.IsReloading;
        SetWarningVisibility(reloadIndicator, isReloading);
        
        // TODO: Add actual reload progress tracking
        if (reloadProgress != null && isReloading)
        {
            // This would need to be implemented in WeaponSystem
            reloadProgress.value = 0.5f; // Placeholder
        }
    }
    
    private void UpdateTargetInfo()
    {
        // TODO: Implement target locking and distance display
        if (targetInfoText != null)
        {
            targetInfoText.text = ""; // Clear for now
        }
    }
    
    private void UpdateWarnings()
    {
        warningBlinkTimer += Time.deltaTime;
        if (warningBlinkTimer >= 0.5f)
        {
            warningVisible = !warningVisible;
            warningBlinkTimer = 0f;
        }
        
        // Stall warning
        bool stallCondition = playerFlight != null && playerFlight.IsStalling;
        UpdateBlinkingWarning(stallWarning, stallCondition);
        
        // Low health warning
        bool lowHealthCondition = playerHealth != null && playerHealth.HealthPercentage < 0.25f;
        UpdateBlinkingWarning(lowHealthWarning, lowHealthCondition);
        
        // Low ammo warning
        bool lowAmmoCondition = playerWeapons != null && playerWeapons.AmmoPercentage < 0.2f;
        UpdateBlinkingWarning(lowAmmoWarning, lowAmmoCondition);
        
        // Update warning text
        UpdateWarningText(stallCondition, lowHealthCondition, lowAmmoCondition);
    }
    
    private void UpdateBlinkingWarning(GameObject warning, bool condition)
    {
        if (warning == null) return;
        
        if (condition)
        {
            warning.SetActive(warningVisible);
        }
        else
        {
            warning.SetActive(false);
        }
    }
    
    private void UpdateWarningText(bool stall, bool lowHealth, bool lowAmmo)
    {
        if (warningText == null) return;
        
        string warning = "";
        if (stall) warning += "STALL WARNING\n";
        if (lowHealth) warning += "LOW HEALTH\n";
        if (lowAmmo) warning += "LOW AMMO\n";
        
        warningText.text = warning.TrimEnd('\n');
        warningText.gameObject.SetActive(!string.IsNullOrEmpty(warning) && warningVisible);
    }
    
    private void UpdateCrosshair()
    {
        if (crosshair == null) return;
        
        // TODO: Implement target detection for crosshair color change
        bool hasTarget = false; // Placeholder
        
        if (crosshairImage != null)
        {
            crosshairImage.color = hasTarget ? crosshairTarget : crosshairNormal;
        }
        
        // Keep crosshair centered for now
        // In a more advanced system, this could lead targets or show bullet drop
    }
    
    private void SetWarningVisibility(GameObject warning, bool visible)
    {
        if (warning != null)
        {
            warning.SetActive(visible);
        }
    }
    
    // Public methods for external control
    public void SetPlayerReferences(FlightController flight, WeaponSystem weapons, HealthSystem health)
    {
        playerFlight = flight;
        playerWeapons = weapons;
        playerHealth = health;
        playerTransform = flight?.transform;
    }
    
    public void ShowMessage(string message, float duration = 3f)
    {
        if (targetInfoText != null)
        {
            targetInfoText.text = message;
            CancelInvoke(nameof(ClearMessage));
            Invoke(nameof(ClearMessage), duration);
        }
    }
    
    private void ClearMessage()
    {
        if (targetInfoText != null)
        {
            targetInfoText.text = "";
        }
    }
    
    // TODO: Add damage direction indicators
    // TODO: Add target lead indicator
    // TODO: Add waypoint navigation display
    // TODO: Add multiplayer score/kill feed
}
