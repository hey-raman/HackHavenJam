using UnityEngine;

/// <summary>
/// Fast-paced ricocheting arrow projectile for 2.5D action.
/// Features continuous sweep collision detection to prevent tunneling at extreme speeds,
/// physics reflection based on surface normals with exaggerated bounce spread,
/// speed preservation across bounces, impact spark bursts, audio triggers,
/// damage/knockback to enemies, and danger to the player after a brief grace window.
/// </summary>
public class RicochetArrow : MonoBehaviour
{
    [Header("Speed & Lifetime")]
    [Tooltip("Base projectile speed (units/second).")]
    public float speed = 42f;

    [Tooltip("How long the arrow stays active in the arena.")]
    public float lifetime = 7.5f;

    [Tooltip("Maximum number of ricochets before dissolving.")]
    public int maxBounces = 25;

    [Tooltip("Speed multiplier applied on each ricochet (keeps bounces high-energy).")]
    public float bounceSpeedMultiplier = 1.02f;

    [Header("Ricochet Physics")]
    [Tooltip("Collision detection sweep radius.")]
    public float radius = 0.12f;

    [Tooltip("Exaggerated random angle perturbation added to bounce reflection (degrees).")]
    public float exaggerationAngle = 4.5f;

    [Tooltip("Layers the arrow can bounce off.")]
    public LayerMask collisionMask = ~0;

    [Header("Damage & Knockback")]
    [Tooltip("Damage dealt to enemies or player.")]
    public float damage = 25f;

    [Tooltip("Physics impulse applied to hit targets.")]
    public float knockbackForce = 15f;

    [Tooltip("Grace period (seconds) before arrow can hit its own shooter.")]
    public float shooterArmDelay = 0.15f;

    [Header("Juice & Effects")]
    [Tooltip("Screen shake intensity on bounce.")]
    public float bounceShakeIntensity = 0.1f;

    [Tooltip("Screen shake intensity on enemy/player hit.")]
    public float hitShakeIntensity = 0.28f;

    [Tooltip("Color of the arrow and trail.")]
    public Color arrowColor = new Color(1f, 0.45f, 0.08f, 1f);

    // Runtime state
    private Vector2 _direction;
    private float _currentSpeed;
    private int _bounceCount = 0;
    private float _timeAlive = 0f;
    private Transform _shooter;
    private TrailRenderer _trail;

    public void Initialize(Vector2 direction, Transform shooter, float customSpeed = -1f, float customDamage = -1f)
    {
        _direction = direction.normalized;
        _shooter = shooter;
        _currentSpeed = customSpeed > 0f ? customSpeed : speed;
        if (customDamage > 0f)
            damage = customDamage;
        _timeAlive = 0f;
        _bounceCount = 0;

        UpdateRotation();
        SetupTrail();
    }

    private void Awake()
    {
        ArrowShooter.RegisterArrow();

        if (_currentSpeed <= 0f)
            _currentSpeed = speed;
        if (_direction == Vector2.zero)
            _direction = transform.right;

        SetupTrail();
    }

    private void OnDestroy()
    {
        ArrowShooter.UnregisterArrow();
    }

