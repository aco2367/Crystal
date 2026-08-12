using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class EarlyPhaseEndButton : MonoBehaviour
{
    [Header("Display")]
    public string attackLabel = "공격 종료";
    public string maintenanceLabel = "정비 종료";

    private Button button;
    private TMP_Text label;
    private CanvasGroup canvasGroup;
    private WaveManager waveManager;
    private float nextManagerSearchTime;

    private void Awake()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(EndCurrentPhase);
        Refresh();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(EndCurrentPhase);
    }

    private void Update()
    {
        FindWaveManagerIfNeeded();
        Refresh();
    }

    private void EndCurrentPhase()
    {
        FindWaveManagerIfNeeded();

        if (waveManager == null || !waveManager.TryEndCurrentPhaseEarly())
            return;

        button.interactable = false;
    }

    private void FindWaveManagerIfNeeded()
    {
        if (waveManager != null || Time.unscaledTime < nextManagerSearchTime)
            return;

        nextManagerSearchTime = Time.unscaledTime + 0.25f;
        waveManager = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
    }

    private void Refresh()
    {
        bool shouldShow = waveManager != null &&
            waveManager.CurrentPhase != WavePhase.Defense;

        canvasGroup.alpha = shouldShow ? 1f : 0f;
        canvasGroup.interactable = shouldShow;
        canvasGroup.blocksRaycasts = shouldShow;

        if (!shouldShow)
            return;

        if (button != null)
            button.interactable = waveManager != null && waveManager.CanEndCurrentPhaseEarly;

        if (label == null || waveManager == null)
            return;

        label.text = waveManager.CurrentPhase == WavePhase.Maintenance
            ? maintenanceLabel
            : attackLabel;
    }
}
