using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectController : MonoBehaviour
{
    [Header("Panel")]
    public GameObject characterPanel;

    [Header("Buttons")]
    public Button swordButton;
    public Button archerButton;
    public Button tankButton;
    public Button closeButton;

    [Header("Text")]
    public TMP_Text selectedRoleText;

    private PlayerRole selectedRole = PlayerRole.Sword;

    private void Awake()
    {
        if (characterPanel == null)
            characterPanel = gameObject;

        if (GameSession.Instance != null)
            selectedRole = GameSession.Instance.playerRole;

        ConnectButton(swordButton, SelectSword);
        ConnectButton(archerButton, SelectArcher);
        ConnectButton(tankButton, SelectTank);
        ConnectButton(closeButton, Close);

        RefreshSelectedRoleText();
    }

    public void SelectSword()
    {
        SelectRole(PlayerRole.Sword);
    }

    public void SelectArcher()
    {
        SelectRole(PlayerRole.Archer);
    }

    public void SelectTank()
    {
        SelectRole(PlayerRole.Tank);
    }

    public void SelectRole(PlayerRole role)
    {
        selectedRole = role;
        GameSession.GetOrCreate().SetSelectedRole(role);
        RefreshSelectedRoleText();
        Debug.Log($"Character selected: {role}");
        Close();
    }

    public void Open()
    {
        if (characterPanel != null)
            characterPanel.SetActive(true);

        RefreshSelectedRoleText();
    }

    public void Close()
    {
        if (characterPanel != null)
            characterPanel.SetActive(false);
    }

    private void RefreshSelectedRoleText()
    {
        if (selectedRoleText != null)
            selectedRoleText.text = $"Selected: {selectedRole}";
    }

    private void ConnectButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
