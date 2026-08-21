using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class BossExitPortal : MonoBehaviour
{
    [Header("Boss")]
    [Tooltip("비워두면 씬의 Boss1, Boss2, Boss3를 자동으로 찾습니다.")]
    [SerializeField] private EnemyHealth bossHealth;

    [Header("Portal")]
    [SerializeField] private GameObject portalVisual;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool loadNextBuildSceneWhenNameIsEmpty = true;
    [SerializeField] private bool showBeforeBossDefeat;
    [SerializeField] private bool requireBossDefeat = true;
    [SerializeField] private bool requireFKey = true;

    private Collider2D portalCollider;
    private Renderer portalRenderer;
    private bool isUnlocked;
    private bool isLoading;
    private bool playerInside;

    public static void EnsurePortalForDefeatedBoss(EnemyHealth defeatedBoss)
    {
        if (defeatedBoss == null || FindFirstObjectByType<BossExitPortal>() != null)
            return;

        Debug.LogWarning(
            $"{defeatedBoss.name}: 씬에 BossExitPortal이 없습니다. 원하는 포털 이미지가 연결된 포털 오브젝트를 씬에 배치하세요.",
            defeatedBoss);
    }

    private void Awake()
    {
        portalCollider = GetComponent<Collider2D>();
        portalCollider.isTrigger = true;

        if (portalVisual == null)
        {
            portalRenderer = GetComponentInChildren<Renderer>(true);
            if (portalRenderer != null && portalRenderer.gameObject != gameObject)
                portalVisual = portalRenderer.gameObject;
        }

    }

    private void Start()
    {
        if (bossHealth == null)
            bossHealth = FindBossHealth();

        if (bossHealth != null)
            bossHealth.Died += HandleBossDefeated;
        else if (bossHealth == null && requireBossDefeat)
            Debug.LogWarning($"{name}: 보스 EnemyHealth를 찾지 못했습니다.", this);

        if (requireBossDefeat)
        {
            SetPortalState(false);
        }
        else
        {
            UnlockPortal();
        }
    }

    private void Update()
    {
        if (!isUnlocked || isLoading || !playerInside || !requireFKey)
            return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            LoadNextScene();
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
            bossHealth.Died -= HandleBossDefeated;
    }

    private EnemyHealth FindBossHealth()
    {
        EnemyHealth[] candidates = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (EnemyHealth candidate in candidates)
        {
            if (candidate != null &&
                (candidate.GetComponent<BossController>() != null ||
                 candidate.GetComponent("Boss2Controller") != null ||
                 candidate.GetComponent("Boss3Controller") != null))
                return candidate;
        }
        return null;
    }

    private void HandleBossDefeated(EnemyHealth defeatedBoss)
    {
        BeginPortalReveal(defeatedBoss);
    }

    private void BeginPortalReveal(EnemyHealth defeatedBoss)
    {
        transform.position = defeatedBoss.transform.position + spawnOffset;
        StartCoroutine(RevealAfterDelay(Mathf.Max(0f, defeatedBoss.deathAnimationDuration)));
    }

    private IEnumerator RevealAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        UnlockPortal();
    }

    public void UnlockPortal()
    {
        isUnlocked = true;
        SetPortalState(true);
    }

    private void SetPortalState(bool unlocked)
    {
        if (portalCollider != null)
            portalCollider.enabled = unlocked;
        if (portalVisual != null)
            portalVisual.SetActive(unlocked || showBeforeBossDefeat);
        if (portalRenderer != null && portalVisual == null)
            portalRenderer.enabled = unlocked || showBeforeBossDefeat;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayer(other))
            return;
        playerInside = true;
        if (isUnlocked && !requireFKey)
            LoadNextScene();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (TryGetPlayer(other))
            playerInside = false;
    }

    private static bool TryGetPlayer(Collider2D other)
    {
        return other.GetComponent<PlayerStats>() != null ||
               other.GetComponentInParent<PlayerStats>() != null;
    }

    private void LoadNextScene()
    {
        if (isLoading)
            return;

        string sceneToLoad = nextSceneName;
        if (string.IsNullOrWhiteSpace(sceneToLoad) && loadNextBuildSceneWhenNameIsEmpty)
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextIndex >= 0 && nextIndex < SceneManager.sceneCountInBuildSettings)
                sceneToLoad = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(nextIndex));
        }

        if (string.IsNullOrWhiteSpace(sceneToLoad) || !Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogWarning($"{name}: 이동할 다음 씬이 Build Settings에 없습니다.", this);
            return;
        }

        isLoading = true;
        Time.timeScale = 1f;
        SceneFadeManager.LoadScene(sceneToLoad);
    }

}
