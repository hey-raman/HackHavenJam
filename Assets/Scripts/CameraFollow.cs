using UnityEngine;

/// <summary>
/// Smooth camera follow with look-ahead and integrated screen shake.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Header("Target")]
    [Tooltip("The transform to follow (Player).")]
    public Transform target;

    [Header("Follow Settings")]
    [Tooltip("How long (in seconds) for the camera to reach the target.")]
    public float smoothTime = 0.2f;

    [Tooltip("Maximum speed the camera can move.")]
    public float maxSpeed = 35f;

    [Header("Look-Ahead")]
    [Tooltip("How far ahead of the player the camera peeks in the movement direction.")]
    public float lookAheadDistance = 2.0f;

    [Tooltip("How smoothly the look-ahead offset transitions.")]
    public float lookAheadSmoothTime = 0.35f;

    // Keep the camera Z fixed (2D / 2.5D).
    private float _zDepth;
    private Vector3 _smoothVelocity;
    private Vector2 _lookAheadOffset;
    private Vector2 _lookAheadVelocity;
    private PlayerMovement _playerMovement;

    // Screen shake
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;

    private void Awake()
    {
        Instance = this;
        _zDepth = transform.position.z;

        if (target != null)
            _playerMovement = target.GetComponent<PlayerMovement>();
    }

    /// <summary>
    /// Triggers screen shake with given intensity and duration.
    /// </summary>
    public static void Shake(float intensity, float duration)
    {
        if (Instance != null)
        {
            Instance.TriggerShake(intensity, duration);
        }
    }

    public void TriggerShake(float intensity, float duration)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculate look-ahead: smoothly lean toward player velocity direction.
        Vector2 moveVel = _playerMovement != null ? _playerMovement.Velocity : Vector2.zero;
        if (moveVel.sqrMagnitude > 0.2f)
        {
            Vector2 targetLookAhead = moveVel.normalized * lookAheadDistance;
            _lookAheadOffset = Vector2.SmoothDamp(
                _lookAheadOffset, targetLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);
        }
        else
        {
            _lookAheadOffset = Vector2.SmoothDamp(
                _lookAheadOffset, Vector2.zero, ref _lookAheadVelocity, lookAheadSmoothTime);
        }

        Vector3 desiredPosition = new Vector3(
            target.position.x + _lookAheadOffset.x,
            target.position.y + _lookAheadOffset.y,
            _zDepth
        );

        Vector3 smoothedPos = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref _smoothVelocity, smoothTime, maxSpeed);

        // Apply screen shake offset
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float damper = _shakeDuration > 0f ? (_shakeTimer / _shakeDuration) : 0f;
            float offsetX = (Mathf.PerlinNoise(Time.time * 45f, 0f) - 0.5f) * 2f * _shakeIntensity * damper;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 45f) - 0.5f) * 2f * _shakeIntensity * damper;
            smoothedPos += new Vector3(offsetX, offsetY, 0f);

            if (_shakeTimer <= 0f)
            {
                _shakeIntensity = 0f;
            }
        }

        transform.position = smoothedPos;
    }
}
