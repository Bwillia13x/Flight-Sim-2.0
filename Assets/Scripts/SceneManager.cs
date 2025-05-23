using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Manages scene configuration and setup for the flight simulator
/// Handles terrain generation, sky settings, lighting, and object placement
/// </summary>
public class SceneManager : MonoBehaviour
{
    [Header("Terrain Settings")]
    [SerializeField] private GameObject terrainPrefab;
    [SerializeField] private float terrainSize = 10000f;
    [SerializeField] private int terrainResolution = 513;
    [SerializeField] private float terrainHeight = 500f;
    
    [Header("Sky and Atmosphere")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private Gradient horizonColor;
    [SerializeField] private Gradient zenithColor;
    [SerializeField] private float fogDistance = 5000f;
    [SerializeField] private Color fogColor = Color.gray;
    
    [Header("Lighting")]
    [SerializeField] private Light sunLight;
    [SerializeField] private AnimationCurve sunIntensityCurve;
    [SerializeField] private float dayDuration = 300f; // 5 minutes
    [SerializeField] private bool enableDynamicLighting = true;
    
    [Header("Weather")]
    [SerializeField] private GameObject[] cloudPrefabs;
    [SerializeField] private int cloudCount = 50;
    [SerializeField] private float cloudAltitude = 2000f;
    [SerializeField] private float cloudSpread = 8000f;
    
    [Header("Spawn Points")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private Transform[] enemySpawnPoints;
    [SerializeField] private Transform[] objectivePoints;
    
    [Header("Environment Objects")]
    [SerializeField] private GameObject[] buildingPrefabs;
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] structurePrefabs;
    [SerializeField] private int maxEnvironmentObjects = 100;
    
    [Header("Mission Settings")]
    [SerializeField] private MissionType currentMission = MissionType.FreeRoam;
    [SerializeField] private int enemyCount = 5;
    [SerializeField] private float missionAreaRadius = 5000f;
    
    // Runtime variables
    private float currentTimeOfDay = 0.5f; // 0 = midnight, 0.5 = noon, 1 = midnight
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    
    // Component references
    private Terrain activeTerrain;
    private Camera mainCamera;
    
    public static SceneManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        InitializeScene();
    }
    
    private void Update()
    {
        if (enableDynamicLighting)
        {
            UpdateTimeOfDay();
            UpdateLighting();
        }
    }
    
    /// <summary>
    /// Initializes the entire scene with terrain, lighting, and objects
    /// </summary>
    public void InitializeScene()
    {
        SetupTerrain();
        SetupSkybox();
        SetupLighting();
        SetupFog();
        GenerateEnvironment();
        SpawnMissionObjects();
        
        Debug.Log($"Scene initialized for mission: {currentMission}");
    }
    
