using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages all visual effects for the flight simulator including explosions, muzzle flashes, 
/// damage effects, and atmospheric effects
/// </summary>
public class VFXManager : MonoBehaviour
{
    [System.Serializable]
    public class EffectGroup
    {
        public string name;
        public GameObject[] effectPrefabs;
        public float lifetime = 5f;
        public bool useObjectPooling = true;
        public int poolSize = 10;
    }
    
    [Header("Weapon Effects")]
    [SerializeField] private EffectGroup muzzleFlash;
    [SerializeField] private EffectGroup bulletTrail;
    [SerializeField] private EffectGroup missileTrail;
    [SerializeField] private EffectGroup rocketExhaust;
    
    [Header("Explosion Effects")]
    [SerializeField] private EffectGroup smallExplosion;
    [SerializeField] private EffectGroup mediumExplosion;
    [SerializeField] private EffectGroup largeExplosion;
    [SerializeField] private EffectGroup missileExplosion;
    
    [Header("Damage Effects")]
    [SerializeField] private EffectGroup sparks;
    [SerializeField] private EffectGroup smoke;
    [SerializeField] private EffectGroup fire;
    [SerializeField] private EffectGroup debris;
    
    [Header("Engine Effects")]
    [SerializeField] private EffectGroup jetExhaust;
    [SerializeField] private EffectGroup afterburnerFlame;
    [SerializeField] private EffectGroup contrail;
    
    [Header("Environmental Effects")]
    [SerializeField] private EffectGroup cloudPuff;
    [SerializeField] private EffectGroup windEffect;
    [SerializeField] private EffectGroup sonicBoom;
    
    [Header("Impact Effects")]
    [SerializeField] private EffectGroup bulletImpact;
    [SerializeField] private EffectGroup groundImpact;
    [SerializeField] private EffectGroup waterSplash;
    
    // Object pools for effects
    private Dictionary<string, Queue<GameObject>> effectPools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, List<GameObject>> activeEffects = new Dictionary<string, List<GameObject>>();
    
    // Component references
    private Camera mainCamera;
    private Transform cameraTransform;
    
