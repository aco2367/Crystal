using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AutoHpExpHud : MonoBehaviour
{
    public static AutoHpExpHud Instance { get; private set; }

    [Header("References")]
    public PlayerStats playerStats;

    [Header("Scene")]
    public bool keepAcrossScenes = true;
    [Tooltip("상점이나 수련장 같은 팝업 UI보다 뒤에 표시할 HUD Canvas 정렬 순서입니다.")]
    public int canvasSortingOrder = -100;

    [Header("Sprites")]
    public Sprite frameSprite;
    public Sprite hpFillSprite;
    public Sprite expFillSprite;

    [Header("Layout")]
    [Tooltip("켜면 아래 Position, Size, HUD Scale 값으로 배치를 강제합니다. 끄면 RectTransform을 Unity 에디터에서 자유롭게 배치할 수 있습니다.")]
    public bool controlLayoutFromScript;
    public Vector2 position = new Vector2(24f, -24f);
    public Vector2 size = new Vector2(360f, 34f);
    public Vector3 hudScale = new Vector3(2f, 2f, 2f);
    public float gap = 12f;
    public Vector2 fillPadding = new Vector2(14f, 8f);

    [Header("Text Optional")]
    public bool showText;
    public TMP_FontAsset font;
    public float textSize = 18f;

    [Header("Editor Preview")]
    public bool previewInEditMode = true;
    [Range(0f, 1f)] public float previewHpRate = 1f;
    [Range(0f, 1f)] public float previewExpRate = 0.35f;
    public int previewLevel = 1;
    public int previewHp = 100;
    public int previewMaxHp = 100;
    public int previewExp = 35;
    public int previewExpToNextLevel = 100;

    private RectTransform rootRect;
    private Image hpFrameImage;
    private Image expFrameImage;
    private Image hpFillImage;
    private Image expFillImage;
    private TMP_Text hpText;
    private TMP_Text expText;
    [Header("Optional UI Reference")]
    [Tooltip("비워두어도 이름이 LevelText인 TMP 텍스트를 자동으로 찾습니다.")]
    public TMP_Text levelText;
    private bool isBuilding;
    private bool isBuilt;

    private void Awake()
    {
        InitializePersistence();

        if (Instance != this)
            return;

        FindPlayerStatsIfNeeded();
        BuildHud();
        Refresh();
    }

    private void OnEnable()
    {
        InitializePersistence();

        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        FindPlayerStatsIfNeeded();
        BuildHud();
        Refresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        FindPlayerStatsIfNeeded();
        FindLevelTextIfNeeded();
        Refresh();
    }

    private void InitializePersistence()
    {
        if (!Application.isPlaying || !keepAcrossScenes)
            return;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        MoveToPersistentHudCanvas();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playerStats = null;
        levelText = null;
        FindPlayerStatsIfNeeded();
        FindLevelTextIfNeeded();
        Refresh();
    }

    private void MoveToPersistentHudCanvas()
    {
        Canvas currentCanvas = GetComponentInParent<Canvas>();

        if (currentCanvas != null && currentCanvas.name == "PersistentHpExpHudCanvas")
        {
            currentCanvas.overrideSorting = true;
            currentCanvas.sortingOrder = canvasSortingOrder;
            DontDestroyOnLoad(currentCanvas.gameObject);
            return;
        }

        GameObject canvasObject = GameObject.Find("PersistentHpExpHudCanvas");

        if (canvasObject == null)
        {
            canvasObject = new GameObject("PersistentHpExpHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            DontDestroyOnLoad(canvasObject);
        }

        Canvas persistentCanvas = canvasObject.GetComponent<Canvas>();
        persistentCanvas.overrideSorting = true;
        persistentCanvas.sortingOrder = canvasSortingOrder;

        transform.SetParent(canvasObject.transform, false);
    }

    private void BuildHud()
    {
        if (isBuilding || isBuilt)
            return;

        isBuilding = true;
        rootRect = GetHudRoot();

        // 씬에 배치된 UI를 참조만 한다. 런타임에 생성하거나 RectTransform을
        // 재설정하지 않으므로 사용자가 편집한 크기와 위치가 그대로 유지된다.
        hpFrameImage = FindImage(rootRect, "HPFrame");
        expFrameImage = FindImage(rootRect, "EXPFrame");
        hpFillImage = FindImage(hpFrameImage != null ? hpFrameImage.transform : null, "HPFill");
        expFillImage = FindImage(expFrameImage != null ? expFrameImage.transform : null, "EXPFill");
        hpText = FindText(hpFrameImage != null ? hpFrameImage.transform : null, "HPText");
        expText = FindText(expFrameImage != null ? expFrameImage.transform : null, "EXPText");
        levelText = FindText(rootRect, "LevelText");

        // LevelText를 HUD 루트의 형제로 직접 배치한 기존 씬 구성도 지원합니다.
        if (levelText == null && rootRect != null && rootRect.parent != null)
            levelText = FindText(rootRect.parent, "LevelText");

        FindLevelTextIfNeeded();

        SetTextActive(hpText, showText);
        SetTextActive(expText, showText);
        SetTextActive(levelText, showText);

        if (hpFillImage == null || expFillImage == null)
            Debug.LogWarning("AutoHpExpHud 아래에서 HPFill 또는 EXPFill을 찾지 못했습니다.", this);

        isBuilding = false;
        isBuilt = true;
    }

    private Image FindImage(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindText(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void FindLevelTextIfNeeded()
    {
        if (levelText != null)
            return;

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.name == "LevelText")
            {
                levelText = text;
                SetTextActive(levelText, showText);
                return;
            }
        }
    }

    private Canvas EnsureCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas;

        if (!Application.isPlaying)
            return null;

        GameObject canvasObject = new GameObject("AutoHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (keepAcrossScenes)
            DontDestroyOnLoad(canvasObject);

        transform.SetParent(canvas.transform, false);
        return canvas;
    }

    private RectTransform GetHudRoot()
    {
        RectTransform ownRect = transform as RectTransform;
        if (ownRect != null)
            return ownRect;

        Transform existing = transform.Find("HudRoot");
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            return existingRect;

        GameObject rootObject = new GameObject("HudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        return rootObject.GetComponent<RectTransform>();
    }

    private void ConfigureRootRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size.x, size.y * 2f + gap + 28f);
        rect.localScale = hudScale;
    }

    private Image CreateImage(string objectName, Transform parent, Sprite sprite, Image.Type imageType)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        imageObject.transform.SetParent(parent, false);

        if (!imageObject.TryGetComponent(out Image image))
            image = imageObject.AddComponent<Image>();

        if (imageObject.GetComponent<CanvasRenderer>() == null)
            imageObject.AddComponent<CanvasRenderer>();

        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.type = imageType;

        if (imageType == Image.Type.Filled)
        {
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
        }

        return image;
    }

    private void ConfigureFrame(RectTransform rect, float y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private void ConfigureFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(fillPadding.x, fillPadding.y);
        rect.offsetMax = new Vector2(-fillPadding.x, -fillPadding.y);
        rect.localScale = Vector3.one;
    }

    private TMP_Text CreateText(string objectName, Transform parent, TextAnchor anchor)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));

        textObject.transform.SetParent(parent, false);

        if (!textObject.TryGetComponent(out TMP_Text text))
            text = textObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.fontSize = textSize;
        text.color = Color.white;
        text.alignment = ConvertAnchor(anchor);
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        return text;
    }

    private void ConfigureCenterText(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ConfigureLevelText(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(0f, 6f);
        rect.sizeDelta = new Vector2(size.x, 24f);
        rect.localScale = Vector3.one;
    }

    private TextAlignmentOptions ConvertAnchor(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            _ => TextAlignmentOptions.Center
        };
    }

    private void Refresh()
    {
        int hp = previewHp;
        int maxHp = Mathf.Max(1, previewMaxHp);
        int exp = previewExp;
        int expToNext = Mathf.Max(1, previewExpToNextLevel);
        int level = previewLevel;
        float hpRate = Mathf.Clamp01(previewHpRate);
        float expRate = Mathf.Clamp01(previewExpRate);

        if (playerStats != null)
        {
            hp = playerStats.hp;
            maxHp = Mathf.Max(1, playerStats.maxHp);
            exp = playerStats.exp;
            expToNext = Mathf.Max(1, playerStats.expToNextLevel);
            level = playerStats.level;
            hpRate = Mathf.Clamp01((float)hp / maxHp);
            expRate = Mathf.Clamp01((float)exp / expToNext);
        }

        if (hpFillImage != null)
            hpFillImage.fillAmount = hpRate;

        if (expFillImage != null)
            expFillImage.fillAmount = expRate;

        if (showText)
        {
            if (hpText != null)
                hpText.text = $"{hp} / {maxHp}";

            if (expText != null)
                expText.text = $"EXP {exp} / {expToNext}";

            if (levelText != null)
                levelText.text = $"Lv.{level}";
        }
    }

    private void FindPlayerStatsIfNeeded()
    {
        if (playerStats != null)
            return;

        playerStats = FindFirstObjectByType<PlayerStats>();
    }

    private void SetTextActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }
}
