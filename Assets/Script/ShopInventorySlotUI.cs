using UnityEngine;
using UnityEngine.UI;

public class ShopInventorySlotUI : MonoBehaviour
{
    public Image iconImage;

    public void Show(ShopItemData item)
    {
        Show(item, Color.white);
    }

    public void Show(ShopItemData item, Color tint)
    {
        BindChildrenIfNeeded();

        if (iconImage == null)
            return;

        iconImage.sprite = item != null ? item.iconSprite : null;
        iconImage.color = item != null ? GetVisibleIconColor(item.iconColor) * tint : Color.clear;
        iconImage.gameObject.SetActive(item != null && item.iconSprite != null);
    }

    public void Clear()
    {
        BindChildrenIfNeeded();

        if (iconImage == null)
            return;

        iconImage.sprite = null;
        iconImage.color = Color.clear;
        iconImage.gameObject.SetActive(false);
    }

    private void BindChildrenIfNeeded()
    {
        if (iconImage != null)
            return;

        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.name == "Icon" || image.name == "ItemIcon")
            {
                iconImage = image;
                return;
            }
        }
    }

    private Color GetVisibleIconColor(Color color)
    {
        return color.a <= 0f ? Color.white : color;
    }
}
