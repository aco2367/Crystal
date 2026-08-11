using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TrainingPanelController : MonoBehaviour
{
    public static bool AnyOpen { get; private set; }
    public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;

    private static int escapeConsumedFrame = -1;

    [Header("Panel")]
    public GameObject panelRoot;
    public bool pauseGameWhileOpen = true;
    [Tooltip("체력/경험치/레벨/프로필 HUD보다 위에 표시할 정렬 순서입니다.")]
    public int panelSortingOrder = 1000;

    [Header("Common")]
    public TMP_Text availablePointsText;
    public Button closeButton;

    [Header("Attack Power Card")]
    public TMP_Text attackLevelText;
    public TMP_Text attackValueText;
    public Button attackDecreaseButton;
    public Button attackIncreaseButton;

    [Header("Attack Speed Card")]
    public TMP_Text attackSpeedLevelText;
    public TMP_Text attackSpeedValueText;
    public Button attackSpeedDecreaseButton;
    public Button attackSpeedIncreaseButton;

    [Header("Max HP Card")]
    public TMP_Text maxHpLevelText;
    public TMP_Text maxHpValueText;
    public Button maxHpDecreaseButton;
    public Button maxHpIncreaseButton;

    private PlayerStats playerStats;
    private float previousTimeScale = 1f;
    private bool pauseApplied;

    private void Awake()
    {
        ConnectButtons();

        if (panelRoot == null)
            panelRoot = gameObject;

        Close(false);
    }

    private void Update()
    {
        if (!IsOpen())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            escapeConsumedFrame = Time.frameCount;
            Close();
        }
    }

    private void OnDisable()
    {
        if (IsOpen())
            Close();
    }

    public void Open(PlayerStats stats)
    {
        if (stats == null || panelRoot == null)
            return;

        playerStats = stats;
        EnsurePanelDisplayOrder();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        AnyOpen = true;
        ApplyPauseIfNeeded();
        Refresh();
    }

    public void Close()
    {
        Close(true);
    }

    private void Close(bool restorePause)
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        playerStats = null;
        AnyOpen = false;

        if (restorePause)
            RestorePauseIfNeeded();
    }

    public bool IsOpen()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }

    private void EnsurePanelDisplayOrder()
    {
        if (panelRoot == null)
            return;

        Canvas panelCanvas = panelRoot.GetComponent<Canvas>();

        if (panelCanvas == null)
            panelCanvas = panelRoot.AddComponent<Canvas>();

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = panelSortingOrder;

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();
    }

    public void IncreaseAttack()
    {
        if (playerStats != null && playerStats.IncreaseAttackTraining())
            SaveAndRefresh();
    }

    public void DecreaseAttack()
    {
        if (playerStats != null && playerStats.DecreaseAttackTraining())
            SaveAndRefresh();
    }

    public void IncreaseAttackSpeed()
    {
        if (playerStats != null && playerStats.IncreaseAttackSpeedTraining())
            SaveAndRefresh();
    }

    public void DecreaseAttackSpeed()
    {
        if (playerStats != null && playerStats.DecreaseAttackSpeedTraining())
            SaveAndRefresh();
    }

    public void IncreaseMaxHp()
    {
        if (playerStats != null && playerStats.IncreaseMaxHpTraining())
            SaveAndRefresh();
    }

    public void DecreaseMaxHp()
    {
        if (playerStats != null && playerStats.DecreaseMaxHpTraining())
            SaveAndRefresh();
    }

    private void ConnectButtons()
    {
        ConnectButton(closeButton, Close);
        ConnectButton(attackDecreaseButton, DecreaseAttack);
        ConnectButton(attackIncreaseButton, IncreaseAttack);
        ConnectButton(attackSpeedDecreaseButton, DecreaseAttackSpeed);
        ConnectButton(attackSpeedIncreaseButton, IncreaseAttackSpeed);
        ConnectButton(maxHpDecreaseButton, DecreaseMaxHp);
        ConnectButton(maxHpIncreaseButton, IncreaseMaxHp);
    }

    private void ConnectButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void Refresh()
    {
        if (playerStats == null)
            return;

        SetText(availablePointsText, $"남은 포인트: {playerStats.trainingPoints}");
        SetText(attackLevelText, $"{playerStats.attackTrainingLevel} / {playerStats.maxAttackTrainingLevel}");
        SetText(attackValueText, $"공격력 +{playerStats.attackPowerPerTrainingPoint}");
        SetText(attackSpeedLevelText, $"{playerStats.attackSpeedTrainingLevel} / {playerStats.maxAttackSpeedTrainingLevel}");
        SetText(attackSpeedValueText, $"공격속도 +{playerStats.attackSpeedPerTrainingPoint:0.##}");
        SetText(maxHpLevelText, $"{playerStats.maxHpTrainingLevel} / {playerStats.maxHpTrainingLevelLimit}");
        SetText(maxHpValueText, $"최대 체력 +{playerStats.maxHpPerTrainingPoint}");

        bool hasPoint = playerStats.trainingPoints > 0;
        SetInteractable(attackIncreaseButton, hasPoint && playerStats.attackTrainingLevel < playerStats.maxAttackTrainingLevel);
        SetInteractable(attackSpeedIncreaseButton, hasPoint && playerStats.attackSpeedTrainingLevel < playerStats.maxAttackSpeedTrainingLevel);
        SetInteractable(maxHpIncreaseButton, hasPoint && playerStats.maxHpTrainingLevel < playerStats.maxHpTrainingLevelLimit);
        SetInteractable(attackDecreaseButton, playerStats.attackTrainingLevel > 0);
        SetInteractable(attackSpeedDecreaseButton, playerStats.attackSpeedTrainingLevel > 0);
        SetInteractable(maxHpDecreaseButton, playerStats.maxHpTrainingLevel > 0);
    }

    private void SaveAndRefresh()
    {
        if (GameSession.Instance != null)
            GameSession.Instance.SavePlayer(playerStats, playerStats.GetComponent<PlayerController>());

        Refresh();
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

    private void SetInteractable(Button target, bool value)
    {
        if (target != null)
            target.interactable = value;
    }
}
