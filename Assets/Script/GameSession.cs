using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Wave")]
    public int currentWave = 1;
    public WavePhase currentPhase = WavePhase.Attack;
    public WavePhase nextPhaseAfterMaintenance = WavePhase.Defense;
    public int currentRoundIndex;
    public bool healPlayerToFullOnSceneLoad = true;

    [Header("Player Stats")]
    public int level = 1;
    public int exp = 0;
    public int expToNextLevel = 100;
    public int hp = 100;
    public int maxHp = 100;
    public int attackPower = 10;
    public int defensePower = 2;
    public float attackSpeed = 1f;
    public float criticalChance = 0.1f;
    public float criticalDamage = 1.5f;
    public float skillCooldownReduction;
    public float moveSpeed = 5f;
    public int gold = 0;
    public int crystal = 0;
    public int trainingPoints;
    public int augmentPoints;
    public int attackTrainingLevel;
    public int attackSpeedTrainingLevel;
    public int maxHpTrainingLevel;
    public PlayerRole playerRole = PlayerRole.Sword;
    public System.Collections.Generic.List<string> selectedAugmentNames = new System.Collections.Generic.List<string>();

    [Header("Shop Inventory")]
    public System.Collections.Generic.List<string> shopInventoryItemIds = new System.Collections.Generic.List<string>();
    public System.Collections.Generic.List<string> purchasedUniqueShopItemIds = new System.Collections.Generic.List<string>();

    [Header("Scene Names")]
    public string currentAttackSceneName;
    public string[] attackSceneNames;
    public string[] maintenanceSceneNames;
    public string[] defenseSceneNames;

    private bool hasSavedPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public static GameSession GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject sessionObject = new GameObject("GameSession");
        return sessionObject.AddComponent<GameSession>();
    }

    public void SetSelectedRole(PlayerRole selectedRole)
    {
        playerRole = selectedRole;
        Debug.Log($"Selected player role: {playerRole}");
    }

    public void SavePlayer(PlayerStats stats, PlayerController controller)
    {
        if (stats == null)
            return;

        level = stats.level;
        exp = stats.exp;
        expToNextLevel = stats.expToNextLevel;
        hp = Mathf.Max(1, stats.hp);
        maxHp = stats.maxHp;
        attackPower = stats.attackPower;
        defensePower = stats.defensePower;
        attackSpeed = stats.attackSpeed;
        criticalChance = stats.criticalChance;
        criticalDamage = stats.criticalDamage;
        skillCooldownReduction = stats.skillCooldownReduction;
        moveSpeed = stats.moveSpeed;
        gold = stats.gold;
        crystal = stats.crystal;
        trainingPoints = stats.trainingPoints;
        augmentPoints = stats.augmentPoints;
        attackTrainingLevel = stats.attackTrainingLevel;
        attackSpeedTrainingLevel = stats.attackSpeedTrainingLevel;
        maxHpTrainingLevel = stats.maxHpTrainingLevel;

        if (controller != null)
        {
            playerRole = controller.CurrentRole;
        }

        PlayerAugmentController augmentController = stats.GetComponent<PlayerAugmentController>();
        selectedAugmentNames.Clear();
        if (augmentController != null && augmentController.selectedAugmentNames != null)
        {
            for (int i = 0; i < augmentController.selectedAugmentNames.Count; i++)
            {
                string augmentName = augmentController.selectedAugmentNames[i];
                if (!string.IsNullOrWhiteSpace(augmentName) && !selectedAugmentNames.Contains(augmentName))
                    selectedAugmentNames.Add(augmentName);
            }
        }

        hasSavedPlayer = true;
    }

    public void SaveShopInventory(
        System.Collections.Generic.IList<string> inventoryItemIds,
        System.Collections.Generic.IEnumerable<string> uniqueItemIds)
    {
        shopInventoryItemIds.Clear();
        purchasedUniqueShopItemIds.Clear();

        if (inventoryItemIds != null)
        {
            for (int i = 0; i < inventoryItemIds.Count; i++)
            {
                string itemId = inventoryItemIds[i];
                if (!string.IsNullOrWhiteSpace(itemId))
                    shopInventoryItemIds.Add(itemId);
            }
        }

        if (uniqueItemIds == null)
            return;

        foreach (string itemId in uniqueItemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !purchasedUniqueShopItemIds.Contains(itemId))
            {
                purchasedUniqueShopItemIds.Add(itemId);
            }
        }
    }

    public void LoadPlayer(PlayerStats stats, PlayerController controller)
    {
        if (controller != null)
        {
            controller.SetRole(playerRole);
        }

        if (stats == null || !hasSavedPlayer)
            return;

        stats.level = level;
        stats.exp = exp;
        stats.expToNextLevel = expToNextLevel;
        stats.maxHp = maxHp;
        stats.hp = healPlayerToFullOnSceneLoad ? maxHp : Mathf.Clamp(hp, 1, maxHp);
        hp = stats.hp;
        stats.attackPower = attackPower;
        stats.defensePower = defensePower;
        stats.attackSpeed = attackSpeed;
        stats.criticalChance = criticalChance;
        stats.criticalDamage = criticalDamage;
        stats.skillCooldownReduction = skillCooldownReduction;
        stats.moveSpeed = moveSpeed;
        stats.gold = gold;
        stats.crystal = crystal;
        stats.trainingPoints = trainingPoints;
        stats.augmentPoints = augmentPoints;
        stats.attackTrainingLevel = attackTrainingLevel;
        stats.attackSpeedTrainingLevel = attackSpeedTrainingLevel;
        stats.maxHpTrainingLevel = maxHpTrainingLevel;
        stats.isDead = false;
        stats.ClearDefenseBuffs();

        PlayerAugmentController augmentController = stats.GetComponent<PlayerAugmentController>();
        if (augmentController == null)
            augmentController = stats.gameObject.AddComponent<PlayerAugmentController>();

        augmentController.selectedAugmentNames.Clear();
        augmentController.RestoreSelectedAugments(selectedAugmentNames);

        if (controller != null)
        {
            controller.HandleRespawn();
        }
    }

    public void SaveWave(WaveManager waveManager)
    {
        if (waveManager == null)
            return;

        currentWave = waveManager.currentWave;
        currentPhase = waveManager.CurrentPhase;

        if (waveManager.CurrentPhase == WavePhase.Attack)
        {
            currentAttackSceneName = SceneManager.GetActiveScene().name;
        }

        if (waveManager.attackSceneNames != null && waveManager.attackSceneNames.Length > 0)
        {
            attackSceneNames = waveManager.attackSceneNames;
        }

        if (waveManager.maintenanceSceneNames != null && waveManager.maintenanceSceneNames.Length > 0)
        {
            maintenanceSceneNames = waveManager.maintenanceSceneNames;
        }

        if (waveManager.defenseSceneNames != null && waveManager.defenseSceneNames.Length > 0)
        {
            defenseSceneNames = waveManager.defenseSceneNames;
        }
    }

    public void ResetForNewGame()
    {
        currentWave = 1;
        currentPhase = WavePhase.Attack;
        nextPhaseAfterMaintenance = WavePhase.Defense;
        currentRoundIndex = 0;

        level = 1;
        exp = 0;
        expToNextLevel = 100;
        hp = 100;
        maxHp = 100;
        attackPower = 10;
        defensePower = 2;
        attackSpeed = 1f;
        criticalChance = 0.1f;
        criticalDamage = 1.5f;
        skillCooldownReduction = 0f;
        moveSpeed = 5f;
        gold = 0;
        crystal = 0;
        trainingPoints = 0;
        augmentPoints = 0;
        attackTrainingLevel = 0;
        attackSpeedTrainingLevel = 0;
        maxHpTrainingLevel = 0;
        shopInventoryItemIds.Clear();
        purchasedUniqueShopItemIds.Clear();
        selectedAugmentNames.Clear();

        currentAttackSceneName = string.Empty;
        hasSavedPlayer = false;
        Time.timeScale = 1f;
    }

    public void LoadSavedAttackScene()
    {
        if (!string.IsNullOrWhiteSpace(currentAttackSceneName))
        {
            LoadSceneByName(currentAttackSceneName, WavePhase.Attack);
            return;
        }

        LoadRandomScene(attackSceneNames, WavePhase.Attack);
    }

    public void LoadCurrentDefenseScene()
    {
        LoadSceneByOrder(defenseSceneNames, currentRoundIndex, WavePhase.Defense);
    }

    public void LoadCurrentMaintenanceScene(WavePhase nextPhase)
    {
        nextPhaseAfterMaintenance = nextPhase;
        LoadSceneByOrder(maintenanceSceneNames, currentRoundIndex, WavePhase.Maintenance);
    }

    public void LoadNextAttackScene()
    {
        currentRoundIndex = GetNextSceneIndex(attackSceneNames, currentRoundIndex);
        LoadSceneByOrder(attackSceneNames, currentRoundIndex, WavePhase.Attack);
    }

    public void LoadRandomAttackScene()
    {
        LoadRandomScene(attackSceneNames, WavePhase.Attack);
    }

    public void LoadRandomDefenseScene()
    {
        LoadRandomScene(defenseSceneNames, WavePhase.Defense);
    }

    private void LoadRandomScene(string[] sceneNames, WavePhase nextPhase)
    {
        if (sceneNames == null || sceneNames.Length == 0)
        {
            Debug.LogWarning($"{nextPhase} 씬 이름이 비어 있습니다. WaveManager의 Scene Transition 목록을 채워주세요.");
            return;
        }

        string sceneName = sceneNames[Random.Range(0, sceneNames.Length)];
        LoadSceneByName(sceneName, nextPhase);
    }

    private void LoadSceneByOrder(string[] sceneNames, int index, WavePhase nextPhase)
    {
        if (sceneNames == null || sceneNames.Length == 0)
        {
            Debug.LogWarning($"{nextPhase} 씬 이름이 비어 있습니다. WaveManager의 Scene Transition 목록을 채워주세요.");
            return;
        }

        int sceneIndex = Mathf.Abs(index) % sceneNames.Length;
        LoadSceneByName(sceneNames[sceneIndex], nextPhase);
    }

    private int GetNextSceneIndex(string[] sceneNames, int currentIndex)
    {
        if (sceneNames == null || sceneNames.Length == 0)
            return currentIndex + 1;

        return (currentIndex + 1) % sceneNames.Length;
    }

    private void LoadSceneByName(string sceneName, WavePhase nextPhase)
    {
        SaveCurrentPlayerOnly();
        currentPhase = nextPhase;
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"{nextPhase} 씬 이름이 비어 있습니다.");
            return;
        }

        SceneFadeManager.LoadScene(sceneName);
    }

    private void SaveCurrentPlayerOnly()
    {
        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        PlayerController controller = stats != null ? stats.GetComponent<PlayerController>() : FindFirstObjectByType<PlayerController>();
        SavePlayer(stats, controller);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        PlayerController controller = stats != null ? stats.GetComponent<PlayerController>() : FindFirstObjectByType<PlayerController>();

        LoadPlayer(stats, controller);
        MovePlayerToSpawnPoint(stats);
    }

    private void MovePlayerToSpawnPoint(PlayerStats stats)
    {
        if (stats == null)
            return;

        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
        Transform selectedSpawnPoint = null;

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint != null && spawnPoint.phase == currentPhase)
            {
                selectedSpawnPoint = spawnPoint.transform;
                break;
            }
        }

        if (selectedSpawnPoint == null)
        {
            GameObject namedSpawnPoint = GameObject.Find("PlayerSpawnPoint");

            if (namedSpawnPoint != null)
            {
                selectedSpawnPoint = namedSpawnPoint.transform;
            }
        }

        if (selectedSpawnPoint == null)
            return;

        stats.transform.position = selectedSpawnPoint.position;

        Rigidbody2D rb = stats.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}
