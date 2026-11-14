using System.Collections;
using UnityEngine;

/// <summary>
/// Fast-paced arcade 2.5D top-down action player controller.
/// Features omnidirectional top-down movement on a horizontal plane,
/// snappy acceleration, friction, instant turn-snapping, evasive combat dash
/// with i-frames and ghost trails, physics knockback handling, 2.5D visual tilt,
/// and strict plane constraints.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Top-Down Movement")]
    [Tooltip("Maximum top-down movement speed.")]
    public float maxSpeed = 12f;

    [Tooltip("Acceleration when moving.")]
    public float acceleration = 90f;

    [Tooltip("Deceleration / friction when stopping.")]
    public float friction = 70f;

    [Tooltip("Instant turn-snapping boost when reversing or sharply changing direction.")]
    public float turnSnapBoost = 130f;

    [Header("Evasive Dash / Dodge")]
    [Tooltip("Dash burst velocity.")]
    public float dashSpeed = 28f;

    [Tooltip("Duration of dash in seconds.")]
    public float dashDuration = 0.16f;

    [Tooltip("Cooldown between dashes.")]
    public float dashCooldown = 0.45f;

    [Header("Game Feel & 2.5D Visuals")]
    [Tooltip("Visual transform to tilt, squash, and rotate (defaults to this transform).")]
    public Transform visualTransform;

    [Tooltip("Degrees of 2.5D lean/tilt based on velocity.")]
    public float maxTiltAngle = 10f;

    [Tooltip("Squash/stretch scale during a dash burst.")]
    public Vector3 dashSquash = new Vector3(1.35f, 0.75f, 1f);

    // Runtime state
    private Vector2 _moveVelocity;
    private Vector2 _knockbackVelocity;
    private Rigidbody2D _rb2D;
    private PlayerHealth _health;
    private bool _isDashing = false;
    private float _nextDashTime = 0f;
    private Vector2 _lastMoveDirection = Vector2.right;
    private Vector3 _baseScale;
    private Camera _mainCam;

    private void Awake()
    {
        _rb2D = GetComponent<Rigidbody2D>();
        _health = GetComponent<PlayerHealth>();
        _mainCam = Camera.main;

        if (visualTransform == null)
            visualTransform = transform;

        _baseScale = visualTransform.localScale;

        if (_rb2D != null)
        {
            _rb2D.gravityScale = 0f; // Pure top-down physics
            _rb2D.freezeRotation = true;
            _rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void Update()
    {
        // 1. Evasive Dash Input: Space, Left Shift, or Right Mouse Button
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            && Time.time >= _nextDashTime && !_isDashing)
        {
            StartCoroutine(PerformDashRoutine());
        }

        // 2. Smooth visual recovery (squash/stretch)
        if (visualTransform != null)
        {
            visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, _baseScale, Time.deltaTime * 14f);
            Apply2DTilt();
        }

        // 3. Strict 2.5D constraint on Z axis
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.z) > 0.001f)
        {
            pos.z = 0f;
            transform.position = pos;
        }
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            ApplyMovement(_moveVelocity * Time.fixedDeltaTime);
            return;
        }

        // Read 8-directional top-down input
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector2 inputDir = new Vector2(inputX, inputY);

        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        if (inputDir.sqrMagnitude > 0.01f)
        {
            _lastMoveDirection = inputDir;
            Vector2 targetVel = inputDir * maxSpeed;

            // Turn-snapping: if steering away from current velocity, boost acceleration
            float currentAccel = acceleration;
            if (Vector2.Dot(_moveVelocity.normalized, inputDir) < 0.2f)
            {
                currentAccel += turnSnapBoost;
            }

            _moveVelocity = Vector2.MoveTowards(_moveVelocity, targetVel, currentAccel * Time.fixedDeltaTime);
        }
        else
        {
            // Decelerate smoothly to halt
            _moveVelocity = Vector2.MoveTowards(_moveVelocity, Vector2.zero, friction * Time.fixedDeltaTime);
        }

        // Knockback decay
        if (_knockbackVelocity.sqrMagnitude > 0.01f)
        {
            _knockbackVelocity = Vector2.MoveTowards(_knockbackVelocity, Vector2.zero, 45f * Time.fixedDeltaTime);
        }
        else
        {
            _knockbackVelocity = Vector2.zero;
        }

        Vector2 totalVelocity = _moveVelocity + _knockbackVelocity;
        ApplyMovement(totalVelocity * Time.fixedDeltaTime);
    }

    private void ApplyMovement(Vector2 delta)
    {
        if (_rb2D != null)
        {
            _rb2D.MovePosition(_rb2D.position + delta);
        }
        else
        {
            transform.Translate(delta);
        }
    }

    private IEnumerator PerformDashRoutine()
    {
        _isDashing = true;
        _nextDashTime = Time.time + dashCooldown;

        // Determine dash direction: current movement input, or toward mouse cursor if stationary
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector2 dashDir = new Vector2(inputX, inputY).normalized;

        if (dashDir.sqrMagnitude < 0.01f)
        {
            // Dash toward mouse cursor
            dashDir = GetDirectionToMouse();
            if (dashDir.sqrMagnitude < 0.01f)
                dashDir = _lastMoveDirection;
        }

        _moveVelocity = dashDir * dashSpeed;
        _knockbackVelocity = Vector2.zero;

        // Grant invincibility frames during dash
        if (_health != null)
        {
            _health.SetInvincible(dashDuration + 0.06f);
        }

        SoundEffects.PlayDash();
        CameraFollow.Shake(0.12f, 0.08f);
        TriggerSquash(dashSquash);
        StartCoroutine(SpawnGhostTrailRoutine(dashDuration));

        yield return new WaitForSeconds(dashDuration);

        _moveVelocity = dashDir * maxSpeed;
        _isDashing = false;
    }

    private Vector2 GetDirectionToMouse()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return _lastMoveDirection;

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPos = ray.GetPoint(enter);
            Vector2 diff = (Vector2)worldPos - (Vector2)transform.position;
            if (diff.sqrMagnitude > 0.01f)
                return diff.normalized;
        }
        return _lastMoveDirection;
    }

    private IEnumerator SpawnGhostTrailRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ghost.transform.position = transform.position;
            ghost.transform.rotation = transform.rotation;
            ghost.transform.localScale = visualTransform != null ? visualTransform.localScale : transform.localScale;

            Destroy(ghost.GetComponent<Collider>());
            var rend = ghost.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.15f, 0.85f, 1f, 0.45f);
            rend.material = mat;

            Destroy(ghost, 0.16f);

            timer += 0.035f;
            yield return new WaitForSeconds(0.035f);
        }
    }

    private void Apply2DTilt()
    {
        // 2.5D visual flair: lean slightly into lateral movement
        float tiltZ = (-_moveVelocity.x / maxSpeed) * maxTiltAngle;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
    }

    private void TriggerSquash(Vector3 targetSquash)
    {
        if (visualTransform != null)
        {
            visualTransform.localScale = Vector3.Scale(_baseScale, targetSquash);
        }
    }

    /// <summary>
    /// Applies an external impulse (e.g. from ricochets, explosions, or recoil kickback).
    /// </summary>
    public void ApplyKnockback(Vector2 force)
    {
        if (!_isDashing)
        {
            _knockbackVelocity += force;
            // Cap maximum knockback to prevent uncontrollable ejections
            if (_knockbackVelocity.magnitude > 35f)
            {
                _knockbackVelocity = _knockbackVelocity.normalized * 35f;
            }
            TriggerSquash(new Vector3(1.15f, 0.85f, 1f));
        }
    }

    // Public getters
    public Vector2 Velocity => _moveVelocity + _knockbackVelocity;
    public bool IsDashing => _isDashing;
    public float DashCooldownRatio => Mathf.Clamp01((_nextDashTime - Time.time) / dashCooldown);
    public Vector2 LastMoveDirection => _lastMoveDirection;
}
