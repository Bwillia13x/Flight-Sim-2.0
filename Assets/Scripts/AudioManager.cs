using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Comprehensive audio system for the flight simulator
/// Handles engine sounds, weapon audio, ambient music, and 3D positional audio
/// </summary>
public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class AudioClipGroup
    {
        public string name;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool randomizePitch = false;
        [Range(0f, 0.5f)] public float pitchVariation = 0.1f;
    }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource engineSource;
    [SerializeField] private AudioSource weaponSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource uiSource;
    
    [Header("Engine Audio")]
    [SerializeField] private AudioClipGroup engineIdle;
    [SerializeField] private AudioClipGroup engineLow;
    [SerializeField] private AudioClipGroup engineMid;
    [SerializeField] private AudioClipGroup engineHigh;
    [SerializeField] private AudioClipGroup afterburner;
    [SerializeField] private AudioClipGroup windSound;
    
    [Header("Weapon Audio")]
    [SerializeField] private AudioClipGroup machineGunFire;
    [SerializeField] private AudioClipGroup cannonFire;
    [SerializeField] private AudioClipGroup missileLaunch;
    [SerializeField] private AudioClipGroup missileHit;
    [SerializeField] private AudioClipGroup bulletImpact;
    [SerializeField] private AudioClipGroup explosion;
    
    [Header("UI Audio")]
    [SerializeField] private AudioClipGroup buttonClick;
    [SerializeField] private AudioClipGroup warning;
    [SerializeField] private AudioClipGroup lockOn;
    [SerializeField] private AudioClipGroup radarPing;
    
    [Header("Music")]
    [SerializeField] private AudioClipGroup combatMusic;
    [SerializeField] private AudioClipGroup ambientMusic;
    [SerializeField] private AudioClipGroup menuMusic;
    
    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float engineVolume = 0.8f;
    [Range(0f, 1f)] public float weaponVolume = 0.9f;
    [Range(0f, 1f)] public float uiVolume = 0.6f;
    
    [Header("Engine Audio Curves")]
    [SerializeField] private AnimationCurve thrustPitchCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 2f);
    [SerializeField] private AnimationCurve thrustVolumeCurve = AnimationCurve.Linear(0f, 0.3f, 1f, 1f);
    [SerializeField] private AnimationCurve speedPitchCurve = AnimationCurve.Linear(0f, 0.8f, 300f, 1.5f);
    
    [Header("3D Audio Settings")]
    [SerializeField] private float maxAudioDistance = 1000f;
    [SerializeField] private float dopplerLevel = 1f;
    
    // Component references
    private FlightController flightController;
    private WeaponSystem weaponSystem;
    private HealthSystem healthSystem;
    
    // Current audio state
    private float currentEngineThrust = 0f;
    private float currentSpeed = 0f;
    private bool inCombat = false;
    private Coroutine musicFadeCoroutine;
    
    // Audio object pool for 3D sounds
    private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();
    private List<AudioSource> activeAudioSources = new List<AudioSource>();
    
    public static AudioManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            CreateAudioSourcePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Find component references
        flightController = FindObjectOfType<FlightController>();
        weaponSystem = FindObjectOfType<WeaponSystem>();
        healthSystem = FindObjectOfType<HealthSystem>();
        
        // Subscribe to events
        SubscribeToEvents();
        
        // Start ambient music
        PlayMusic(ambientMusic);
    }
    
    private void Update()
    {
        UpdateEngineAudio();
        UpdateWindAudio();
        CleanupFinishedAudioSources();
    }
    
    /// <summary>
    /// Initializes all audio sources with proper settings
    /// </summary>
    private void InitializeAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.spatialBlend = 0f; // 2D
            musicSource.priority = 64;
        }
        
        if (engineSource == null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.loop = true;
            engineSource.spatialBlend = 0.5f; // Semi-3D
            engineSource.priority = 32;
        }
        
        if (weaponSource == null)
        {
            weaponSource = gameObject.AddComponent<AudioSource>();
            weaponSource.spatialBlend = 0.8f; // Mostly 3D
            weaponSource.priority = 16;
        }
        
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f; // 2D
            ambientSource.priority = 128;
        }
        
        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f; // 2D
            uiSource.priority = 0;
        }
    }
    
    /// <summary>
    /// Creates a pool of audio sources for 3D positional audio
    /// </summary>
    private void CreateAudioSourcePool()
    {
        for (int i = 0; i < 20; i++)
        {
            GameObject audioObject = new GameObject("PooledAudioSource");
            audioObject.transform.SetParent(transform);
            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // Full 3D
            audioSource.maxDistance = maxAudioDistance;
            audioSource.dopplerLevel = dopplerLevel;
            audioObject.SetActive(false);
            audioSourcePool.Enqueue(audioSource);
        }
    }
    
    /// <summary>
    /// Subscribes to relevant game events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (weaponSystem != null)
        {
            // Subscribe to weapon events (would need to be added to WeaponSystem)
            // weaponSystem.OnWeaponFired += PlayWeaponSound;
        }
        
        if (healthSystem != null)
        {
            // Subscribe to damage events
            // healthSystem.OnDamaged += PlayDamageSound;
            // healthSystem.OnDestroyed += PlayExplosionSound;
        }
        
        // Subscribe to input events
        InputManager.OnWeaponCycle += () => PlayUISound(buttonClick);
    }
    
    /// <summary>
    /// Updates engine audio based on thrust and speed
    /// </summary>
    private void UpdateEngineAudio()
    {
        if (flightController == null || engineSource == null) return;
        
        float targetThrust = flightController.GetCurrentThrust();
        float targetSpeed = flightController.GetCurrentSpeed();
        
        // Smooth transitions
        currentEngineThrust = Mathf.Lerp(currentEngineThrust, targetThrust, Time.deltaTime * 2f);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 1f);
        
        // Calculate pitch and volume based on thrust and speed
        float thrustPitch = thrustPitchCurve.Evaluate(currentEngineThrust);
        float speedPitch = speedPitchCurve.Evaluate(currentSpeed);
        float finalPitch = (thrustPitch + speedPitch) * 0.5f;
        
        float thrustVolume = thrustVolumeCurve.Evaluate(currentEngineThrust);
        
        // Apply to engine source
        engineSource.pitch = finalPitch;
        engineSource.volume = thrustVolume * engineVolume * masterVolume;
        
        // Switch engine sounds based on thrust level
        AudioClipGroup targetEngineSound = GetEngineSound(currentEngineThrust);
        if (targetEngineSound.clips.Length > 0 && engineSource.clip != targetEngineSound.clips[0])
        {
            engineSource.clip = targetEngineSound.clips[0];
            if (!engineSource.isPlaying)
                engineSource.Play();
        }
    }
    
    /// <summary>
    /// Gets the appropriate engine sound based on thrust level
    /// </summary>
    private AudioClipGroup GetEngineSound(float thrust)
    {
        if (thrust < 0.2f) return engineIdle;
        else if (thrust < 0.5f) return engineLow;
        else if (thrust < 0.8f) return engineMid;
        else return engineHigh;
    }
    
    /// <summary>
    /// Updates wind audio based on speed
    /// </summary>
    private void UpdateWindAudio()
    {
        if (flightController == null || ambientSource == null) return;
        
        float speed = flightController.GetCurrentSpeed();
        float windVolume = Mathf.Clamp01(speed / 200f) * sfxVolume * masterVolume;
        
        if (windSound.clips.Length > 0)
        {
            if (ambientSource.clip != windSound.clips[0])
            {
                ambientSource.clip = windSound.clips[0];
                ambientSource.Play();
            }
            ambientSource.volume = windVolume;
        }
    }
    
    /// <summary>
    /// Plays a sound from an audio clip group
    /// </summary>
    public void PlaySound(AudioClipGroup clipGroup, Vector3 position = default)
    {
        if (clipGroup.clips.Length == 0) return;
        
        AudioClip clip = clipGroup.clips[Random.Range(0, clipGroup.clips.Length)];
        
        if (position == default)
        {
            PlaySound2D(clip, clipGroup.volume, clipGroup.pitch, clipGroup.randomizePitch, clipGroup.pitchVariation);
        }
        else
        {
            PlaySound3D(clip, position, clipGroup.volume, clipGroup.pitch, clipGroup.randomizePitch, clipGroup.pitchVariation);
        }
    }
    
    /// <summary>
    /// Plays a 2D sound effect
    /// </summary>
    public void PlaySound2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool randomizePitch = false, float pitchVariation = 0.1f)
    {
        if (clip == null) return;
        
        float finalPitch = randomizePitch ? pitch + Random.Range(-pitchVariation, pitchVariation) : pitch;
        uiSource.pitch = finalPitch;
        uiSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }
    
    /// <summary>
    /// Plays a 3D positional sound effect
    /// </summary>
    public void PlaySound3D(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, bool randomizePitch = false, float pitchVariation = 0.1f)
    {
        if (clip == null) return;
        
        AudioSource audioSource = GetPooledAudioSource();
        if (audioSource == null) return;
        
        audioSource.transform.position = position;
        audioSource.clip = clip;
        audioSource.volume = volume * sfxVolume * masterVolume;
        audioSource.pitch = randomizePitch ? pitch + Random.Range(-pitchVariation, pitchVariation) : pitch;
        audioSource.gameObject.SetActive(true);
        audioSource.Play();
        
        activeAudioSources.Add(audioSource);
    }
    
    /// <summary>
    /// Gets a pooled audio source for 3D sounds
    /// </summary>
    private AudioSource GetPooledAudioSource()
    {
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }
        return null;
    }
    
    /// <summary>
    /// Cleans up finished audio sources and returns them to pool
    /// </summary>
    private void CleanupFinishedAudioSources()
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = activeAudioSources[i];
            if (!source.isPlaying)
            {
                source.gameObject.SetActive(false);
                audioSourcePool.Enqueue(source);
                activeAudioSources.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Plays weapon fire sound
    /// </summary>
    public void PlayWeaponSound(WeaponType weaponType, Vector3 position)
    {
        AudioClipGroup soundGroup = null;
        
        switch (weaponType)
        {
            case WeaponType.MachineGun:
                soundGroup = machineGunFire;
                break;
            case WeaponType.Cannon:
                soundGroup = cannonFire;
                break;
            case WeaponType.Missile:
                soundGroup = missileLaunch;
                break;
        }
        
        if (soundGroup != null)
            PlaySound(soundGroup, position);
    }
    
    /// <summary>
    /// Plays UI sound effect
    /// </summary>
    public void PlayUISound(AudioClipGroup clipGroup)
    {
        PlaySound(clipGroup);
    }
    
    /// <summary>
    /// Plays explosion sound at position
    /// </summary>
    public void PlayExplosion(Vector3 position)
    {
        PlaySound(explosion, position);
    }
    
    /// <summary>
    /// Plays bullet impact sound at position
    /// </summary>
    public void PlayBulletImpact(Vector3 position)
    {
        PlaySound(bulletImpact, position);
    }
    
    /// <summary>
    /// Plays background music
    /// </summary>
    public void PlayMusic(AudioClipGroup musicGroup)
    {
        if (musicGroup.clips.Length == 0) return;
        
        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);
        
        musicFadeCoroutine = StartCoroutine(FadeToMusic(musicGroup.clips[Random.Range(0, musicGroup.clips.Length)]));
    }
    
    /// <summary>
    /// Fades between music tracks
    /// </summary>
    private IEnumerator FadeToMusic(AudioClip newClip)
    {
        float fadeTime = 2f;
        float startVolume = musicSource.volume;
        
        // Fade out current music
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }
        
        // Switch to new music
        musicSource.clip = newClip;
        musicSource.Play();
        
        // Fade in new music
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume * masterVolume, t / fadeTime);
            yield return null;
        }
        
        musicSource.volume = musicVolume * masterVolume;
    }
    
    /// <summary>
    /// Sets combat state and changes music accordingly
    /// </summary>
    public void SetCombatState(bool inCombat)
    {
        if (this.inCombat != inCombat)
        {
            this.inCombat = inCombat;
            PlayMusic(inCombat ? combatMusic : ambientMusic);
        }
    }
    
    /// <summary>
    /// Sets master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
    }
    
    /// <summary>
    /// Sets music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
            musicSource.volume = musicVolume * masterVolume;
    }
    
    /// <summary>
    /// Sets SFX volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
    
    /// <summary>
    /// Updates all audio source volumes
    /// </summary>
    private void UpdateAllVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume * masterVolume;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        InputManager.OnWeaponCycle -= () => PlayUISound(buttonClick);
    }
}
