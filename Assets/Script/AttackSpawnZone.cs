using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ZoneEnemySpawnEntry
{
    public GameObject prefab;
    public int unlockWave = 1;
    public int weight = 1;
}

[Serializable]
public class ZoneSpawnPoint
{
    public Transform point;
    public float statMultiplier = 1f;
}

public enum AttackZoneSpawnMode
{
    SpawnPoints,
    RandomInBox
}

public enum EnemyMovementMode
{
    Default,
    Direct,
    AStar,
    Waypoint,
    WaypointThenDirect,
    WaypointThenAStar
}

public struct ZoneSpawnResult
{
    public Vector3 position;
    public float statMultiplier;

    public ZoneSpawnResult(Vector3 position, float statMultiplier)
    {
        this.position = position;
        this.statMultiplier = statMultiplier;
    }
}

public class AttackSpawnZone : MonoBehaviour
{
    [Header("Zone")]
    public string zoneName;
    public int unlockWave = 1;
    public int weight = 1;
    public float zoneStatMultiplier = 1f;
    public bool requirePlayerNear = true;

    [Header("Spawn Batch Count")]
    [FormerlySerializedAs("minEnemiesPerAttackPhase")]
    public int minEnemiesPerSpawn = 1;
    [FormerlySerializedAs("maxEnemiesPerAttackPhase")]
    public int maxEnemiesPerSpawn = 5;
    public float respawnCooldown = 3f;

    [Header("Spawns")]
    public AttackZoneSpawnMode spawnMode = AttackZoneSpawnMode.SpawnPoints;
    public List<ZoneSpawnPoint> spawnPoints = new List<ZoneSpawnPoint>();

    [Header("Movement")]
    public EnemyMovementMode enemyMovementMode = EnemyMovementMode.Default;
    public List<Transform> waypoints = new List<Transform>();

    [Header("Random Box Spawn")]
    public BoxCollider2D randomSpawnArea;
    public float randomSpawnStatMultiplier = 1f;
    public LayerMask blockedSpawnLayers;
    public float spawnCollisionCheckRadius = 0.25f;
    public int randomSpawnAttempts = 12;

    [Header("Enemies")]
    public List<ZoneEnemySpawnEntry> enemies = new List<ZoneEnemySpawnEntry>();

    private readonly List<EnemyHealth> spawnedEnemies = new List<EnemyHealth>();
    private float cooldownRemaining;

    private void Reset()
    {
        randomSpawnArea = GetComponent<BoxCollider2D>();
    }

    private void Awake()
    {
        if (randomSpawnArea == null)
            randomSpawnArea = GetComponent<BoxCollider2D>();
    }

    public bool CanUse(int currentWave)
    {
        return currentWave >= unlockWave && weight > 0 && HasValidSpawnSource();
    }

    public bool CanSpawnBatch(int currentWave)
    {
        RemoveNullSpawnedEnemies();
        return CanUse(currentWave) && spawnedEnemies.Count == 0 && cooldownRemaining <= 0f;
    }

    public void BeginAttackPhase()
    {
        ClearSpawnTracking();
        cooldownRemaining = 0f;
    }

