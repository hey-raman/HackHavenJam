using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Periodically spawns Enemy 1 and Enemy 2 into the playable arena.
/// Ensures enemies do not spawn inside obstacles or directly on top of the player.
/// Features configurable wave pacing, spawn visual effects, and maximum concurrency caps.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Templates / Prefabs")]
    [Tooltip("Enemy 1 and Enemy 2 prefabs or scene templates.")]
    public GameObject[] enemyPrefabs;

    [Header("Spawn Timing")]
    [Tooltip("Seconds between enemy spawns.")]
    public float spawnInterval = 3.5f;

    [Tooltip("Number of enemies to spawn immediately when the game begins.")]
    public int initialSpawnCount = 2;

    [Tooltip("Maximum number of alive enemies allowed at any time.")]
    public int maxActiveEnemies = 10;

    [Header("Spawn Area & Clearance")]
    [Tooltip("Center of the arena spawn zone (matches the playable tilemap area).")]
    public Vector2 arenaCenter = new Vector2(42.5f, 1.0f);

    [Tooltip("Extents (half-width, half-height) of the spawnable area.")]
    public Vector2 arenaExtents = new Vector2(10.0f, 5.5f);

    [Tooltip("Minimum distance from the player to spawn an enemy.")]
    public float minDistanceFromPlayer = 4.5f;

    [Tooltip("Layer mask for obstacles to avoid spawning inside walls.")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("Explicit spawn points (optional). If empty, random safe arena positions are generated.")]
    public Transform[] customSpawnPoints;

    private List<EnemyController> _activeEnemies = new List<EnemyController>();
    private Transform _playerTransform;
    private float _spawnTimer = 0f;

    public int ActiveEnemyCount => _activeEnemies.Count;

    private void Start()
    {
        FindPlayer();

        // Spawn initial wave
        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnEnemy();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
    }

    private void Update()
    {
        // Cleanup any null references in list
        _activeEnemies.RemoveAll(e => e == null);

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;

            if (_activeEnemies.Count < maxActiveEnemies)
            {
                SpawnEnemy();
            }
        }
    }

    public void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        // Pick enemy variant (Enemy 1 vs Enemy 2)
        int prefabIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject prefab = enemyPrefabs[prefabIndex];
        if (prefab == null) return;

        // Find safe spawn position
        Vector2 spawnPos = FindSafeSpawnPosition();

        // Spawn visual flash portal effect
        StartCoroutine(SpawnSequence(prefab, spawnPos));
    }

    private IEnumerator SpawnSequence(GameObject prefab, Vector2 spawnPos)
    {
        SpawnPortalEffect(spawnPos);

        // Brief dramatic delay for the warp-in effect
        yield return new WaitForSeconds(0.25f);

        GameObject newEnemy = Instantiate(prefab, new Vector3(spawnPos.x, spawnPos.y, 0f), Quaternion.identity);
        newEnemy.SetActive(true);

        EnemyController controller = newEnemy.GetComponent<EnemyController>();
        if (controller == null)
        {
            controller = newEnemy.AddComponent<EnemyController>();
        }

        controller.spawner = this;
        _activeEnemies.Add(controller);
    }

    private Vector2 FindSafeSpawnPosition()
    {
        if (_playerTransform == null) FindPlayer();
        Vector2 playerPos = _playerTransform != null ? (Vector2)_playerTransform.position : arenaCenter;

        // If custom spawn points are assigned, pick one that's far enough from the player
        if (customSpawnPoints != null && customSpawnPoints.Length > 0)
        {
            List<Transform> candidates = new List<Transform>();
            foreach (var pt in customSpawnPoints)
            {
                if (pt != null && Vector2.Distance(pt.position, playerPos) >= minDistanceFromPlayer)
                {
                    candidates.Add(pt);
                }
            }
            if (candidates.Count > 0)
            {
                return candidates[Random.Range(0, candidates.Count)].position;
            }
        }

        // Random sampling with obstacle clearance check
        for (int attempts = 0; attempts < 15; attempts++)
        {
            float randX = Random.Range(arenaCenter.x - arenaExtents.x, arenaCenter.x + arenaExtents.x);
            float randY = Random.Range(arenaCenter.y - arenaExtents.y, arenaCenter.y + arenaExtents.y);
            Vector2 candidate = new Vector2(randX, randY);

            if (Vector2.Distance(candidate, playerPos) < minDistanceFromPlayer)
                continue;

            // Check if position overlaps tilemap obstacle or wall
            Collider2D overlap = Physics2D.OverlapCircle(candidate, 0.45f, obstacleMask);
            if (overlap == null || overlap.isTrigger || overlap.CompareTag("Player"))
            {
                return candidate;
            }
        }

        // Fallback: Default safe offset from center
        return arenaCenter + new Vector2(Random.Range(-3f, 3f), Random.Range(-2f, 2f));
    }

    private void SpawnPortalEffect(Vector2 position)
    {
        GameObject fx = new GameObject("SpawnWarpFX");
        fx.transform.position = new Vector3(position.x, position.y, 0f);

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.35f;
        main.startSpeed = 3.5f;
        main.startSize = 0.18f;
        main.startColor = new Color(0.9f, 0.4f, 1f, 0.9f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        var rend = fx.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(fx, 0.6f);
    }

    public void OnEnemyDied(EnemyController enemy)
    {
        if (_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Remove(enemy);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireCube(new Vector3(arenaCenter.x, arenaCenter.y, 0f), new Vector3(arenaExtents.x * 2f, arenaExtents.y * 2f, 0.1f));
    }
}
