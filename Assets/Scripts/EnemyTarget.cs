using System.Collections;
using UnityEngine;

/// <summary>
/// 2.5D Target / Enemy that reacts to ricocheting arrow impacts.
/// Takes damage, flashes, suffers physics knockback, and respawns on death.
/// </summary>
public class EnemyTarget : MonoBehaviour, IDamageable
{
    [Header("Enemy Stats")]
    public float maxHealth = 75f;
    public float currentHealth;

    [Header("Visual Feedback")]
    public Color flashColor = Color.white;
    public Color normalColor = new Color(0.9f, 0.2f, 0.25f, 1f);

    private MeshRenderer _renderer;
    private Rigidbody2D _rb2D;
    private Rigidbody _rb3D;
    private Vector3 _spawnPosition;
    private bool _isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        _spawnPosition = transform.position;
        _renderer = GetComponentInChildren<MeshRenderer>();
        _rb2D = GetComponent<Rigidbody2D>();
        _rb3D = GetComponent<Rigidbody>();

        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.color = normalColor;
        }
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 knockback)
    {
        if (_isDead) return;

        currentHealth -= amount;

        // Apply knockback
        if (_rb2D != null && _rb2D.bodyType != RigidbodyType2D.Kinematic)
        {
            _rb2D.AddForce(knockback, ForceMode2D.Impulse);
        }
        else if (_rb3D != null && !_rb3D.isKinematic)
        {
            _rb3D.AddForce(new Vector3(knockback.x, knockback.y, 0f), ForceMode.Impulse);
        }
        else
        {
            // Direct position nudge if no dynamic rigidbody
            transform.position += (Vector3)(knockback.normalized * 0.35f);
        }

        StartCoroutine(HitFlashRoutine());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.color = flashColor;
            yield return new WaitForSeconds(0.09f);
            _renderer.material.color = normalColor;
        }
    }

    private void Die()
    {
        _isDead = true;
        CameraFollow.Shake(0.3f, 0.2f);
        SpawnDeathExplosion();
        StartCoroutine(RespawnRoutine());
    }

    private void SpawnDeathExplosion()
    {
        GameObject burst = new GameObject("EnemyDeathBurst");
        burst.transform.position = transform.position;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = Random.Range(7f, 15f);
        main.startSize = 0.25f;
        main.startColor = normalColor;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;

        var rend = burst.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burst, 0.6f);
    }

    private IEnumerator RespawnRoutine()
    {
        // Hide during respawn
        if (_renderer != null) _renderer.enabled = false;
        Collider2D c2D = GetComponent<Collider2D>();
        if (c2D != null) c2D.enabled = false;
        Collider c3D = GetComponent<Collider>();
        if (c3D != null) c3D.enabled = false;

        yield return new WaitForSeconds(2.5f);

        // Respawn
        transform.position = _spawnPosition;
        if (_rb2D != null) _rb2D.linearVelocity = Vector2.zero;
        if (_rb3D != null) _rb3D.linearVelocity = Vector3.zero;

        currentHealth = maxHealth;
        _isDead = false;

        if (_renderer != null)
        {
            _renderer.enabled = true;
            _renderer.material.color = normalColor;
        }
        if (c2D != null) c2D.enabled = true;
        if (c3D != null) c3D.enabled = true;
    }
}
