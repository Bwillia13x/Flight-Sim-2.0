using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for Lists and Dictionaries

/// <summary>
/// Weapon system handling projectile firing, ammo management, and damage
/// Supports both hitscan and projectile-based weapons with visual effects
/// </summary>
public class WeaponSystem : MonoBehaviour
{
    // Public enum for different ammunition types
    public enum AmmunitionType
    {
        Standard,
        ArmorPiercing,
        Explosive,
        Incendiary
    }

    // Struct to hold properties for each ammunition type
    [System.Serializable]
    public struct AmmoTypeProperties
    {
        public AmmunitionType type;
        public float damageMultiplier;
        public float heatPerShotMultiplier;
        public float projectileSpeedMultiplier;
        public int maxAmmoCount;
        [HideInInspector] public int currentAmmoCount;
        public float explosionRadius; // For explosive rounds

        // Constructor to initialize with default values
        public AmmoTypeProperties(AmmunitionType type, float dmgMult = 1f, float heatMult = 1f, float speedMult = 1f, int maxAmmo = 100, float exploRadius = 0f)
        {
            this.type = type;
            this.damageMultiplier = dmgMult;
            this.heatPerShotMultiplier = heatMult;
            this.projectileSpeedMultiplier = speedMult;
            this.maxAmmoCount = maxAmmo;
            this.currentAmmoCount = maxAmmo; // Initialize current to max
            this.explosionRadius = exploRadius;
        }
    }

    [Header("Weapon Configuration")]
    [SerializeField] private WeaponType weaponType = WeaponType.MachineGun;
    [SerializeField] private float fireRate = 600f;          // Rounds per minute
    [SerializeField] private float damage = 25f; // Base damage, acts as standard if no ammo types defined
    [SerializeField] private float range = 1000f;
    [SerializeField] private float spread = 1f;              // Degrees of inaccuracy
    
    [Header("Ammo System")]
    [SerializeField] private int maxAmmo = 500; // Fallback/Standard max ammo
    [SerializeField] private int currentAmmo = 500; // Fallback/Standard current ammo
    [SerializeField] private float reloadTime = 3f;
    [SerializeField] private bool infiniteAmmo = false;

    [Header("Ammunition Types")]
    [SerializeField] private AmmunitionType currentSelectedAmmoType = AmmunitionType.Standard;
    [SerializeField] private List<AmmoTypeProperties> availableAmmoTypes = new List<AmmoTypeProperties>();
    private Dictionary<AmmunitionType, AmmoTypeProperties> ammoTypePropertiesMap = new Dictionary<AmmunitionType, AmmoTypeProperties>();
    private AmmoTypeProperties _activeAmmoProps; // Changed to _activeAmmoProps

    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 800f;
    [SerializeField] private float projectileLifetime = 5f;
    
    [Header("Visual Effects")]
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private LineRenderer tracerRenderer;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptySound;
    
    [Header("Recoil")]
    [SerializeField] private float recoilForce = 500f;
    [SerializeField] private Vector3 recoilTorque = new Vector3(0.5f, 0f, 0f);

    [Header("Overheating System")]
    public float heatPerShot = 10.0f;
    public float maxHeat = 100.0f;
    public float heatDissipationRate = 20.0f; // Per second
    private float currentHeat = 0f;
    // TODO: Update weapon heat UI element here with currentHeat / maxHeat percentage.
    
    // Private variables
    private float nextFireTime = 0f;
    private bool isReloading = false;
    private int currentFirePoint = 0;
    private Rigidbody aircraftRb;
    private Camera targetingCamera;
    
    // Weapon types
    public enum WeaponType
    {
        MachineGun,
        Cannon,
        Missile,
        Laser
    }
    
    // Public properties
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public float AmmoPercentage => _activeAmmoProps.maxAmmoCount > 0 ? (float)_activeAmmoProps.currentAmmoCount / _activeAmmoProps.maxAmmoCount : 0f;
    public bool CanFire => !isReloading && (infiniteAmmo || _activeAmmoProps.currentAmmoCount > 0) && Time.time >= nextFireTime && currentHeat < maxHeat; // Added heat check to CanFire
    public bool IsReloading => isReloading;
    public AmmunitionType CurrentSelectedAmmoType => currentSelectedAmmoType;
    public AmmoTypeProperties ActiveAmmoProps => _activeAmmoProps; // Public getter for active ammo properties
    public float CurrentHeat => currentHeat; // Public getter for currentHeat

