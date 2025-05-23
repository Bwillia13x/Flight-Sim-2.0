using UnityEngine;

/// <summary>
/// Third-person chase camera with smooth following and look-ahead prediction
/// Provides cinematic camera movement for flight simulation
/// </summary>
public class FlightCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private FlightController targetFlight;
    
    [Header("Camera Position")]
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -15);
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float rotationSpeed = 3f;
    
    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 50f;
    [SerializeField] private float lookAheadSmoothing = 2f;
    
    [Header("Dynamic Adjustments")]
    [SerializeField] private float speedOffsetMultiplier = 0.1f;
    [SerializeField] private float bankingTiltAmount = 0.3f;
    [SerializeField] private float maxBankAngle = 30f;
    
    [Header("Camera Shake")]
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeFrequency = 20f;
    
    private Vector3 currentVelocity;
    private Vector3 lookAheadPoint;
    private float currentBankAngle;
    
    private void Start()
    {
        if (target == null)
        {
            // Try to find the player aircraft
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                targetFlight = player.GetComponent<FlightController>();
            }
        }
        
        if (target != null)
        {
            // Initialize camera position
            transform.position = target.position + target.TransformDirection(offset);
            lookAheadPoint = target.position;
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        UpdateCameraPosition();
        UpdateCameraRotation();
        ApplyCameraShake();
    }
    
    private void UpdateCameraPosition()
    {
        // Calculate dynamic offset based on speed
        Vector3 dynamicOffset = offset;
        if (targetFlight != null)
        {
            float speedFactor = targetFlight.CurrentSpeed * speedOffsetMultiplier;
            dynamicOffset.z -= speedFactor; // Pull back more at higher speeds
        }
        
        // Calculate desired position
        Vector3 desiredPosition = target.position + target.TransformDirection(dynamicOffset);
        
        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, 
            ref currentVelocity, 1f / followSpeed);
    }
    
    private void UpdateCameraRotation()
    {
        // Calculate look-ahead point
        Vector3 targetVelocity = Vector3.zero;
        if (targetFlight != null)
        {
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetVelocity = targetRb.velocity;
            }
        }
        
        Vector3 predictedPosition = target.position + targetVelocity.normalized * lookAheadDistance;
        lookAheadPoint = Vector3.Slerp(lookAheadPoint, predictedPosition, 
            lookAheadSmoothing * Time.deltaTime);
        
        // Calculate banking based on aircraft roll
        float targetBankAngle = 0f;
        if (target != null)
        {
            Vector3 localUp = target.InverseTransformDirection(Vector3.up);
            targetBankAngle = Mathf.Atan2(localUp.x, localUp.y) * Mathf.Rad2Deg;
            targetBankAngle = Mathf.Clamp(targetBankAngle * bankingTiltAmount, -maxBankAngle, maxBankAngle);
        }
        
        currentBankAngle = Mathf.LerpAngle(currentBankAngle, targetBankAngle, 
            rotationSpeed * Time.deltaTime);
        
        // Look at the look-ahead point
        Vector3 lookDirection = (lookAheadPoint - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
        
        // Apply banking
        Quaternion bankRotation = Quaternion.AngleAxis(currentBankAngle, lookDirection);
        Quaternion finalRotation = bankRotation * lookRotation;
        
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, 
            rotationSpeed * Time.deltaTime);
    }
    
    private void ApplyCameraShake()
    {
        if (targetFlight != null && shakeIntensity > 0)
        {
            // More shake at higher speeds and lower altitudes
            float speedFactor = Mathf.Clamp01(targetFlight.CurrentSpeed / 100f);
            float altitudeFactor = Mathf.Clamp01(1f - (targetFlight.Altitude / 1000f));
            float shakeAmount = shakeIntensity * speedFactor * altitudeFactor;
            
            // Generate shake offset
            float shakeX = Mathf.Sin(Time.time * shakeFrequency) * shakeAmount;
            float shakeY = Mathf.Sin(Time.time * shakeFrequency * 1.1f) * shakeAmount;
            
            Vector3 shakeOffset = new Vector3(shakeX, shakeY, 0);
            transform.position += transform.TransformDirection(shakeOffset);
        }
    }
    
    // Public methods for external control
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetFlight = newTarget?.GetComponent<FlightController>();
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void Shake(float intensity, float duration)
    {
        StartCoroutine(ShakeCoroutine(intensity, duration));
    }
    
    private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float originalIntensity = shakeIntensity;
        shakeIntensity = intensity;
        
        yield return new WaitForSeconds(duration);
        
        shakeIntensity = originalIntensity;
    }
    
    // TODO: Add multiple camera modes (cockpit, external, cinematic)
    // TODO: Add smooth transitions between camera modes
    // TODO: Add collision detection to prevent camera clipping
    // TODO: Add zoom functionality for targeting
}
