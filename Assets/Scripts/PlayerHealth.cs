using System.Collections;
using UnityEngine;

/// <summary>
/// Manages player health, damage response, and invincibility frames.
/// Allows arrows (including the player's own ricochets) to hit and knock back the player.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Invincibility & Feedback")]
    [Tooltip("Brief i-frames duration after taking a hit.")]
    public float invincibilityDuration = 0.22f;

    [Tooltip("Color to flash when taking damage.")]
    public Color damageFlashColor = Color.red;

    private float _invincibleTimer = 0f;
    private MeshRenderer _meshRenderer;
    private Color _originalColor = Color.white;
    private PlayerMovement _playerMovement;
    private Vector3 _spawnPosition;

    private bool _isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        _spawnPosition = transform.position;
        _meshRenderer = GetComponent<MeshRenderer>();
        _playerMovement = GetComponent<PlayerMovement>();

        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _originalColor = _meshRenderer.material.color;
        }
    }

    private void Update()
    {
        if (_invincibleTimer > 0f)
        {
            _invincibleTimer -= Time.deltaTime;
        }
    }

    public void SetInvincible(float duration)
    {
        _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 knockback)
    {
        if (_isDead || _invincibleTimer > 0f) return;

        currentHealth -= amount;
        _invincibleTimer = invincibilityDuration;

        // Apply knockback
        if (_playerMovement != null)
        {
            _playerMovement.ApplyKnockback(knockback * 0.85f);
        }

        // Screen shake on hit
        CameraFollow.Shake(0.38f, 0.2f);

        // Visual flash feedback
        StartCoroutine(DamageFlashRoutine());

        Debug.Log($"[PlayerHealth] Hit by ricochet! Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            OnDeath();
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _meshRenderer.material.color = damageFlashColor;
            yield return new WaitForSeconds(0.12f);
            _meshRenderer.material.color = _originalColor;
        }
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;

        Debug.LogWarning("[PlayerHealth] Player fell to ricocheting arrows! Transitioning to retry scene...");
        CameraFollow.Shake(0.5f, 0.35f);
        SoundEffects.PlayEnemyExplode();
        SpawnDeathExplosion();

        // Disable controls
        if (_playerMovement != null) _playerMovement.enabled = false;
        var shooter = GetComponent<ArrowShooter>();
        if (shooter != null) shooter.enabled = false;

        StartCoroutine(LoadGameOverRoutine());
    }

    private void SpawnDeathExplosion()
    {
        GameObject burst = new GameObject("PlayerDeathExplosion");
        burst.transform.position = transform.position;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = Random.Range(6f, 14f);
        main.startSize = 0.28f;
        main.startColor = new Color(1f, 0.2f, 0.2f, 1f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 32) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;

        var rend = burst.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burst, 0.8f);
    }

    private IEnumerator LoadGameOverRoutine()
    {
        yield return new WaitForSeconds(0.65f);

        if (RetryUI.Instance != null)
        {
            RetryUI.Instance.ShowRetry();
        }
        else if (Application.CanStreamedLevelBeLoaded("GameOver"))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        }
        else
        {
            // Safe fallback respawn in current scene
            currentHealth = maxHealth;
            transform.position = _spawnPosition;
            _isDead = false;
            if (_playerMovement != null) _playerMovement.enabled = true;
            var shooter = GetComponent<ArrowShooter>();
            if (shooter != null) shooter.enabled = true;
        }
    }
}
