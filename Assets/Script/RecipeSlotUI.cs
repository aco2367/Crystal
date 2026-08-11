using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeSlotUI : MonoBehaviour
{
    public Image backgroundImage;
    public Image iconImage;
    public Image unavailableOverlayImage;
    public TMP_Text countText;
    public TMP_Text nameText;
    public TMP_Text priceText;
    private Color lastIconColor = Color.white;

    public void BindChildren()
    {
        BindChildrenIfNeeded();
    }

    public void Show(ShopItemData item, int count)
    {
        Show(item, count, -1);
    }

    public void Show(ShopItemData item, int count, int price)
    {
        BindChildrenIfNeeded();
        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.iconSprite : null;
            lastIconColor = item != null ? GetVisibleIconColor(item.iconColor) : Color.white;
            iconImage.color = item != null ? lastIconColor : Color.clear;
            iconImage.gameObject.SetActive(item != null && item.iconSprite != null);
        }

        if (countText != null)
            countText.text = count > 1 ? count.ToString() : "";

        if (nameText != null)
            nameText.text = item != null ? item.itemName : "";

        if (priceText != null)
            priceText.text = price >= 0 ? price.ToString() : "";
    }

    public void Hide()
    {
        BindChildrenIfNeeded();
        gameObject.SetActive(false);

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = Color.clear;
        }

        if (countText != null)
            countText.text = "";

        if (nameText != null)
            nameText.text = "";

        if (priceText != null)
            priceText.text = "";

        SetUnavailableOverlay(false, Color.clear);
    }

    private void BindChildrenIfNeeded()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null)
            iconImage = FindChildImage("Icon", "ItemIcon");

        if (countText == null)
            countText = FindChildText("CountText", "Count");

        if (nameText == null)
            nameText = FindChildText("NameText", "Name");

        if (priceText == null)
            priceText = FindChildText("PriceText", "Price", "CostText", "Cost");
    }

    public void SetUnavailableOverlay(bool active, Color color)
    {
        BindChildrenIfNeeded();

        if (unavailableOverlayImage == null)
            unavailableOverlayImage = FindChildImage("UnavailableOverlay");

        if (unavailableOverlayImage == null)
            unavailableOverlayImage = CreateUnavailableOverlay();

        unavailableOverlayImage.color = active ? color : Color.clear;
        unavailableOverlayImage.gameObject.SetActive(active);
        unavailableOverlayImage.transform.SetAsLastSibling();
    }

    private Image FindChildImage(params string[] names)
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        foreach (string targetName in names)
        {
            foreach (Image image in images)
            {
                if (image.name == targetName)
                    return image;
            }
        }

        return null;
    }

    private TMP_Text FindChildText(params string[] names)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (string targetName in names)
        {
            foreach (TMP_Text text in texts)
            {
                if (text.name == targetName)
                    return text;
            }
        }

        return null;
    }

    private Image CreateUnavailableOverlay()
    {
        GameObject overlayObject = new GameObject("UnavailableOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.transform.SetParent(transform, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = overlayObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        image.gameObject.SetActive(false);
        return image;
    }

    private Color GetVisibleIconColor(Color color)
    {
        if (color.a <= 0f)
            return Color.white;

        return color;
    }

    public Color GetIconColor()
    {
        return lastIconColor;
    }
}
