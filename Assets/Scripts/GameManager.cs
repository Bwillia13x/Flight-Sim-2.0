using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main game manager handling game state, scoring, and scene management
/// Controls overall game flow and coordinates between systems
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private GameMode currentGameMode = GameMode.Dogfight;
    [SerializeField] private int scoreToWin = 10;
    [SerializeField] private float matchTimeLimit = 300f; // 5 minutes
    
    [Header("Player References")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private FlightController playerController;
    
    [Header("Enemy Settings")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] enemySpawnPoints;
    [SerializeField] private int maxEnemies = 3;
    [SerializeField] private float enemyRespawnDelay = 10f;
    
    [Header("UI References")]
    [SerializeField] private FlightHUD gameHUD;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;
    [SerializeField] private TMPro.TextMeshProUGUI timerText;
    
    // Game state
    private GameState currentState = GameState.Playing;
    private int playerScore = 0;
    private int enemyScore = 0;
    private float gameTimer = 0f;
    private bool isPaused = false;
    
    // Enemy management
    private System.Collections.Generic.List<GameObject> activeEnemies = new System.Collections.Generic.List<GameObject>();
    
    public enum GameMode
    {
        Dogfight,
        Survival,
        TimeAttack,
        Tutorial
    }
    
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver,
        Victory
    }
    
    // Events
    public System.Action<int> OnScoreChanged;
    public System.Action<GameState> OnGameStateChanged;
    public System.Action OnGameStart;
    public System.Action OnGameEnd;
    
    // Properties
    public GameState CurrentState => currentState;
    public int PlayerScore => playerScore;
    public float TimeRemaining => Mathf.Max(0f, matchTimeLimit - gameTimer);
    public bool IsPaused => isPaused;
    
    private void Awake()
    {
        // Ensure only one GameManager exists
        if (FindObjectsOfType<GameManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        InitializeGame();
    }
    
    private void Update()
    {
        HandleInput();
        
        if (currentState == GameState.Playing && !isPaused)
        {
            UpdateGameTimer();
            UpdateEnemyCount();
        }
        
        UpdateUI();
    }
    
    private void InitializeGame()
    {
        // Find or spawn player
        SetupPlayer();
        
        // Spawn initial enemies
        SpawnEnemies();
        
        // Setup UI
        SetupUI();
        
        // Start game
        StartGame();
    }
    
    private void SetupPlayer()
    {
        // Find existing player or spawn new one
        GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");
        
        if (existingPlayer != null)
        {
            playerController = existingPlayer.GetComponent<FlightController>();
        }
        else if (playerPrefab != null && playerSpawnPoint != null)
        {
            GameObject player = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
            playerController = player.GetComponent<FlightController>();
        }
        
        // Setup player events
        if (playerController != null)
        {
            HealthSystem playerHealth = playerController.GetComponent<HealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath.AddListener(OnPlayerDeath);
                playerHealth.OnRespawn.AddListener(OnPlayerRespawn);
            }
        }
    }
    
    private void SetupUI()
    {
        // Find HUD if not assigned
        if (gameHUD == null)
        {
            gameHUD = FindObjectOfType<FlightHUD>();
        }
        
        // Setup HUD with player references
        if (gameHUD != null && playerController != null)
        {
            WeaponSystem playerWeapons = playerController.GetComponent<WeaponSystem>();
            HealthSystem playerHealth = playerController.GetComponent<HealthSystem>();
            gameHUD.SetPlayerReferences(playerController, playerWeapons, playerHealth);
        }
        
        // Hide menus initially
        if (pauseMenu != null) pauseMenu.SetActive(false);
        if (gameOverMenu != null) gameOverMenu.SetActive(false);
    }
    
    private void SpawnEnemies()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            SpawnEnemy();
        }
    }
    
    private void SpawnEnemy()
    {
        if (enemyPrefabs.Length == 0 || enemySpawnPoints.Length == 0) return;
        
        // Choose random prefab and spawn point
        GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPoint = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
        
        // Spawn enemy
        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        activeEnemies.Add(enemy);
        
        // Setup enemy events
        HealthSystem enemyHealth = enemy.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath.AddListener(() => OnEnemyDestroyed(enemy));
        }
    }
    
    private void HandleInput()
    {
        // Pause/Resume
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }
        
        // Restart (R key during game over)
        if (Input.GetKeyDown(KeyCode.R) && 
            (currentState == GameState.GameOver || currentState == GameState.Victory))
        {
            RestartGame();
        }
    }
    
    private void UpdateGameTimer()
    {
        gameTimer += Time.deltaTime;
        
        // Check for time limit
        if (matchTimeLimit > 0 && gameTimer >= matchTimeLimit)
        {
            EndGame(playerScore > enemyScore);
        }
    }
    
    private void UpdateEnemyCount()
    {
        // Remove destroyed enemies from list
        activeEnemies.RemoveAll(enemy => enemy == null);
        
        // Spawn new enemies if below max
        while (activeEnemies.Count < maxEnemies)
        {
            Invoke(nameof(SpawnEnemy), enemyRespawnDelay);
            break; // Only spawn one per frame
        }
    }
    
    private void UpdateUI()
    {
        // Update score display
        if (scoreText != null)
        {
            scoreText.text = $"Score: {playerScore}";
        }
        
        // Update timer display
        if (timerText != null && matchTimeLimit > 0)
        {
            float timeLeft = TimeRemaining;
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    public void StartGame()
    {
        SetGameState(GameState.Playing);
        Time.timeScale = 1f;
        isPaused = false;
        gameTimer = 0f;
        
        OnGameStart?.Invoke();
    }
    
    public void PauseGame()
    {
        SetGameState(GameState.Paused);
        Time.timeScale = 0f;
        isPaused = true;
        
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }
    }
    
    public void ResumeGame()
    {
        SetGameState(GameState.Playing);
        Time.timeScale = 1f;
        isPaused = false;
        
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
    }
    
    public void RestartGame()
    {
        // Reset scores and timer
        playerScore = 0;
        enemyScore = 0;
        gameTimer = 0f;
        
        // Hide game over menu
        if (gameOverMenu != null)
        {
            gameOverMenu.SetActive(false);
        }
        
        // Restart current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void EndGame(bool playerWon)
    {
        SetGameState(playerWon ? GameState.Victory : GameState.GameOver);
        
        if (gameOverMenu != null)
        {
            gameOverMenu.SetActive(true);
            
            // Update game over text
            TMPro.TextMeshProUGUI gameOverText = gameOverMenu.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (gameOverText != null)
            {
                gameOverText.text = playerWon ? "VICTORY!" : "GAME OVER";
            }
        }
        
        OnGameEnd?.Invoke();
    }
    
    private void SetGameState(GameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
    
    public void AddScore(int points)
    {
        playerScore += points;
        OnScoreChanged?.Invoke(playerScore);
        
        // Check win condition
        if (scoreToWin > 0 && playerScore >= scoreToWin)
        {
            EndGame(true);
        }
    }
    
    private void OnPlayerDeath()
    {
        // Handle player death based on game mode
        switch (currentGameMode)
        {
            case GameMode.Dogfight:
                // Respawn after delay
                break;
            case GameMode.Survival:
                // Game over
                EndGame(false);
                break;
        }
    }
    
    private void OnPlayerRespawn()
    {
        // Reset any temporary effects
        if (gameHUD != null)
        {
            gameHUD.ShowMessage("Respawned!", 2f);
        }
    }
    
    private void OnEnemyDestroyed(GameObject enemy)
    {
        AddScore(1);
        
        if (gameHUD != null)
        {
            gameHUD.ShowMessage("Enemy Destroyed!", 1.5f);
        }
    }
    
    // Public methods for UI
    public void SetGameMode(GameMode mode)
    {
        currentGameMode = mode;
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
    
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
    
    // TODO: Add save/load system
    // TODO: Add difficulty settings
    // TODO: Add achievement system
    // TODO: Add multiplayer support
}
