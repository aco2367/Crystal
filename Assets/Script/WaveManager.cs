using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WavePhase
{
    Attack = 0,
    Defense = 1,
    Maintenance = 2
}

[Serializable]
public class EnemySpawnEntry
{
    public GameObject prefab;
    public int unlockWave = 1;
    public int weight = 1;
    public bool spawnInAttackPhase = true;
    public bool spawnInDefensePhase = true;
}

[Serializable]
public class DefenseWaypointPath
{
    public string pathName;
    public Transform spawnPoint;
    public List<Transform> waypoints = new List<Transform>();
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave")]
    public int currentWave = 1;
    public bool startOnAwake = true;
    public float phaseTransitionDelay = 3f;

    [Header("Phase Duration")]
    public float attackPhaseDuration = 60f;
    public float maintenancePhaseDuration = 20f;
    public float defensePhaseDuration = 35f;

    [Header("Player Travel")]
    public Transform player;
    public Transform attackStartPoint;
    public Transform homeSpawnPoint;
    public float defenseRespawnDelay = 3f;

    [Header("Home")]
    public HomeBase homeBase;
    public Transform homeTargetPoint;

    [Header("Enemy Entries")]
    public List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();

    [Header("Attack Phase Spawns")]
    public Transform[] attackSpawnPoints;
    public AttackSpawnZone[] attackSpawnZones;
    public bool autoFindAttackSpawnZones = true;
    public float attackSpawnInterval = 2f;
    public int attackMaxAliveEnemies = 12;

    [Header("Defense Phase Spawns")]
    public Transform[] defenseSpawnPoints;
    public float defenseSpawnInterval = 2.5f;
    public int defenseMaxAliveEnemies = 10;

    [Header("Defense Movement")]
    public EnemyMovementMode defenseEnemyMovementMode = EnemyMovementMode.Default;
    public List<Transform> defenseWaypoints = new List<Transform>();
    public List<DefenseWaypointPath> defensePaths = new List<DefenseWaypointPath>();

    [Header("Scaling")]
    public int extraMaxAlivePerWave = 2;
    public float spawnIntervalDecreasePerWave = 0.05f;
    public float minSpawnInterval = 0.6f;

    [Header("Enemy Power Scaling")]
    public bool useWaveScaling = true;
    public float enemyStatIncreasePerWave = 0.12f;
    public bool useDistanceScalingInAttackPhase = true;
    public Transform distanceOrigin;
    public float distanceForMaxBonus = 30f;
    public float maxDistanceStatBonus = 1.5f;

    [Header("Spawn Awareness")]
    public bool requirePlayerNearAttackSpawn = true;
    public bool requirePlayerNearDefenseSpawn = false;
    public float spawnAwarenessRange = 8f;
    [Header("Scene Transition")]
    public bool useSceneTransitionForPhases = false;
    public string[] attackSceneNames;
    public string[] maintenanceSceneNames;
    public string[] defenseSceneNames;

    [Header("Debug Phase Skip")]
    public bool enableHoldSkipToNextPhase = true;
    public float skipHoldDuration = 1f;

    public WavePhase CurrentPhase => currentPhase;
    public float PhaseTimeRemaining => phaseTimeRemaining;
    public int AliveEnemyCount => aliveEnemies.Count;
    public bool IsDefensePhase => currentPhase == WavePhase.Defense;
    public bool CanEndCurrentPhaseEarly =>
        phaseRoutine != null && phaseTimeRemaining > 0f && currentPhase != WavePhase.Defense;

    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action<WavePhase> PhaseChanged;

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
    private readonly List<ZoneSpawnResult> pendingZoneSpawns = new List<ZoneSpawnResult>();
    private WavePhase currentPhase;
    private float phaseTimeRemaining;
    private Coroutine phaseRoutine;
    private float skipHoldTimer;

