using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI controller for enemy aircraft with patrol and pursuit behaviors
/// Features waypoint navigation, player detection, and combat maneuvers
/// </summary>
[RequireComponent(typeof(FlightController))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI Behavior")]
    [SerializeField] private AIState currentState = AIState.Patrol;
    [SerializeField] private float detectionRange = 500f;
    [SerializeField] private float engagementRange = 800f;
    [SerializeField] private float disengageRange = 1200f;
    
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private float waypointRadius = 50f;
    [SerializeField] private float patrolSpeed = 0.6f;
    
    [Header("Combat Settings")]
    [SerializeField] private float pursuitSpeed = 0.9f;
    [SerializeField] private float combatSpeed = 1.0f;
    [SerializeField] private float evasionChance = 0.3f;
    [SerializeField] private float aggressiveness = 0.7f;
    
    [Header("Targeting")]
    [SerializeField] private float aimingAccuracy = 0.8f;
    [SerializeField] private float reactionTime = 0.5f;
    [SerializeField] private LayerMask targetLayers = -1;
    
    // Private variables
    private FlightController flightController;
    private WeaponSystem weaponSystem;
    private Transform player;
    private int currentWaypointIndex = 0;
    private Vector3 targetPosition;
    private float lastReactionTime;
    private float evasionTimer;
    private bool isEvading;
    
    // AI States
    public enum AIState
    {
        Patrol,
        Pursuit,
        Combat,
        Evasion,
        ReturnToPatrol
    }
    
    // Public properties
    public AIState CurrentState => currentState;
    public float DistanceToPlayer => player ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
    
    private void Awake()
    {
        flightController = GetComponent<FlightController>();
        weaponSystem = GetComponent<WeaponSystem>();
        
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        
        // Initialize patrol if waypoints exist
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            targetPosition = patrolWaypoints[0].position;
        }
        else
        {
            // Create random patrol points if none assigned
            GenerateRandomPatrolPoints();
        }
    }
    
    private void Update()
    {
        UpdateAI();
        DebugDrawGizmos();
    }
    
    private void UpdateAI()
    {
        float distanceToPlayer = DistanceToPlayer;
        
        // State transitions
        CheckStateTransitions(distanceToPlayer);
        
        // Execute current state behavior
        switch (currentState)
        {
            case AIState.Patrol:
                PatrolBehavior();
                break;
            case AIState.Pursuit:
                PursuitBehavior();
                break;
            case AIState.Combat:
                CombatBehavior();
                break;
            case AIState.Evasion:
                EvasionBehavior();
                break;
            case AIState.ReturnToPatrol:
                ReturnToPatrolBehavior();
                break;
        }
        
        // Update reaction timing
        lastReactionTime += Time.deltaTime;
    }
    
    private void CheckStateTransitions(float distanceToPlayer)
    {
        switch (currentState)
        {
            case AIState.Patrol:
                if (player && distanceToPlayer <= detectionRange)
                {
                    TransitionToState(AIState.Pursuit);
                }
                break;
                
            case AIState.Pursuit:
                if (!player || distanceToPlayer > disengageRange)
                {
                    TransitionToState(AIState.ReturnToPatrol);
                }
                else if (distanceToPlayer <= engagementRange)
                {
                    TransitionToState(AIState.Combat);
                }
                break;
                
            case AIState.Combat:
                if (!player || distanceToPlayer > disengageRange)
                {
                    TransitionToState(AIState.ReturnToPatrol);
                }
                else if (ShouldEvade())
                {
                    TransitionToState(AIState.Evasion);
                }
                break;
                
            case AIState.Evasion:
                evasionTimer -= Time.deltaTime;
                if (evasionTimer <= 0f)
                {
                    TransitionToState(AIState.Combat);
                }
                break;
                
            case AIState.ReturnToPatrol:
                if (Vector3.Distance(transform.position, targetPosition) < waypointRadius)
                {
                    TransitionToState(AIState.Patrol);
                }
                break;
        }
    }
    
    private void TransitionToState(AIState newState)
    {
        currentState = newState;
        lastReactionTime = 0f;
        
        switch (newState)
        {
            case AIState.Evasion:
                evasionTimer = Random.Range(2f, 4f);
                isEvading = true;
                break;
            case AIState.ReturnToPatrol:
                FindNearestWaypoint();
                break;
            default:
                isEvading = false;
                break;
        }
    }
    
    private void PatrolBehavior()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
        
        // Navigate to current waypoint
        NavigateToPosition(targetPosition, patrolSpeed);
        
        // Check if reached waypoint
        if (Vector3.Distance(transform.position, targetPosition) < waypointRadius)
        {
            // Move to next waypoint
            currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            targetPosition = patrolWaypoints[currentWaypointIndex].position;
        }
    }
    
    private void PursuitBehavior()
    {
        if (!player) return;
        
        // Intercept player position
        Vector3 interceptPoint = CalculateInterceptPoint(player);
        NavigateToPosition(interceptPoint, pursuitSpeed);
    }
    
    private void CombatBehavior()
    {
        if (!player) return;
        
        Vector3 attackPosition = CalculateAttackPosition();
        NavigateToPosition(attackPosition, combatSpeed);
        
        // Fire weapons if in range and aimed
        if (weaponSystem && CanFireAtTarget())
        {
            if (lastReactionTime >= reactionTime)
            {
                weaponSystem.Fire();
                lastReactionTime = 0f;
            }
        }
    }
    
    private void EvasionBehavior()
    {
        // Perform evasive maneuvers
        Vector3 evasionDirection = CalculateEvasionDirection();
        NavigateToPosition(transform.position + evasionDirection * 200f, combatSpeed);
        
        // Random barrel rolls and quick turns
        if (Random.value < 0.1f)
        {
            PerformEvasiveManeuver();
        }
    }
    
    private void ReturnToPatrolBehavior()
    {
        NavigateToPosition(targetPosition, patrolSpeed);
    }
    
    private void NavigateToPosition(Vector3 target, float throttleLevel)
    {
        if (!flightController) return;
        
        // Calculate direction to target
        Vector3 directionToTarget = (target - transform.position).normalized;
        
        // Calculate desired rotation
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        Vector3 targetEuler = targetRotation.eulerAngles;
        Vector3 currentEuler = transform.eulerAngles;
        
        // Convert to -180 to 180 range for proper interpolation
        float deltaYaw = Mathf.DeltaAngle(currentEuler.y, targetEuler.y);
        float deltaPitch = Mathf.DeltaAngle(currentEuler.x, targetEuler.x);
        
        // Apply control inputs
        float pitchInput = Mathf.Clamp(-deltaPitch / 30f, -1f, 1f);
        float rollInput = Mathf.Clamp(deltaYaw / 45f, -1f, 1f);
        float yawInput = Mathf.Clamp(deltaYaw / 60f, -1f, 1f);
        
        // Set flight controller inputs (you'll need to expose these)
        SetFlightInputs(pitchInput, rollInput, yawInput, throttleLevel);
    }
    
    private Vector3 CalculateInterceptPoint(Transform target)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (!targetRb) return target.position;
        
        Vector3 targetVelocity = targetRb.velocity;
        Vector3 relativePosition = target.position - transform.position;
        
        // Simple intercept calculation
        float timeToIntercept = relativePosition.magnitude / (flightController.CurrentSpeed + 50f);
        return target.position + targetVelocity * timeToIntercept;
    }
    
    private Vector3 CalculateAttackPosition()
    {
        if (!player) return transform.position;
        
        // Position behind and slightly above the target for attack run
        Vector3 attackOffset = -player.forward * 300f + player.up * 100f;
        return player.position + attackOffset;
    }
    
    private Vector3 CalculateEvasionDirection()
    {
        // Random evasion direction with some bias away from player
        Vector3 awayFromPlayer = player ? (transform.position - player.position).normalized : Vector3.forward;
        Vector3 randomDirection = Random.insideUnitSphere.normalized;
        
        return Vector3.Slerp(randomDirection, awayFromPlayer, 0.6f);
    }
    
    private bool ShouldEvade()
    {
        return Random.value < evasionChance && !isEvading;
    }
    
    private bool CanFireAtTarget()
    {
        if (!player || !weaponSystem) return false;
        
        // Check if target is in front and within range
        Vector3 directionToTarget = (player.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.forward, directionToTarget);
        
        return dotProduct > aimingAccuracy && DistanceToPlayer <= engagementRange;
    }
    
    private void PerformEvasiveManeuver()
    {
        // Add some random roll input for barrel rolls
        float evasiveRoll = Random.Range(-1f, 1f);
        SetFlightInputs(0f, evasiveRoll, 0f, combatSpeed);
    }
    
    private void SetFlightInputs(float pitch, float roll, float yaw, float throttle)
    {
        if (flightController)
        {
            flightController.SetFlightInputs(pitch, roll, yaw, throttle);
        }
    }
    
    private void FindNearestWaypoint()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
        
        float nearestDistance = float.MaxValue;
        int nearestIndex = 0;
        
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, patrolWaypoints[i].position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }
        
        currentWaypointIndex = nearestIndex;
        targetPosition = patrolWaypoints[currentWaypointIndex].position;
    }
    
    private void GenerateRandomPatrolPoints()
    {
        // Generate random patrol points around starting position
        List<Transform> waypoints = new List<Transform>();
        
        for (int i = 0; i < 4; i++)
        {
            GameObject waypoint = new GameObject($"AI_Waypoint_{i}");
            Vector3 randomPosition = transform.position + Random.insideUnitSphere * 1000f;
            randomPosition.y = Mathf.Max(randomPosition.y, 200f); // Keep above ground
            waypoint.transform.position = randomPosition;
            waypoints.Add(waypoint.transform);
        }
        
        patrolWaypoints = waypoints.ToArray();
        targetPosition = patrolWaypoints[0].position;
    }
    
    private void DebugDrawGizmos()
    {
        // Draw detection range
        if (currentState != AIState.Patrol)
        {
            Debug.DrawWireSphere(transform.position, detectionRange, Color.yellow);
        }
        
        // Draw line to target
        Debug.DrawLine(transform.position, targetPosition, Color.red);
        
        // Draw state info
        Debug.DrawRay(transform.position, transform.up * 20f, GetStateColor());
    }
    
    private Color GetStateColor()
    {
        switch (currentState)
        {
            case AIState.Patrol: return Color.green;
            case AIState.Pursuit: return Color.yellow;
            case AIState.Combat: return Color.red;
            case AIState.Evasion: return Color.magenta;
            case AIState.ReturnToPatrol: return Color.blue;
            default: return Color.white;
        }
    }
    
    // Public methods for external control
    public void SetPatrolWaypoints(Transform[] waypoints)
    {
        patrolWaypoints = waypoints;
        currentWaypointIndex = 0;
        if (waypoints.Length > 0)
        {
            targetPosition = waypoints[0].position;
        }
    }
    
    public void SetAggressiveness(float level)
    {
        aggressiveness = Mathf.Clamp01(level);
        evasionChance = Mathf.Lerp(0.5f, 0.1f, aggressiveness);
        aimingAccuracy = Mathf.Lerp(0.5f, 0.9f, aggressiveness);
    }
    
    // TODO: Add formation flying capabilities
    // TODO: Add different AI personalities (aggressive, defensive, etc.)
    // TODO: Add communication between AI units
    // TODO: Add advanced combat maneuvers (Immelmann, Split-S, etc.)
}