    public static VFXManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEffectPools();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
            cameraTransform = mainCamera.transform;
    }
    
    private void Update()
    {
        CleanupFinishedEffects();
        UpdateContrails();
    }
    
    /// <summary>
    /// Initializes object pools for all effect groups
    /// </summary>
    private void InitializeEffectPools()
    {
        InitializeEffectGroup(muzzleFlash);
        InitializeEffectGroup(bulletTrail);
        InitializeEffectGroup(missileTrail);
        InitializeEffectGroup(rocketExhaust);
        
        InitializeEffectGroup(smallExplosion);
        InitializeEffectGroup(mediumExplosion);
        InitializeEffectGroup(largeExplosion);
        InitializeEffectGroup(missileExplosion);
        
        InitializeEffectGroup(sparks);
        InitializeEffectGroup(smoke);
        InitializeEffectGroup(fire);
        InitializeEffectGroup(debris);
        
        InitializeEffectGroup(jetExhaust);
        InitializeEffectGroup(afterburnerFlame);
        InitializeEffectGroup(contrail);
        
        InitializeEffectGroup(cloudPuff);
        InitializeEffectGroup(windEffect);
        InitializeEffectGroup(sonicBoom);
        
        InitializeEffectGroup(bulletImpact);
        InitializeEffectGroup(groundImpact);
        InitializeEffectGroup(waterSplash);
    }
    
    /// <summary>
    /// Initializes object pool for a specific effect group
    /// </summary>
    private void InitializeEffectGroup(EffectGroup effectGroup)
    {
        if (effectGroup.effectPrefabs == null || effectGroup.effectPrefabs.Length == 0) return;
        
        string groupName = effectGroup.name;
        
        if (effectGroup.useObjectPooling)
        {
            effectPools[groupName] = new Queue<GameObject>();
            activeEffects[groupName] = new List<GameObject>();
            
            // Create pool objects
            for (int i = 0; i < effectGroup.poolSize; i++)
            {
                GameObject prefab = effectGroup.effectPrefabs[Random.Range(0, effectGroup.effectPrefabs.Length)];
                if (prefab != null)
                {
                    GameObject poolObject = Instantiate(prefab, transform);
                    poolObject.SetActive(false);
                    effectPools[groupName].Enqueue(poolObject);
                }
            }
        }
        else
        {
            activeEffects[groupName] = new List<GameObject>();
        }
    }
    
    /// <summary>
    /// Plays an effect from the specified group at the given position
    /// </summary>
    public GameObject PlayEffect(EffectGroup effectGroup, Vector3 position, Quaternion rotation = default, Transform parent = null)
    {
        if (effectGroup.effectPrefabs == null || effectGroup.effectPrefabs.Length == 0) return null;
        
        GameObject effectObject = null;
        string groupName = effectGroup.name;
        
        if (effectGroup.useObjectPooling && effectPools.ContainsKey(groupName))
        {
            // Try to get from pool
            if (effectPools[groupName].Count > 0)
            {
                effectObject = effectPools[groupName].Dequeue();
                effectObject.transform.position = position;
                effectObject.transform.rotation = rotation == default ? Quaternion.identity : rotation;
                effectObject.transform.SetParent(parent);
                effectObject.SetActive(true);
                
                // Reset particle systems
                ParticleSystem[] particles = effectObject.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in particles)
                {
                    ps.Clear();
                    ps.Play();
                }
            }
        }
        
        // If pooling failed or not using pooling, instantiate new object
        if (effectObject == null)
        {
            GameObject prefab = effectGroup.effectPrefabs[Random.Range(0, effectGroup.effectPrefabs.Length)];
            if (prefab != null)
            {
                effectObject = Instantiate(prefab, position, rotation == default ? Quaternion.identity : rotation, parent);
            }
        }
        
        if (effectObject != null)
        {
            activeEffects[groupName].Add(effectObject);
            
            // Auto-destroy if not using pooling
            if (!effectGroup.useObjectPooling)
            {
                StartCoroutine(DestroyEffectAfterDelay(effectObject, groupName, effectGroup.lifetime));
            }
            else
            {
                StartCoroutine(ReturnEffectToPool(effectObject, groupName, effectGroup.lifetime));
            }
        }
        
        return effectObject;
    }
    
    /// <summary>
    /// Creates a muzzle flash effect at weapon position
    /// </summary>
    public void CreateMuzzleFlash(Vector3 position, Vector3 direction)
    {
        Quaternion rotation = Quaternion.LookRotation(direction);
        PlayEffect(muzzleFlash, position, rotation);
    }
    
    /// <summary>
    /// Creates a bullet trail effect
    /// </summary>
    public void CreateBulletTrail(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 direction = (endPosition - startPosition).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);
        
        GameObject trail = PlayEffect(bulletTrail, startPosition, rotation);
        if (trail != null)
        {
            // Scale trail based on distance
            float distance = Vector3.Distance(startPosition, endPosition);
            trail.transform.localScale = new Vector3(1f, 1f, distance);
        }
    }
    
    /// <summary>
    /// Creates an explosion effect based on explosion size
    /// </summary>
    public void CreateExplosion(Vector3 position, ExplosionSize size = ExplosionSize.Medium)
    {
        EffectGroup explosionGroup;
        
        switch (size)
        {
            case ExplosionSize.Small:
                explosionGroup = smallExplosion;
                break;
            case ExplosionSize.Large:
                explosionGroup = largeExplosion;
                break;
            case ExplosionSize.Missile:
                explosionGroup = missileExplosion;
                break;
            default:
                explosionGroup = mediumExplosion;
                break;
        }
        
        PlayEffect(explosionGroup, position);
        
        // Add screen shake for nearby explosions
        if (mainCamera != null)
        {
            float distance = Vector3.Distance(position, cameraTransform.position);
            if (distance < 200f)
            {
                float intensity = Mathf.Lerp(0.5f, 0.05f, distance / 200f);
                StartCoroutine(ScreenShake(intensity, 0.5f));
            }
        }
    }
    
    /// <summary>
    /// Creates damage effects (sparks, smoke, etc.)
    /// </summary>
    public void CreateDamageEffect(Vector3 position, Vector3 normal, DamageType damageType)
    {
        Quaternion rotation = Quaternion.LookRotation(normal);
        
        switch (damageType)
        {
            case DamageType.Bullet:
                PlayEffect(sparks, position, rotation);
                break;
            case DamageType.Explosion:
                PlayEffect(fire, position, rotation);
                PlayEffect(smoke, position, rotation);
                break;
            case DamageType.Impact:
                PlayEffect(debris, position, rotation);
                break;
        }
    }
    
    /// <summary>
    /// Creates engine exhaust effects
    /// </summary>
    public void CreateEngineExhaust(Vector3 position, Vector3 direction, float thrust, bool afterburner = false)
    {
        Quaternion rotation = Quaternion.LookRotation(-direction); // Exhaust goes opposite to thrust direction
        
        if (afterburner)
        {
            GameObject flame = PlayEffect(afterburnerFlame, position, rotation);
            if (flame != null)
            {
                // Scale flame based on thrust
                float scale = Mathf.Lerp(0.5f, 2f, thrust);
                flame.transform.localScale = Vector3.one * scale;
            }
        }
        else
        {
            GameObject exhaust = PlayEffect(jetExhaust, position, rotation);
            if (exhaust != null)
            {
                // Adjust exhaust intensity based on thrust
                ParticleSystem[] particles = exhaust.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in particles)
                {
                    var emission = ps.emission;
                    emission.rateOverTime = Mathf.Lerp(10f, 50f, thrust);
                }
            }
        }
    }
    
    /// <summary>
    /// Creates impact effects based on surface type
    /// </summary>
    public void CreateImpactEffect(Vector3 position, Vector3 normal, SurfaceType surfaceType)
    {
        Quaternion rotation = Quaternion.LookRotation(normal);
        
        switch (surfaceType)
        {
            case SurfaceType.Metal:
                PlayEffect(sparks, position, rotation);
                break;
            case SurfaceType.Ground:
                PlayEffect(groundImpact, position, rotation);
                break;
            case SurfaceType.Water:
                PlayEffect(waterSplash, position, rotation);
                break;
            default:
                PlayEffect(bulletImpact, position, rotation);
                break;
        }
    }
    
    /// <summary>
    /// Creates contrail effects for high-speed flight
    /// </summary>
    public void CreateContrail(Vector3 position, bool enable)
    {
        // This would typically be handled by a persistent effect on the aircraft
        // Implementation depends on specific contrail system design
    }
    
    /// <summary>
    /// Updates contrail effects based on flight conditions
    /// </summary>
    private void UpdateContrails()
    {
        // Update contrail visibility based on altitude, humidity, speed, etc.
        // This is a placeholder for more complex contrail simulation
    }
    
    /// <summary>
    /// Creates a sonic boom effect
    /// </summary>
    public void CreateSonicBoom(Vector3 position)
    {
        PlayEffect(sonicBoom, position);
    }
    
    /// <summary>
    /// Screen shake coroutine for explosion effects
    /// </summary>
    private IEnumerator ScreenShake(float intensity, float duration)
    {
        if (mainCamera == null) yield break;
        
        Vector3 originalPosition = cameraTransform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            
            cameraTransform.localPosition = originalPosition + new Vector3(x, y, 0f);
            
            elapsed += Time.deltaTime;
            intensity = Mathf.Lerp(intensity, 0f, elapsed / duration);
            yield return null;
        }
        
        cameraTransform.localPosition = originalPosition;
    }
    
    /// <summary>
    /// Returns an effect to the object pool after a delay
    /// </summary>
    private IEnumerator ReturnEffectToPool(GameObject effectObject, string groupName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (effectObject != null && activeEffects.ContainsKey(groupName))
        {
            activeEffects[groupName].Remove(effectObject);
            effectObject.SetActive(false);
            effectObject.transform.SetParent(transform);
            
            if (effectPools.ContainsKey(groupName))
            {
                effectPools[groupName].Enqueue(effectObject);
            }
        }
    }
    
    /// <summary>
    /// Destroys an effect after a delay (for non-pooled effects)
    /// </summary>
    private IEnumerator DestroyEffectAfterDelay(GameObject effectObject, string groupName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (effectObject != null && activeEffects.ContainsKey(groupName))
        {
            activeEffects[groupName].Remove(effectObject);
            Destroy(effectObject);
        }
    }
    
    /// <summary>
    /// Cleans up finished particle effects
    /// </summary>
    private void CleanupFinishedEffects()
    {
        foreach (var effectList in activeEffects.Values)
        {
            for (int i = effectList.Count - 1; i >= 0; i--)
            {
                GameObject effect = effectList[i];
                if (effect == null)
                {
                    effectList.RemoveAt(i);
                    continue;
                }
                
                // Check if all particle systems have finished
                ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
                bool allFinished = true;
                
                foreach (ParticleSystem ps in particles)
                {
                    if (ps.isPlaying)
                    {
                        allFinished = false;
                        break;
                    }
                }
                
                // If all particles finished and not pooled, mark for cleanup
                if (allFinished && !effect.activeInHierarchy)
                {
                    effectList.RemoveAt(i);
                }
            }
        }
    }
    
    /// <summary>
    /// Stops all active effects
    /// </summary>
    public void StopAllEffects()
    {
        foreach (var effectList in activeEffects.Values)
        {
            foreach (GameObject effect in effectList)
            {
                if (effect != null)
                {
                    ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
                    foreach (ParticleSystem ps in particles)
                    {
                        ps.Stop();
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Sets the quality level for effects
    /// </summary>
    public void SetEffectQuality(int qualityLevel)
    {
        // Adjust particle counts, distances, and complexity based on quality level
        float qualityMultiplier = qualityLevel / 3f; // Assuming 0-3 quality levels
        
        foreach (var effectList in activeEffects.Values)
        {
            foreach (GameObject effect in effectList)
            {
                if (effect != null)
                {
                    ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
                    foreach (ParticleSystem ps in particles)
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = emission.rateOverTime.constant * qualityMultiplier;
                    }
                }
            }
        }
    }
}

/// <summary>
/// Enumeration for explosion sizes
/// </summary>
public enum ExplosionSize
{
    Small,
    Medium,
    Large,
    Missile
}

/// <summary>
/// Enumeration for damage types
/// </summary>
public enum DamageType
{
    Bullet,
    Explosion,
    Impact
}

/// <summary>
/// Enumeration for surface types
/// </summary>
public enum SurfaceType
{
    Metal,
    Ground,
    Water,
    Wood,
    Concrete
}
