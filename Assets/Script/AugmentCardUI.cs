using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardUI : MonoBehaviour
{
    public Button button;
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text effectText;
    public TMP_Text descriptionText;

    public void Bind(AugmentData augment, UnityEngine.Events.UnityAction onClick)
    {
        if (augment == null)
            return;

        FindReferencesIfNeeded();

        if (!augment.keepPrefabVisuals && iconImage != null)
        {
            iconImage.sprite = augment.icon;
            iconImage.color = augment.icon != null ? Color.white : Color.clear;
            iconImage.gameObject.SetActive(augment.icon != null);
        }

        if (!augment.keepPrefabVisuals)
        {
            SetText(nameText, augment.augmentName);
            SetText(effectText, augment.GetEffectText());
            SetText(descriptionText, augment.description);
        }

        ConnectButtons(onClick);
    }

    private void ConnectButtons(UnityEngine.Events.UnityAction onClick)
    {
        Button rootButton = GetComponent<Button>();
        if (rootButton == null)
        {
            Image rootImage = GetComponent<Image>();
            if (rootImage == null)
            {
                rootImage = gameObject.AddComponent<Image>();
                rootImage.color = new Color(1f, 1f, 1f, 0f);
            }

            rootImage.raycastTarget = true;
            rootButton = gameObject.AddComponent<Button>();
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button targetButton = buttons[i];
            if (targetButton == null)
                continue;

            targetButton.interactable = true;
            targetButton.onClick.RemoveAllListeners();
            targetButton.onClick.AddListener(onClick);
        }

        button = rootButton;
    }

    private void FindReferencesIfNeeded()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            button = GetComponentInChildren<Button>(true);

        if (iconImage == null)
            iconImage = FindChild<Image>("Icon", "IconImage", "AugmentIcon");

        if (nameText == null)
            nameText = FindChild<TMP_Text>("NameText", "TitleText", "AugmentNameText");

        if (effectText == null)
            effectText = FindChild<TMP_Text>("EffectText", "StatText", "AugmentEffectText");

        if (descriptionText == null)
            descriptionText = FindChild<TMP_Text>("DescriptionText", "DescText", "AugmentDescriptionText");
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private T FindChild<T>(params string[] names) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
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
}
