using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopPanelController : MonoBehaviour
{
    public static ShopPanelController Instance { get; private set; }

    [Header("References")]
    public GameObject panelRoot;
    public PlayerStats playerStats;

    [Header("Auto Build")]
    public bool autoBuildPanel = true;
    public Canvas targetCanvas;
    public Vector2 panelSize = new Vector2(980f, 620f);
    [Tooltip("상점이 체력/경험치/레벨/프로필 HUD보다 위에 표시되도록 별도 정렬합니다.")]
    public bool forceShopAboveHud = true;
    public int shopSortingOrder = 1000;
    public bool useSingleScrollableItemList = true;
    public bool showOnlyCurrentTier = true;
    public bool autoFixItemListLayout = true;
    [Tooltip("Auto Fix Item List Layout이 켜져 있을 때 ItemList에 적용할 X 위치입니다.")]
    public float itemListPositionX;
    public bool useRectMaskForItemViewport = true;
    public float itemButtonHeight = 78f;
    public bool overrideGeneratedItemButtonHeight = true;
    public bool autoFixItemButtonLayout = true;
    public Vector2 itemIconSize = new Vector2(56f, 56f);
    public float itemIconLeft = 36f;
    public Vector2 itemStatIconSize = new Vector2(22f, 22f);
    public bool forceSquareItemStatIcon = false;
    public float itemStatIconSpacing = 4f;
    public Vector2 itemStatEntrySize = new Vector2(72f, 24f);
    public Vector2 itemStatValueSize = new Vector2(44f, 24f);
    public bool forceSquareItemStatValue = false;
    public int itemStatEntriesPerRow = 2;
    public float itemStatRowSpacing = 4f;
    public float itemStatValueFontSize = 16f;
    public Vector2 itemStatEntriesOffset = new Vector2(28f, -14f);
    public bool useItemStatEntriesAnchor = false;
    public Vector2 itemStatEntriesPadding = new Vector2(10f, 8f);

    [Header("Editable Images")]
    public Sprite panelSprite;
    public Sprite itemListPanelSprite;
    public Sprite lowTierListPanelSprite;
    public Sprite midTierListPanelSprite;
    public Sprite highTierListPanelSprite;
    public Sprite detailPanelSprite;
    public Sprite recipePanelSprite;
    public Sprite tabButtonSprite;
    public Sprite lowTierButtonSprite;
    public Sprite midTierButtonSprite;
    public Sprite highTierButtonSprite;
    public Sprite itemButtonSprite;
    public Sprite itemIconSlotSprite;
    public Sprite lowTierItemStatBackgroundSprite;
    public Sprite midTierItemStatBackgroundSprite;
    public Sprite highTierItemStatBackgroundSprite;
    public Sprite itemStatIconSprite;
    public Sprite attackSpeedStatIconSprite;
    public Sprite attackPowerStatIconSprite;
    public Sprite criticalChanceStatIconSprite;
    public Sprite maxHpStatIconSprite;
    public Sprite skillCooldownStatIconSprite;
    public Sprite slotSprite;
    public Sprite singleRecipeSlotSprite;
    public Sprite buyButtonSprite;
    public Sprite closeButtonSprite;

    [Header("Editable Font")]
    public TMP_FontAsset uiFont;
    public Color titleTextColor = Color.white;
    public Color bodyTextColor = Color.white;
    public Color messageTextColor = new Color(1f, 0.85f, 0.35f, 1f);
    public Color itemButtonNormalColor = Color.white;
    public Color itemButtonSelectedColor = new Color(0.55f, 0.45f, 0.35f, 0.75f);

    [Header("Designed UI Bindings")]
    public Button lowTierButton;
    public Button midTierButton;
    public Button highTierButton;
    public ScrollRect itemScrollRect;
    public Image itemListPanelImage;
    public RectTransform itemListRoot;
    public Button itemButtonPrefab;
    public Image detailIcon;
    public TMP_Text detailNameText;
    public TMP_Text detailDescriptionText;
    public Image recipePanelImage;
    public Image singleRecipeSlotImage;
    public RecipeSlotUI resultRecipeSlot;
    public RecipeSlotUI singleResultRecipeSlot;
    public List<RecipeSlotUI> materialRecipeSlots = new List<RecipeSlotUI>();
    public TMP_Text detailPriceText;
    public TMP_Text goldText;
    public TMP_Text messageText;
    public Button buyButton;
    public Button closeButton;
    public List<ShopInventorySlotUI> inventorySlots = new List<ShopInventorySlotUI>();

    [Header("Items")]
    public bool requireRecipeMaterialsForPurchase;
    public int inventoryCapacity = 6;
    public Color recipeSlotUnavailableColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
    public Color recipeSlotUnavailableOverlayColor = new Color(0f, 0f, 0f, 0.45f);
    public bool hideRecipeSlotBackgrounds = true;
    public bool autoFixRecipeIconLayout = true;
    public Vector2 recipeIconSize = new Vector2(54f, 54f);
    public bool autoFixSingleRecipeSlotSize = true;
    public Vector2 singleRecipeSlotSize = new Vector2(130f, 130f);
    public List<ShopItemData> items = new List<ShopItemData>();

    private readonly HashSet<string> purchasedUniqueItemIds = new HashSet<string>();
    private readonly List<InventoryEntry> inventoryItems = new List<InventoryEntry>();
    private readonly Dictionary<ShopItemTier, RectTransform> tierFirstItemButtons = new Dictionary<ShopItemTier, RectTransform>();
    private ShopItemTier currentTier = ShopItemTier.Low;
    private ShopItemData selectedItem;

    private class InventoryEntry
    {
        public ShopItemData item;

        public InventoryEntry(ShopItemData item)
        {
            this.item = item;
        }
    }

    private class AggregatedRecipeMaterial
    {
        public ShopItemData item;
        public int requiredCount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsurePanel();
        Close();
    }

    public static ShopPanelController GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        ShopPanelController existing = FindFirstObjectByType<ShopPanelController>();
        if (existing != null)
            return existing;

        GameObject controllerObject = new GameObject("ShopPanelController");
        return controllerObject.AddComponent<ShopPanelController>();
    }

    public void Open(PlayerStats stats)
    {
        GameAudioManager.Play(GameSfx.ForgeEnter);
        playerStats = stats != null ? stats : FindFirstObjectByType<PlayerStats>();
        EnsurePanel();
        RestoreInventoryFromSession();

        if (panelRoot == null)
        {
            Debug.LogWarning("ShopPanelController needs a panelRoot or Auto Build enabled.");
            return;
        }

        EnsureShopDisplayOrder();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        currentTier = ShopItemTier.Low;
        selectedItem = null;
        Refresh();
        SetMessage("Select an item.");
    }

    public void Close()
    {
        if (IsOpen())
            GameAudioManager.Play(GameSfx.ForgeExit);
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public bool IsOpen()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }

    private void EnsureShopDisplayOrder()
    {
        if (!forceShopAboveHud || panelRoot == null)
            return;

        Canvas shopCanvas = panelRoot.GetComponent<Canvas>();

        if (shopCanvas == null)
            shopCanvas = panelRoot.AddComponent<Canvas>();

        shopCanvas.overrideSorting = true;
        shopCanvas.sortingOrder = shopSortingOrder;

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();
    }

    private void EnsurePanel()
    {
        if (panelRoot == null && autoBuildPanel)
            BuildAutoPanel();

        BindPanelIfNeeded();
        ConnectButtons();
    }

    private void BuildAutoPanel()
    {
        Canvas canvas = EnsureCanvas();
        if (canvas == null)
            return;

        GameObject panelObject = CreateUIObject("ShopPanel", canvas.transform);
        panelRoot = panelObject;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.color = panelSprite != null ? Color.white : new Color(0.12f, 0.1f, 0.09f, 0.96f);
        panelImage.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        CreateTabs(panelRect);
        CreateItemScroll(panelRect);
        CreateDetailArea(panelRect);
    }

    private Canvas EnsureCanvas()
    {
        if (targetCanvas != null)
            return targetCanvas;

        targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas != null)
            return targetCanvas;

        targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas != null)
            return targetCanvas;

        GameObject canvasObject = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return targetCanvas;
    }

    private void CreateTabs(RectTransform panelRect)
    {
        lowTierButton = CreateButton("LowTierButton", panelRect, "LOW", GetTierButtonSprite(ShopItemTier.Low), new Vector2(30f, -30f), new Vector2(130f, 44f), TextAlignmentOptions.Center);
        midTierButton = CreateButton("MidTierButton", panelRect, "MID", GetTierButtonSprite(ShopItemTier.Mid), new Vector2(170f, -30f), new Vector2(130f, 44f), TextAlignmentOptions.Center);
        highTierButton = CreateButton("HighTierButton", panelRect, "HIGH", GetTierButtonSprite(ShopItemTier.High), new Vector2(310f, -30f), new Vector2(130f, 44f), TextAlignmentOptions.Center);
        closeButton = CreateButton("CloseButton", panelRect, "X", closeButtonSprite, new Vector2(-58f, -30f), new Vector2(44f, 44f), TextAlignmentOptions.Center, true);
    }

    private Sprite GetTierButtonSprite(ShopItemTier tier)
    {
        if (tier == ShopItemTier.Low && lowTierButtonSprite != null)
            return lowTierButtonSprite;

        if (tier == ShopItemTier.Mid && midTierButtonSprite != null)
            return midTierButtonSprite;

        if (tier == ShopItemTier.High && highTierButtonSprite != null)
            return highTierButtonSprite;

        return tabButtonSprite;
    }

    private void CreateItemScroll(RectTransform panelRect)
    {
        GameObject scrollObject = CreateImageObject("ItemScrollView", panelRect, itemListPanelSprite, new Color(0.07f, 0.065f, 0.06f, 0.9f));
        itemListPanelImage = scrollObject.GetComponent<Image>();
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetStretchRect(scrollRectTransform, new Vector2(30f, 95f), new Vector2(560f, 80f));

        itemScrollRect = scrollObject.AddComponent<ScrollRect>();
        itemScrollRect.horizontal = false;
        itemScrollRect.vertical = true;
        itemScrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = CreateImageObject("Viewport", scrollRectTransform, null, new Color(0f, 0f, 0f, 0f));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetStretchRect(viewportRect, Vector2.zero, Vector2.zero);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = CreateUIObject("ItemList", viewportRect);
        itemListRoot = contentObject.GetComponent<RectTransform>();
        itemListRoot.anchorMin = new Vector2(0f, 1f);
        itemListRoot.anchorMax = new Vector2(1f, 1f);
        itemListRoot.pivot = new Vector2(0.5f, 1f);
        itemListRoot.anchoredPosition = Vector2.zero;
        itemListRoot.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        itemScrollRect.viewport = viewportRect;
        itemScrollRect.content = itemListRoot;

        itemButtonPrefab = CreateButton("ItemButtonPrefab", itemListRoot, "Item", itemButtonSprite, Vector2.zero, new Vector2(0f, 78f), TextAlignmentOptions.Left);
        itemButtonPrefab.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
        itemButtonPrefab.gameObject.SetActive(false);

        RectTransform prefabRect = itemButtonPrefab.GetComponent<RectTransform>();
        prefabRect.anchorMin = new Vector2(0f, 1f);
        prefabRect.anchorMax = new Vector2(1f, 1f);
        prefabRect.sizeDelta = new Vector2(0f, 78f);

        GameObject iconSlotObject = CreateImageObject("IconSlot", prefabRect, itemIconSlotSprite != null ? itemIconSlotSprite : slotSprite, new Color(0.28f, 0.23f, 0.18f, 1f));
        RectTransform iconSlotRect = iconSlotObject.GetComponent<RectTransform>();
        iconSlotRect.anchorMin = new Vector2(0f, 0.5f);
        iconSlotRect.anchorMax = new Vector2(0f, 0.5f);
        iconSlotRect.pivot = new Vector2(0.5f, 0.5f);
        iconSlotRect.anchoredPosition = new Vector2(38f, 0f);
        iconSlotRect.sizeDelta = new Vector2(58f, 58f);

        GameObject iconObject = CreateImageObject("Icon", iconSlotRect, null, Color.clear);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetStretchRect(iconRect, new Vector2(5f, 5f), new Vector2(5f, 5f));

        TMP_Text label = itemButtonPrefab.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.name = "Label";
            RectTransform labelRect = label.rectTransform;
            labelRect.offsetMin = new Vector2(76f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
        }

        CreateStatEntriesAnchor(prefabRect);
    }

    private void CreateDetailArea(RectTransform panelRect)
    {
        GameObject detailObject = CreateImageObject("DetailArea", panelRect, detailPanelSprite, new Color(0.18f, 0.13f, 0.1f, 0.92f));
        RectTransform detailRect = detailObject.GetComponent<RectTransform>();
        SetStretchRect(detailRect, new Vector2(610f, 80f), new Vector2(30f, 80f));

        detailIcon = CreateImageObject("DetailIcon", detailRect, slotSprite, new Color(0.3f, 0.26f, 0.21f, 1f)).GetComponent<Image>();
        RectTransform iconRect = detailIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(18f, -18f);
        iconRect.sizeDelta = new Vector2(82f, 82f);

        detailNameText = CreateText("DetailNameText", detailRect, "No Item", 26f, titleTextColor, TextAlignmentOptions.Left);
        SetStretchRect(detailNameText.rectTransform, new Vector2(115f, 20f), new Vector2(20f, 405f));

        detailDescriptionText = CreateText("DetailDescriptionText", detailRect, "", 18f, bodyTextColor, TextAlignmentOptions.TopLeft);
        SetStretchRect(detailDescriptionText.rectTransform, new Vector2(20f, 120f), new Vector2(20f, 300f));
        detailDescriptionText.gameObject.SetActive(false);

        GameObject recipePanel = CreateImageObject("RecipePanel", detailRect, recipePanelSprite, new Color(0.09f, 0.075f, 0.06f, 0.85f));
        recipePanelImage = recipePanel.GetComponent<Image>();
        RectTransform recipeRect = recipePanel.GetComponent<RectTransform>();
        SetStretchRect(recipeRect, new Vector2(20f, 260f), new Vector2(20f, 115f));

        resultRecipeSlot = CreateRecipeSlot("ResultSlot", recipeRect, new Vector2(0.5f, 1f), new Vector2(0f, -52f));
        materialRecipeSlots.Clear();
        materialRecipeSlots.Add(CreateRecipeSlot("MaterialSlot1", recipeRect, new Vector2(0.2f, 0f), new Vector2(0f, 52f)));
        materialRecipeSlots.Add(CreateRecipeSlot("MaterialSlot2", recipeRect, new Vector2(0.4f, 0f), new Vector2(0f, 52f)));
        materialRecipeSlots.Add(CreateRecipeSlot("MaterialSlot3", recipeRect, new Vector2(0.6f, 0f), new Vector2(0f, 52f)));
        materialRecipeSlots.Add(CreateRecipeSlot("MaterialSlot4", recipeRect, new Vector2(0.8f, 0f), new Vector2(0f, 52f)));

        detailPriceText = CreateText("DetailPriceText", detailRect, "-", 22f, titleTextColor, TextAlignmentOptions.Left);
        SetStretchRect(detailPriceText.rectTransform, new Vector2(20f, 390f), new Vector2(180f, 55f));

        goldText = CreateText("GoldText", detailRect, "Gold -", 20f, titleTextColor, TextAlignmentOptions.Right);
        SetStretchRect(goldText.rectTransform, new Vector2(180f, 390f), new Vector2(20f, 55f));

        messageText = CreateText("MessageText", detailRect, "", 18f, messageTextColor, TextAlignmentOptions.Center);
        SetStretchRect(messageText.rectTransform, new Vector2(20f, 445f), new Vector2(20f, 70f));

        buyButton = CreateButton("BuyButton", detailRect, "BUY", buyButtonSprite, new Vector2(20f, 0f), new Vector2(170f, 48f), TextAlignmentOptions.Center);
        RectTransform buyRect = buyButton.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0f, 0f);
        buyRect.anchorMax = new Vector2(0f, 0f);
        buyRect.pivot = new Vector2(0f, 0f);
        buyRect.anchoredPosition = new Vector2(20f, 18f);
    }

    private RecipeSlotUI CreateRecipeSlot(string name, RectTransform parent, Vector2 anchor, Vector2 position)
    {
        GameObject slotObject = CreateImageObject(name, parent, slotSprite, new Color(0.28f, 0.23f, 0.18f, 1f));
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.anchorMin = anchor;
        slotRect.anchorMax = anchor;
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.anchoredPosition = position;
        slotRect.sizeDelta = new Vector2(74f, 74f);

        GameObject iconObject = CreateImageObject("Icon", slotRect, null, Color.clear);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetStretchRect(iconRect, new Vector2(8f, 8f), new Vector2(8f, 8f));

        TMP_Text countText = CreateText("CountText", slotRect, "", 18f, titleTextColor, TextAlignmentOptions.BottomRight);
        SetStretchRect(countText.rectTransform, new Vector2(4f, 4f), new Vector2(4f, 4f));

        TMP_Text nameText = CreateText("NameText", slotRect, "", 11f, bodyTextColor, TextAlignmentOptions.Center);
        nameText.gameObject.SetActive(false);

        RecipeSlotUI slot = slotObject.AddComponent<RecipeSlotUI>();
        slot.iconImage = iconObject.GetComponent<Image>();
        slot.countText = countText;
        slot.nameText = nameText;
        slot.Hide();
        return slot;
    }

    private void BindPanelIfNeeded()
    {
        if (panelRoot == null)
            return;

        if (lowTierButton == null)
            lowTierButton = FindInPanel<Button>("LowTierButton", "LowButton", "LowTab", "LowerTab");

        if (midTierButton == null)
            midTierButton = FindInPanel<Button>("MidTierButton", "MidButton", "MidTab", "MiddleTab");

        if (highTierButton == null)
            highTierButton = FindInPanel<Button>("HighTierButton", "HighButton", "HighTab", "UpperTab");

        if (itemScrollRect == null)
            itemScrollRect = FindInPanel<ScrollRect>("ItemScrollRect", "ItemScrollView", "ItemScroll", "Scroll View");

        if (itemListPanelImage == null && itemScrollRect != null)
            itemListPanelImage = itemScrollRect.GetComponent<Image>();

        if (itemListPanelImage == null)
            itemListPanelImage = FindInPanel<Image>("ItemScrollView", "ItemScrollRect", "ItemScroll", "Scroll View", "ItemListPanel");

        if (itemListRoot == null)
            itemListRoot = FindInPanel<RectTransform>("ItemList", "ItemListRoot", "ItemListContent", "ItemContent");

        if (itemButtonPrefab == null)
            itemButtonPrefab = FindInPanel<Button>("ItemButtonPrefab", "ShopItemButtonPrefab");

        if (detailIcon == null)
            detailIcon = FindInPanel<Image>("DetailIcon", "ItemIcon");

        if (detailNameText == null)
            detailNameText = FindInPanel<TMP_Text>("DetailNameText", "ItemNameText", "NameText");

        if (detailDescriptionText == null)
            detailDescriptionText = FindInPanel<TMP_Text>("DetailDescriptionText", "DescriptionText", "ItemDescriptionText");

        if (resultRecipeSlot == null)
            resultRecipeSlot = FindInPanel<RecipeSlotUI>("ResultSlot", "RecipeResultSlot", "SelectedItemSlot");

        if (recipePanelImage == null)
            recipePanelImage = FindInPanel<Image>("RecipePanel", "RecipeBackground", "RecipeImage");

        if (singleRecipeSlotImage == null)
            singleRecipeSlotImage = FindInPanel<Image>("SingleRecipeSlotImage", "SingleRecipeSlot", "SingleSlotImage");

        if (singleResultRecipeSlot == null)
            singleResultRecipeSlot = FindInPanel<RecipeSlotUI>("SingleResultSlot", "SingleRecipeResultSlot", "SingleRecipeSlot");

        if (materialRecipeSlots.Count == 0)
        {
            AddMaterialRecipeSlotIfFound("MaterialSlot1", "RecipeSlot1", "RecipeMaterialSlot1");
            AddMaterialRecipeSlotIfFound("MaterialSlot2", "RecipeSlot2", "RecipeMaterialSlot2");
            AddMaterialRecipeSlotIfFound("MaterialSlot3", "RecipeSlot3", "RecipeMaterialSlot3");
            AddMaterialRecipeSlotIfFound("MaterialSlot4", "RecipeSlot4", "RecipeMaterialSlot4");
        }

        if (detailPriceText == null)
            detailPriceText = FindInPanel<TMP_Text>("DetailPriceText", "PriceText", "ItemPriceText");

        if (goldText == null)
            goldText = FindInPanel<TMP_Text>("GoldText", "PlayerGoldText", "MoneyText");

        if (messageText == null)
            messageText = FindInPanel<TMP_Text>("MessageText", "ResultText", "NoticeText");

        if (buyButton == null)
            buyButton = FindInPanel<Button>("BuyButton", "PurchaseButton");

        if (closeButton == null)
            closeButton = FindInPanel<Button>("CloseButton", "XButton", "ExitButton", "ShopCloseButton");

        if (inventorySlots.Count == 0)
        {
            AddInventorySlotIfFound("InventorySlot1", "ShopInventorySlot1", "Slot1");
            AddInventorySlotIfFound("InventorySlot2", "ShopInventorySlot2", "Slot2");
            AddInventorySlotIfFound("InventorySlot3", "ShopInventorySlot3", "Slot3");
            AddInventorySlotIfFound("InventorySlot4", "ShopInventorySlot4", "Slot4");
            AddInventorySlotIfFound("InventorySlot5", "ShopInventorySlot5", "Slot5");
            AddInventorySlotIfFound("InventorySlot6", "ShopInventorySlot6", "Slot6");
        }
    }

    private void ConnectButtons()
    {
        ConnectButton(lowTierButton, ShowLowTier);
        ConnectButton(midTierButton, ShowMidTier);
        ConnectButton(highTierButton, ShowHighTier);
        ConnectButton(buyButton, BuySelectedItem);
        ConnectButton(closeButton, Close);
    }

    private void Refresh()
    {
        RefreshGold();
        RefreshItemList();
        RefreshDetail();
        RefreshInventorySlots();
    }

    private void RefreshGold()
    {
        if (goldText != null)
            goldText.text = playerStats != null ? $"Gold {playerStats.gold}" : "Gold -";
    }

    private void RefreshItemList()
    {
        tierFirstItemButtons.Clear();
        RefreshItemListPanelVisual();

        if (itemListRoot == null || itemButtonPrefab == null)
            return;

        if (autoFixItemListLayout)
            FixItemListLayout();

        for (int i = itemListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = itemListRoot.GetChild(i);

            if (child == itemButtonPrefab.transform)
                continue;

            Destroy(child.gameObject);
        }

        itemButtonPrefab.gameObject.SetActive(false);

        if (!showOnlyCurrentTier && useSingleScrollableItemList)
        {
            CreateTierItems(ShopItemTier.Low, itemListRoot);
            CreateTierItems(ShopItemTier.Mid, itemListRoot);
            CreateTierItems(ShopItemTier.High, itemListRoot);
        }
        else
        {
            CreateTierItems(currentTier, itemListRoot);
        }
    }

    private void RefreshItemListPanelVisual()
    {
        if (itemListPanelImage == null)
            return;

        Sprite sprite = GetTierListPanelSprite(currentTier);
        if (sprite == null)
            return;

        itemListPanelImage.sprite = sprite;
        itemListPanelImage.color = Color.white;
        itemListPanelImage.type = Image.Type.Sliced;
    }

    private Sprite GetTierListPanelSprite(ShopItemTier tier)
    {
        if (tier == ShopItemTier.Low && lowTierListPanelSprite != null)
            return lowTierListPanelSprite;

        if (tier == ShopItemTier.Mid && midTierListPanelSprite != null)
            return midTierListPanelSprite;

        if (tier == ShopItemTier.High && highTierListPanelSprite != null)
            return highTierListPanelSprite;

        return itemListPanelSprite;
    }

    private void CreateTierItems(ShopItemTier tier, RectTransform parent)
    {
        foreach (ShopItemData item in items)
        {
            if (item == null || item.tier != tier)
                continue;

            Button button = CreateItemButton(item, parent);

            if (!tierFirstItemButtons.ContainsKey(tier))
                tierFirstItemButtons.Add(tier, button.transform as RectTransform);
        }
    }

    private void RefreshDetail()
    {
        bool hasSelection = selectedItem != null;

        if (detailIcon != null)
        {
            detailIcon.sprite = hasSelection ? selectedItem.iconSprite : null;
            detailIcon.color = hasSelection ? selectedItem.iconColor : Color.clear;
        }

        if (detailNameText != null)
            detailNameText.text = hasSelection ? selectedItem.itemName : "No Item";

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = "";
            detailDescriptionText.gameObject.SetActive(false);
        }

        RefreshRecipeSlots();

        if (detailPriceText != null)
            detailPriceText.text = hasSelection ? $"Price {GetPurchasePrice(selectedItem)}" : "Price -";

        if (buyButton != null)
            buyButton.interactable = hasSelection && CanBuy(selectedItem);
    }

    private void SelectItem(ShopItemData item)
    {
        selectedItem = item;
        SetMessage("");
        Refresh();
    }

    private void BuySelectedItem()
    {
        BuyItem(selectedItem, true);
    }

    private void BuyRecipeItem(ShopItemData item)
    {
        BuyItem(item, false);
    }

    private void BuyItem(ShopItemData item, bool keepSelectedItem)
    {
        if (item == null || playerStats == null)
            return;

        ShopItemData previousSelection = selectedItem;
        if (!keepSelectedItem)
            selectedItem = previousSelection;

        if (!CanBuy(item))
        {
            SetMessage(GetCannotBuyMessage(item));
            RefreshDetail();
            return;
        }

        int purchasePrice = GetPurchasePrice(item);
        if (!playerStats.SpendGold(purchasePrice))
        {
            SetMessage("Not enough gold.");
            RefreshGold();
            return;
        }

        int inventoryInsertIndex = ConsumeOwnedMaterials(item);
        playerStats.ApplyShopItem(item);
        AddInventoryItem(item, inventoryInsertIndex);
        GameAudioManager.Play(GameSfx.Purchase);
        if (!item.canBuyMultiple)
            purchasedUniqueItemIds.Add(GetItemId(item));

        GameSession session = GameSession.GetOrCreate();
        SaveInventoryToSession();
        session.SavePlayer(playerStats, playerStats.GetComponent<PlayerController>());

        SetMessage($"Purchased {item.itemName}.");
        if (!keepSelectedItem)
            selectedItem = previousSelection;

        Refresh();
    }

    private void ShowLowTier()
    {
        ShowTier(ShopItemTier.Low);
    }

    private void ShowMidTier()
    {
        ShowTier(ShopItemTier.Mid);
    }

    private void ShowHighTier()
    {
        ShowTier(ShopItemTier.High);
    }

    private void ShowTier(ShopItemTier tier)
    {
        currentTier = tier;
        selectedItem = null;
        SetMessage("");
        Refresh();

        if (!showOnlyCurrentTier && useSingleScrollableItemList)
            ScrollToTier(tier);
    }

    private void ScrollToTier(ShopItemTier tier)
    {
        if (itemScrollRect == null || itemListRoot == null)
            return;

        if (!tierFirstItemButtons.TryGetValue(tier, out RectTransform firstItemButton) || firstItemButton == null)
            return;

        Canvas.ForceUpdateCanvases();

        int templateOffset = itemButtonPrefab != null && itemButtonPrefab.transform.parent == itemListRoot ? 1 : 0;
        int visibleItemCount = Mathf.Max(1, itemListRoot.childCount - templateOffset);
        int itemIndex = Mathf.Max(0, firstItemButton.GetSiblingIndex() - templateOffset);

        itemScrollRect.verticalNormalizedPosition = visibleItemCount <= 1
            ? 1f
            : 1f - Mathf.Clamp01((float)itemIndex / (visibleItemCount - 1));
    }

    private void RefreshRecipeSlots()
    {
        if (selectedItem == null)
        {
            RefreshRecipePanelVisuals(false);

            if (resultRecipeSlot != null)
                resultRecipeSlot.Hide();

            if (singleResultRecipeSlot != null && singleResultRecipeSlot != resultRecipeSlot)
                singleResultRecipeSlot.Hide();

            HideMaterialRecipeSlots();
            return;
        }

        bool hasMaterials = HasRecipeMaterials(selectedItem);
        RefreshRecipePanelVisuals(hasMaterials);
        RecipeSlotUI activeResultSlot = hasMaterials || singleResultRecipeSlot == null ? resultRecipeSlot : singleResultRecipeSlot;
        RecipeSlotUI inactiveResultSlot = activeResultSlot == resultRecipeSlot ? singleResultRecipeSlot : resultRecipeSlot;

        if (inactiveResultSlot != null && inactiveResultSlot != activeResultSlot)
            inactiveResultSlot.Hide();

        if (activeResultSlot != null)
        {
            PrepareRecipeSlot(activeResultSlot);
            activeResultSlot.Show(selectedItem, 1, GetPurchasePrice(selectedItem));
            ConnectRecipeSlotClick(activeResultSlot, selectedItem);
        }

        for (int i = 0; i < materialRecipeSlots.Count; i++)
        {
            RecipeSlotUI slot = materialRecipeSlots[i];
            if (slot == null)
                continue;

            if (hasMaterials && i < selectedItem.recipeMaterials.Count && selectedItem.recipeMaterials[i] != null)
            {
                ShopItemData.RecipeMaterial material = selectedItem.recipeMaterials[i];
                int requiredCount = Mathf.Max(1, material.count);
                PrepareRecipeSlot(slot);
                slot.Show(material.item, requiredCount, GetPurchasePrice(material.item));
                ConnectRecipeSlotClick(slot, material.item);
            }
            else
            {
                slot.Hide();
            }
        }
    }

    private void RefreshRecipePanelVisuals(bool hasMaterials)
    {
        if (recipePanelImage != null)
        {
            if (recipePanelSprite != null)
            {
                recipePanelImage.sprite = recipePanelSprite;
                recipePanelImage.color = Color.white;
            }

            if (singleRecipeSlotImage != null)
                recipePanelImage.enabled = hasMaterials;
        }

        if (singleRecipeSlotImage == null)
            return;

        if (singleRecipeSlotSprite != null)
        {
            singleRecipeSlotImage.sprite = singleRecipeSlotSprite;
            singleRecipeSlotImage.color = Color.white;
        }

        singleRecipeSlotImage.gameObject.SetActive(!hasMaterials);

        if (!autoFixSingleRecipeSlotSize)
            return;

        RectTransform slotRect = singleRecipeSlotImage.rectTransform;
        slotRect.sizeDelta = singleRecipeSlotSize;
        slotRect.localScale = Vector3.one;
    }

    private void PrepareRecipeSlot(RecipeSlotUI slot)
    {
        if (slot == null)
            return;

        slot.BindChildren();

        if (hideRecipeSlotBackgrounds)
        {
            Image slotBackground = slot.GetComponent<Image>();
            if (slotBackground != null && slotBackground != singleRecipeSlotImage)
                slotBackground.color = Color.clear;
        }

        if (!autoFixRecipeIconLayout || slot.iconImage == null)
            return;

        RectTransform iconRect = slot.iconImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = recipeIconSize;
        iconRect.localScale = Vector3.one;
    }

    private bool HasRecipeMaterials(ShopItemData item)
    {
        if (item == null || item.recipeMaterials == null)
            return false;

        foreach (ShopItemData.RecipeMaterial material in item.recipeMaterials)
        {
            if (material != null && material.item != null)
                return true;
        }

        return false;
    }

    private void HideMaterialRecipeSlots()
    {
        foreach (RecipeSlotUI slot in materialRecipeSlots)
        {
            if (slot != null)
                slot.Hide();
        }
    }

    private Button CreateItemButton(ShopItemData item, RectTransform parent)
    {
        Button button = Instantiate(itemButtonPrefab, parent);
        button.name = $"{item.itemName}Button";
        button.gameObject.SetActive(true);

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.localScale = Vector3.one;
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
        if (overrideGeneratedItemButtonHeight)
            buttonRect.sizeDelta = new Vector2(0f, itemButtonHeight);
        }

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();

        if (overrideGeneratedItemButtonHeight)
        {
            layoutElement.preferredHeight = itemButtonHeight;
            layoutElement.minHeight = itemButtonHeight;
        }

        TMP_Text nameText = FindChildComponent<TMP_Text>(button.transform, "NameText", "ItemNameText");
        if (nameText != null)
        {
            nameText.text = "";
            nameText.gameObject.SetActive(false);
        }

        TMP_Text statsText = FindChildComponent<TMP_Text>(button.transform, "StatsText", "EffectText", "ItemStatsText");
        if (statsText != null)
        {
            statsText.text = GetItemListText(item);
            statsText.color = bodyTextColor;
            if (uiFont != null)
                statsText.font = uiFont;
        }

        TMP_Text priceText = FindChildComponent<TMP_Text>(button.transform, "PriceText", "ItemPriceText", "CostText");
        if (priceText != null)
        {
            priceText.text = GetPurchasePrice(item).ToString();
            priceText.color = titleTextColor;
            if (uiFont != null)
                priceText.font = uiFont;
        }

        TMP_Text label = FindChildComponent<TMP_Text>(button.transform, "Label");
        if (label != null)
        {
            if (statsText != null || priceText != null)
            {
                label.text = "";
                label.gameObject.SetActive(false);
            }
            else
            {
                label.text = GetItemListText(item);
                label.color = bodyTextColor;
                if (uiFont != null)
                    label.font = uiFont;
            }
        }

        Image iconImage = FindChildComponent<Image>(button.transform, "Icon", "ItemIcon");
        if (iconImage != null)
        {
            iconImage.sprite = item.iconSprite;
            iconImage.color = item.iconColor.a <= 0f ? Color.white : item.iconColor;
            iconImage.gameObject.SetActive(true);
        }

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = itemButtonSprite;
            buttonImage.color = selectedItem == item
                ? itemButtonSelectedColor
                : itemButtonSprite != null ? itemButtonNormalColor : Color.clear;
        }

        Image statBackgroundImage = FindChildComponent<Image>(button.transform, "StatBackground", "StatsBackground", "DescriptionBackground");
        if (statBackgroundImage != null)
        {
            statBackgroundImage.sprite = GetItemStatBackgroundSprite(item.tier);
            statBackgroundImage.color = Color.white;
            statBackgroundImage.gameObject.SetActive(true);
        }

        Image statIconImage = FindChildComponent<Image>(button.transform, "StatIcon", "EffectIcon", "DescriptionIcon");
        if (statIconImage != null)
        {
            statIconImage.gameObject.SetActive(false);
        }

        bool createdStatEntries = CreateItemStatEntries(button.transform, item, statsText);
        if (createdStatEntries && statsText != null)
        {
            statsText.text = "";
            statsText.gameObject.SetActive(true);
        }

        if (autoFixItemButtonLayout)
            FixItemButtonChildrenLayout(button.transform);

        button.onClick.RemoveAllListeners();
        ConnectItemButtonClicks(button, item);
        return button;
    }

    private bool CreateItemStatEntries(Transform buttonTransform, ShopItemData item, TMP_Text statsText)
    {
        if (buttonTransform == null || item == null)
            return false;

        DestroyGeneratedChild(buttonTransform, "GeneratedStatIcons");
        DestroyGeneratedChild(buttonTransform, "GeneratedStatEntries");
        DestroyGeneratedChild(buttonTransform, "GeneratedStatBackground");

        List<StatEntryViewData> statEntries = GetItemStatEntries(item);
        if (statEntries.Count == 0)
            return false;

        RectTransform statBackground = FindStatBackgroundRect(buttonTransform);
        RectTransform anchor = statBackground != null ? null : FindStatEntriesAnchor(buttonTransform);
        Transform entriesParent = GetStatEntriesParent(buttonTransform, anchor, statsText, statBackground);

        GameObject entryRootObject = CreateUIObject("GeneratedStatEntries", entriesParent);
        RectTransform entryRootRect = entryRootObject.GetComponent<RectTransform>();
        entryRootRect.anchorMin = new Vector2(0f, 1f);
        entryRootRect.anchorMax = new Vector2(0f, 1f);
        entryRootRect.pivot = new Vector2(0f, 1f);
        entryRootRect.anchoredPosition = GetItemStatEntryRootPosition(buttonTransform, anchor, entriesParent, statsText, statBackground);
        entryRootRect.sizeDelta = GetStatEntriesRootSize(statEntries.Count, entriesParent as RectTransform);
        entryRootRect.localScale = Vector3.one;
        entryRootRect.SetAsLastSibling();

        int perRow = Mathf.Max(1, itemStatEntriesPerRow);
        for (int i = 0; i < statEntries.Count; i++)
        {
            StatEntryViewData entry = statEntries[i];
            GameObject entryObject = CreateUIObject($"StatEntry{i + 1}", entryRootRect);
            RectTransform entryRect = entryObject.GetComponent<RectTransform>();
            entryRect.anchorMin = new Vector2(0f, 1f);
            entryRect.anchorMax = new Vector2(0f, 1f);
            entryRect.pivot = new Vector2(0f, 1f);
            entryRect.anchoredPosition = new Vector2(
                (i % perRow) * (itemStatEntrySize.x + itemStatIconSpacing),
                -(i / perRow) * (itemStatEntrySize.y + itemStatRowSpacing));
            entryRect.sizeDelta = itemStatEntrySize;
            entryRect.localScale = Vector3.one;

            GameObject iconObject = CreateImageObject("Icon", entryRect, entry.icon, entry.icon != null ? Color.white : Color.clear);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            Vector2 statIconSize = GetStatPartSize(itemStatIconSize, forceSquareItemStatIcon);
            iconRect.sizeDelta = statIconSize;
            iconRect.localScale = Vector3.one;

            TMP_Text valueText = CreateText("Value", entryRect, entry.valueText, itemStatValueFontSize, bodyTextColor, TextAlignmentOptions.MidlineLeft);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0f, 0.5f);
            valueRect.anchorMax = new Vector2(0f, 0.5f);
            valueRect.pivot = new Vector2(0f, 0.5f);
            valueRect.anchoredPosition = new Vector2(statIconSize.x + itemStatIconSpacing, 0f);
            valueRect.sizeDelta = GetStatPartSize(itemStatValueSize, forceSquareItemStatValue);
            valueText.enableWordWrapping = false;
        }

        return true;
    }

    private List<StatEntryViewData> GetItemStatEntries(ShopItemData item)
    {
        List<StatEntryViewData> entries = new List<StatEntryViewData>();

        AddStatEntry(entries, !Mathf.Approximately(item.attackSpeedPercent, 0f), attackSpeedStatIconSprite, $"{item.attackSpeedPercent:0.#}%");
        AddStatEntry(entries, item.attackPowerBonus != 0, attackPowerStatIconSprite, $"+{item.attackPowerBonus}");
        AddStatEntry(entries, !Mathf.Approximately(item.criticalChancePercent, 0f), criticalChanceStatIconSprite, $"{item.criticalChancePercent:0.#}%");
        AddStatEntry(entries, item.maxHpBonus != 0, maxHpStatIconSprite, $"+{item.maxHpBonus}");
        AddStatEntry(entries, !Mathf.Approximately(item.skillCooldownReductionPercent, 0f), skillCooldownStatIconSprite, $"{item.skillCooldownReductionPercent:0.#}%");

        return entries;
    }

    private void AddStatEntry(List<StatEntryViewData> entries, bool shouldAdd, Sprite sprite, string valueText)
    {
        if (!shouldAdd)
            return;

        entries.Add(new StatEntryViewData(sprite != null ? sprite : itemStatIconSprite, valueText));
    }

    private Vector2 GetItemStatIconRootPosition()
    {
        return new Vector2(itemIconLeft + itemIconSize.x + itemStatEntriesOffset.x, itemStatEntriesOffset.y);
    }

    private Vector2 GetItemStatEntryRootPosition(Transform buttonTransform, RectTransform anchor, Transform entriesParent, TMP_Text statsText, RectTransform statBackground)
    {
        if (anchor != null)
            return anchor.anchoredPosition;

        if (statBackground != null && entriesParent == statBackground)
            return new Vector2(itemStatEntriesPadding.x, -itemStatEntriesPadding.y);

        if (statsText != null && entriesParent == statsText.transform)
            return new Vector2(0f, 0f);

        return GetItemStatIconRootPosition();
    }

    private Transform GetStatEntriesParent(Transform buttonTransform, RectTransform anchor, TMP_Text statsText, RectTransform statBackground)
    {
        if (statBackground != null)
            return statBackground;

        if (anchor != null && anchor.parent != null)
            return anchor.parent;

        if (statsText != null)
            return statsText.transform;

        return buttonTransform;
    }

    private Vector2 GetStatPartSize(Vector2 size, bool forceSquare)
    {
        if (!forceSquare)
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));

        float side = Mathf.Max(1f, Mathf.Max(size.x, size.y));
        return new Vector2(side, side);
    }

    private RectTransform FindStatBackgroundRect(Transform buttonTransform)
    {
        Image statBackgroundImage = FindChildComponent<Image>(buttonTransform, "StatBackground", "StatsBackground", "DescriptionBackground");
        if (statBackgroundImage != null)
            return statBackgroundImage.rectTransform;

        return null;
    }

    private void DestroyGeneratedChild(Transform root, string targetName)
    {
        RectTransform rect = FindChildRectTransform(root, targetName);
        if (rect != null)
            Destroy(rect.gameObject);
    }

    private RectTransform FindStatEntriesAnchor(Transform buttonTransform)
    {
        if (!useItemStatEntriesAnchor || buttonTransform == null)
            return null;

        RectTransform[] rects = buttonTransform.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            if (rect.name == "StatEntriesAnchor")
                return rect;
        }

        Image statBackgroundImage = FindChildComponent<Image>(buttonTransform, "StatBackground", "StatsBackground", "DescriptionBackground");
        Transform anchorParent = statBackgroundImage != null ? statBackgroundImage.transform : buttonTransform;
        return CreateStatEntriesAnchor(anchorParent);
    }

    private RectTransform CreateStatEntriesAnchor(Transform parent)
    {
        GameObject anchorObject = CreateUIObject("StatEntriesAnchor", parent);
        RectTransform anchorRect = anchorObject.GetComponent<RectTransform>();
        anchorRect.anchorMin = new Vector2(0f, 1f);
        anchorRect.anchorMax = new Vector2(0f, 1f);
        anchorRect.pivot = new Vector2(0f, 1f);
        anchorRect.anchoredPosition = GetItemStatIconRootPosition();
        anchorRect.sizeDelta = new Vector2(18f, 18f);
        anchorRect.localScale = Vector3.one;
        return anchorRect;
    }

    private Vector2 GetStatEntriesRootSize(int entryCount, RectTransform parentRect)
    {
        int perRow = Mathf.Max(1, itemStatEntriesPerRow);
        int rows = Mathf.CeilToInt(entryCount / (float)perRow);
        int columns = Mathf.Min(entryCount, perRow);

        Vector2 size = new Vector2(
            columns * itemStatEntrySize.x + Mathf.Max(0, columns - 1) * itemStatIconSpacing,
            rows * itemStatEntrySize.y + Mathf.Max(0, rows - 1) * itemStatRowSpacing);

        if (parentRect != null && parentRect.rect.width > 0f && parentRect.rect.height > 0f)
        {
            size.x = Mathf.Min(size.x, Mathf.Max(0f, parentRect.rect.width - itemStatEntriesPadding.x * 2f));
            size.y = Mathf.Min(size.y, Mathf.Max(0f, parentRect.rect.height - itemStatEntriesPadding.y * 2f));
        }

        return size;
    }

    private struct StatEntryViewData
    {
        public Sprite icon;
        public string valueText;

        public StatEntryViewData(Sprite icon, string valueText)
        {
            this.icon = icon;
            this.valueText = valueText;
        }
    }

    private void FixItemButtonChildrenLayout(Transform buttonTransform)
    {
        Image iconImage = FindChildComponent<Image>(buttonTransform, "Icon", "ItemIcon");
        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(itemIconLeft, 0f);
            iconRect.sizeDelta = itemIconSize;
            iconRect.localScale = Vector3.one;
        }

        Image statBackgroundImage = FindChildComponent<Image>(buttonTransform, "StatBackground", "StatsBackground", "DescriptionBackground");
        if (statBackgroundImage != null)
        {
            RectTransform statBackgroundRect = statBackgroundImage.rectTransform;
            statBackgroundRect.anchorMin = new Vector2(0f, 0f);
            statBackgroundRect.anchorMax = new Vector2(1f, 1f);
            statBackgroundRect.offsetMin = new Vector2(itemIconLeft + itemIconSize.x * 0.5f, 0f);
            statBackgroundRect.offsetMax = Vector2.zero;
            statBackgroundRect.localScale = Vector3.one;
        }

        Image statIconImage = FindChildComponent<Image>(buttonTransform, "StatIcon", "EffectIcon", "DescriptionIcon");
        if (statIconImage != null)
        {
            RectTransform statIconRect = statIconImage.rectTransform;
            statIconRect.anchorMin = new Vector2(0f, 1f);
            statIconRect.anchorMax = new Vector2(0f, 1f);
            statIconRect.pivot = new Vector2(0f, 1f);
            statIconRect.anchoredPosition = new Vector2(itemIconLeft + itemIconSize.x * 0.5f + 18f, -8f);
            statIconRect.sizeDelta = new Vector2(22f, 22f);
            statIconRect.localScale = Vector3.one;
        }

        RectTransform generatedStatIconsRect = buttonTransform.Find("GeneratedStatIcons") as RectTransform;
        if (generatedStatIconsRect != null)
        {
            TMP_Text iconParentStatsText = FindChildComponent<TMP_Text>(buttonTransform, "StatsText", "EffectText", "ItemStatsText");
            RectTransform statBackground = FindStatBackgroundRect(buttonTransform);
            RectTransform anchor = statBackground != null ? null : FindStatEntriesAnchor(buttonTransform);
            Transform entriesParent = GetStatEntriesParent(buttonTransform, anchor, iconParentStatsText, statBackground);
            generatedStatIconsRect.anchorMin = new Vector2(0f, 1f);
            generatedStatIconsRect.anchorMax = new Vector2(0f, 1f);
            generatedStatIconsRect.pivot = new Vector2(0f, 1f);
            generatedStatIconsRect.anchoredPosition = GetItemStatEntryRootPosition(buttonTransform, anchor, entriesParent, iconParentStatsText, statBackground);
            generatedStatIconsRect.localScale = Vector3.one;
            generatedStatIconsRect.SetAsLastSibling();
        }

        RectTransform generatedStatEntriesRect = FindChildRectTransform(buttonTransform, "GeneratedStatEntries");
        if (generatedStatEntriesRect != null)
        {
            TMP_Text entryParentStatsText = FindChildComponent<TMP_Text>(buttonTransform, "StatsText", "EffectText", "ItemStatsText");
            RectTransform statBackground = FindStatBackgroundRect(buttonTransform);
            RectTransform anchor = statBackground != null ? null : FindStatEntriesAnchor(buttonTransform);
            Transform entriesParent = GetStatEntriesParent(buttonTransform, anchor, entryParentStatsText, statBackground);
            generatedStatEntriesRect.anchorMin = new Vector2(0f, 1f);
            generatedStatEntriesRect.anchorMax = new Vector2(0f, 1f);
            generatedStatEntriesRect.pivot = new Vector2(0f, 1f);
            generatedStatEntriesRect.anchoredPosition = GetItemStatEntryRootPosition(buttonTransform, anchor, entriesParent, entryParentStatsText, statBackground);
            generatedStatEntriesRect.sizeDelta = GetStatEntriesRootSize(generatedStatEntriesRect.childCount, entriesParent as RectTransform);
            generatedStatEntriesRect.localScale = Vector3.one;
            generatedStatEntriesRect.SetAsLastSibling();
        }

        TMP_Text statsText = FindChildComponent<TMP_Text>(buttonTransform, "StatsText", "EffectText", "ItemStatsText");
        if (statsText != null)
        {
            float textLeft = itemIconLeft + itemIconSize.x * 0.5f + 48f;
            if (generatedStatIconsRect != null && generatedStatIconsRect.gameObject.activeSelf)
                textLeft = generatedStatIconsRect.anchoredPosition.x + generatedStatIconsRect.sizeDelta.x + 8f;

            RectTransform statsRect = statsText.rectTransform;
            statsRect.anchorMin = new Vector2(0f, 0f);
            statsRect.anchorMax = new Vector2(1f, 1f);
            statsRect.offsetMin = new Vector2(textLeft, 6f);
            statsRect.offsetMax = new Vector2(-110f, -6f);
            statsRect.localScale = Vector3.one;
            statsText.alignment = TextAlignmentOptions.MidlineLeft;
            statsText.enableWordWrapping = false;
        }

        TMP_Text priceText = FindChildComponent<TMP_Text>(buttonTransform, "PriceText", "ItemPriceText", "CostText");
        if (priceText != null)
        {
            RectTransform priceRect = priceText.rectTransform;
            priceRect.anchorMin = new Vector2(1f, 0f);
            priceRect.anchorMax = new Vector2(1f, 1f);
            priceRect.pivot = new Vector2(1f, 0.5f);
            priceRect.anchoredPosition = new Vector2(-14f, 0f);
            priceRect.sizeDelta = new Vector2(92f, 0f);
            priceRect.localScale = Vector3.one;
            priceText.alignment = TextAlignmentOptions.MidlineRight;
            priceText.enableWordWrapping = false;
        }
    }

    private Sprite GetItemStatBackgroundSprite(ShopItemTier tier)
    {
        if (tier == ShopItemTier.Low && lowTierItemStatBackgroundSprite != null)
            return lowTierItemStatBackgroundSprite;

        if (tier == ShopItemTier.Mid && midTierItemStatBackgroundSprite != null)
            return midTierItemStatBackgroundSprite;

        if (tier == ShopItemTier.High && highTierItemStatBackgroundSprite != null)
            return highTierItemStatBackgroundSprite;

        return itemButtonSprite;
    }

    private void FixItemListLayout()
    {
        FixItemViewportMask();

        RectTransform listRect = itemListRoot;
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = new Vector2(itemListPositionX, 0f);
        listRect.localScale = Vector3.one;

        VerticalLayoutGroup layout = itemListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = itemListRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = itemListRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = itemListRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement prefabLayout = itemButtonPrefab.GetComponent<LayoutElement>();
        if (prefabLayout == null)
            prefabLayout = itemButtonPrefab.gameObject.AddComponent<LayoutElement>();

        if (overrideGeneratedItemButtonHeight)
        {
            prefabLayout.preferredHeight = itemButtonHeight;
            prefabLayout.minHeight = itemButtonHeight;
        }
    }

    private void FixItemViewportMask()
    {
        if (!useRectMaskForItemViewport)
            return;

        RectTransform viewport = itemScrollRect != null ? itemScrollRect.viewport : null;

        if (viewport == null && itemListRoot != null && itemListRoot.parent is RectTransform parentRect)
            viewport = parentRect;

        if (viewport == null)
            return;

        Mask oldMask = viewport.GetComponent<Mask>();
        if (oldMask != null)
            oldMask.enabled = false;

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
            rectMask = viewport.gameObject.AddComponent<RectMask2D>();

        rectMask.enabled = true;

        if (itemScrollRect != null)
            itemScrollRect.viewport = viewport;
    }

    private void ConnectItemButtonClicks(Button button, ShopItemData item)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        EventTrigger.Entry clickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };

        clickEntry.callback.AddListener(eventData =>
        {
            PointerEventData pointerData = eventData as PointerEventData;

            if (pointerData != null && pointerData.button == PointerEventData.InputButton.Right)
            {
                selectedItem = item;
                BuySelectedItem();
                return;
            }

            SelectItem(item);
        });

        trigger.triggers.Add(clickEntry);
    }

    private Button CreateButton(string name, Transform parent, string labelText, Sprite sprite, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment, bool anchorRight = false)
    {
        GameObject buttonObject = CreateImageObject(name, parent, sprite, sprite != null ? Color.white : new Color(0.32f, 0.22f, 0.16f, 1f));
        Button button = buttonObject.AddComponent<Button>();

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.anchorMax = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.pivot = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = CreateText("Label", rect, labelText, 18f, titleTextColor, alignment);
        SetStretchRect(text.rectTransform, new Vector2(8f, 4f), new Vector2(8f, 4f));
        return button;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent);
        TMP_Text tmpText = textObject.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = color;
        tmpText.alignment = alignment;
        tmpText.raycastTarget = false;

        if (uiFont != null)
            tmpText.font = uiFont;

        return tmpText;
    }

    private GameObject CreateImageObject(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = CreateUIObject(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = true;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return imageObject;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private void SetStretchRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
        rect.localScale = Vector3.one;
    }

    private void ConnectButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void AddMaterialRecipeSlotIfFound(params string[] names)
    {
        RecipeSlotUI slot = FindInPanel<RecipeSlotUI>(names);

        if (slot != null && !materialRecipeSlots.Contains(slot))
            materialRecipeSlots.Add(slot);
    }

    private void AddInventorySlotIfFound(params string[] names)
    {
        ShopInventorySlotUI slot = FindInPanel<ShopInventorySlotUI>(names);
        if (slot != null && !inventorySlots.Contains(slot))
        {
            inventorySlots.Add(slot);
            return;
        }

        Image image = FindInPanel<Image>(names);
        if (image == null)
            return;

        slot = image.GetComponent<ShopInventorySlotUI>();
        if (slot == null)
            slot = image.gameObject.AddComponent<ShopInventorySlotUI>();

        if (!inventorySlots.Contains(slot))
            inventorySlots.Add(slot);
    }

    private T FindInPanel<T>(params string[] names) where T : Component
    {
        if (panelRoot == null)
            return null;

        T[] components = panelRoot.GetComponentsInChildren<T>(true);

        foreach (string targetName in names)
        {
            foreach (T component in components)
            {
                if (component.name == targetName)
                    return component;
            }
        }

        return null;
    }

    private T FindChildComponent<T>(Transform root, params string[] names) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);

        foreach (string targetName in names)
        {
            foreach (T component in components)
            {
                if (component.name == targetName)
                    return component;
            }
        }

        return null;
    }

    private RectTransform FindChildRectTransform(Transform root, string targetName)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            if (rect.name == targetName)
                return rect;
        }

        return null;
    }

    private bool CanBuy(ShopItemData item)
    {
        if (item == null)
            return false;

        if (!item.canBuyMultiple && purchasedUniqueItemIds.Contains(GetItemId(item)))
            return false;

        if (requireRecipeMaterialsForPurchase)
            return false;

        if (playerStats != null && playerStats.gold < GetPurchasePrice(item))
            return false;

        return GetInventoryCountAfterPurchase(item) <= inventoryCapacity;
    }

    private int GetPurchasePrice(ShopItemData item)
    {
        if (item == null)
            return 0;

        return Mathf.Max(0, item.price - GetOwnedMaterialDiscount(item));
    }

    private int GetOwnedMaterialDiscount(ShopItemData item)
    {
        if (item == null || item.recipeMaterials == null)
            return 0;

        int discount = 0;
        foreach (AggregatedRecipeMaterial material in GetAggregatedRecipeMaterials(item))
        {
            int ownedCount = GetInventoryItemCount(material.item);
            int usableCount = Mathf.Min(material.requiredCount, ownedCount);
            discount += material.item.price * usableCount;
        }

        return discount;
    }

    private void AddInventoryItem(ShopItemData item)
    {
        AddInventoryItem(item, -1);
    }

    private void AddInventoryItem(ShopItemData item, int insertIndex)
    {
        if (item == null)
            return;

        InventoryEntry entry = new InventoryEntry(item);
        if (insertIndex >= 0 && insertIndex <= inventoryItems.Count)
            inventoryItems.Insert(insertIndex, entry);
        else
            inventoryItems.Add(entry);
    }

    private void SaveInventoryToSession()
    {
        if (GameSession.Instance == null)
            return;

        List<string> inventoryItemIds = new List<string>(inventoryItems.Count);

        foreach (InventoryEntry entry in inventoryItems)
        {
            if (entry != null && entry.item != null)
                inventoryItemIds.Add(GetItemId(entry.item));
        }

        GameSession.Instance.SaveShopInventory(inventoryItemIds, purchasedUniqueItemIds);
    }

    private void RestoreInventoryFromSession()
    {
        GameSession session = GameSession.Instance;

        if (session == null)
            return;

        Dictionary<string, ShopItemData> itemLookup = BuildItemLookup();
        inventoryItems.Clear();
        purchasedUniqueItemIds.Clear();

        foreach (string itemId in session.shopInventoryItemIds)
        {
            if (itemLookup.TryGetValue(itemId, out ShopItemData item))
                inventoryItems.Add(new InventoryEntry(item));
            else
                Debug.LogWarning($"상점 인벤토리 아이템 '{itemId}'을 현재 씬의 Items 목록에서 찾지 못했습니다.");
        }

        foreach (string itemId in session.purchasedUniqueShopItemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
                purchasedUniqueItemIds.Add(itemId);
        }
    }

    private Dictionary<string, ShopItemData> BuildItemLookup()
    {
        Dictionary<string, ShopItemData> lookup = new Dictionary<string, ShopItemData>();
        HashSet<ShopItemData> visited = new HashSet<ShopItemData>();

        foreach (ShopItemData item in items)
            AddItemToLookup(item, lookup, visited);

        return lookup;
    }

    private void AddItemToLookup(
        ShopItemData item,
        Dictionary<string, ShopItemData> lookup,
        HashSet<ShopItemData> visited)
    {
        if (item == null || !visited.Add(item))
            return;

        string itemId = GetItemId(item);

        if (!string.IsNullOrWhiteSpace(itemId) && !lookup.ContainsKey(itemId))
            lookup.Add(itemId, item);

        if (item.recipeMaterials == null)
            return;

        foreach (ShopItemData.RecipeMaterial material in item.recipeMaterials)
        {
            if (material != null)
                AddItemToLookup(material.item, lookup, visited);
        }
    }

    private int GetInventoryItemCount(ShopItemData item)
    {
        if (item == null)
            return 0;

        string itemId = GetItemId(item);
        int count = 0;
        foreach (InventoryEntry entry in inventoryItems)
        {
            if (entry != null && entry.item != null && GetItemId(entry.item) == itemId)
                count++;
        }

        return count;
    }

    private int GetInventoryCountAfterPurchase(ShopItemData item)
    {
        if (item == null)
            return inventoryItems.Count;

        return inventoryItems.Count - GetConsumedMaterialCount(item) + 1;
    }

    private int GetConsumedMaterialCount(ShopItemData item)
    {
        if (item == null || item.recipeMaterials == null)
            return 0;

        int consumedCount = 0;
        foreach (AggregatedRecipeMaterial material in GetAggregatedRecipeMaterials(item))
        {
            int ownedCount = GetInventoryItemCount(material.item);
            consumedCount += Mathf.Min(material.requiredCount, ownedCount);
        }

        return consumedCount;
    }

    private int ConsumeOwnedMaterials(ShopItemData item)
    {
        if (item == null || item.recipeMaterials == null)
            return -1;

        int firstConsumedIndex = -1;
        foreach (AggregatedRecipeMaterial material in GetAggregatedRecipeMaterials(item))
        {
            int removeCount = Mathf.Min(material.requiredCount, GetInventoryItemCount(material.item));
            int consumedIndex = RemoveInventoryItems(material.item, removeCount);
            if (consumedIndex >= 0 && (firstConsumedIndex < 0 || consumedIndex < firstConsumedIndex))
                firstConsumedIndex = consumedIndex;
        }

        return firstConsumedIndex;
    }

    private List<AggregatedRecipeMaterial> GetAggregatedRecipeMaterials(ShopItemData item)
    {
        List<AggregatedRecipeMaterial> result = new List<AggregatedRecipeMaterial>();
        if (item == null || item.recipeMaterials == null)
            return result;

        Dictionary<string, AggregatedRecipeMaterial> materialsById =
            new Dictionary<string, AggregatedRecipeMaterial>();

        foreach (ShopItemData.RecipeMaterial material in item.recipeMaterials)
        {
            if (material == null || material.item == null)
                continue;

            string itemId = GetItemId(material.item);
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!materialsById.TryGetValue(itemId, out AggregatedRecipeMaterial aggregated))
            {
                aggregated = new AggregatedRecipeMaterial
                {
                    item = material.item,
                    requiredCount = 0
                };
                materialsById.Add(itemId, aggregated);
                result.Add(aggregated);
            }

            aggregated.requiredCount += Mathf.Max(1, material.count);
        }

        return result;
    }

    private int RemoveInventoryItems(ShopItemData item, int count)
    {
        if (item == null || count <= 0)
            return -1;

        string itemId = GetItemId(item);
        int firstRemovedIndex = -1;
        for (int i = inventoryItems.Count - 1; i >= 0 && count > 0; i--)
        {
            InventoryEntry entry = inventoryItems[i];
            if (entry == null || entry.item == null || GetItemId(entry.item) != itemId)
                continue;

            inventoryItems.RemoveAt(i);
            firstRemovedIndex = firstRemovedIndex < 0 ? i : Mathf.Min(firstRemovedIndex, i);
            if (playerStats != null)
                playerStats.RemoveShopItem(entry.item);
            count--;
        }

        return firstRemovedIndex;
    }

    private void RefreshInventorySlots()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            ShopInventorySlotUI slot = inventorySlots[i];
            if (slot == null)
                continue;

            if (i < inventoryItems.Count && inventoryItems[i] != null)
                slot.Show(inventoryItems[i].item);
            else
            {
                slot.Clear();
            }
        }
    }

    private void SetRecipeSlotAvailability(RecipeSlotUI slot, bool isAvailable)
    {
        if (slot == null)
            return;

        slot.BindChildren();

        if (slot.backgroundImage != null)
            slot.backgroundImage.color = Color.white;

        if (slot.iconImage != null)
            slot.iconImage.color = slot.GetIconColor();

        slot.SetUnavailableOverlay(!isAvailable, recipeSlotUnavailableOverlayColor);

        Color textColor = isAvailable ? titleTextColor : recipeSlotUnavailableColor;
        if (slot.countText != null)
            slot.countText.color = textColor;

        if (slot.priceText != null)
            slot.priceText.color = textColor;
    }

    private void ConnectRecipeSlotClick(RecipeSlotUI slot, ShopItemData item)
    {
        ConnectRecipeSlotClick(slot, item, CanBuy(item));
    }

    private void ConnectRecipeSlotClick(RecipeSlotUI slot, ShopItemData item, bool isAvailable)
    {
        if (slot == null || item == null)
            return;

        Button button = slot.GetComponent<Button>();
        if (button == null)
            button = slot.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => BuyRecipeItem(item));
        button.interactable = true;

        SetRecipeSlotAvailability(slot, isAvailable);
    }

    private string GetCannotBuyMessage(ShopItemData item)
    {
        if (item != null && !item.canBuyMultiple && purchasedUniqueItemIds.Contains(GetItemId(item)))
            return "Already purchased.";

        if (requireRecipeMaterialsForPurchase)
            return "Inventory crafting is not ready yet.";

        if (item != null && playerStats != null && playerStats.gold < GetPurchasePrice(item))
            return "Not enough gold.";

        if (item != null && GetInventoryCountAfterPurchase(item) > inventoryCapacity)
            return "Inventory is full.";

        return "Cannot buy.";
    }

    private string GetItemListText(ShopItemData item)
    {
        if (item == null)
            return "";

        string effectText = item.GetEffectText();
        if (effectText == "No effect")
            return GetPurchasePrice(item).ToString();

        return $"{effectText.Replace("\n", "   ")}\n{GetPurchasePrice(item)}";
    }

    private string GetItemId(ShopItemData item)
    {
        if (!string.IsNullOrWhiteSpace(item.id))
            return item.id;

        return item.itemName;
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }
}