    /// <summary>
    /// Sets up terrain if not already present
    /// </summary>
    private void SetupTerrain()
    {
        activeTerrain = FindObjectOfType<Terrain>();
        
        if (activeTerrain == null && terrainPrefab != null)
        {
            GameObject terrainObject = Instantiate(terrainPrefab);
            activeTerrain = terrainObject.GetComponent<Terrain>();
        }
        
        if (activeTerrain == null)
        {
            // Create terrain procedurally
            CreateProceduralTerrain();
        }
        
        // Configure terrain settings
        if (activeTerrain != null)
        {
            TerrainData terrainData = activeTerrain.terrainData;
            terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);
            
            // Generate height map if needed
            if (terrainData.heightmapResolution != terrainResolution)
            {
                GenerateTerrainHeightmap();
            }
        }
    }
    
    /// <summary>
    /// Creates a procedural terrain
    /// </summary>
    private void CreateProceduralTerrain()
    {
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = terrainResolution;
        terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);
        
        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        activeTerrain = terrainObject.GetComponent<Terrain>();
        
        GenerateTerrainHeightmap();
    }
    
    /// <summary>
    /// Generates a heightmap for the terrain using Perlin noise
    /// </summary>
    private void GenerateTerrainHeightmap()
    {
        if (activeTerrain == null) return;
        
        TerrainData terrainData = activeTerrain.terrainData;
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        
        float[,] heights = new float[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = (float)x / width * 5f;
                float yCoord = (float)y / height * 5f;
                
                // Layer multiple octaves of noise
                float heightValue = 0f;
                heightValue += Mathf.PerlinNoise(xCoord, yCoord) * 0.5f;
                heightValue += Mathf.PerlinNoise(xCoord * 2f, yCoord * 2f) * 0.25f;
                heightValue += Mathf.PerlinNoise(xCoord * 4f, yCoord * 4f) * 0.125f;
                
                heights[x, y] = heightValue;
            }
        }
        
        terrainData.SetHeights(0, 0, heights);
    }
    
    /// <summary>
    /// Sets up the skybox material
    /// </summary>
    private void SetupSkybox()
    {
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
        
        // Configure sky colors based on time of day
        UpdateSkyColors();
    }
    
    /// <summary>
    /// Sets up lighting configuration
    /// </summary>
    private void SetupLighting()
    {
        if (sunLight == null)
        {
            // Find or create the main directional light
            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    sunLight = light;
                    break;
                }
            }
            
            if (sunLight == null)
            {
                GameObject sunObject = new GameObject("Sun");
                sunLight = sunObject.AddComponent<Light>();
                sunLight.type = LightType.Directional;
            }
        }
        
        // Configure shadow settings
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = 0.8f;
        sunLight.shadowResolution = LightShadowResolution.High;
        
        // Set ambient lighting
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.white;
        RenderSettings.ambientEquatorColor = Color.gray;
        RenderSettings.ambientGroundColor = Color.black;
    }
    
    /// <summary>
    /// Sets up fog settings
    /// </summary>
    private void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0001f;
        RenderSettings.fogStartDistance = fogDistance * 0.5f;
        RenderSettings.fogEndDistance = fogDistance;
        RenderSettings.fogColor = fogColor;
    }
    
    /// <summary>
    /// Generates environment objects like buildings, trees, etc.
    /// </summary>
    private void GenerateEnvironment()
    {
        ClearSpawnedObjects();
        
        // Generate buildings in clusters
        GenerateBuildingClusters();
        
        // Generate scattered trees
        GenerateVegetation();
        
        // Generate clouds
        GenerateClouds();
        
        // Generate structures
        GenerateStructures();
    }
    
    /// <summary>
    /// Generates building clusters around the map
    /// </summary>
    private void GenerateBuildingClusters()
    {
        if (buildingPrefabs.Length == 0) return;
        
        int clusterCount = Random.Range(3, 8);
        
        for (int i = 0; i < clusterCount; i++)
        {
            Vector3 clusterCenter = GetRandomGroundPosition(missionAreaRadius * 0.8f);
            int buildingsInCluster = Random.Range(5, 15);
            
            for (int j = 0; j < buildingsInCluster; j++)
            {
                Vector3 buildingPosition = clusterCenter + Random.insideUnitSphere * 200f;
                buildingPosition = GetGroundPosition(buildingPosition);
                
                if (buildingPosition != Vector3.zero)
                {
                    GameObject building = Instantiate(
                        buildingPrefabs[Random.Range(0, buildingPrefabs.Length)],
                        buildingPosition,
                        Quaternion.Euler(0, Random.Range(0, 360), 0)
                    );
                    
                    spawnedObjects.Add(building);
                }
            }
        }
    }
    
    /// <summary>
    /// Generates scattered vegetation
    /// </summary>
    private void GenerateVegetation()
    {
        if (treePrefabs.Length == 0) return;
        
        int treeCount = Random.Range(50, 150);
        
        for (int i = 0; i < treeCount; i++)
        {
            Vector3 treePosition = GetRandomGroundPosition(missionAreaRadius);
            
            if (treePosition != Vector3.zero)
            {
                GameObject tree = Instantiate(
                    treePrefabs[Random.Range(0, treePrefabs.Length)],
                    treePosition,
                    Quaternion.Euler(0, Random.Range(0, 360), 0)
                );
                
                // Random scale variation
                float scale = Random.Range(0.8f, 1.2f);
                tree.transform.localScale = Vector3.one * scale;
                
                spawnedObjects.Add(tree);
            }
        }
    }
    
    /// <summary>
    /// Generates cloud objects in the sky
    /// </summary>
    private void GenerateClouds()
    {
        if (cloudPrefabs.Length == 0) return;
        
        for (int i = 0; i < cloudCount; i++)
        {
            Vector3 cloudPosition = new Vector3(
                Random.Range(-cloudSpread, cloudSpread),
                cloudAltitude + Random.Range(-200f, 200f),
                Random.Range(-cloudSpread, cloudSpread)
            );
            
            GameObject cloud = Instantiate(
                cloudPrefabs[Random.Range(0, cloudPrefabs.Length)],
                cloudPosition,
                Quaternion.Euler(0, Random.Range(0, 360), 0)
            );
            
            // Random scale and movement
            float scale = Random.Range(0.5f, 2f);
            cloud.transform.localScale = Vector3.one * scale;
            
            // Add slow movement script
            CloudMover mover = cloud.AddComponent<CloudMover>();
            mover.moveSpeed = Random.Range(1f, 5f);
            mover.moveDirection = Random.onUnitSphere;
            mover.moveDirection.y = 0; // Keep clouds horizontal
            
            spawnedObjects.Add(cloud);
        }
    }
    
    /// <summary>
    /// Generates various structures
    /// </summary>
    private void GenerateStructures()
    {
        if (structurePrefabs.Length == 0) return;
        
        int structureCount = Random.Range(10, 30);
        
        for (int i = 0; i < structureCount; i++)
        {
            Vector3 structurePosition = GetRandomGroundPosition(missionAreaRadius);
            
            if (structurePosition != Vector3.zero)
            {
                GameObject structure = Instantiate(
                    structurePrefabs[Random.Range(0, structurePrefabs.Length)],
                    structurePosition,
                    Quaternion.Euler(0, Random.Range(0, 360), 0)
                );
                
                spawnedObjects.Add(structure);
            }
        }
    }
    
    /// <summary>
    /// Spawns mission-specific objects and enemies
    /// </summary>
    private void SpawnMissionObjects()
    {
        switch (currentMission)
        {
            case MissionType.Dogfight:
                SpawnEnemies();
                break;
            case MissionType.GroundAttack:
                SpawnGroundTargets();
                SpawnEnemies();
                break;
            case MissionType.Escort:
                SpawnAlliedUnits();
                SpawnEnemies();
                break;
            case MissionType.FreeRoam:
                SpawnEnemies();
                break;
        }
    }
    
    /// <summary>
    /// Spawns enemy aircraft
    /// </summary>
    private void SpawnEnemies()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPosition = GetEnemySpawnPosition();
            
            // This would reference an enemy prefab
            // GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            // spawnedEnemies.Add(enemy);
        }
    }
    
    /// <summary>
    /// Gets a random ground position within the specified radius
    /// </summary>
    private Vector3 GetRandomGroundPosition(float radius)
    {
        Vector3 randomPosition = Random.insideUnitCircle * radius;
        return GetGroundPosition(new Vector3(randomPosition.x, 0, randomPosition.y));
    }
    
    /// <summary>
    /// Gets the ground position at the specified world position
    /// </summary>
    private Vector3 GetGroundPosition(Vector3 worldPosition)
    {
        RaycastHit hit;
        if (Physics.Raycast(worldPosition + Vector3.up * 1000f, Vector3.down, out hit, 2000f))
        {
            return hit.point;
        }
        
        // If raycast fails, use terrain height
        if (activeTerrain != null)
        {
            float height = activeTerrain.SampleHeight(worldPosition);
            return new Vector3(worldPosition.x, height, worldPosition.z);
        }
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Gets a spawn position for enemy aircraft
    /// </summary>
    private Vector3 GetEnemySpawnPosition()
    {
        if (enemySpawnPoints.Length > 0)
        {
            Transform spawnPoint = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
            return spawnPoint.position;
        }
        
        // Generate random position at altitude
        Vector3 position = Random.insideUnitCircle * missionAreaRadius;
        return new Vector3(position.x, Random.Range(500f, 2000f), position.y);
    }
    
    /// <summary>
    /// Updates time of day
    /// </summary>
    private void UpdateTimeOfDay()
    {
        currentTimeOfDay += Time.deltaTime / dayDuration;
        if (currentTimeOfDay >= 1f) currentTimeOfDay = 0f;
    }
    
    /// <summary>
    /// Updates lighting based on time of day
    /// </summary>
    private void UpdateLighting()
    {
        if (sunLight == null) return;
        
        // Update sun rotation (24 hour cycle)
        float sunAngle = currentTimeOfDay * 360f - 90f; // -90 so noon is at the top
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
        
        // Update sun intensity
        float intensity = sunIntensityCurve.Evaluate(currentTimeOfDay);
        sunLight.intensity = intensity;
        
        // Update sky colors
        UpdateSkyColors();
    }
    
    /// <summary>
    /// Updates sky colors based on time of day
    /// </summary>
    private void UpdateSkyColors()
    {
        if (skyboxMaterial != null)
        {
            Color horizon = horizonColor.Evaluate(currentTimeOfDay);
            Color zenith = zenithColor.Evaluate(currentTimeOfDay);
            
            // Update skybox material properties if it supports them
            if (skyboxMaterial.HasProperty("_HorizonColor"))
                skyboxMaterial.SetColor("_HorizonColor", horizon);
            if (skyboxMaterial.HasProperty("_ZenithColor"))
                skyboxMaterial.SetColor("_ZenithColor", zenith);
        }
    }
    
    /// <summary>
    /// Clears all spawned objects
    /// </summary>
    private void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
        
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (obj != null) DestroyImmediate(enemy);
        }
        spawnedEnemies.Clear();
    }
    
    /// <summary>
    /// Sets the current mission type
    /// </summary>
    public void SetMissionType(MissionType missionType)
    {
        currentMission = missionType;
        SpawnMissionObjects();
    }
    
    /// <summary>
    /// Gets the nearest spawn point to a position
    /// </summary>
    public Transform GetNearestSpawnPoint(Vector3 position, Transform[] spawnPoints)
    {
        if (spawnPoints.Length == 0) return null;
        
        Transform nearest = spawnPoints[0];
        float nearestDistance = Vector3.Distance(position, nearest.position);
        
        foreach (Transform spawnPoint in spawnPoints)
        {
            float distance = Vector3.Distance(position, spawnPoint.position);
            if (distance < nearestDistance)
            {
                nearest = spawnPoint;
                nearestDistance = distance;
            }
        }
        
        return nearest;
    }
    
    private void SpawnGroundTargets()
    {
        // Implementation for spawning ground targets
    }
    
    private void SpawnAlliedUnits()
    {
        // Implementation for spawning allied units
    }
}

/// <summary>
/// Simple cloud movement component
/// </summary>
public class CloudMover : MonoBehaviour
{
    public float moveSpeed = 2f;
    public Vector3 moveDirection = Vector3.forward;
    
    private void Update()
    {
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
    }
}

/// <summary>
/// Mission types for the flight simulator
/// </summary>
public enum MissionType
{
    FreeRoam,
    Dogfight,
    GroundAttack,
    Escort,
    Patrol
}