    // Events
    public System.Action OnAmmoChanged;
    public System.Action OnWeaponFired;
    public System.Action OnReloadStarted;
    public System.Action OnReloadCompleted;
    
    private void Awake()
    {
        aircraftRb = GetComponentInParent<Rigidbody>();

        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = 0.7f;
        }

        // Find targeting camera
        targetingCamera = Camera.main;
        if (targetingCamera == null)
        {
            targetingCamera = FindObjectOfType<Camera>();
        }

        // Initialize fire points if not set
        if (firePoints == null || firePoints.Length == 0)
        {
            firePoints = new Transform[] { transform };
        }

        // Setup tracer renderer
        if (tracerRenderer == null && weaponType == WeaponType.Cannon) // Assuming tracer for Cannon (hitscan)
        {
            GameObject tracerObj = new GameObject("TracerRenderer");
            tracerObj.transform.SetParent(transform);
            tracerRenderer = tracerObj.AddComponent<LineRenderer>();
            // Configure tracer material, color, width etc.
            // Example:
            tracerRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            tracerRenderer.startColor = Color.yellow;
            tracerRenderer.endColor = new Color(1f, 1f, 0f, 0.5f); // Fades out
            tracerRenderer.startWidth = 0.05f;
            tracerRenderer.endWidth = 0.01f;
            tracerRenderer.positionCount = 2;
            tracerRenderer.enabled = false;
        }