    private void Awake()
    {
        Instance = this;
        GameSession session = GameSession.GetOrCreate();
        currentWave = Mathf.Max(1, session.currentWave);
        currentPhase = session.currentPhase;

        if (player == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                player = playerController.transform;
            }
            else
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

                if (playerObject != null)
                {
                    PlayerController controller = playerObject.GetComponentInParent<PlayerController>();
                    player = controller != null ? controller.transform : playerObject.transform;
                }
            }
        }

        if (homeTargetPoint == null && homeBase != null)
        {
            homeTargetPoint = homeBase.transform;
        }

        if (startOnAwake)
        {
            StartPhaseLoop();
        }
    }

    private void Update()
    {
        UpdateDebugPhaseSkip();
    }

    public void MoveToAttackScene()
    {
        GameSession session = GameSession.GetOrCreate();
        session.SaveWave(this);
        session.currentPhase = WavePhase.Attack;
        session.currentWave = currentWave;
        session.LoadNextAttackScene();
    }

    public void MoveToDefenseScene()
    {
        GameSession session = GameSession.GetOrCreate();
        session.SaveWave(this);
        session.currentPhase = WavePhase.Defense;
        session.currentWave = currentWave;
        session.LoadCurrentDefenseScene();
    }

    public void MoveToMaintenanceScene(WavePhase nextPhaseAfterMaintenance)
    {
        GameSession session = GameSession.GetOrCreate();
        session.SaveWave(this);
        session.currentPhase = WavePhase.Maintenance;
        session.currentWave = currentWave;
        session.LoadCurrentMaintenanceScene(nextPhaseAfterMaintenance);
    }

    public bool TryEndCurrentPhaseEarly()
    {
        if (!CanEndCurrentPhaseEarly)
            return false;

        phaseTimeRemaining = 0f;
        Debug.Log($"{currentPhase} 페이즈를 조기 종료했습니다.");
        return true;
    }

    public void StartPhaseLoop()
    {
        if (phaseRoutine != null)
        {
            StopCoroutine(phaseRoutine);
        }

        phaseRoutine = StartCoroutine(PhaseLoop());
    }

    private IEnumerator PhaseLoop()
    {
        if (ShouldUseSceneTransition())
        {
            yield return PhaseLoopWithSceneTransitions();
            yield break;
        }

        while (true)
        {
            WaveStarted?.Invoke(currentWave);
            Debug.Log($"웨이브 {currentWave} 공격 페이즈 시작");

            TeleportPlayer(attackStartPoint);
            yield return RunPhase(WavePhase.Attack, attackPhaseDuration, attackSpawnInterval, GetAttackMaxAliveEnemies(), attackSpawnPoints, false);

            ClearAliveEnemies();

            Debug.Log("공격 페이즈 종료: 정비 페이즈 시작");
            yield return RunMaintenancePhase();

            Debug.Log("방어 페이즈 준비: 집으로 귀환");
            TeleportPlayer(homeSpawnPoint);
            yield return new WaitForSeconds(phaseTransitionDelay);

            Debug.Log($"웨이브 {currentWave} 방어 페이즈 시작");
            yield return RunPhase(WavePhase.Defense, defensePhaseDuration, defenseSpawnInterval, GetDefenseMaxAliveEnemies(), defenseSpawnPoints, true);

            ClearAliveEnemies();

            WaveCleared?.Invoke(currentWave);
            Debug.Log($"웨이브 {currentWave} 클리어");

            currentWave++;
            Debug.Log("방어 페이즈 종료: 정비 페이즈 시작");
            yield return RunMaintenancePhase();
            yield return new WaitForSeconds(phaseTransitionDelay);
        }
    }

    private bool ShouldUseSceneTransition()
    {
        if (useSceneTransitionForPhases)
            return true;

        if (currentPhase == WavePhase.Defense)
            return true;

        if (currentPhase == WavePhase.Maintenance)
            return true;

        if (defenseSceneNames != null && defenseSceneNames.Length > 0)
            return true;

        if (maintenanceSceneNames != null && maintenanceSceneNames.Length > 0)
            return true;

        GameSession session = GameSession.Instance;

        if (session != null && session.currentPhase == WavePhase.Defense)
            return true;

        if (session != null && session.currentPhase == WavePhase.Maintenance)
            return true;

        if (session != null && session.defenseSceneNames != null && session.defenseSceneNames.Length > 0)
            return true;

        if (session != null && session.maintenanceSceneNames != null && session.maintenanceSceneNames.Length > 0)
            return true;

        return false;
    }
    private IEnumerator PhaseLoopWithSceneTransitions()
    {
        GameSession session = GameSession.GetOrCreate();

        if (currentPhase == WavePhase.Maintenance)
        {
            Debug.Log($"웨이브 {currentWave} 정비 페이즈 시작");
            yield return RunMaintenancePhase();

            yield return new WaitForSeconds(phaseTransitionDelay);

            if (session.nextPhaseAfterMaintenance == WavePhase.Defense)
            {
                currentPhase = WavePhase.Defense;
                MoveToDefenseScene();
            }
            else
            {
                currentPhase = WavePhase.Attack;
                MoveToAttackScene();
            }

            yield break;
        }

        if (currentPhase == WavePhase.Defense)
        {
            Debug.Log($"웨이브 {currentWave} 방어 페이즈 시작");
            TeleportPlayer(homeSpawnPoint);
            yield return RunPhase(WavePhase.Defense, defensePhaseDuration, defenseSpawnInterval, GetDefenseMaxAliveEnemies(), defenseSpawnPoints, true);

            ClearAliveEnemies();
            WaveCleared?.Invoke(currentWave);
            Debug.Log($"웨이브 {currentWave} 클리어");

            currentWave++;
            currentPhase = WavePhase.Maintenance;
            yield return new WaitForSeconds(phaseTransitionDelay);
            MoveToMaintenanceScene(WavePhase.Attack);
            yield break;
        }

        currentPhase = WavePhase.Attack;
        WaveStarted?.Invoke(currentWave);
        Debug.Log($"웨이브 {currentWave} 공격 페이즈 시작");

        TeleportPlayer(attackStartPoint);
        yield return RunPhase(WavePhase.Attack, attackPhaseDuration, attackSpawnInterval, GetAttackMaxAliveEnemies(), attackSpawnPoints, false);

        ClearAliveEnemies();
        Debug.Log("공격 페이즈 종료: 정비 맵으로 이동");

        currentPhase = WavePhase.Maintenance;
        yield return new WaitForSeconds(phaseTransitionDelay);
        MoveToMaintenanceScene(WavePhase.Defense);
    }

    private IEnumerator RunMaintenancePhase()
    {
        ChangePhase(WavePhase.Maintenance, maintenancePhaseDuration);

        while (phaseTimeRemaining > 0f)
        {
            phaseTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        phaseTimeRemaining = 0f;
    }

    private IEnumerator RunPhase(WavePhase phase, float duration, float baseSpawnInterval, int maxAliveEnemies, Transform[] spawnPoints, bool targetHome)
    {
        ChangePhase(phase, duration);

        if (phase == WavePhase.Attack)
            BeginAttackZoneSpawnBudgets();

        float spawnTimer = 0f;
        float spawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval - (currentWave - 1) * spawnIntervalDecreasePerWave);
        bool useAttackZoneSpawns = phase == WavePhase.Attack && HasAttackSpawnZones();

        while (phaseTimeRemaining > 0f)
        {
            phaseTimeRemaining -= Time.deltaTime;

            if (useAttackZoneSpawns)
            {
                TickAttackSpawnZoneCooldowns(Time.deltaTime);
                TrySpawnReadyAttackZoneBatches(maxAliveEnemies);
                yield return null;
                continue;
            }

            spawnTimer -= Time.deltaTime;

            if (spawnTimer <= 0f)
            {
                TrySpawnEnemy(phase, maxAliveEnemies, spawnPoints, targetHome);
                spawnTimer = spawnInterval;
            }

            yield return null;
        }

        phaseTimeRemaining = 0f;
    }

    private void TrySpawnEnemy(WavePhase phase, int maxAliveEnemies, Transform[] spawnPoints, bool targetHome)
    {
        RemoveNullEnemies();

        if (aliveEnemies.Count >= maxAliveEnemies)
            return;

        if (phase == WavePhase.Attack && HasAttackSpawnZones())
        {
            TrySpawnReadyAttackZoneBatches(maxAliveEnemies);
            return;
        }

        GameObject prefab = ChooseEnemyPrefab(phase);

        if (prefab == null)
            return;

        Transform spawnPoint = ChooseSpawnPoint(spawnPoints, phase);

        if (spawnPoint == null)
        {
            Debug.Log($"{phase} 페이즈: 플레이어가 스폰 인식 범위 밖에 있어서 적을 생성하지 않았습니다.");
            return;
        }

        GameObject enemyObject = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        EnemyController enemyController = enemyObject.GetComponent<EnemyController>();

        if (enemyController == null)
        {
            enemyController = enemyObject.GetComponentInChildren<EnemyController>();
        }

        if (enemyController != null)
        {
            float statMultiplier = CalculateEnemyStatMultiplier(phase, spawnPoint.position);
            enemyController.ApplyRuntimeScaling(statMultiplier);

            if (phase == WavePhase.Defense)
                enemyController.ConfigureMovement(defenseEnemyMovementMode, GetDefenseWaypointsForSpawn(spawnPoint));
        }

        if (targetHome && enemyController != null && homeTargetPoint != null)
        {
            enemyController.SetTarget(homeTargetPoint, homeBase);
        }

        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            enemyHealth = enemyObject.GetComponentInChildren<EnemyHealth>();
        }

        if (enemyHealth == null)
        {
            Debug.LogWarning($"{prefab.name} 프리팹에 EnemyHealth가 없습니다.");
            return;
        }

        aliveEnemies.Add(enemyHealth);
        enemyHealth.Died += OnEnemyDied;
    }

    private List<Transform> GetDefenseWaypointsForSpawn(Transform spawnPoint)
    {
        if (defensePaths == null || defensePaths.Count == 0)
            return defenseWaypoints;

        DefenseWaypointPath exactPath = null;
        DefenseWaypointPath nearestPath = null;
        float nearestDistance = float.MaxValue;

        foreach (DefenseWaypointPath path in defensePaths)
        {
            if (path == null || path.waypoints == null || path.waypoints.Count == 0)
                continue;

            if (path.spawnPoint != null && path.spawnPoint == spawnPoint)
            {
                exactPath = path;
                break;
            }

            Transform firstWaypoint = GetFirstValidWaypoint(path.waypoints);

            if (spawnPoint == null || firstWaypoint == null)
                continue;

            float distance = Vector2.Distance(spawnPoint.position, firstWaypoint.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPath = path;
            }
        }

        if (exactPath != null)
            return exactPath.waypoints;

        if (nearestPath != null)
            return nearestPath.waypoints;

        return defenseWaypoints;
    }

    private Transform GetFirstValidWaypoint(List<Transform> waypoints)
    {
        if (waypoints == null)
            return null;

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint != null)
                return waypoint;
        }

        return null;
    }

    private void BeginAttackZoneSpawnBudgets()
    {
        AttackSpawnZone[] zones = GetAttackSpawnZones();

        if (zones == null)
            return;

        foreach (AttackSpawnZone zone in zones)
        {
            if (zone != null)
                zone.BeginAttackPhase();
        }
    }

    private void TickAttackSpawnZoneCooldowns(float deltaTime)
    {
        AttackSpawnZone[] zones = GetAttackSpawnZones();

        if (zones == null)
            return;

        foreach (AttackSpawnZone zone in zones)
        {
            if (zone != null)
                zone.TickCooldown(deltaTime);
        }
    }

    private void TrySpawnReadyAttackZoneBatches(int maxAliveEnemies)
    {
        RemoveNullEnemies();

        if (aliveEnemies.Count >= maxAliveEnemies)
            return;

        AttackSpawnZone[] zones = GetAttackSpawnZones();

        if (zones == null || zones.Length == 0)
            return;

        foreach (AttackSpawnZone zone in zones)
        {
            if (zone == null || !zone.CanSpawnBatch(currentWave))
                continue;

            int remainingGlobalSlots = maxAliveEnemies - aliveEnemies.Count;

            if (remainingGlobalSlots <= 0)
                return;

            int minimumBatchCount = Mathf.Max(1, zone.minEnemiesPerSpawn);

            if (remainingGlobalSlots < minimumBatchCount)
                continue;

            int batchCount = Mathf.Min(zone.GetRandomSpawnBatchCount(), remainingGlobalSlots);
            pendingZoneSpawns.Clear();

            for (int i = 0; i < batchCount; i++)
            {
                if (!zone.TryChooseSpawn(player, spawnAwarenessRange, out ZoneSpawnResult zoneSpawn))
                    break;

                pendingZoneSpawns.Add(zoneSpawn);
            }

            if (pendingZoneSpawns.Count < minimumBatchCount)
            {
                continue;
            }

            for (int i = 0; i < pendingZoneSpawns.Count; i++)
            {
                if (!TrySpawnSingleAttackZoneEnemy(zone, pendingZoneSpawns[i]))
                    break;
            }
        }
    }

    private bool TrySpawnSingleAttackZoneEnemy(AttackSpawnZone zone, ZoneSpawnResult zoneSpawn)
    {
        if (zone == null)
            return false;

        GameObject prefab = zone.HasEnemyEntries()
            ? zone.ChooseEnemyPrefab(currentWave)
            : ChooseEnemyPrefab(WavePhase.Attack);

        if (prefab == null)
            return false;

        GameObject enemyObject = Instantiate(prefab, zoneSpawn.position, Quaternion.identity);
        EnemyController enemyController = enemyObject.GetComponent<EnemyController>();

        if (enemyController == null)
        {
            enemyController = enemyObject.GetComponentInChildren<EnemyController>();
        }

        if (enemyController != null)
        {
            float statMultiplier = CalculateEnemyStatMultiplier(WavePhase.Attack, zoneSpawn.position);
            statMultiplier *= Mathf.Max(0.1f, zone.zoneStatMultiplier);
            statMultiplier *= Mathf.Max(0.1f, zoneSpawn.statMultiplier);
            enemyController.ApplyHealthAndAttackScaling(statMultiplier);
            enemyController.ConfigureMovement(zone.enemyMovementMode, zone.waypoints);
        }

        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            enemyHealth = enemyObject.GetComponentInChildren<EnemyHealth>();
        }

        if (enemyHealth == null)
        {
            Debug.LogWarning($"{prefab.name} 프리팹에 EnemyHealth가 없습니다.");
            return false;
        }

        aliveEnemies.Add(enemyHealth);
        enemyHealth.Died += OnEnemyDied;
        zone.RegisterSpawnedEnemy(enemyHealth);
        return true;
    }

    private AttackSpawnZone[] GetAttackSpawnZones()
    {
        if (attackSpawnZones != null && attackSpawnZones.Length > 0)
            return attackSpawnZones;

        if (!autoFindAttackSpawnZones)
            return attackSpawnZones;

        attackSpawnZones = FindObjectsByType<AttackSpawnZone>(FindObjectsSortMode.None);
        return attackSpawnZones;
    }

    private bool HasAttackSpawnZones()
    {
        AttackSpawnZone[] zones = GetAttackSpawnZones();
        return zones != null && zones.Length > 0;
    }

    private GameObject ChooseEnemyPrefab(WavePhase phase)
    {
        int totalWeight = 0;

        foreach (EnemySpawnEntry entry in enemies)
        {
            if (!CanSpawnEntry(entry, phase))
                continue;

            totalWeight += Mathf.Max(0, entry.weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (EnemySpawnEntry entry in enemies)
        {
            if (!CanSpawnEntry(entry, phase))
                continue;

            currentWeight += Mathf.Max(0, entry.weight);

            if (roll < currentWeight)
                return entry.prefab;
        }

        return null;
    }

    private bool CanSpawnEntry(EnemySpawnEntry entry, WavePhase phase)
    {
        if (entry == null || entry.prefab == null)
            return false;

        if (currentWave < entry.unlockWave)
            return false;

        if (phase == WavePhase.Attack && !entry.spawnInAttackPhase)
            return false;

        if (phase == WavePhase.Defense && !entry.spawnInDefensePhase)
            return false;

        return entry.weight > 0;
    }

    private Transform ChooseSpawnPoint(Transform[] spawnPoints, WavePhase phase)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        bool requirePlayerNear = phase == WavePhase.Attack
            ? requirePlayerNearAttackSpawn
            : requirePlayerNearDefenseSpawn;

        if (!requirePlayerNear || player == null)
        {
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }

        List<Transform> validSpawnPoints = new List<Transform>();

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
                continue;

            float distanceToPlayer = Vector2.Distance(player.position, spawnPoint.position);

            if (distanceToPlayer <= spawnAwarenessRange)
            {
                validSpawnPoints.Add(spawnPoint);
            }
        }

        if (validSpawnPoints.Count == 0)
            return null;

        return validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Count)];
    }

    private float CalculateEnemyStatMultiplier(WavePhase phase, Vector3 spawnPosition)
    {
        float multiplier = 1f;

        if (useWaveScaling)
        {
            multiplier += Mathf.Max(0, currentWave - 1) * enemyStatIncreasePerWave;
        }

        if (phase == WavePhase.Attack && useDistanceScalingInAttackPhase)
        {
            Transform origin = GetDistanceOrigin();

            if (origin != null && distanceForMaxBonus > 0f)
            {
                float distance = Vector2.Distance(origin.position, spawnPosition);
                float distanceRate = Mathf.Clamp01(distance / distanceForMaxBonus);
                multiplier += maxDistanceStatBonus * distanceRate;
            }
        }

        return Mathf.Max(0.1f, multiplier);
    }

    private Transform GetDistanceOrigin()
    {
        if (distanceOrigin != null)
            return distanceOrigin;

        if (homeTargetPoint != null)
            return homeTargetPoint;

        if (homeBase != null)
            return homeBase.transform;

        return homeSpawnPoint;
    }

    public void HandlePlayerDefenseDeath(PlayerStats playerStats)
    {
        StartCoroutine(RespawnPlayerAfterDelay(playerStats));
    }

    private IEnumerator RespawnPlayerAfterDelay(PlayerStats playerStats)
    {
        yield return new WaitForSeconds(defenseRespawnDelay);

        if (playerStats == null)
            yield break;

        Vector3 respawnPosition = homeSpawnPoint != null ? homeSpawnPoint.position : playerStats.transform.position;
        playerStats.RespawnAt(respawnPosition);
    }

    private void TeleportPlayer(Transform point)
    {
        if (player == null || point == null)
            return;

        player.position = point.position;
    }

    private void ClearAliveEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = aliveEnemies[i];

            if (enemy != null)
            {
                enemy.Died -= OnEnemyDied;
                Destroy(enemy.gameObject);
            }
        }

        aliveEnemies.Clear();
    }

    private void OnEnemyDied(EnemyHealth enemy)
    {
        if (enemy != null)
        {
            enemy.Died -= OnEnemyDied;
        }

        aliveEnemies.Remove(enemy);
    }

    private void RemoveNullEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    private void ChangePhase(WavePhase phase, float duration)
    {
        currentPhase = phase;
        phaseTimeRemaining = duration;
        skipHoldTimer = 0f;
        PhaseChanged?.Invoke(currentPhase);
        Debug.Log($"페이즈 변경: {currentPhase}");
    }

    private void UpdateDebugPhaseSkip()
    {
        if (!enableHoldSkipToNextPhase || phaseRoutine == null || phaseTimeRemaining <= 0f)
        {
            skipHoldTimer = 0f;
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.fKey.isPressed)
        {
            skipHoldTimer = 0f;
            return;
        }

        skipHoldTimer += Time.unscaledDeltaTime;

        if (skipHoldTimer < skipHoldDuration)
            return;

        phaseTimeRemaining = 0f;
        skipHoldTimer = 0f;
        Debug.Log($"F 키 홀드로 {currentPhase} 페이즈를 넘겼습니다.");
    }

    private int GetAttackMaxAliveEnemies()
    {
        return attackMaxAliveEnemies + (currentWave - 1) * extraMaxAlivePerWave;
    }

    private int GetDefenseMaxAliveEnemies()
    {
        return defenseMaxAliveEnemies + (currentWave - 1) * extraMaxAlivePerWave;
    }
}