    public void TickCooldown(float deltaTime)
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= deltaTime;
    }

    public int GetRandomSpawnBatchCount()
    {
        int minCount = Mathf.Max(0, minEnemiesPerSpawn);
        int maxCount = Mathf.Max(minCount, maxEnemiesPerSpawn);
        return UnityEngine.Random.Range(minCount, maxCount + 1);
    }

    public void RegisterSpawnedEnemy(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
            return;

        RemoveNullSpawnedEnemies();
        spawnedEnemies.Add(enemyHealth);
        enemyHealth.Died += OnTrackedEnemyDied;
    }

    public bool HasEnemyEntries()
    {
        foreach (ZoneEnemySpawnEntry entry in enemies)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0)
                return true;
        }

        return false;
    }

    public bool TryChooseSpawn(Transform player, float awarenessRange, out ZoneSpawnResult result)
    {
        if (spawnMode == AttackZoneSpawnMode.RandomInBox)
            return TryChooseRandomBoxSpawn(player, awarenessRange, out result);

        ZoneSpawnPoint spawnPoint = ChooseSpawnPoint(player, awarenessRange);

        if (spawnPoint != null && spawnPoint.point != null)
        {
            result = new ZoneSpawnResult(spawnPoint.point.position, spawnPoint.statMultiplier);
            return true;
        }

        result = default;
        return false;
    }

    private ZoneSpawnPoint ChooseSpawnPoint(Transform player, float awarenessRange)
    {
        List<ZoneSpawnPoint> validPoints = new List<ZoneSpawnPoint>();

        foreach (ZoneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint == null || spawnPoint.point == null)
                continue;

            if (requirePlayerNear && player != null)
            {
                float distance = Vector2.Distance(player.position, spawnPoint.point.position);

                if (distance > awarenessRange)
                    continue;
            }

            validPoints.Add(spawnPoint);
        }

        if (validPoints.Count == 0)
            return null;

        return validPoints[UnityEngine.Random.Range(0, validPoints.Count)];
    }

    private bool TryChooseRandomBoxSpawn(Transform player, float awarenessRange, out ZoneSpawnResult result)
    {
        result = default;

        if (randomSpawnArea == null)
            return false;

        Bounds bounds = randomSpawnArea.bounds;
        int attempts = Mathf.Max(1, randomSpawnAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y)
            );

            if (!randomSpawnArea.OverlapPoint(candidate))
                continue;

            if (requirePlayerNear && player != null)
            {
                float distance = Vector2.Distance(player.position, candidate);

                if (distance > awarenessRange)
                    continue;
            }

            if (IsBlocked(candidate))
                continue;

            result = new ZoneSpawnResult(candidate, randomSpawnStatMultiplier);
            return true;
        }

        return false;
    }

    public GameObject ChooseEnemyPrefab(int currentWave)
    {
        int totalWeight = 0;

        foreach (ZoneEnemySpawnEntry entry in enemies)
        {
            if (!CanSpawnEntry(entry, currentWave))
                continue;

            totalWeight += Mathf.Max(0, entry.weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (ZoneEnemySpawnEntry entry in enemies)
        {
            if (!CanSpawnEntry(entry, currentWave))
                continue;

            currentWeight += Mathf.Max(0, entry.weight);

            if (roll < currentWeight)
                return entry.prefab;
        }

        return null;
    }

    private bool HasValidSpawnSource()
    {
        if (spawnMode == AttackZoneSpawnMode.RandomInBox)
            return randomSpawnArea != null || GetComponent<BoxCollider2D>() != null;

        return HasValidSpawnPoint();
    }

    private bool HasValidSpawnPoint()
    {
        foreach (ZoneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint != null && spawnPoint.point != null)
                return true;
        }

        return false;
    }

    private bool IsBlocked(Vector2 position)
    {
        if (blockedSpawnLayers.value == 0 || spawnCollisionCheckRadius <= 0f)
            return false;

        return Physics2D.OverlapCircle(position, spawnCollisionCheckRadius, blockedSpawnLayers) != null;
    }

    private bool CanSpawnEntry(ZoneEnemySpawnEntry entry, int currentWave)
    {
        return entry != null
            && entry.prefab != null
            && entry.weight > 0
            && currentWave >= entry.unlockWave;
    }

    private void OnTrackedEnemyDied(EnemyHealth enemyHealth)
    {
        if (enemyHealth != null)
            enemyHealth.Died -= OnTrackedEnemyDied;

        spawnedEnemies.Remove(enemyHealth);

        if (spawnedEnemies.Count == 0)
            cooldownRemaining = Mathf.Max(0f, respawnCooldown);
    }

    private void RemoveNullSpawnedEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null)
                spawnedEnemies.RemoveAt(i);
        }
    }

    private void ClearSpawnTracking()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] != null)
                spawnedEnemies[i].Died -= OnTrackedEnemyDied;
        }

        spawnedEnemies.Clear();
    }
}
