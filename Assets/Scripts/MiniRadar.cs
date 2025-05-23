using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Mini radar system showing nearby aircraft positions and threats
/// Displays player position, enemies, allies, and objectives on a circular radar display
/// </summary>
public class MiniRadar : MonoBehaviour
{
    [Header("Radar Settings")]
    [SerializeField] private float radarRange = 2000f;
    [SerializeField] private RectTransform radarDisplay;
    [SerializeField] private float radarRadius = 100f;
    [SerializeField] private bool rotateWithPlayer = true;
    
    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private FlightController playerFlight;
    
    [Header("Radar Blips")]
    [SerializeField] private GameObject enemyBlipPrefab;
    [SerializeField] private GameObject allyBlipPrefab;
    [SerializeField] private GameObject objectiveBlipPrefab;
    [SerializeField] private GameObject playerBlipPrefab;
    
    [Header("Colors")]
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color allyColor = Color.blue;
    [SerializeField] private Color objectiveColor = Color.yellow;
    [SerializeField] private Color playerColor = Color.green;
    
    [Header("Update Settings")]
    [SerializeField] private float updateRate = 0.2f; // Updates per second
    [SerializeField] private LayerMask radarLayers = -1;
    
    // Private variables
    private Dictionary<Transform, GameObject> trackedObjects = new Dictionary<Transform, GameObject>();
    private List<Transform> enemies = new List<Transform>();
    private List<Transform> allies = new List<Transform>();
    private List<Transform> objectives = new List<Transform>();
    private GameObject playerBlip;
    private float lastUpdateTime;
    
