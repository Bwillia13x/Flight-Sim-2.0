using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health and damage system for aircraft and objects
/// Handles damage, destruction, and respawn mechanics
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool canRegenerate = false;
    [SerializeField] private float regenerationRate = 5f; // HP per second
    [SerializeField] private float regenerationDelay = 5f; // Seconds before regen starts
    
    [Header("Destruction")]
    [SerializeField] private GameObject destructionEffect;
    [SerializeField] private GameObject wreckagePrefab;
    [SerializeField] private float destructionForce = 1000f;
    
    [Header("Respawn")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private Transform respawnPoint;
    
    [Header("Audio")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip destructionSound;
    [SerializeField] private AudioSource audioSource;
    
    // Private variables
    private float lastDamageTime;
    private bool isDead = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    
    // Events
    [System.Serializable]
    public class HealthEvent : UnityEvent<float> { }
    
    public HealthEvent OnHealthChanged;
    public UnityEvent OnDamageTaken;
    public UnityEvent OnDeath;
    public UnityEvent OnRespawn;
    
    // Public properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead => isDead;
    public bool IsFullHealth => currentHealth >= maxHealth;
    
    private void Awake()
    {
        // Store original position for respawn
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        
        // Setup audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // 3D sound
            }
        }
        
        // Initialize health
        currentHealth = maxHealth;
    }
    
    private void Update()
    {
        if (!isDead && canRegenerate)
        {
            HandleRegeneration();
        }
    }
    
    private void HandleRegeneration()
    {
        // Only regenerate if enough time has passed since last damage
        if (Time.time - lastDamageTime >= regenerationDelay && currentHealth < maxHealth)
        {
            float regenAmount = regenerationRate * Time.deltaTime;
            Heal(regenAmount);
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0) return;
        
        // Apply damage
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        lastDamageTime = Time.time;
        
        // Play damage sound
        PlaySound(damageSound);
        
        // Invoke events
        OnHealthChanged?.Invoke(currentHealth);
        OnDamageTaken?.Invoke();
        
        // Check for death
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
        
        // Visual damage effects
        ShowDamageEffect();
    }
    
    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
        
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
        else if (currentHealth > 0f && isDead)
        {
            Revive();
        }
    }
    
    public void SetMaxHealth(float newMaxHealth)
    {
        float healthRatio = HealthPercentage;
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = maxHealth * healthRatio;
        
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        // Play destruction sound
        PlaySound(destructionSound);
        
        // Create destruction effects
        CreateDestructionEffects();
        
        // Disable components
        DisableComponents();
        
        // Invoke death event
        OnDeath?.Invoke();
        
        // Handle respawn
        if (canRespawn)
        {
            StartCoroutine(RespawnCoroutine());
        }
    }
    
    private void CreateDestructionEffects()
    {
        // Create explosion effect
        if (destructionEffect != null)
        {
            GameObject effect = Instantiate(destructionEffect, transform.position, transform.rotation);
            Destroy(effect, 5f);
        }
        
        // Create wreckage
        if (wreckagePrefab != null)
        {
            GameObject wreckage = Instantiate(wreckagePrefab, transform.position, transform.rotation);
            
            // Apply destruction force to wreckage pieces
            Rigidbody[] wreckagePieces = wreckage.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody piece in wreckagePieces)
            {
                Vector3 explosionDirection = (piece.transform.position - transform.position).normalized;
                piece.AddForce(explosionDirection * destructionForce + Vector3.up * destructionForce * 0.5f, 
                    ForceMode.Impulse);
                piece.AddTorque(Random.insideUnitSphere * destructionForce * 0.1f, ForceMode.Impulse);
            }
            
            // Auto-clean wreckage
            Destroy(wreckage, 30f);
        }
    }
    
    private void DisableComponents()
    {
        // Disable renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }
        
        // Disable colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
        
        // Disable aircraft-specific components
        FlightController flightController = GetComponent<FlightController>();
        if (flightController != null)
        {
            flightController.enabled = false;
        }
        
        WeaponSystem weaponSystem = GetComponent<WeaponSystem>();
        if (weaponSystem != null)
        {
            weaponSystem.enabled = false;
        }
        
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }
    }
    
    private void EnableComponents()
    {
        // Enable renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = true;
        }
        
        // Enable colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;
        }
        
        // Enable aircraft components
        FlightController flightController = GetComponent<FlightController>();
        if (flightController != null)
        {
            flightController.enabled = true;
        }
        
        WeaponSystem weaponSystem = GetComponent<WeaponSystem>();
        if (weaponSystem != null)
        {
            weaponSystem.enabled = true;
        }
        
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = true;
        }
    }
    
    private void Revive()
    {
        isDead = false;
        EnableComponents();
        OnRespawn?.Invoke();
    }
    
    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        
        // Reset position
        Vector3 spawnPosition = respawnPoint ? respawnPoint.position : originalPosition;
        Quaternion spawnRotation = respawnPoint ? respawnPoint.rotation : originalRotation;
        
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        // Reset velocity
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Restore health
        currentHealth = maxHealth;
        
        // Revive
        Revive();
        
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    private void ShowDamageEffect()
    {
        // TODO: Add screen shake, damage indicators, etc.
        // Could flash red, show sparks, create smoke effects
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // Public utility methods
    public void Kill()
    {
        TakeDamage(currentHealth);
    }
    
    public void FullHeal()
    {
        Heal(maxHealth - currentHealth);
    }
    
    public void SetRespawnPoint(Transform newRespawnPoint)
    {
        respawnPoint = newRespawnPoint;
    }
    
    public void SetCanRespawn(bool canRespawnValue)
    {
        canRespawn = canRespawnValue;
    }
    
    // TODO: Add armor/damage resistance system
    // TODO: Add different damage types (kinetic, explosive, energy)
    // TODO: Add critical hit zones
    // TODO: Add temporary invincibility after respawn
}
