using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class WaveStatusUI : MonoBehaviour
{
    private static WaveStatusUI instance;

    [Header("UI References")]
    [Tooltip("비워두면 이름이 Time인 TMP 텍스트를 자동으로 찾습니다.")]
    public TMP_Text nextSceneTimeText;
    [Tooltip("비워두면 이름이 Monster인 TMP 텍스트를 자동으로 찾습니다.")]
    public TMP_Text aliveEnemyCountText;
    [Tooltip("비워두면 이름이 Gold인 TMP 텍스트를 자동으로 찾습니다.")]
    public TMP_Text goldText;

    [Header("Text Format")]
    public string timeTextObjectName = "Time";
    public string enemyCountTextObjectName = "Monster";
    public string goldTextObjectName = "Gold";
    public string timePrefix = "";
    public string enemyCountPrefix = "";
    public string goldPrefix = "";
    public bool useMinuteSecondFormat = true;

    [Header("Persistence")]
    public bool keepUiAcrossScenes = true;
    public int persistentCanvasSortingOrder = 0;

    private WaveManager waveManager;
    private PlayerStats playerStats;
    private float nextReferenceSearchTime;
    private Canvas persistentCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAutomatically()
    {
        if (instance != null)
            return;

        WaveStatusUI existing = FindFirstObjectByType<WaveStatusUI>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject uiController = new GameObject("WaveStatusUI");
        uiController.AddComponent<WaveStatusUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        FindReferences();
        EnsureStatusUiPersistence();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        waveManager = null;
        playerStats = null;

        if (!keepUiAcrossScenes)
        {
            nextSceneTimeText = null;
            aliveEnemyCountText = null;
            goldText = null;
        }

        FindReferences();
        EnsureStatusUiPersistence();
        DisableDuplicateSceneStatusUi();
        Refresh();
    }

    private void Update()
    {
        if (waveManager == null || playerStats == null ||
            nextSceneTimeText == null || aliveEnemyCountText == null || goldText == null)
        {
            if (Time.unscaledTime >= nextReferenceSearchTime)
            {
                nextReferenceSearchTime = Time.unscaledTime + 0.5f;
                FindReferences();
                EnsureStatusUiPersistence();
            }
        }

        Refresh();
    }

    private void FindReferences()
    {
        if (waveManager == null)
            waveManager = WaveManager.Instance != null
                ? WaveManager.Instance
                : FindFirstObjectByType<WaveManager>();

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (nextSceneTimeText != null && aliveEnemyCountText != null && goldText != null)
            return;

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (TMP_Text text in texts)
        {
            if (text == null || !text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            if (nextSceneTimeText == null && text.name == timeTextObjectName)
                nextSceneTimeText = text;

            if (aliveEnemyCountText == null && text.name == enemyCountTextObjectName)
                aliveEnemyCountText = text;

            if (goldText == null && text.name == goldTextObjectName)
                goldText = text;
        }
    }

    private void EnsureStatusUiPersistence()
    {
        if (!keepUiAcrossScenes || nextSceneTimeText == null ||
            aliveEnemyCountText == null || goldText == null)
        {
            return;
        }

        if (persistentCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("PersistentGameStatusCanvas");

            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    "PersistentGameStatusCanvas",
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0f;
            }

            persistentCanvas = canvasObject.GetComponent<Canvas>();
            persistentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            persistentCanvas.overrideSorting = true;
            persistentCanvas.sortingOrder = persistentCanvasSortingOrder;
            DontDestroyOnLoad(canvasObject);
        }

        MovePanelToPersistentCanvas(nextSceneTimeText);
        MovePanelToPersistentCanvas(aliveEnemyCountText);
        MovePanelToPersistentCanvas(goldText);
    }

    private void MovePanelToPersistentCanvas(TMP_Text text)
    {
        if (text == null || persistentCanvas == null)
            return;

        Transform panel = text.transform.parent != null ? text.transform.parent : text.transform;

        if (panel.parent == persistentCanvas.transform)
            return;

        RectTransform panelRect = panel as RectTransform;
        Vector2 anchoredPosition = panelRect != null ? panelRect.anchoredPosition : Vector2.zero;
        Vector2 anchorMin = panelRect != null ? panelRect.anchorMin : Vector2.zero;
        Vector2 anchorMax = panelRect != null ? panelRect.anchorMax : Vector2.one;
        Vector2 pivot = panelRect != null ? panelRect.pivot : new Vector2(0.5f, 0.5f);
        Vector2 sizeDelta = panelRect != null ? panelRect.sizeDelta : Vector2.zero;

        panel.SetParent(persistentCanvas.transform, false);

        if (panelRect != null)
        {
            panelRect.anchorMin = anchorMin;
            panelRect.anchorMax = anchorMax;
            panelRect.pivot = pivot;
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = sizeDelta;
        }
    }

    private void DisableDuplicateSceneStatusUi()
    {
        if (!keepUiAcrossScenes || persistentCanvas == null)
            return;

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (TMP_Text text in texts)
        {
            if (text == null || text == nextSceneTimeText ||
                text == aliveEnemyCountText || text == goldText)
            {
                continue;
            }

            bool isStatusText = text.name == timeTextObjectName ||
                text.name == enemyCountTextObjectName ||
                text.name == goldTextObjectName;

            if (!isStatusText)
                continue;

            GameObject panelObject = text.transform.parent != null
                ? text.transform.parent.gameObject
                : text.gameObject;

            panelObject.SetActive(false);
        }
    }

    private void Refresh()
    {
        if (goldText != null)
        {
            int currentGold = playerStats != null
                ? playerStats.gold
                : GameSession.Instance != null ? GameSession.Instance.gold : 0;

            goldText.text = $"{goldPrefix}{currentGold}";
        }

        if (waveManager == null)
            return;

        if (nextSceneTimeText != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(waveManager.PhaseTimeRemaining));

            if (useMinuteSecondFormat)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                nextSceneTimeText.text = $"{timePrefix}{minutes}:{seconds:00}";
            }
            else
            {
                nextSceneTimeText.text = $"{timePrefix}{totalSeconds}";
            }
        }

        if (aliveEnemyCountText != null)
            aliveEnemyCountText.text = $"{enemyCountPrefix}{waveManager.AliveEnemyCount}";
    }
}