    private void Start()
    {
        InitializeRadar();
        FindPlayerReference();
        CreatePlayerBlip();
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateRate)
        {
            UpdateRadar();
            lastUpdateTime = Time.time;
        }
    }
    
    private void InitializeRadar()
    {
        // Ensure we have a radar display
        if (radarDisplay == null)
        {
            radarDisplay = GetComponent<RectTransform>();
        }
        
        // Create default blip prefabs if not assigned
        CreateDefaultBlipPrefabs();
    }
    
    private void FindPlayerReference()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerFlight = player.GetComponent<FlightController>();
            }
        }
    }
    
    private void CreateDefaultBlipPrefabs()
    {
        if (enemyBlipPrefab == null)
        {
            enemyBlipPrefab = CreateBlipPrefab("EnemyBlip", enemyColor, 4f);
        }
        
        if (allyBlipPrefab == null)
        {
            allyBlipPrefab = CreateBlipPrefab("AllyBlip", allyColor, 3f);
        }
        
        if (objectiveBlipPrefab == null)
        {
            objectiveBlipPrefab = CreateBlipPrefab("ObjectiveBlip", objectiveColor, 5f);
        }
        
        if (playerBlipPrefab == null)
        {
            playerBlipPrefab = CreateBlipPrefab("PlayerBlip", playerColor, 6f);
        }
    }
    
    private GameObject CreateBlipPrefab(string name, Color color, float size)
    {
        GameObject blip = new GameObject(name);
        blip.transform.SetParent(transform, false);
        
        Image image = blip.AddComponent<Image>();
        image.color = color;
        
        RectTransform rectTransform = blip.GetComponent<RectTransform>();
        rectTransform.sizeDelta = Vector2.one * size;
        
        blip.SetActive(false);
        return blip;
    }
    
    private void CreatePlayerBlip()
    {
        if (playerBlipPrefab != null && playerBlip == null)
        {
            playerBlip = Instantiate(playerBlipPrefab, radarDisplay);
            playerBlip.SetActive(true);
            
            // Player is always at center
            RectTransform playerRect = playerBlip.GetComponent<RectTransform>();
            playerRect.anchoredPosition = Vector2.zero;
        }
    }
    
    private void UpdateRadar()
    {
        if (playerTransform == null) return;
        
        // Find all radar targets
        FindRadarTargets();
        
        // Update blip positions
        UpdateBlipPositions();
        
        // Rotate radar if needed
        if (rotateWithPlayer)
        {
            RotateRadarWithPlayer();
        }
        
        // Clean up old blips
        CleanupOldBlips();
    }
    
    private void FindRadarTargets()
    {
        // Clear previous lists
        enemies.Clear();
        allies.Clear();
        objectives.Clear();
        
        // Find all objects within radar range
        Collider[] objectsInRange = Physics.OverlapSphere(playerTransform.position, radarRange, radarLayers);
        
        foreach (Collider obj in objectsInRange)
        {
            if (obj.transform == playerTransform) continue;
            
            // Categorize objects by tag
            switch (obj.tag)
            {
                case "Enemy":
                    enemies.Add(obj.transform);
                    break;
                case "Ally":
                    allies.Add(obj.transform);
                    break;
                case "Objective":
                    objectives.Add(obj.transform);
                    break;
                default:
                    // Check for EnemyAI component
                    if (obj.GetComponent<EnemyAI>() != null)
                    {
                        enemies.Add(obj.transform);
                    }
                    break;
            }
        }
    }
    
    private void UpdateBlipPositions()
    {
        // Update enemy blips
        UpdateBlipCategory(enemies, enemyBlipPrefab, "Enemy");
        
        // Update ally blips
        UpdateBlipCategory(allies, allyBlipPrefab, "Ally");
        
        // Update objective blips
        UpdateBlipCategory(objectives, objectiveBlipPrefab, "Objective");
    }
    
    private void UpdateBlipCategory(List<Transform> targets, GameObject blipPrefab, string category)
    {
        foreach (Transform target in targets)
        {
            if (target == null) continue;
            
            // Get or create blip for this target
            if (!trackedObjects.ContainsKey(target))
            {
                GameObject newBlip = Instantiate(blipPrefab, radarDisplay);
                newBlip.name = $"{category}Blip_{target.name}";
                trackedObjects[target] = newBlip;
            }
            
            GameObject blip = trackedObjects[target];
            if (blip == null) continue;
            
            // Calculate relative position
            Vector3 relativePosition = target.position - playerTransform.position;
            
            // Check if target is within radar range
            float distance = relativePosition.magnitude;
            if (distance > radarRange)
            {
                blip.SetActive(false);
                continue;
            }
            
            // Convert world position to radar position
            Vector2 radarPosition = WorldToRadarPosition(relativePosition);
            
            // Update blip position and visibility
            RectTransform blipRect = blip.GetComponent<RectTransform>();
            blipRect.anchoredPosition = radarPosition;
            blip.SetActive(true);
            
            // Optional: Scale blip based on distance or altitude difference
            UpdateBlipAppearance(blip, target, distance);
        }
    }
    
    private Vector2 WorldToRadarPosition(Vector3 worldPosition)
    {
        // Convert 3D world position to 2D radar position
        Vector2 flatPosition = new Vector2(worldPosition.x, worldPosition.z);
        
        // Apply player rotation if radar rotates with player
        if (rotateWithPlayer && playerTransform != null)
        {
            float playerYaw = playerTransform.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(-playerYaw);
            float sin = Mathf.Sin(-playerYaw);
            
            Vector2 rotatedPosition = new Vector2(
                flatPosition.x * cos - flatPosition.y * sin,
                flatPosition.x * sin + flatPosition.y * cos
            );
            flatPosition = rotatedPosition;
        }
        
        // Scale to radar display size
        Vector2 radarPosition = flatPosition * (radarRadius / radarRange);
        
        // Clamp to radar circle
        if (radarPosition.magnitude > radarRadius)
        {
            radarPosition = radarPosition.normalized * radarRadius;
        }
        
        return radarPosition;
    }
    
    private void UpdateBlipAppearance(GameObject blip, Transform target, float distance)
    {
        Image blipImage = blip.GetComponent<Image>();
        if (blipImage == null) return;
        
        // Fade based on distance
        float alpha = Mathf.Lerp(1f, 0.3f, distance / radarRange);
        Color color = blipImage.color;
        color.a = alpha;
        blipImage.color = color;
        
        // Scale based on altitude difference (optional)
        float altitudeDiff = Mathf.Abs(target.position.y - playerTransform.position.y);
        float scale = Mathf.Lerp(1f, 0.5f, altitudeDiff / 1000f); // Smaller if far above/below
        
        RectTransform blipRect = blip.GetComponent<RectTransform>();
        blipRect.localScale = Vector3.one * scale;
    }
    
    private void RotateRadarWithPlayer()
    {
        if (playerTransform != null && radarDisplay != null)
        {
            // Rotate radar display opposite to player rotation to keep north up
            float playerYaw = playerTransform.eulerAngles.y;
            radarDisplay.rotation = Quaternion.Euler(0, 0, playerYaw);
        }
    }
    
    private void CleanupOldBlips()
    {
        List<Transform> toRemove = new List<Transform>();
        
        foreach (var kvp in trackedObjects)
        {
            Transform target = kvp.Key;
            GameObject blip = kvp.Value;
            
            // Remove if target is destroyed or out of range
            if (target == null || blip == null)
            {
                if (blip != null) Destroy(blip);
                toRemove.Add(target);
            }
            else
            {
                float distance = Vector3.Distance(target.position, playerTransform.position);
                if (distance > radarRange * 1.2f) // Add some hysteresis
                {
                    blip.SetActive(false);
                }
            }
        }
        
        // Remove destroyed objects from tracking
        foreach (Transform target in toRemove)
        {
            trackedObjects.Remove(target);
        }
    }
    
    // Public methods for external control
    public void SetRadarRange(float range)
    {
        radarRange = Mathf.Max(100f, range);
    }
    
    public void SetPlayerReference(Transform player)
    {
        playerTransform = player;
        playerFlight = player?.GetComponent<FlightController>();
    }
    
    public void ToggleRotateWithPlayer()
    {
        rotateWithPlayer = !rotateWithPlayer;
    }
    
    public void AddCustomTarget(Transform target, GameObject customBlipPrefab)
    {
        if (target != null && customBlipPrefab != null)
        {
            GameObject newBlip = Instantiate(customBlipPrefab, radarDisplay);
            trackedObjects[target] = newBlip;
        }
    }
    
    public void RemoveTarget(Transform target)
    {
        if (trackedObjects.ContainsKey(target))
        {
            if (trackedObjects[target] != null)
            {
                Destroy(trackedObjects[target]);
            }
            trackedObjects.Remove(target);
        }
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTransform.position, radarRange);
        }
    }
    
    // TODO: Add threat level indicators (missile locks, etc.)
    // TODO: Add altitude indicators for targets
    // TODO: Add target identification (friendly/hostile/unknown)
    // TODO: Add radar jamming effects
}
