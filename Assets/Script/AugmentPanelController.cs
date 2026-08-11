using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AugmentPanelController : MonoBehaviour
{
    public static AugmentPanelController Instance { get; private set; }

    [Header("Panel")]
    public GameObject panelRoot;
    public bool autoBuildPanel = true;
    public Canvas targetCanvas;
    public bool pauseGameWhileOpen = true;
    public int panelSortingOrder = 1100;
    public Vector2 panelSize = new Vector2(860f, 420f);
    public Button closeButton;
    public TMP_Text titleText;
    public TMP_Text roleText;
    public List<Button> optionButtons = new List<Button>();
    public Transform cardContainer;
    public List<Transform> cardSpawnParents = new List<Transform>();
    public AugmentCardUI cardPrefab;

    [Header("Text")]
    public TMP_FontAsset uiFont;
    public Color panelColor = new Color(0.1f, 0.085f, 0.07f, 0.96f);
    public Color cardColor = new Color(0.22f, 0.18f, 0.14f, 1f);
    public Color titleColor = Color.white;
    public Color bodyColor = Color.white;

    [Header("Augments")]
    public List<AugmentData> swordAugments = new List<AugmentData>();
    public List<AugmentData> archerAugments = new List<AugmentData>();
    public List<AugmentData> tankAugments = new List<AugmentData>();

    [Header("Augment Card Prefabs")]
    public bool preferCardPrefabsAsChoices = true;
    public List<AugmentCardUI> swordCardPrefabs = new List<AugmentCardUI>();
    public List<AugmentCardUI> archerCardPrefabs = new List<AugmentCardUI>();
    public List<AugmentCardUI> tankCardPrefabs = new List<AugmentCardUI>();

    private readonly List<AugmentData> currentChoices = new List<AugmentData>();
    private readonly List<AugmentData> cachedChoices = new List<AugmentData>();
    private readonly List<AugmentCardUI> spawnedCards = new List<AugmentCardUI>();
    private PlayerStats playerStats;
    private PlayerController playerController;
    private float previousTimeScale = 1f;
    private bool pauseApplied;
    private string cachedSceneName;
    private PlayerRole cachedRole;
    private int cachedAugmentPoints = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDefaultAugments();
        EnsurePanel();
        AutoWireCustomCardSlots();
        Close(false);
    }

    private void Update()
    {
        if (!IsOpen())
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public static AugmentPanelController GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        AugmentPanelController existing = FindFirstObjectByType<AugmentPanelController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        AugmentPanelController[] allControllers = Resources.FindObjectsOfTypeAll<AugmentPanelController>();
        for (int i = 0; i < allControllers.Length; i++)
        {
            AugmentPanelController controller = allControllers[i];
            if (controller == null)
                continue;

            if (!controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded)
                continue;

            Instance = controller;
            return controller;
        }

        GameObject panelObject = new GameObject("AugmentPanelController");
        return panelObject.AddComponent<AugmentPanelController>();
    }

    public void Open(PlayerStats stats)
    {
        if (stats == null)
            return;

        if (!stats.HasAugmentPoint())
        {
            Debug.Log("사용 가능한 증강 포인트가 없어 증강 패널을 열 수 없습니다.", this);
            return;
        }

        playerStats = stats;
        playerController = stats.GetComponent<PlayerController>();
        EnsureDefaultAugments();
        EnsurePanel();
        AutoWireCustomCardSlots();
        BuildChoicesIfNeeded(stats);
        Refresh();
        EnsurePanelDisplayOrder();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        ApplyPauseIfNeeded();
        Debug.Log($"증강 패널 열림: role={GetCurrentRole()}, choices={currentChoices.Count}, points={stats.augmentPoints}", this);
    }

    public void Close()
    {
        Close(true);
    }

    private void Close(bool restorePause)
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        ClearSpawnedCards();
        playerStats = null;
        playerController = null;

        if (restorePause)
            RestorePauseIfNeeded();
    }

    public bool IsOpen()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }

    private void ChooseAugment(int index)
    {
        if (playerStats == null || index < 0 || index >= currentChoices.Count)
        {
            Debug.LogWarning($"증강 선택 실패: playerStats={playerStats}, index={index}, choices={currentChoices.Count}", this);
            return;
        }

        if (!playerStats.SpendAugmentPoint())
        {
            Debug.Log("사용 가능한 증강 포인트가 없어 증강을 선택할 수 없습니다.", this);
            Close();
            return;
        }

        AugmentData augment = currentChoices[index];
        augment.Apply(playerStats);
        Debug.Log($"증강 선택됨: {augment.augmentName}, 남은 증강 포인트: {playerStats.augmentPoints}", this);
        ClearChoiceCache();

        if (GameSession.Instance != null)
            GameSession.Instance.SavePlayer(playerStats, playerController);

        Close();
    }

    private void BuildChoices()
    {
        currentChoices.Clear();

        PlayerRole role = GetCurrentRole();
        List<AugmentData> source = preferCardPrefabsAsChoices ? BuildAugmentsFromCardPrefabs(role) : null;

        if (source == null || source.Count == 0)
            source = GetAugmentsForRole(role);

        AutoAssignCardPrefabs(source);
        if (source == null || source.Count == 0)
            return;

        List<AugmentData> pool = FilterAlreadySelectedAugments(source);
        while (currentChoices.Count < 3 && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            currentChoices.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    private List<AugmentData> FilterAlreadySelectedAugments(List<AugmentData> source)
    {
        List<AugmentData> filtered = new List<AugmentData>();
        PlayerAugmentController augmentController = playerStats != null
            ? playerStats.GetComponent<PlayerAugmentController>()
            : null;

        for (int i = 0; i < source.Count; i++)
        {
            AugmentData augment = source[i];
            if (augment == null)
                continue;

            string name = !string.IsNullOrWhiteSpace(augment.augmentName) ? augment.augmentName : augment.id;
            if (augmentController != null && augmentController.HasAugment(name))
                continue;

            filtered.Add(augment);
        }

        return filtered;
    }

    private void BuildChoicesIfNeeded(PlayerStats stats)
    {
        PlayerRole role = GetCurrentRole();
        string sceneName = SceneManager.GetActiveScene().name;
        int augmentPoints = stats != null ? stats.augmentPoints : -1;

        bool canReuseCache =
            cachedChoices.Count > 0 &&
            cachedSceneName == sceneName &&
            cachedRole == role &&
            cachedAugmentPoints == augmentPoints;

        if (canReuseCache)
        {
            currentChoices.Clear();
            currentChoices.AddRange(cachedChoices);
            return;
        }

        BuildChoices();
        cachedChoices.Clear();
        cachedChoices.AddRange(currentChoices);
        cachedSceneName = sceneName;
        cachedRole = role;
        cachedAugmentPoints = augmentPoints;
    }

    private void ClearChoiceCache()
    {
        cachedChoices.Clear();
        cachedSceneName = null;
        cachedAugmentPoints = -1;
    }

    private void Refresh()
    {
        SetText(titleText, "증강 선택");
        SetText(roleText, GetRoleLabel(GetCurrentRole()));

        if (HasCardPrefabSetup() || HasChoiceSpecificCardPrefab())
        {
            RefreshPrefabCards();
            return;
        }

        for (int i = 0; i < optionButtons.Count; i++)
        {
            Button button = optionButtons[i];
            if (button == null)
                continue;

            bool hasChoice = i < currentChoices.Count;
            button.gameObject.SetActive(hasChoice);
            button.onClick.RemoveAllListeners();

            if (!hasChoice)
                continue;

            int choiceIndex = i;
            AugmentData augment = currentChoices[i];
            button.onClick.AddListener(() => ChooseAugment(choiceIndex));

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                text = CreateOptionText(button.transform);

            if (text != null)
            {
                text.text = $"{augment.augmentName}\n<size=70%>{augment.GetEffectText()}</size>\n<size=60%>{augment.description}</size>";
                text.color = bodyColor;
                if (uiFont != null)
                    text.font = uiFont;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = cardColor;
        }
    }

    private void RefreshPrefabCards()
    {
        ClearSpawnedCards();

        for (int i = 0; i < currentChoices.Count; i++)
        {
            int choiceIndex = i;
            Transform parent = GetCardSpawnParent(i);
            AugmentCardUI prefab = currentChoices[i] != null && currentChoices[i].cardPrefab != null ? currentChoices[i].cardPrefab : cardPrefab;

            if (parent == null || prefab == null)
            {
                Debug.LogWarning($"증강 카드 생성 실패: index={i}, parent={parent}, prefab={prefab}", this);
                continue;
            }

            AugmentCardUI card = Instantiate(prefab, parent);
            card.gameObject.SetActive(true);
            StretchToParent(card.transform);
            card.Bind(currentChoices[i], () => ChooseAugment(choiceIndex));
            spawnedCards.Add(card);
        }
    }

    private void StretchToParent(Transform target)
    {
        RectTransform rect = target as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.SetAsLastSibling();
    }

    private bool HasCardPrefabSetup()
    {
        return cardPrefab != null && (cardContainer != null || cardSpawnParents.Count > 0);
    }

    private bool HasChoiceSpecificCardPrefab()
    {
        if (cardContainer == null && cardSpawnParents.Count == 0)
            return false;

        for (int i = 0; i < currentChoices.Count; i++)
        {
            if (currentChoices[i] != null && currentChoices[i].cardPrefab != null)
                return true;
        }

        return false;
    }

    private Transform GetCardSpawnParent(int index)
    {
        if (index >= 0 && index < cardSpawnParents.Count && cardSpawnParents[index] != null)
            return cardSpawnParents[index];

        return cardContainer;
    }

    private void ClearSpawnedCards()
    {
        for (int i = spawnedCards.Count - 1; i >= 0; i--)
        {
            if (spawnedCards[i] != null)
                Destroy(spawnedCards[i].gameObject);
        }

        spawnedCards.Clear();
    }

    private bool HasAnyRoleCardPrefabs()
    {
        return HasAnyCardPrefab(swordCardPrefabs) ||
            HasAnyCardPrefab(archerCardPrefabs) ||
            HasAnyCardPrefab(tankCardPrefabs);
    }

    private bool HasAnyCardPrefab(List<AugmentCardUI> prefabs)
    {
        if (prefabs == null)
            return false;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }

    private TMP_Text CreateOptionText(Transform parent)
    {
        if (parent == null)
            return null;

        GameObject textObject = CreateUIObject("GeneratedAugmentText", parent);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.color = bodyColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        if (uiFont != null)
            text.font = uiFont;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(18f, 18f);
        rect.offsetMax = new Vector2(-18f, -18f);

        return text;
    }

    private PlayerRole GetCurrentRole()
    {
        if (playerController != null)
            return playerController.CurrentRole;

        if (GameSession.Instance != null)
            return GameSession.Instance.playerRole;

        return PlayerRole.Sword;
    }

    private List<AugmentData> GetAugmentsForRole(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                return archerAugments;
            case PlayerRole.Tank:
                return tankAugments;
            default:
                return swordAugments;
        }
    }

    private List<AugmentData> BuildAugmentsFromCardPrefabs(PlayerRole role)
    {
        List<AugmentCardUI> prefabs = GetCardPrefabsForRole(role);
        if (prefabs == null || prefabs.Count == 0)
            return null;

        List<AugmentData> augments = new List<AugmentData>();
        for (int i = 0; i < prefabs.Count; i++)
        {
            AugmentCardUI prefab = prefabs[i];
            if (prefab == null)
                continue;

            string displayName = GetDisplayNameFromCardPrefab(prefab.name);
            AugmentData existing = FindAugmentByName(GetAugmentsForRole(role), displayName);
            AugmentData augment = existing ?? CreateAugmentFromCardPrefabName(prefab.name, displayName);
            augment.cardPrefab = prefab;
            augment.keepPrefabVisuals = true;
            augments.Add(augment);
        }

        return augments;
    }

    private AugmentData FindAugmentByName(List<AugmentData> augments, string displayName)
    {
        if (augments == null || string.IsNullOrWhiteSpace(displayName))
            return null;

        for (int i = 0; i < augments.Count; i++)
        {
            AugmentData augment = augments[i];
            if (augment == null)
                continue;

            if (augment.augmentName == displayName || augment.id == displayName)
                return augment;
        }

        return null;
    }

    private string GetDisplayNameFromCardPrefab(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return string.Empty;

        int roleStartIndex = prefabName.IndexOf('(');
        if (roleStartIndex > 0)
            return prefabName.Substring(0, roleStartIndex);

        return prefabName;
    }

    private AugmentData CreateAugmentFromCardPrefabName(string prefabName, string displayName)
    {
        AugmentData augment = CreateAugment(prefabName, displayName, string.Empty);

        if (displayName.Contains("검기방출"))
            augment.attackPowerBonus = 14;
        else if (displayName.Contains("불굴"))
            augment.maxHpBonus = 50;
        else if (displayName.Contains("피의맹세"))
            augment.criticalChancePercent = 15f;

        return augment;
    }

    private string GetRoleLabel(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                return "궁수 전용 증강";
            case PlayerRole.Tank:
                return "탱커 전용 증강";
            default:
                return "검사 전용 증강";
        }
    }

    private void EnsurePanel()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelRoot == null && autoBuildPanel)
            BuildAutoPanel();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void AutoWireCustomCardSlots()
    {
        if (panelRoot == null)
            return;

        if (cardSpawnParents.Count == 0)
        {
            for (int i = 0; i < panelRoot.transform.childCount; i++)
            {
                Transform child = panelRoot.transform.GetChild(i);
                if (child != null && child.name.StartsWith("CardContainer"))
                    cardSpawnParents.Add(child);
            }
        }

        if (cardPrefab != null || HasAnyRoleCardPrefabs())
            return;

        for (int i = 0; i < cardSpawnParents.Count; i++)
        {
            if (cardSpawnParents[i] == null)
                continue;

            Button button = cardSpawnParents[i].GetComponent<Button>();
            if (button == null)
                button = cardSpawnParents[i].GetComponentInChildren<Button>(true);

            if (button == null)
            {
                Image image = cardSpawnParents[i].GetComponent<Image>();
                if (image == null)
                    image = cardSpawnParents[i].gameObject.AddComponent<Image>();

                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
                button = cardSpawnParents[i].gameObject.AddComponent<Button>();
            }

            if (button != null && !optionButtons.Contains(button))
                optionButtons.Add(button);
        }
    }

    private void BuildAutoPanel()
    {
        Canvas canvas = EnsureCanvas();
        if (canvas == null)
            return;

        GameObject root = CreateUIObject("AugmentPanel", canvas.transform);
        panelRoot = root;

        Image panelImage = root.AddComponent<Image>();
        panelImage.color = panelColor;

        RectTransform panelRect = root.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText("TitleText", panelRect, "증강 선택", 32f, titleColor, TextAlignmentOptions.Left);
        SetStretch(titleText.rectTransform, new Vector2(28f, 24f), new Vector2(80f, 344f));

        roleText = CreateText("RoleText", panelRect, string.Empty, 20f, bodyColor, TextAlignmentOptions.Right);
        SetStretch(roleText.rectTransform, new Vector2(420f, 30f), new Vector2(90f, 346f));

        closeButton = CreateButton("CloseButton", panelRect, "X", new Vector2(-28f, -24f), new Vector2(44f, 44f), true);

        optionButtons.Clear();
        for (int i = 0; i < 3; i++)
        {
            Button option = CreateButton(
                $"AugmentOption{i + 1}",
                panelRect,
                string.Empty,
                new Vector2(32f + i * 270f, -96f),
                new Vector2(248f, 260f),
                false);
            optionButtons.Add(option);
        }
    }

    private Canvas EnsureCanvas()
    {
        if (targetCanvas != null)
            return targetCanvas;

        targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas != null)
            return targetCanvas;

        GameObject canvasObject = new GameObject("AugmentCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return targetCanvas;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, bool anchorRight)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = cardColor;

        Button button = buttonObject.AddComponent<Button>();
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.anchorMax = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.pivot = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = CreateText("Label", rect, label, 20f, bodyColor, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, new Vector2(14f, 14f), new Vector2(14f, 14f));
        return button;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent);
        TMP_Text tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        if (uiFont != null)
            tmp.font = uiFont;
        return tmp;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    private void EnsurePanelDisplayOrder()
    {
        if (panelRoot == null)
            return;

        Canvas canvas = panelRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panelRoot.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = panelSortingOrder;

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();
    }

    private void ApplyPauseIfNeeded()
    {
        if (!pauseGameWhileOpen || pauseApplied)
            return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pauseApplied = true;
    }

    private void RestorePauseIfNeeded()
    {
        if (!pauseApplied)
            return;

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        pauseApplied = false;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private void EnsureDefaultAugments()
    {
        if (!HasUsableAugments(swordAugments))
        {
            swordAugments.Clear();
            swordAugments.Add(CreateAugment("sword_power", "검사의 맹공", "공격력이 증가합니다.", attackPowerBonus: 8));
            swordAugments.Add(CreateAugment("sword_speed", "빠른 연격", "공격속도가 증가합니다.", attackSpeedPercent: 15f));
            swordAugments.Add(CreateAugment("sword_crit", "약점 베기", "치명타 확률이 증가합니다.", criticalChancePercent: 10f));
            swordAugments.Add(CreateAugment("sword_hp", "전투 단련", "최대 체력이 증가합니다.", maxHpBonus: 45));
            swordAugments.Add(CreateAugment("sword_cooldown", "칼끝 집중", "스킬 쿨타임이 감소합니다.", skillCooldownReductionPercent: 12f));
        }

        if (!HasUsableAugments(archerAugments))
        {
            archerAugments.Clear();
            archerAugments.Add(CreateAugment("archer_power", "관통 화살", "공격력이 증가합니다.", attackPowerBonus: 6));
            archerAugments.Add(CreateAugment("archer_speed", "민첩한 사격", "공격속도가 증가합니다.", attackSpeedPercent: 18f));
            archerAugments.Add(CreateAugment("archer_crit", "정조준", "치명타 확률이 증가합니다.", criticalChancePercent: 14f));
            archerAugments.Add(CreateAugment("archer_crit_damage", "급소 사격", "치명타 피해가 증가합니다.", criticalDamagePercent: 25f));
            archerAugments.Add(CreateAugment("archer_move", "가벼운 발걸음", "이동속도가 증가합니다.", moveSpeedBonus: 0.35f));
        }

        if (!HasUsableAugments(tankAugments))
        {
            tankAugments.Clear();
            tankAugments.Add(CreateAugment("tank_hp", "철벽 체력", "최대 체력이 크게 증가합니다.", maxHpBonus: 80));
            tankAugments.Add(CreateAugment("tank_power", "묵직한 일격", "공격력이 증가합니다.", attackPowerBonus: 7));
            tankAugments.Add(CreateAugment("tank_speed", "방패 훈련", "공격속도가 증가합니다.", attackSpeedPercent: 12f));
            tankAugments.Add(CreateAugment("tank_cooldown", "인내 훈련", "스킬 쿨타임이 감소합니다.", skillCooldownReductionPercent: 15f));
            tankAugments.Add(CreateAugment("tank_move", "중장 돌파", "이동속도가 증가합니다.", moveSpeedBonus: 0.25f));
        }
    }

    private void AutoAssignCardPrefabs(List<AugmentData> augments)
    {
        if (augments == null)
            return;

        PlayerRole role = GetCurrentRole();
        string roleLabel = GetRolePrefabLabel(role);
        List<AugmentCardUI> prefabPool = GetCardPrefabsForRole(role);

        for (int i = 0; i < augments.Count; i++)
        {
            AugmentData augment = augments[i];
            if (augment == null || augment.cardPrefab != null)
                continue;

            string[] namesToTry =
            {
                $"{augment.augmentName}({roleLabel})",
                augment.augmentName,
                augment.id
            };

            for (int nameIndex = 0; nameIndex < namesToTry.Length; nameIndex++)
            {
                AugmentCardUI prefab = FindCardPrefabByName(prefabPool, namesToTry[nameIndex]);
                if (prefab != null)
                {
                    augment.cardPrefab = prefab;
                    augment.keepPrefabVisuals = true;
                    break;
                }
            }
        }
    }

    private List<AugmentCardUI> GetCardPrefabsForRole(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                return archerCardPrefabs;
            case PlayerRole.Tank:
                return tankCardPrefabs;
            default:
                return swordCardPrefabs;
        }
    }

    private AugmentCardUI FindCardPrefabByName(List<AugmentCardUI> prefabs, string prefabName)
    {
        if (prefabs == null || string.IsNullOrWhiteSpace(prefabName))
            return null;

        for (int i = 0; i < prefabs.Count; i++)
        {
            AugmentCardUI prefab = prefabs[i];
            if (prefab != null && prefab.name == prefabName)
                return prefab;
        }

        return null;
    }

    private string GetRolePrefabLabel(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                return "궁수";
            case PlayerRole.Tank:
                return "탱커";
            default:
                return "검사";
        }
    }

    private bool HasUsableAugments(List<AugmentData> augments)
    {
        if (augments == null || augments.Count == 0)
            return false;

        for (int i = 0; i < augments.Count; i++)
        {
            AugmentData augment = augments[i];
            if (augment == null)
                continue;

            bool hasLabel = !string.IsNullOrWhiteSpace(augment.id) || !string.IsNullOrWhiteSpace(augment.augmentName);
            bool hasEffect =
                augment.attackPowerBonus != 0 ||
                augment.attackSpeedPercent != 0f ||
                augment.criticalChancePercent != 0f ||
                augment.criticalDamagePercent != 0f ||
                augment.maxHpBonus != 0 ||
                augment.skillCooldownReductionPercent != 0f ||
                augment.moveSpeedBonus != 0f;

            if (hasLabel && hasEffect)
                return true;
        }

        return false;
    }

    private AugmentData CreateAugment(
        string id,
        string name,
        string description,
        int attackPowerBonus = 0,
        float attackSpeedPercent = 0f,
        float criticalChancePercent = 0f,
        float criticalDamagePercent = 0f,
        int maxHpBonus = 0,
        float skillCooldownReductionPercent = 0f,
        float moveSpeedBonus = 0f)
    {
        return new AugmentData
        {
            id = id,
            augmentName = name,
            description = description,
            attackPowerBonus = attackPowerBonus,
            attackSpeedPercent = attackSpeedPercent,
            criticalChancePercent = criticalChancePercent,
            criticalDamagePercent = criticalDamagePercent,
            maxHpBonus = maxHpBonus,
            skillCooldownReductionPercent = skillCooldownReductionPercent,
            moveSpeedBonus = moveSpeedBonus
        };
    }
}
