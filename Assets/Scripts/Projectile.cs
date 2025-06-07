using UnityEngine;

/// <summary>
/// Projectile behavior for bullets, shells, and missiles
/// Handles movement, collision detection, and damage application
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 25f; // Base damage, will be overridden by Initialize
    [SerializeField] private float lifetime = 5f; // Default lifetime, will be overridden
    [SerializeField] private float explosionRadius = 0f; // Default, might be overridden by ammo type
    [SerializeField] private bool isExplosive = false; // Determined by ammo type
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private TrailRenderer trail;
    
    // Private variables
    private GameObject shooter;
    private Rigidbody rb;
    private bool hasExploded = false;
    private WeaponSystem.AmmunitionType projectileAmmoType; // Added
    private float actualExplosionRadius = 0f; // Added

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Setup trail if not assigned
        if (trail == null)
        {
            trail = GetComponent<TrailRenderer>();
            if (trail) trail.emitting = true; // Ensure trail is emitting if found
        }
    }

    public void Initialize(float projectileDamage, float projectileLifetime, GameObject projectileShooter, WeaponSystem.AmmunitionType ammoType, float explosionRadiusOverride = 0f)
    {
        this.damage = projectileDamage; // Set by WeaponSystem based on base damage + ammo type multiplier
        this.lifetime = projectileLifetime;
        this.shooter = projectileShooter;
        this.projectileAmmoType = ammoType;
        this.actualExplosionRadius = explosionRadiusOverride;

        if (this.actualExplosionRadius > 0f)
        {
            this.isExplosive = true;
        }
        else
        {
            this.isExplosive = false; // Ensure it's false if no radius
        }

        // Destroy after lifetime
        Destroy(gameObject, this.lifetime); // Use the initialized lifetime
    }

    private void OnTriggerEnter(Collider other)
    {
        // Don't hit the shooter
        if (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform))
            return;
            
        // Don't hit other projectiles
        if (other.GetComponent<Projectile>() != null)
            return;
            
        HandleHit(other);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Don't hit the shooter
        if (collision.gameObject == shooter || collision.transform.IsChildOf(shooter.transform))
            return;
            
        HandleHit(collision.collider);
    }
    
    private void HandleHit(Collider hitCollider)
    {
        if (hasExploded) return;

        // Use actualExplosionRadius for check
        if (isExplosive && actualExplosionRadius > 0)
        {
            Explode();
        }
        else
        {
            ApplyDirectDamage(hitCollider);
        }
        
        // Create hit effect
        CreateHitEffect();
        
        // Destroy projectile
        DestroyProjectile();
    }
    
    private void ApplyDirectDamage(Collider target)
    {
        HealthSystem health = target.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }
    }
    
    private void Explode()
    {
        hasExploded = true;

        // Find all objects in explosion radius using actualExplosionRadius
        Collider[] hitObjects = Physics.OverlapSphere(transform.position, actualExplosionRadius);

        foreach (Collider hitObject in hitObjects)
        {
            // Skip the shooter
            if (shooter != null && (hitObject.gameObject == shooter || hitObject.transform.IsChildOf(shooter.transform)))
                continue;

            HealthSystem health = hitObject.GetComponent<HealthSystem>();
            if (health != null)
            {
                // Calculate damage based on distance
                float distance = Vector3.Distance(transform.position, hitObject.transform.position);
                // Ensure actualExplosionRadius is not zero to prevent division by zero
                float damageMultiplier = actualExplosionRadius > 0 ? Mathf.Clamp01(1f - (distance / actualExplosionRadius)) : 1f;
                float explosionDamage = damage * damageMultiplier; // Base damage is already adjusted by ammo type in WeaponSystem

                health.TakeDamage(explosionDamage);
            }

            // Apply explosion force to rigidbodies
            Rigidbody hitRb = hitObject.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                // Use a configurable force magnitude if desired, here using damage as a base
                hitRb.AddExplosionForce(damage * 10f, transform.position, actualExplosionRadius);
            }
        }

        // Create explosion effect
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 5f);
        }
    }
    
    private void CreateHitEffect()
    {
        // TODO: Create impact sparks, smoke, etc.
        // This would depend on what type of surface was hit
    }
    
    private void DestroyProjectile()
    {
        // Disable colliders and renderer
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
        
        // Keep trail for a bit
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, 2f);
        }
        
        // Destroy the projectile
        Destroy(gameObject, 0.1f);
    }
    
    // Visualize explosion radius in editor
    private void OnDrawGizmosSelected()
    {
        // Use actualExplosionRadius for Gizmo
        if (isExplosive && actualExplosionRadius > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, actualExplosionRadius);
        }
    }
}
