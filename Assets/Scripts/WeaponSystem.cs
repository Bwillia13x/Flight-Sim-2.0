using UnityEngine;
using System.Collections;

/// <summary>
/// Weapon system handling projectile firing, ammo management, and damage
/// Supports both hitscan and projectile-based weapons with visual effects
/// </summary>
public class WeaponSystem : MonoBehaviour
{
    [Header("Weapon Configuration")]
    [SerializeField] private WeaponType weaponType = WeaponType.MachineGun;
    [SerializeField] private float fireRate = 600f;          // Rounds per minute
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 1000f;
    [SerializeField] private float spread = 1f;              // Degrees of inaccuracy
    
    [Header("Ammo System")]
    [SerializeField] private int maxAmmo = 500;
    [SerializeField] private int currentAmmo = 500;
    [SerializeField] private float reloadTime = 3f;
    [SerializeField] private bool infiniteAmmo = false;
    
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
    public float AmmoPercentage => maxAmmo > 0 ? (float)currentAmmo / maxAmmo : 0f;
    public bool CanFire => !isReloading && (infiniteAmmo || currentAmmo > 0) && Time.time >= nextFireTime;
    public bool IsReloading => isReloading;
    
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
        if (tracerRenderer == null)
        {
            GameObject tracerObj = new GameObject("TracerRenderer");
            tracerObj.transform.SetParent(transform);
            tracerRenderer = tracerObj.AddComponent<LineRenderer>();
            tracerRenderer.material = new Material(Shader.Find("Sprites/Default"));
            tracerRenderer.color = Color.yellow;
            tracerRenderer.startWidth = 0.1f;
            tracerRenderer.endWidth = 0.05f;
            tracerRenderer.enabled = false;
        }
    }
    
    private void Update()
    {
        HandleInput();
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
        if (!CanFire) 
        {
            // Play empty sound if out of ammo
            if (!infiniteAmmo && currentAmmo <= 0 && !isReloading)
            {
                PlaySound(emptySound);
            }
            return;
        }
        
        // Set next fire time based on fire rate
        float fireInterval = 60f / fireRate;
        nextFireTime = Time.time + fireInterval;
        
        // Consume ammo
        if (!infiniteAmmo)
        {
            currentAmmo--;
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
        if (!infiniteAmmo && currentAmmo <= 0)
        {
            StartCoroutine(AutoReload());
        }
        
        OnWeaponFired?.Invoke();
    }
    
    private void FireProjectile(Transform firePoint)
    {
        if (projectilePrefab == null) return;
        
        // Calculate firing direction with spread
        Vector3 fireDirection = CalculateFireDirection(firePoint);
        
        // Instantiate projectile
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, 
            Quaternion.LookRotation(fireDirection));
        
        // Set projectile velocity
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            // Add aircraft velocity for realistic ballistics
            Vector3 inheritedVelocity = aircraftRb ? aircraftRb.velocity : Vector3.zero;
            projectileRb.velocity = fireDirection * projectileSpeed + inheritedVelocity;
        }
        
        // Setup projectile component
        Projectile projScript = projectile.GetComponent<Projectile>();
        if (projScript != null)
        {
            projScript.Initialize(damage, projectileLifetime, gameObject);
        }
        
        // Destroy after lifetime
        Destroy(projectile, projectileLifetime);
    }
    
    private void FireHitscan(Transform firePoint)
    {
        Vector3 fireDirection = CalculateFireDirection(firePoint);
        
        // Raycast for hit detection
        if (Physics.Raycast(firePoint.position, fireDirection, out RaycastHit hit, range))
        {
            // Apply damage
            HealthSystem targetHealth = hit.collider.GetComponent<HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
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
        // For now, use projectile firing
        FireProjectile(firePoint);
    }
    
    private void FireLaser(Transform firePoint)
    {
        // TODO: Implement continuous laser beam
        // For now, use hitscan
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
        if (isReloading || currentAmmo == maxAmmo) return;
        
        StartCoroutine(ReloadCoroutine());
    }
    
    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        OnReloadStarted?.Invoke();
        
        PlaySound(reloadSound);
        
        yield return new WaitForSeconds(reloadTime);
        
        currentAmmo = maxAmmo;
        isReloading = false;
        
        OnAmmoChanged?.Invoke();
        OnReloadCompleted?.Invoke();
    }
    
    private IEnumerator AutoReload()
    {
        yield return new WaitForSeconds(0.5f); // Brief delay before auto-reload
        if (currentAmmo <= 0)
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
    public void SetAmmo(int ammo)
    {
        currentAmmo = Mathf.Clamp(ammo, 0, maxAmmo);
        OnAmmoChanged?.Invoke();
    }
    
    public void AddAmmo(int amount)
    {
        SetAmmo(currentAmmo + amount);
    }
    
    public void SetWeaponType(WeaponType type)
    {
        weaponType = type;
    }
    
    public void SetInfiniteAmmo(bool infinite)
    {
        infiniteAmmo = infinite;
    }
    
    // TODO: Add weapon overheating mechanics
    // TODO: Add different ammo types (armor-piercing, explosive, etc.)
    // TODO: Add weapon upgrade system
    // TODO: Add multi-weapon selection
}
