using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class AugmentTrigger : MonoBehaviour
{
    public AugmentPanelController augmentPanel;
    public string[] allowedSceneNames = { "MainMap1", "MainMap2" };

    private PlayerStats currentPlayerStats;
    private bool playerInside;

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;

        if (augmentPanel == null)
            augmentPanel = AugmentPanelController.GetOrCreate();
    }

    private void Update()
    {
        if (!playerInside || currentPlayerStats == null || !IsAllowedScene())
            return;

        if (!KeyBindingManager.WasPressedThisFrame(GameKeyAction.Interact))
            return;

        AugmentPanelController panel = GetPanel();
        if (panel == null)
        {
            Debug.LogWarning("AugmentPanelController를 찾지 못해 증강 패널을 열 수 없습니다.", this);
            return;
        }

        if (panel.IsOpen())
            panel.Close();
        else
            panel.Open(currentPlayerStats);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAllowedScene())
            return;

        PlayerStats stats = FindPlayerStats(other);
        if (stats == null)
            return;

        playerInside = true;
        currentPlayerStats = stats;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerStats stats = FindPlayerStats(other);
        if (stats == null || stats != currentPlayerStats)
            return;

        playerInside = false;
        currentPlayerStats = null;

        AugmentPanelController panel = augmentPanel != null ? augmentPanel : AugmentPanelController.Instance;
        if (panel != null)
            panel.Close();
    }

    private PlayerStats FindPlayerStats(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null)
            stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null)
            stats = other.GetComponentInChildren<PlayerStats>();
        return stats;
    }

    private AugmentPanelController GetPanel()
    {
        if (augmentPanel == null)
            augmentPanel = AugmentPanelController.GetOrCreate();

        return augmentPanel;
    }

    private bool IsAllowedScene()
    {
        if (allowedSceneNames == null || allowedSceneNames.Length == 0)
            return true;

        string activeSceneName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < allowedSceneNames.Length; i++)
        {
            if (activeSceneName == allowedSceneNames[i])
                return true;
        }

        return false;
    }
}
