using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BossBattleTrigger : MonoBehaviour
{
    [Header("Boss Scenes")]
    public string[] bossSceneNames = { "BossMap", "BossMap-2" };
    public bool chooseRandomScene = true;
    [Min(0)] public int fixedSceneIndex;

    [Header("Interaction")]
    [Tooltip("플레이어가 범위 안에 있을 때만 표시할 안내 UI입니다. 비워도 입장 기능은 동작합니다.")]
    public GameObject interactionPrompt;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private PlayerStats currentPlayerStats;
    private bool isLoading;

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;

        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        currentPlayerStats = null;
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (isLoading || currentPlayerStats == null || playerColliders.Count == 0)
            return;

        if (!KeyBindingManager.WasPressedThisFrame(GameKeyAction.Interact))
            return;

        EnterBossBattle();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats stats = FindPlayerStats(other);
        if (stats == null)
            return;

        playerColliders.Add(other);
        currentPlayerStats = stats;
        SetPromptVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other))
            return;

        if (playerColliders.Count > 0)
            return;

        currentPlayerStats = null;
        SetPromptVisible(false);
    }

    private void EnterBossBattle()
    {
        string sceneName = SelectBossSceneName();
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("BossBattleTrigger의 Boss Scene Names가 비어 있습니다.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"보스 씬 '{sceneName}'을 로드할 수 없습니다. Build Profiles의 Scene List에 추가했는지 확인하세요.", this);
            return;
        }

        isLoading = true;
        SetPromptVisible(false);

        GameSession session = GameSession.GetOrCreate();
        PlayerController controller = currentPlayerStats.GetComponent<PlayerController>();
        if (controller == null)
            controller = currentPlayerStats.GetComponentInParent<PlayerController>();

        session.SavePlayer(currentPlayerStats, controller);
        Time.timeScale = 1f;
        SceneFadeManager.LoadScene(sceneName);
    }

    private string SelectBossSceneName()
    {
        if (bossSceneNames == null || bossSceneNames.Length == 0)
            return string.Empty;

        if (!chooseRandomScene)
        {
            int index = Mathf.Clamp(fixedSceneIndex, 0, bossSceneNames.Length - 1);
            return bossSceneNames[index];
        }

        List<string> validSceneNames = new List<string>();
        for (int i = 0; i < bossSceneNames.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(bossSceneNames[i]))
                validSceneNames.Add(bossSceneNames[i]);
        }

        if (validSceneNames.Count == 0)
            return string.Empty;

        return validSceneNames[Random.Range(0, validSceneNames.Count)];
    }

    private PlayerStats FindPlayerStats(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null)
            stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null)
            stats = other.GetComponentInChildren<PlayerStats>();
        return stats;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(visible);
    }
}
