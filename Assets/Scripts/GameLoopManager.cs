using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates the overarching gameplay loop:
/// Enter Area -> Fight Enemies -> Shoot & Dodge Ricochets -> Survive Chaos -> Complete Objective -> Progress to Next Area.
/// </summary>
public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    [Header("Area Progression")]
    public int currentArea = 1;
    public int enemiesDefeatedInArea = 0;
    public int targetQuota = 6;
    public bool isObjectiveComplete = false;

    [Header("References")]
    public ArenaGateway gateway;
    public EnemySpawner spawner;
    public Transform playerEntrancePoint;

    [Header("Banner Announcements")]
    public string currentBanner = "";
    public float bannerDuration = 3.5f;
    private float _bannerTimer = 0f;

    private readonly string[] _areaNames = new string[]
    {
        "THE GOTHIC COURTYARD",
        "THE RELIC CHAMBER",
        "THE FORGOTTEN CRYPT",
        "THE GRAND CRUCIBLE",
        "THE INFINITE HAVEN"
    };

    private Transform _playerTransform;
    private PlayerHealth _playerHealth;

    public string CurrentAreaName => _areaNames[Mathf.Clamp(currentArea - 1, 0, _areaNames.Length - 1)];
    public float ProgressNormalized => targetQuota > 0 ? Mathf.Clamp01((float)enemiesDefeatedInArea / targetQuota) : 1f;
    public bool HasActiveBanner => _bannerTimer > 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (spawner == null)
        {
            spawner = Object.FindAnyObjectByType<EnemySpawner>();
        }
        if (gateway == null)
        {
            gateway = Object.FindAnyObjectByType<ArenaGateway>();
        }
    }

    private void Start()
    {
        FindPlayer();
        StartArea(1);
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
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.deltaTime;
        }
    }

    public void StartArea(int areaIndex)
    {
        currentArea = areaIndex;
        enemiesDefeatedInArea = 0;
        isObjectiveComplete = false;

        // Scale difficulty with each tier
        targetQuota = 5 + (currentArea * 3); // 8, 11, 14, 17...

        if (spawner != null)
        {
            spawner.spawnInterval = Mathf.Max(1.8f, 3.8f - (currentArea * 0.4f));
            spawner.maxActiveEnemies = Mathf.Min(14, 8 + currentArea);
            spawner.enabled = true;
        }

        // Lock gateway until objective is done
        if (gateway != null)
        {
            gateway.SetGatewayState(false);
        }

        // Heal player on advancing
        if (_playerHealth != null)
        {
            _playerHealth.currentHealth = Mathf.Min(_playerHealth.maxHealth, _playerHealth.currentHealth + 35f);
        }

        ShowBanner($"AREA {currentArea}: {CurrentAreaName}\nOBJECTIVE: ELIMINATE {targetQuota} HOSTILES");
        SoundEffects.PlayBowDraw();
    }

    public void OnEnemyDefeated(EnemyController enemy)
    {
        if (isObjectiveComplete) return;

        enemiesDefeatedInArea++;

        if (enemiesDefeatedInArea >= targetQuota)
        {
            CompleteObjective();
        }
    }

    private void CompleteObjective()
    {
        isObjectiveComplete = true;

        // 1. Safely dissolve all currently flying ricochet arrows into golden sparks
        DissolveAllActiveArrows();

        // 2. Unlock the exit gateway
        if (gateway != null)
        {
            gateway.SetGatewayState(true);
        }

        // 3. Audio & Juice
        SoundEffects.PlayBowRelease(1.2f);
        CameraFollow.Shake(0.35f, 0.25f);

        ShowBanner("★ OBJECTIVE COMPLETE! ★\nENTER THE GATEWAY TO ADVANCE");
    }

    private void DissolveAllActiveArrows()
    {
        RicochetArrow[] arrows = Object.FindObjectsByType<RicochetArrow>(FindObjectsInactive.Exclude);
        foreach (var arrow in arrows)
        {
            if (arrow != null)
            {
                // Trigger harmless burst
                SpawnGoldSparks(arrow.transform.position);
                Destroy(arrow.gameObject);
            }
        }
    }

    private void SpawnGoldSparks(Vector3 pos)
    {
        GameObject burst = new GameObject("ArrowPeaceBurst");
        burst.transform.position = pos;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.35f;
        main.startSpeed = Random.Range(3f, 7f);
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        var rend = burst.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burst, 0.5f);
    }

    public void OnGatewayEntered()
    {
        StartCoroutine(TransitionToNextAreaRoutine());
    }

    private IEnumerator TransitionToNextAreaRoutine()
    {
        // Warp flash
        if (gateway != null)
        {
            SpawnGoldSparks(gateway.transform.position);
        }

        SoundEffects.PlayDash();
        CameraFollow.Shake(0.4f, 0.2f);

        yield return new WaitForSeconds(0.35f);

        // Reposition player to area entrance
        if (_playerTransform != null)
        {
            Vector3 spawnPos = playerEntrancePoint != null ? playerEntrancePoint.position : new Vector3(35.60f, -1.50f, 0f);
            _playerTransform.position = spawnPos;

            Rigidbody2D rb = _playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // Start next area tier
        StartArea(currentArea + 1);
    }

    public void ShowBanner(string text)
    {
        currentBanner = text;
        _bannerTimer = bannerDuration;
    }
}