        // Initialize Ammunition System
        if (availableAmmoTypes.Count > 0)
        {
            ammoTypePropertiesMap.Clear();
            for (int i = 0; i < availableAmmoTypes.Count; i++)
            {
                AmmoTypeProperties props = availableAmmoTypes[i]; // Get a copy
                props.currentAmmoCount = props.maxAmmoCount;      // Initialize current ammo
                availableAmmoTypes[i] = props;                    // Assign the modified copy back
                if (!ammoTypePropertiesMap.ContainsKey(props.type))
                {
                    ammoTypePropertiesMap.Add(props.type, props);
                }
                else
                {
                    Debug.LogWarning($"WeaponSystem: Duplicate ammo type '{props.type}' defined in availableAmmoTypes. Using the first definition.");
                }
            }
            SetActiveAmmoType(currentSelectedAmmoType);
        }
        else
        {
            // Fallback to base stats if no ammo types are defined
            _activeAmmoProps = new AmmoTypeProperties(
                AmmunitionType.Standard,
                1f,
                1f,
                1f,
                this.maxAmmo, // Use the serialized maxAmmo
                0f
            );
            _activeAmmoProps.currentAmmoCount = this.currentAmmo; // Use the serialized currentAmmo
            // Store this default in the map as well, so CycleNextAmmunitionType can work if only one (default) type exists.
            if (!ammoTypePropertiesMap.ContainsKey(AmmunitionType.Standard))
            {
                ammoTypePropertiesMap.Add(AmmunitionType.Standard, _activeAmmoProps);
            }
            // Ensure currentSelectedAmmoType reflects this state
            currentSelectedAmmoType = AmmunitionType.Standard;
            // Update base currentAmmo and maxAmmo to reflect this default state
            this.currentAmmo = _activeAmmoProps.currentAmmoCount;
            this.maxAmmo = _activeAmmoProps.maxAmmoCount;
            OnAmmoChanged?.Invoke();
        }
    }

    private void SetActiveAmmoType(AmmunitionType typeToSet)
    {
        if (ammoTypePropertiesMap.TryGetValue(typeToSet, out AmmoTypeProperties props))
        {
            _activeAmmoProps = props;
            currentSelectedAmmoType = typeToSet;
            // Update the base WeaponSystem currentAmmo and maxAmmo fields
            // This helps with existing UI or systems that might use WeaponSystem.CurrentAmmo directly
            this.maxAmmo = _activeAmmoProps.maxAmmoCount;
            this.currentAmmo = _activeAmmoProps.currentAmmoCount;
            OnAmmoChanged?.Invoke();
        }
        else
        {
            Debug.LogError($"WeaponSystem: Ammunition type '{typeToSet}' not found in ammoTypePropertiesMap.");
            // Attempt to default to the first available type or standard if the requested one is missing
            if (availableAmmoTypes.Count > 0 && ammoTypePropertiesMap.TryGetValue(availableAmmoTypes[0].type, out AmmoTypeProperties firstProps))
            {
                _activeAmmoProps = firstProps;
                currentSelectedAmmoType = firstProps.type;
                this.maxAmmo = _activeAmmoProps.maxAmmoCount;
                this.currentAmmo = _activeAmmoProps.currentAmmoCount;
                Debug.LogWarning($"Defaulting to first available ammo type: {currentSelectedAmmoType}");
                OnAmmoChanged?.Invoke();
            }
            else if (ammoTypePropertiesMap.TryGetValue(AmmunitionType.Standard, out AmmoTypeProperties standardProps)) // Check if Standard was added as fallback
            {
                 _activeAmmoProps = standardProps;
                currentSelectedAmmoType = AmmunitionType.Standard;
                this.maxAmmo = _activeAmmoProps.maxAmmoCount;
                this.currentAmmo = _activeAmmoProps.currentAmmoCount;
                Debug.LogWarning($"Defaulting to Standard ammo type as fallback.");
                OnAmmoChanged?.Invoke();
            }
            else if (availableAmmoTypes.Count == 0) // True fallback if no ammo types were ever defined
            {
                // This case is handled by the initial _activeAmmoProps setup in Awake()
                // Ensure it's consistent.
                 _activeAmmoProps = new AmmoTypeProperties(AmmunitionType.Standard, 1f, 1f, 1f, this.maxAmmo, 0f);
                _activeAmmoProps.currentAmmoCount = this.currentAmmo;
                currentSelectedAmmoType = AmmunitionType.Standard;
                 this.maxAmmo = _activeAmmoProps.maxAmmoCount; // Redundant with initial setup but safe
                 this.currentAmmo = _activeAmmoProps.currentAmmoCount;
                Debug.LogWarning($"No ammo types defined, defaulting to base weapon stats for Standard ammo.");
                OnAmmoChanged?.Invoke();
            }
        }
    }
    
    private void Update()
    {
        HandleInput();
        DissipateHeat();
    }

    private void DissipateHeat()
    {
        if (currentHeat > 0)
        {
            currentHeat -= heatDissipationRate * Time.deltaTime;
            currentHeat = Mathf.Max(0f, currentHeat);
        }
    }
    
    private void HandleInput()
    {
        // Fire input (Space or Left Mouse Button)
        if (Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space))
        {
            Fire();
        }
        
        // Reload input (R key)
        if (Input.GetKeyDown(KeyCode.R))
        {
            Reload();
        }
    }
    
    public void Fire()
    {
        // Use _activeAmmoProps for checks and logic
        if (!isReloading && (infiniteAmmo || _activeAmmoProps.currentAmmoCount > 0) && Time.time >= nextFireTime)
        {
            // Overheating check
            float effectiveHeatPerShot = heatPerShot * _activeAmmoProps.heatPerShotMultiplier;
            if (currentHeat >= maxHeat || (currentHeat + effectiveHeatPerShot > maxHeat && effectiveHeatPerShot > 0))
            {
                Debug.Log("Weapon Overheated!");
                // Optionally, play an overheat sound
                return;
            }

            // Set next fire time based on fire rate
            float fireInterval = 60f / fireRate;
            nextFireTime = Time.time + fireInterval;

            // Consume ammo
            if (!infiniteAmmo)
            {
                _activeAmmoProps.currentAmmoCount--;
                // Update the dictionary as structs are value types
                ammoTypePropertiesMap[currentSelectedAmmoType] = _activeAmmoProps;
                // Also update the base currentAmmo for external systems if they rely on it
                this.currentAmmo = _activeAmmoProps.currentAmmoCount;
                OnAmmoChanged?.Invoke();
            }

            // Fire from current fire point
            Transform firePoint = firePoints[currentFirePoint];
            currentFirePoint = (currentFirePoint + 1) % firePoints.Length;

            // Apply weapon-specific firing logic
            switch (weaponType)
            {
                case WeaponType.MachineGun:
                    FireProjectile(firePoint);
                    break;
                case WeaponType.Cannon:
                    FireHitscan(firePoint);
                    break;
                case WeaponType.Missile:
                    FireMissile(firePoint);
                    break;
                case WeaponType.Laser:
                    FireLaser(firePoint);
                    break;
            }

            // Visual and audio effects
            CreateMuzzleFlash(firePoint);
            PlaySound(fireSound);
            ApplyRecoil();

            // Integrate with audio and VFX managers
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWeaponSound(weaponType, firePoint.position);
            }

            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.CreateMuzzleFlash(firePoint.position, firePoint.forward);
            }

            // Auto-reload if empty
            if (!infiniteAmmo && _activeAmmoProps.currentAmmoCount <= 0)
            {
                StartCoroutine(AutoReload());
            }

            // Apply heat
            currentHeat += effectiveHeatPerShot;
            currentHeat = Mathf.Min(currentHeat, maxHeat); // Clamp heat to maxHeat

            OnWeaponFired?.Invoke();
        }
        else if (!isReloading && (infiniteAmmo || _activeAmmoProps.currentAmmoCount <= 0) && currentHeat < maxHeat) // Check for empty sound
        {
            PlaySound(emptySound);
        }
    }

    private void FireProjectile(Transform firePoint)
    {
        if (projectilePrefab == null) return;

        // Calculate firing direction with spread
        Vector3 fireDirection = CalculateFireDirection(firePoint);

        // Instantiate projectile
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position,
            Quaternion.LookRotation(fireDirection));

        // Set projectile velocity
        Rigidbody projectileRb = projectileObj.GetComponent<Rigidbody>();
        float effectiveProjectileSpeed = projectileSpeed * _activeAmmoProps.projectileSpeedMultiplier;
        if (projectileRb != null)
        {
            // Add aircraft velocity for realistic ballistics
            Vector3 inheritedVelocity = aircraftRb ? aircraftRb.velocity : Vector3.zero;
            projectileRb.velocity = fireDirection * effectiveProjectileSpeed + inheritedVelocity;
        }

        // Setup projectile component
        Projectile projScript = projectileObj.GetComponent<Projectile>();
        if (projScript != null)
        {
            float effectiveDamage = damage * _activeAmmoProps.damageMultiplier;
            projScript.Initialize(effectiveDamage, projectileLifetime, gameObject, currentSelectedAmmoType, _activeAmmoProps.explosionRadius);
        }

        // Destroy after lifetime (already handled in Projectile.Initialize)
        // Destroy(projectileObj, projectileLifetime); // This would be redundant if Projectile handles its own destruction
    }

    private void FireHitscan(Transform firePoint)
    {
        Vector3 fireDirection = CalculateFireDirection(firePoint);
        float effectiveDamage = damage * _activeAmmoProps.damageMultiplier;

        // Raycast for hit detection
        if (Physics.Raycast(firePoint.position, fireDirection, out RaycastHit hit, range))
        {
            // Apply damage
            HealthSystem targetHealth = hit.collider.GetComponent<HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(effectiveDamage);
                // TODO: Consider different damage types for hitscan (e.g., Incendiary might apply a DoT)
            }

            // Create hit effect
            CreateHitEffect(hit.point, hit.normal);

            // Show tracer to hit point
            ShowTracer(firePoint.position, hit.point);
        }
        else
        {
            // Show tracer to max range
            Vector3 endPoint = firePoint.position + fireDirection * range;
            ShowTracer(firePoint.position, endPoint);
        }
    }

    private void FireMissile(Transform firePoint)
    {
        // TODO: Implement guided missile system
        // For now, use projectile firing with current ammo type
        FireProjectile(firePoint);
    }

    private void FireLaser(Transform firePoint)
    {
        // TODO: Implement continuous laser beam
        // For now, use hitscan with current ammo type
        FireHitscan(firePoint);
    }

    private Vector3 CalculateFireDirection(Transform firePoint)
    {
        Vector3 baseDirection = firePoint.forward;
        
        // Add spread (inaccuracy)
        if (spread > 0f)
        {
            Vector3 spreadVector = Random.insideUnitSphere * Mathf.Tan(spread * Mathf.Deg2Rad);
            spreadVector.z = 0; // Keep spread perpendicular to forward direction
            baseDirection = (baseDirection + spreadVector).normalized;
        }
        
        return baseDirection;
    }
    
    private void CreateMuzzleFlash(Transform firePoint)
    {
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            flash.transform.SetParent(firePoint);
            
            // Auto-destroy muzzle flash
            Destroy(flash, 0.1f);
        }
    }
    
    private void CreateHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, 
                Quaternion.LookRotation(normal));
            
            // Auto-destroy hit effect
            Destroy(effect, 2f);
        }
    }
    
    private void ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracerRenderer != null)
        {
            StartCoroutine(ShowTracerCoroutine(start, end));
        }
    }
    
    private IEnumerator ShowTracerCoroutine(Vector3 start, Vector3 end)
    {
        tracerRenderer.enabled = true;
        tracerRenderer.SetPosition(0, start);
        tracerRenderer.SetPosition(1, end);
        
        yield return new WaitForSeconds(0.1f);
        
        tracerRenderer.enabled = false;
    }
    
    private void ApplyRecoil()
    {
        if (aircraftRb != null)
        {
            // Apply recoil force
            Vector3 recoilDirection = -transform.forward;
            aircraftRb.AddForce(recoilDirection * recoilForce, ForceMode.Impulse);
            
            // Apply recoil torque for weapon kick
            aircraftRb.AddTorque(transform.TransformDirection(recoilTorque), ForceMode.Impulse);
        }
    }
    
    public void Reload()
    {
        // Use _activeAmmoProps for checks
        if (isReloading || _activeAmmoProps.currentAmmoCount == _activeAmmoProps.maxAmmoCount) return;

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        OnReloadStarted?.Invoke();

        PlaySound(reloadSound);

        yield return new WaitForSeconds(reloadTime);

        _activeAmmoProps.currentAmmoCount = _activeAmmoProps.maxAmmoCount;
        // Update the dictionary as structs are value types
        ammoTypePropertiesMap[currentSelectedAmmoType] = _activeAmmoProps;
        // Also update the base currentAmmo for external systems
        this.currentAmmo = _activeAmmoProps.currentAmmoCount;

        isReloading = false;

        OnAmmoChanged?.Invoke();
        OnReloadCompleted?.Invoke();
    }

    private IEnumerator AutoReload()
    {
        yield return new WaitForSeconds(0.5f); // Brief delay before auto-reload
        // Use _activeAmmoProps for check
        if (_activeAmmoProps.currentAmmoCount <= 0 && !infiniteAmmo) // Ensure not infinite
        {
            Reload();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // Public methods for external control
    public void SetAmmo(int ammo) // This method might need adjustment based on new ammo system logic
    {
        // This method now primarily affects the *active* ammunition type.
        if (availableAmmoTypes.Count > 0)
        {
            _activeAmmoProps.currentAmmoCount = Mathf.Clamp(ammo, 0, _activeAmmoProps.maxAmmoCount);
            ammoTypePropertiesMap[currentSelectedAmmoType] = _activeAmmoProps;
            this.currentAmmo = _activeAmmoProps.currentAmmoCount; // Update base field
        }
        else // Fallback for no ammo types defined
        {
            this.currentAmmo = Mathf.Clamp(ammo, 0, this.maxAmmo);
            _activeAmmoProps.currentAmmoCount = this.currentAmmo; // Keep active props consistent
        }
        OnAmmoChanged?.Invoke();
    }

    public void AddAmmo(int amount) // This method might need adjustment
    {
        if (availableAmmoTypes.Count > 0)
        {
            SetAmmo(_activeAmmoProps.currentAmmoCount + amount);
        }
        else
        {
             SetAmmo(this.currentAmmo + amount);
        }
    }

    public void CycleNextAmmunitionType()
    {
        if (availableAmmoTypes.Count <= 1) return; // No other types to cycle to

        int currentIndex = -1;
        for (int i = 0; i < availableAmmoTypes.Count; i++)
        {
            if (availableAmmoTypes[i].type == currentSelectedAmmoType)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex != -1)
        {
            int nextIndex = (currentIndex + 1) % availableAmmoTypes.Count;
            SetActiveAmmoType(availableAmmoTypes[nextIndex].type);
        }
        else
        {
            // Should not happen if currentSelectedAmmoType is always valid
            Debug.LogError("Current ammo type not found in available list. Defaulting to first.");
            SetActiveAmmoType(availableAmmoTypes[0].type);
        }
    }

    // Optional: CyclePreviousAmmunitionType()
    public void CyclePreviousAmmunitionType()
    {
        if (availableAmmoTypes.Count <= 1) return;

        int currentIndex = -1;
        for (int i = 0; i < availableAmmoTypes.Count; i++)
        {
            if (availableAmmoTypes[i].type == currentSelectedAmmoType)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex != -1)
        {
            int prevIndex = (currentIndex - 1 + availableAmmoTypes.Count) % availableAmmoTypes.Count;
            SetActiveAmmoType(availableAmmoTypes[prevIndex].type);
        }
        else
        {
            Debug.LogError("Current ammo type not found in available list. Defaulting to first.");
            SetActiveAmmoType(availableAmmoTypes[0].type);
        }
    }


    public void SetWeaponType(WeaponType type)
    {
        weaponType = type;
    }
    
    public void SetInfiniteAmmo(bool infinite)
    {
        infiniteAmmo = infinite;
    }
    
    // TODO: Add weapon upgrade system (could tie into unlocking/improving ammo types)
    // TODO: Add multi-weapon selection (each weapon could have its own ammo types)
}
