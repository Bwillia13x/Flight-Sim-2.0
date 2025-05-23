using UnityEngine;

/// <summary>
/// Projectile behavior for bullets, shells, and missiles
/// Handles movement, collision detection, and damage application
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float explosionRadius = 0f;
    [SerializeField] private bool isExplosive = false;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private TrailRenderer trail;
    
    // Private variables
    private GameObject shooter;
    private Rigidbody rb;
    private bool hasExploded = false;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Setup trail if not assigned
        if (trail == null)
        {
            trail = GetComponent<TrailRenderer>();
        }
    }
    
    public void Initialize(float projectileDamage, float projectileLifetime, GameObject projectileShooter)
    {
        damage = projectileDamage;
        lifetime = projectileLifetime;
        shooter = projectileShooter;
        
        // Destroy after lifetime
        Destroy(gameObject, lifetime);
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
        
        if (isExplosive && explosionRadius > 0)
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
        
        // Find all objects in explosion radius
        Collider[] hitObjects = Physics.OverlapSphere(transform.position, explosionRadius);
        
        foreach (Collider hitObject in hitObjects)
        {
            // Skip the shooter
            if (hitObject.gameObject == shooter || hitObject.transform.IsChildOf(shooter.transform))
                continue;
                
            HealthSystem health = hitObject.GetComponent<HealthSystem>();
            if (health != null)
            {
                // Calculate damage based on distance
                float distance = Vector3.Distance(transform.position, hitObject.transform.position);
                float damageMultiplier = Mathf.Clamp01(1f - (distance / explosionRadius));
                float explosionDamage = damage * damageMultiplier;
                
                health.TakeDamage(explosionDamage);
            }
            
            // Apply explosion force to rigidbodies
            Rigidbody hitRb = hitObject.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(damage * 10f, transform.position, explosionRadius);
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
        if (isExplosive && explosionRadius > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