    private void SetupTrail()
    {
        _trail = GetComponent<TrailRenderer>();
        if (_trail == null)
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.2f;
            _trail.startWidth = 0.18f;
            _trail.endWidth = 0.01f;
            _trail.material = new Material(Shader.Find("Sprites/Default"));
            _trail.startColor = arrowColor;
            _trail.endColor = new Color(arrowColor.r, arrowColor.g, arrowColor.b, 0f);
        }
    }

    private void Update()
    {
        _timeAlive += Time.deltaTime;
        if (_timeAlive >= lifetime)
        {
            Dissolve();
            return;
        }

        MoveAndCollide(Time.deltaTime);
    }

    private void MoveAndCollide(float dt)
    {
        float remainingDistance = _currentSpeed * dt;
        int maxSubSteps = 3;

        while (remainingDistance > 0.001f && maxSubSteps > 0)
        {
            maxSubSteps--;
            Vector2 origin = transform.position;

            // 1. Check 2D Physics: Raycast + CircleCast hybrid to handle both fine custom polygon vertices and broad collision
            RaycastHit2D rayHit = Physics2D.Raycast(origin, _direction, remainingDistance, collisionMask);
            RaycastHit2D circleHit = Physics2D.CircleCast(origin, radius, _direction, remainingDistance, collisionMask);

            RaycastHit2D hit2D = default;
            if (circleHit.collider != null && circleHit.distance > 0.0001f)
            {
                hit2D = circleHit;
            }
            else if (rayHit.collider != null)
            {
                hit2D = rayHit;
            }
            else if (circleHit.collider != null)
            {
                hit2D = circleHit;
            }

            // 2. Check 3D Physics (SphereCast) for hybrid 2.5D geometry
            RaycastHit hit3D;
            bool has3DHit = Physics.SphereCast(
                new Vector3(origin.x, origin.y, 0f),
                radius,
                new Vector3(_direction.x, _direction.y, 0f),
                out hit3D,
                remainingDistance,
                collisionMask
            );

            bool hitFound = false;
            float hitDistance = remainingDistance;
            Vector2 hitPoint = Vector2.zero;
            Vector2 hitNormal = Vector2.zero;
            GameObject hitGO = null;

            if (hit2D.collider != null && (!has3DHit || hit2D.distance <= hit3D.distance))
            {
                if (hit2D.transform == _shooter && _timeAlive < shooterArmDelay)
                {
                    // Ignore shooter during initial arming delay
                }
                else
                {
                    hitFound = true;
                    hitDistance = hit2D.distance;
                    hitPoint = hit2D.point;
                    hitNormal = hit2D.normal;

                    // Custom polygon physics shapes & tight corners can return zero/near-zero normal
                    if (hitNormal.sqrMagnitude < 0.001f)
                    {
                        Vector2 closest = hit2D.collider.ClosestPoint(origin);
                        Vector2 outNormal = (origin - closest).normalized;
                        hitNormal = outNormal.sqrMagnitude > 0.001f ? outNormal : -_direction;
                    }

                    hitGO = hit2D.collider.gameObject;
                }
            }
            else if (has3DHit)
            {
                if (hit3D.transform == _shooter && _timeAlive < shooterArmDelay)
                {
                    // Ignore shooter during initial arming delay
                }
                else
                {
                    hitFound = true;
                    hitDistance = hit3D.distance;
                    hitPoint = hit3D.point;
                    hitNormal = hit3D.normal;
                    hitGO = hit3D.collider.gameObject;
                }
            }

            if (hitFound)
            {
                // Move projectile up to surface contact with outward push
                transform.position = hitPoint + hitNormal * (radius + 0.03f);
                remainingDistance -= Mathf.Max(hitDistance, 0.001f);

                OnRicochet(hitPoint, hitNormal, hitGO);

                _bounceCount++;
                if (_bounceCount >= maxBounces)
                {
                    Dissolve();
                    return;
                }
            }
            else
            {
                transform.position += (Vector3)(_direction * remainingDistance);
                remainingDistance = 0f;
            }
        }

        // Strict 2.5D Z-axis lock
        Vector3 pos = transform.position;
        pos.z = 0f;
        transform.position = pos;
    }

    private void OnRicochet(Vector2 hitPoint, Vector2 surfaceNormal, GameObject hitObject)
    {
        // 1. Calculate realistic reflection
        Vector2 reflected = Vector2.Reflect(_direction, surfaceNormal);

        // 2. Add exaggerated angle perturbation for arcade feel
        float angleOffset = Random.Range(-exaggerationAngle, exaggerationAngle);
        reflected = Quaternion.Euler(0, 0, angleOffset) * reflected;

        if (Vector2.Dot(reflected, surfaceNormal) < 0.05f)
        {
            reflected = (reflected + surfaceNormal * 0.4f).normalized;
        }

        _direction = reflected.normalized;
        _currentSpeed = Mathf.Min(_currentSpeed * bounceSpeedMultiplier, 65f);

        UpdateRotation();

        // 3. Audio & Visual Juice
        SoundEffects.PlayRicochet();
        CameraFollow.Shake(bounceShakeIntensity, 0.06f);
        SpawnImpactSpark(hitPoint, surfaceNormal);

        // 4. Damage & Knockback resolution
        if (hitObject != null)
        {
            IDamageable damageable = hitObject.GetComponentInParent<IDamageable>();
            bool isPlayer = hitObject.CompareTag("Player") || (_shooter != null && hitObject.transform == _shooter);

            if (damageable != null)
            {
                Vector2 knockback = _direction * knockbackForce;
                damageable.TakeDamage(damage, hitPoint, knockback);
                CameraFollow.Shake(hitShakeIntensity, 0.15f);

                if (isPlayer)
                    SoundEffects.PlayHitPlayer();
                else
                    SoundEffects.PlayHitEnemy();
            }
            else
            {
                // Knockback on dynamic rigidbodies
                Rigidbody2D rb2D = hitObject.GetComponentInParent<Rigidbody2D>();
                if (rb2D != null && rb2D.bodyType != RigidbodyType2D.Kinematic)
                {
                    rb2D.AddForce(_direction * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }
    }

    private void UpdateRotation()
    {
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void SpawnImpactSpark(Vector2 position, Vector2 normal)
    {
        GameObject spark = new GameObject("ArrowSpark");
        spark.transform.position = position;

        ParticleSystem ps = spark.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.22f;
        main.startSpeed = Random.Range(7f, 14f);
        main.startSize = Random.Range(0.08f, 0.18f);
        main.startColor = arrowColor;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, Random.Range(12, 20)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.04f;
        spark.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg);

        var renderer = spark.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(spark, 0.35f);
    }

    private void Dissolve()
    {
        SpawnImpactSpark(transform.position, -_direction);
        Destroy(gameObject);
    }
}
