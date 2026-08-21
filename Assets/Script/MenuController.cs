using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "EmberKeep";

    [Header("Panels")]
    public GameObject optionPanel;
    public GameObject characterPanel;
    public SettingsMenuController settingsMenu;

    [Header("Buttons")]
    public bool autoConnectButtons = true;
    public Button playButton;
    public Button optionButton;
    public Button characterButton;
    public Button exitButton;

    private void Awake()
    {
        FindSettingsMenuIfNeeded();
        FindButtonsIfNeeded();
        ConnectButtons();

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (characterPanel != null)
            characterPanel.SetActive(false);
    }

    public void PlayGame()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogWarning("Game Scene Name is empty.");
            return;
        }

        if (!CanLoadScene(gameSceneName))
        {
            Debug.LogWarning($"Scene '{gameSceneName}' cannot be loaded. Add it to Build Profiles / Scene List.");
            return;
        }

        GameSession.GetOrCreate().ResetForNewGame();
        PreserveSettingsMenuBeforeSceneLoad();
        SceneFadeManager.LoadScene(gameSceneName);
    }

    public void OpenOptions()
    {
        GameAudioManager.Play(GameSfx.SettingsOpen);
        FindSettingsMenuIfNeeded();

        if (settingsMenu != null && settingsMenu.settingsPanel != null)
        {
            settingsMenu.Open();
            Debug.Log("Open settings menu.");
            return;
        }

        if (optionPanel != null)
        {
            optionPanel.SetActive(true);
            Debug.Log("Open option panel.");
            return;
        }

        Debug.LogWarning("SettingsMenuController or Option Panel is missing.");
    }

    public void CloseOptions()
    {
        FindSettingsMenuIfNeeded();

        if (settingsMenu != null && settingsMenu.settingsPanel != null)
        {
            settingsMenu.Close();
            return;
        }

        if (optionPanel != null)
            optionPanel.SetActive(false);
    }

    public void OpenCharacterPanel()
    {
        if (characterPanel == null)
        {
            Debug.LogWarning("Character Panel is missing.");
            return;
        }

        characterPanel.SetActive(true);
        Debug.Log("Open character panel.");
    }

    public void CloseCharacterPanel()
    {
        if (characterPanel != null)
            characterPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("Quit game.");
        Application.Quit();
    }

    private void FindSettingsMenuIfNeeded()
    {
        if (settingsMenu != null && settingsMenu.settingsPanel != null)
            return;

        settingsMenu = null;

        SettingsMenuController attachedSettingsMenu = GetComponent<SettingsMenuController>();

        if (attachedSettingsMenu != null && attachedSettingsMenu.settingsPanel != null)
        {
            settingsMenu = attachedSettingsMenu;
            return;
        }

        settingsMenu = SettingsMenuController.GetAvailableInstance();

        if (settingsMenu != null)
            return;

        SettingsMenuController[] allSettingsMenus = Resources.FindObjectsOfTypeAll<SettingsMenuController>();

        foreach (SettingsMenuController foundSettingsMenu in allSettingsMenus)
        {
            if (foundSettingsMenu != null &&
                foundSettingsMenu.gameObject.scene.IsValid() &&
                foundSettingsMenu.settingsPanel != null)
            {
                settingsMenu = foundSettingsMenu;
                return;
            }
        }
    }

    private void PreserveSettingsMenuBeforeSceneLoad()
    {
        FindSettingsMenuIfNeeded();

        if (settingsMenu == null)
        {
            Debug.LogWarning("SettingsMenuController was not found before loading game scene.");
            return;
        }

        if (settingsMenu.settingsPanel == null && optionPanel != null)
            settingsMenu.settingsPanel = optionPanel;

        settingsMenu.EnsureInitialized();
        Debug.Log("Settings menu is preserved for other scenes.");
    }

    private void FindButtonsIfNeeded()
    {
        if (!autoConnectButtons)
            return;

        if (playButton == null)
            playButton = FindButtonByNames("PlayButton", "StartButton");

        if (optionButton == null)
            optionButton = FindButtonByNames("OptionButton", "SettingsButton");

        if (characterButton == null)
            characterButton = FindButtonByNames("CharButton", "CharacterButton", "CharacterSelectButton");

        if (exitButton == null)
            exitButton = FindButtonByNames("ExitButton", "QuitButton");
    }

    private Button FindButtonByNames(params string[] names)
    {
        foreach (string buttonName in names)
        {
            GameObject foundObject = GameObject.Find(buttonName);

            if (foundObject != null && IsInsidePanel(foundObject, optionPanel))
                continue;

            if (foundObject != null && IsInsidePanel(foundObject, characterPanel))
                continue;

            if (foundObject != null && foundObject.TryGetComponent(out Button foundButton))
                return foundButton;
        }

        return null;
    }

    private bool IsInsidePanel(GameObject targetObject, GameObject panel)
    {
        if (panel == null || targetObject == null)
            return false;

        Transform current = targetObject.transform;

        while (current != null)
        {
            if (current.gameObject == panel)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void ConnectButtons()
    {
        if (!autoConnectButtons)
            return;

        ConnectButton(playButton, PlayGame);
        ConnectButton(optionButton, OpenOptions);
        ConnectButton(characterButton, OpenCharacterPanel);
        ConnectButton(exitButton, QuitGame);
    }

    private void ConnectButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private bool CanLoadScene(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);

            if (scenePath.EndsWith("/" + sceneName + ".unity") || scenePath.EndsWith("\\" + sceneName + ".unity"))
                return true;
        }

        return false;
    }
}
