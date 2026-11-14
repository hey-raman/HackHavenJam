using System.Collections;
using UnityEngine;

/// <summary>
/// Fast-paced arcade 2.5D enemy controller.
/// Moves toward the player, reacts to ricocheting arrow impacts with knockback and hit flash,
/// deals contact damage, and detonates with juicy effects upon defeat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour, IDamageable
{
    public enum EnemyType { Skirmisher, Brute }

    [Header("Enemy Identity")]
    public EnemyType enemyType = EnemyType.Skirmisher;
    public string enemyName = "Enemy";

    [Header("Stats")]
    public float maxHealth = 50f;
    public float currentHealth;
    public float moveSpeed = 3.5f;
    public float acceleration = 18f;
    public float contactDamage = 15f;
    public float contactCooldown = 0.8f;
    public float knockbackResistance = 0.2f; // Lower = more knockback

    [Header("Visual Feedback")]
    public Color flashColor = Color.white;
    public Color deathColor = new Color(1f, 0.3f, 0.2f, 1f);

    [HideInInspector]
    public EnemySpawner spawner;

    private Rigidbody2D _rb2D;
    private SpriteRenderer _spriteRenderer;
    private Color _baseColor = Color.white;
    private Transform _playerTransform;
    private PlayerHealth _playerHealth;
    private bool _isDead = false;
    private float _nextContactDamageTime = 0f;
    private Vector2 _knockbackVelocity;
    private Vector3 _baseScale;
    private float _wobbleOffset;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        _rb2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _baseColor = _spriteRenderer.color;
        }

        _baseScale = transform.localScale;
        _wobbleOffset = Random.Range(0f, 10f);
        currentHealth = maxHealth;

        // Configure 2D physics for top-down action
        _rb2D.gravityScale = 0f;
        _rb2D.freezeRotation = true;
        _rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb2D.linearDamping = 4f; // Clean arcade deceleration
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (_isDead) return;

        // Visual wobble / breath animation
        float wobble = 1f + Mathf.Sin(Time.time * 7f + _wobbleOffset) * 0.04f;
        transform.localScale = new Vector3(_baseScale.x * wobble, _baseScale.y * (2f - wobble), _baseScale.z);

        // Keep strictly on 2.5D plane
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.z) > 0.001f)
        {
            pos.z = 0f;
            transform.position = pos;
        }
    }

    private void FixedUpdate()
    {
        if (_isDead) return;

        if (_playerTransform == null)
        {
            FindPlayer();
            if (_playerTransform == null) return;
        }

        // Decay knockback velocity smoothly
        _knockbackVelocity = Vector2.MoveTowards(_knockbackVelocity, Vector2.zero, Time.fixedDeltaTime * 22f);

        Vector2 currentPos = _rb2D.position;
        Vector2 targetPos = _playerTransform.position;
        Vector2 desiredDir = (targetPos - currentPos).normalized;

        // Sprite facing direction
        if (_spriteRenderer != null && Mathf.Abs(desiredDir.x) > 0.05f)
        {
            _spriteRenderer.flipX = desiredDir.x < 0;
        }

        // Obstacle avoidance probe
        Vector2 finalMoveDir = CalculateMoveDirectionWithAvoidance(desiredDir);

        // Compute movement velocity
        Vector2 targetMoveVel = finalMoveDir * moveSpeed;
        Vector2 blendedVel = Vector2.MoveTowards(_rb2D.linearVelocity, targetMoveVel + _knockbackVelocity, acceleration * Time.fixedDeltaTime);
        _rb2D.linearVelocity = blendedVel;
    }

    private Vector2 CalculateMoveDirectionWithAvoidance(Vector2 desiredDir)
    {
        float checkDist = 0.85f;
        RaycastHit2D hit = Physics2D.CircleCast(_rb2D.position, 0.35f, desiredDir, checkDist, LayerMask.GetMask("Default"));

        // If directly facing an obstacle (that is not the player), slide tangentially
        if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.CompareTag("Player"))
        {
            Vector2 normal = hit.normal;
            Vector2 tangent1 = new Vector2(-normal.y, normal.x);
            Vector2 tangent2 = new Vector2(normal.y, -normal.x);

            // Pick the tangent closest to our desired direction
            return (Vector2.Dot(tangent1, desiredDir) > Vector2.Dot(tangent2, desiredDir)) ? tangent1 : tangent2;
        }

        return desiredDir;
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 knockback)
    {
        if (_isDead) return;

        currentHealth -= amount;

        // Apply knockback
        _knockbackVelocity += knockback * (1f - Mathf.Clamp01(knockbackResistance));

        // Impact spark particles
        SpawnHitSpark(hitPoint, knockback.normalized);

        // Flash visual
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(HitFlashRoutine());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(0.08f);
            _spriteRenderer.color = _baseColor;
        }
    }

    private void SpawnHitSpark(Vector2 point, Vector2 direction)
    {
        GameObject spark = new GameObject("HitSpark");
        spark.transform.position = new Vector3(point.x, point.y, 0f);

        ParticleSystem ps = spark.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.15f;
        main.startSpeed = Random.Range(4f, 8f);
        main.startSize = 0.12f;
        main.startColor = flashColor;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.rotation = new Vector3(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        var rend = spark.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(spark, 0.25f);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject, collision.contacts[0].point, collision.contacts[0].normal);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other.gameObject, transform.position, (_playerTransform ? (Vector2)(_playerTransform.position - transform.position).normalized : Vector2.up));
    }

    private void TryDealContactDamage(GameObject targetObj, Vector2 hitPoint, Vector2 normal)
    {
        if (_isDead) return;

        if (targetObj.CompareTag("Player") || targetObj.GetComponent<PlayerHealth>() != null)
        {
            if (Time.time >= _nextContactDamageTime)
            {
                _nextContactDamageTime = Time.time + contactCooldown;

                PlayerHealth health = targetObj.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    Vector2 pushDir = ((Vector2)targetObj.transform.position - (Vector2)transform.position).normalized;
                    if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector2.up;

                    health.TakeDamage(contactDamage, hitPoint, pushDir * 8f);
                    SoundEffects.PlayHitPlayer();

                    // Push self back slightly from impact
                    _knockbackVelocity = -pushDir * 3.5f;
                }
            }
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Sound & Juice
        SoundEffects.PlayEnemyExplode();
        CameraFollow.Shake(0.28f, 0.16f);

        // Explosion particles
        SpawnDeathExplosion();

        // Notify Spawner
        if (spawner != null)
        {
            spawner.OnEnemyDied(this);
        }

        // Notify GameLoopManager
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnEnemyDefeated(this);
        }

        Destroy(gameObject);
    }

    private void SpawnDeathExplosion()
    {
        GameObject burst = new GameObject("EnemyDeathExplosion");
        burst.transform.position = transform.position;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.45f;
        main.startSpeed = Random.Range(6f, 13f);
        main.startSize = 0.22f;
        main.startColor = deathColor;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;

        var rend = burst.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burst, 0.6f);
    }
}
