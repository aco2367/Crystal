using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShopTrigger : MonoBehaviour
{
    public ShopPanelController shopPanel;
    public PlayerStats currentPlayerStats;

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
    }

    private void Update()
    {
        if (!playerInside || currentPlayerStats == null)
            return;

        if (KeyBindingManager.WasPressedThisFrame(GameKeyAction.Interact))
        {
            ShopPanelController panel = GetPanel();

            if (panel.IsOpen())
                panel.Close();
            else
                panel.Open(currentPlayerStats);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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

        if (shopPanel != null)
            shopPanel.Close();
        else if (ShopPanelController.Instance != null)
            ShopPanelController.Instance.Close();
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

    private ShopPanelController GetPanel()
    {
        if (shopPanel == null)
            shopPanel = ShopPanelController.GetOrCreate();

        return shopPanel;
    }
}
