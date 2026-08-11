using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerProfileUI : MonoBehaviour
{
    public static PlayerProfileUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("역할에 맞는 프로필 스프라이트를 표시할 UI Image입니다.")]
    public Image profileImage;
    [Tooltip("비워두면 실행 중인 PlayerController를 자동으로 찾습니다.")]
    public PlayerController playerController;

    [Header("Role Profile Sprites")]
    public Sprite swordProfile;
    public Sprite archerProfile;
    public Sprite tankProfile;

    [Header("Display")]
    public bool keepAcrossScenes = true;
    public bool preserveAspect = true;
    [Tooltip("프로필이 상점/수련장 팝업보다 뒤에 표시되도록 하는 정렬 순서입니다.")]
    public int canvasSortingOrder = -100;
    [Tooltip("해당 역할의 이미지가 비어 있을 때 Profile Image를 숨깁니다.")]
    public bool hideWhenSpriteIsMissing = true;
    [Min(0.05f)] public float playerSearchInterval = 0.5f;
    [Tooltip("체크하면 모든 씬에서 좌측 상단 기준으로 같은 위치와 크기를 사용합니다.")]
    public bool controlPersistentLayout = true;
    public Vector2 persistentPosition = new Vector2(74.38f, -124.77f);
    public Vector2 persistentSize = new Vector2(127.217f, 116.46f);

    private PlayerRole displayedRole;
    private bool hasDisplayedRole;
    private float nextPlayerSearchTime;

    private void Reset()
    {
        profileImage = GetComponent<Image>();
    }

    private void Awake()
    {
        InitializePersistence();

        if (Instance != this)
            return;

        if (profileImage == null)
            profileImage = GetComponent<Image>();

        ApplyCanvasSorting();
        ApplyImageSettings();
        FindPlayerIfNeeded();
        Refresh(true);
    }

    private void OnEnable()
    {
        InitializePersistence();

        if (Instance != this)
            return;

        FindPlayerIfNeeded();
        Refresh(true);
    }

    private void Update()
    {
        if (Instance != this)
            return;

        FindPlayerIfNeeded();

        PlayerRole role = GetCurrentRole();

        if (!hasDisplayedRole || displayedRole != role)
            Refresh(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void InitializePersistence()
    {
        if (!Application.isPlaying)
            return;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (keepAcrossScenes)
            MoveToPersistentHudCanvas();
    }

    private void MoveToPersistentHudCanvas()
    {
        GameObject canvasObject = GameObject.Find("PersistentHpExpHudCanvas");

        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                "PersistentHpExpHudCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        DontDestroyOnLoad(canvasObject);

        if (transform.parent != canvasObject.transform)
            transform.SetParent(canvasObject.transform, false);

        ApplyPersistentLayout();
    }

    private void ApplyPersistentLayout()
    {
        if (!controlPersistentLayout || !(transform is RectTransform rect))
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = persistentPosition;
        rect.sizeDelta = persistentSize;
        rect.localScale = Vector3.one;
    }

    public void Refresh(bool force = false)
    {
        if (profileImage == null)
            return;

        PlayerRole role = GetCurrentRole();

        if (!force && hasDisplayedRole && displayedRole == role)
            return;

        displayedRole = role;
        hasDisplayedRole = true;

        Sprite profileSprite = GetProfileSprite(role);
        profileImage.sprite = profileSprite;
        profileImage.preserveAspect = preserveAspect;
        profileImage.color = Color.white;

        if (hideWhenSpriteIsMissing)
            profileImage.enabled = profileSprite != null;
        else
            profileImage.enabled = true;
    }

    private void FindPlayerIfNeeded()
    {
        if (playerController != null || Time.unscaledTime < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.unscaledTime + Mathf.Max(0.05f, playerSearchInterval);
        playerController = FindFirstObjectByType<PlayerController>();
    }

    private PlayerRole GetCurrentRole()
    {
        if (playerController != null)
            return playerController.CurrentRole;

        if (GameSession.Instance != null)
            return GameSession.Instance.playerRole;

        return PlayerRole.Sword;
    }

    private Sprite GetProfileSprite(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                return archerProfile;
            case PlayerRole.Tank:
                return tankProfile;
            default:
                return swordProfile;
        }
    }

    private void ApplyImageSettings()
    {
        if (profileImage != null)
            profileImage.preserveAspect = preserveAspect;
    }

    private void ApplyCanvasSorting()
    {
        Canvas profileCanvas = GetComponent<Canvas>();

        if (profileCanvas == null)
            profileCanvas = gameObject.AddComponent<Canvas>();

        profileCanvas.overrideSorting = true;
        profileCanvas.sortingOrder = canvasSortingOrder;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (profileImage == null)
            profileImage = GetComponent<Image>();

        ApplyImageSettings();
    }
#endif
}
