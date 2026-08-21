using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    public static SettingsMenuController Instance { get; private set; }

    [Header("Panel")]
    public GameObject settingsPanel;
    public GameObject settingsRoot;
    public bool pauseGameWhileOpen = true;
    public bool keepSettingsMenuAcrossScenes = true;
    public bool forcePanelVisibleOnOpen = false;

    [Header("Sound")]
    [Range(0.01f, 0.25f)] public float buttonStep = 0.1f;
    [Range(0f, 1f)] public float soundVolume = 0.8f;
    public AudioSource[] controlledAudioSources;
    public TMP_Text soundValueText;
    public Button soundMinusButton;
    public Button soundPlusButton;

    [Header("Screen")]
    [Range(0f, 1f)] public float brightness = 0.6f;
    public TMP_Text brightnessValueText;
    public Image brightnessOverlay;
    public Button brightnessMinusButton;
    public Button brightnessPlusButton;

    [Header("Key Buttons")]
    public Button upKeyButton;
    public Button downKeyButton;
    public Button leftKeyButton;
    public Button rightKeyButton;
    public Button skillKeyButton;
    public Button dodgeKeyButton;
    public Button interactKeyButton;

    [Header("Key Texts")]
    public TMP_Text upKeyText;
    public TMP_Text downKeyText;
    public TMP_Text leftKeyText;
    public TMP_Text rightKeyText;
    public TMP_Text skillKeyText;
    public TMP_Text dodgeKeyText;
    public TMP_Text interactKeyText;
    public string waitingForKeyText = "입력...";

    [Header("Game Screen Blur")]
    public bool useCapturedBlurInGameScene = true;
    [Range(2, 8)] public int blurDownsample = 4;
    [Range(1, 6)] public int blurIterations = 3;
    public RawImage blurBackgroundImage;
    public Image dimBackgroundImage;

    [Header("Navigation")]
    public string lobbySceneName = "Main Menu";
    public Button closeButton;
    public Button lobbyButton;
    public Button quitButton;

    public bool IsOpen { get; private set; }

    private Texture2D blurTexture;
    private GameObject persistentCanvasObject;
    private Coroutine openRoutine;
    private KeyBindingManager keyBindingManager;
    private GameKeyAction? waitingForAction;
    private TMP_Text waitingText;
    private Coroutine listenKeyCoroutine;
    private float previousTimeScale = 1f;
    private bool pauseApplied;
    private bool initialized;
    private const string SoundVolumeKey = "Settings.SoundVolume";
    private const string BrightnessKey = "Settings.Brightness";

    private void Awake()
    {
        InitializeOnce();
    }

    public void EnsureInitialized()
    {
        InitializeOnce();
    }

    private void InitializeOnce()
    {
        // 패널이 연결되지 않은 중복 컴포넌트가 정상 설정 메뉴의
        // 싱글톤 자리를 먼저 차지하지 않도록 한다.
        if (settingsPanel == null)
        {
            Debug.LogWarning($"{name}의 SettingsMenuController에 Settings Panel이 연결되지 않아 비활성화합니다.", this);
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        if (initialized)
            return;

        initialized = true;
        Instance = this;

        soundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, soundVolume);
        brightness = PlayerPrefs.GetFloat(BrightnessKey, brightness);

        MakePersistentIfNeeded();
        SettingsMenuInputHandler.RegisterSettingsMenu(this);
        keyBindingManager = KeyBindingManager.GetOrCreate();
        EnsureEventSystem();
        InitializeControls();
        RefreshKeyTexts();
        Close(false);
    }

    public static void ToggleByEscape()
    {
        Debug.Log("SettingsMenuController.ToggleByEscape 호출");
        SettingsMenuController settingsMenu = GetAvailableInstance();

        if (settingsMenu == null)
        {
            Debug.LogWarning("SettingsMenuController가 씬에 없습니다. Canvas 안에 직접 만든 설정 패널에 SettingsMenuController를 붙여주세요.");
            return;
        }

        settingsMenu.InitializeOnce();

        if (settingsMenu.IsOpen)
        {
            Debug.Log("ESC로 설정창 닫기");
            settingsMenu.Close();
        }
        else
        {
            Debug.Log("ESC로 설정창 열기");
            settingsMenu.Open();
        }
    }

    public static SettingsMenuController GetAvailableInstance()
    {
        if (Instance != null && Instance.settingsPanel != null)
            return Instance;

        SettingsMenuController[] allControllers = Resources.FindObjectsOfTypeAll<SettingsMenuController>();

        foreach (SettingsMenuController controller in allControllers)
        {
            if (controller == null)
                continue;

            if (controller.gameObject.scene.IsValid() && controller.settingsPanel != null)
                return controller;
        }

        return null;
    }

    public void Open()
    {
        Debug.Log("SettingsMenuController.Open 호출");

        if (settingsPanel == null)
        {
            Debug.LogWarning("Settings Panel이 연결되지 않았습니다.");
            return;
        }

        EnsureEventSystem();

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (!gameObject.activeInHierarchy)
        {
            OpenImmediate(false);
            return;
        }

        if (ShouldUseCapturedBlur())
        {
            openRoutine = StartCoroutine(OpenWithCapturedBlurRoutine());
            return;
        }

        OpenImmediate(false);
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Debug.Log("설정창 토글: 닫기");
            Close();
            return;
        }

        Debug.Log("설정창 토글: 열기");
        Open();
    }

    private IEnumerator OpenWithCapturedBlurRoutine()
    {
        SetSettingsObjectsActive(false);
        yield return new WaitForEndOfFrame();

        CaptureBlurredScreen();
        OpenImmediate(true);
        openRoutine = null;
    }

    private void OpenImmediate(bool showBlur)
    {
        Debug.Log("설정창 패널 활성화");
        LogPanelState();

        EnsurePanelIsVisibleOnScreen();

        if (blurBackgroundImage != null)
            blurBackgroundImage.gameObject.SetActive(showBlur && blurBackgroundImage.texture != null);

        if (dimBackgroundImage != null)
        {
            dimBackgroundImage.gameObject.SetActive(true);
            dimBackgroundImage.color = showBlur ? new Color(0f, 0f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.48f);
        }

        SetSettingsObjectsActive(true);
        IsOpen = true;
        RefreshKeyTexts();
        ApplyPauseIfNeeded();
    }

    private void LogPanelState()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("현재 Settings Panel이 비어 있습니다.");
            return;
        }

        Graphic[] graphics = settingsPanel.GetComponentsInChildren<Graphic>(true);
        RectTransform rectTransform = settingsPanel.GetComponent<RectTransform>();
        string rectInfo = rectTransform == null
            ? "RectTransform 없음"
            : $"위치 {rectTransform.anchoredPosition}, 크기 {rectTransform.rect.size}, 스케일 {rectTransform.localScale}";

        Debug.Log($"현재 Settings Panel: {settingsPanel.name}, UI Graphic 개수: {graphics.Length}, {rectInfo}");

        if (graphics.Length == 0)
            Debug.LogWarning("Settings Panel 안에 Image/Text 같은 UI Graphic이 없습니다. Settings Panel에 실제 OptionPanel을 넣어야 화면에 보입니다.");
    }

    private void EnsurePanelIsVisibleOnScreen()
    {
        if (!forcePanelVisibleOnOpen || settingsPanel == null)
            return;

        Transform panelParent = settingsPanel.transform.parent;

        if (panelParent != null && !panelParent.gameObject.activeSelf)
            panelParent.gameObject.SetActive(true);

        settingsPanel.transform.SetAsLastSibling();

        CanvasGroup canvasGroup = settingsPanel.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        RectTransform rectTransform = settingsPanel.GetComponent<RectTransform>();

        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = rectTransform.anchoredPosition;

        if (Mathf.Abs(anchoredPosition.x) > Screen.width || Mathf.Abs(anchoredPosition.y) > Screen.height)
            rectTransform.anchoredPosition = Vector2.zero;
    }

    public void Close()
    {
        Debug.Log("설정창 닫기 버튼 작동");
        Close(true);
    }

    private void Close(bool restorePause)
    {
        CancelKeyListen();

        SetSettingsObjectsActive(false);

        if (blurBackgroundImage != null)
            blurBackgroundImage.gameObject.SetActive(false);

        if (dimBackgroundImage != null)
            dimBackgroundImage.gameObject.SetActive(false);

        IsOpen = false;

        if (restorePause)
            RestorePauseIfNeeded();
    }

    public void IncreaseSoundVolume()
    {
        SetSoundVolume(soundVolume + buttonStep);
    }

    public void DecreaseSoundVolume()
    {
        SetSoundVolume(soundVolume - buttonStep);
    }

    public void IncreaseBrightness()
    {
        SetBrightness(brightness + buttonStep);
    }

    public void DecreaseBrightness()
    {
        SetBrightness(brightness - buttonStep);
    }

    public void StartMoveUpKeyChange()
    {
        StartKeyChange(GameKeyAction.MoveUp, ResolveKeyText(upKeyText, upKeyButton));
    }

    public void StartMoveDownKeyChange()
    {
        StartKeyChange(GameKeyAction.MoveDown, ResolveKeyText(downKeyText, downKeyButton));
    }

    public void StartMoveLeftKeyChange()
    {
        StartKeyChange(GameKeyAction.MoveLeft, ResolveKeyText(leftKeyText, leftKeyButton));
    }

    public void StartMoveRightKeyChange()
    {
        StartKeyChange(GameKeyAction.MoveRight, ResolveKeyText(rightKeyText, rightKeyButton));
    }

    public void StartSkillKeyChange()
    {
        StartKeyChange(GameKeyAction.Skill, ResolveKeyText(skillKeyText, skillKeyButton));
    }

    public void StartDodgeKeyChange()
    {
        StartKeyChange(GameKeyAction.Dodge, ResolveKeyText(dodgeKeyText, dodgeKeyButton));
    }

    public void StartInteractKeyChange()
    {
        StartKeyChange(GameKeyAction.Interact, ResolveKeyText(interactKeyText, interactKeyButton));
    }

    public void StartKeyChange(GameKeyAction action, TMP_Text targetText)
    {
        CancelKeyListen(false);
        waitingForAction = action;
        waitingText = targetText;

        if (waitingText != null)
            waitingText.text = waitingForKeyText;

        listenKeyCoroutine = StartCoroutine(ListenForKeyRoutine());
    }

    public void SetSoundVolume(float value)
    {
        soundVolume = Mathf.Clamp01(value);
        GameAudioManager.SetMasterVolume(soundVolume);
        PlayerPrefs.SetFloat(SoundVolumeKey, soundVolume);
        PlayerPrefs.Save();

        UpdatePercentText(soundValueText, soundVolume);
    }

    public void SetBrightness(float value)
    {
        brightness = Mathf.Clamp01(value);
        EnsureBrightnessOverlay();
        UpdatePercentText(brightnessValueText, brightness);
        PlayerPrefs.SetFloat(BrightnessKey, brightness);
        PlayerPrefs.Save();

        if (brightnessOverlay != null)
        {
            float darkness = 1f - brightness;
            brightnessOverlay.color = new Color(0f, 0f, 0f, darkness * 0.8f);
        }
    }

    private void EnsureBrightnessOverlay()
    {
        if (brightnessOverlay != null)
            return;

        Canvas targetCanvas = persistentCanvasObject != null
            ? persistentCanvasObject.GetComponent<Canvas>()
            : settingsPanel != null ? settingsPanel.GetComponentInParent<Canvas>() : null;

        if (targetCanvas == null)
            return;

        GameObject overlayObject = new GameObject("RuntimeBrightnessOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(targetCanvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsFirstSibling();

        brightnessOverlay = overlayObject.GetComponent<Image>();
        brightnessOverlay.raycastTarget = false;
        brightnessOverlay.color = Color.clear;
    }

    public void GoToLobby()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        Debug.Log("로비로 가기 버튼 작동");
        CancelKeyListen();
        Close(false);
        RestorePauseIfNeeded();

        if (string.IsNullOrEmpty(lobbySceneName) || activeSceneName == lobbySceneName)
            return;

        if (CanLoadScene(lobbySceneName))
        {
            SceneFadeManager.LoadScene(lobbySceneName);
            return;
        }

        Debug.LogWarning($"'{lobbySceneName}' 씬을 로드할 수 없습니다. 현재 활성 씬은 '{activeSceneName}'입니다. Lobby Scene Name과 Build Profiles의 Scene List를 확인하세요.");
    }

    public void QuitGame()
    {
        RestorePauseIfNeeded();
        Debug.Log("게임 종료 버튼 작동");
        Application.Quit();
    }

    private void MakePersistentIfNeeded()
    {
        if (!keepSettingsMenuAcrossScenes || settingsPanel == null)
            return;

        persistentCanvasObject = new GameObject("PersistentSettingsCanvas");
        Canvas canvas = persistentCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = persistentCanvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        persistentCanvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(persistentCanvasObject);

        GameObject rootObject = settingsRoot != null ? settingsRoot : settingsPanel;
        rootObject.transform.SetParent(persistentCanvasObject.transform, false);

        if (transform.root.gameObject != persistentCanvasObject)
            DontDestroyOnLoad(gameObject);
    }

    private void InitializeControls()
    {
        AutoFindControlsIfNeeded();

        SetupButton(soundMinusButton, DecreaseSoundVolume);
        SetupButton(soundPlusButton, IncreaseSoundVolume);
        SetupButton(brightnessMinusButton, DecreaseBrightness);
        SetupButton(brightnessPlusButton, IncreaseBrightness);
        SetupButton(upKeyButton, StartMoveUpKeyChange);
        SetupButton(downKeyButton, StartMoveDownKeyChange);
        SetupButton(leftKeyButton, StartMoveLeftKeyChange);
        SetupButton(rightKeyButton, StartMoveRightKeyChange);
        SetupButton(skillKeyButton, StartSkillKeyChange);
        SetupButton(dodgeKeyButton, StartDodgeKeyChange);
        SetupButton(interactKeyButton, StartInteractKeyChange);
        SetupButton(closeButton, Close);
        SetupButton(lobbyButton, GoToLobby);
        SetupButton(quitButton, QuitGame);

        SetSoundVolume(soundVolume);
        SetBrightness(brightness);
    }

    private void AutoFindControlsIfNeeded()
    {
        if (settingsPanel == null)
            return;

        if (soundMinusButton == null)
            soundMinusButton = FindButtonInPanel("SoundMinusButton", "SoundMinus", "SoundDownButton", "SoundDown");

        if (soundPlusButton == null)
            soundPlusButton = FindButtonInPanel("SoundPlusButton", "SoundPlus", "SoundUpButton", "SoundUp");

        if (brightnessMinusButton == null)
            brightnessMinusButton = FindButtonInPanel("BrightnessMinusButton", "BrightnessMinus", "BrightnessDownButton", "BrightnessDown");

        if (brightnessPlusButton == null)
            brightnessPlusButton = FindButtonInPanel("BrightnessPlusButton", "BrightnessPlus", "BrightnessUpButton", "BrightnessUp");

        if (upKeyButton == null)
            upKeyButton = FindButtonInPanel("UpKeyButton", "MoveUpButton");
        if (upKeyButton == null)
            upKeyButton = FindButtonByChildText("W");

        if (downKeyButton == null)
            downKeyButton = FindButtonInPanel("DownKeyButton", "MoveDownButton");
        if (downKeyButton == null)
            downKeyButton = FindButtonByChildText("S");

        if (leftKeyButton == null)
            leftKeyButton = FindButtonInPanel("LeftKeyButton", "MoveLeftButton");
        if (leftKeyButton == null)
            leftKeyButton = FindButtonByChildText("A");

        if (rightKeyButton == null)
            rightKeyButton = FindButtonInPanel("RightKeyButton", "MoveRightButton");
        if (rightKeyButton == null)
            rightKeyButton = FindButtonByChildText("D");

        if (skillKeyButton == null)
            skillKeyButton = FindButtonInPanel("SkillKeyButton", "SkillButton");
        if (skillKeyButton == null)
            skillKeyButton = FindButtonByChildText("Q");

        if (dodgeKeyButton == null)
            dodgeKeyButton = FindButtonInPanel("DodgeKeyButton", "DashKeyButton", "DodgeButton");
        if (dodgeKeyButton == null)
            dodgeKeyButton = FindButtonByChildText("Space");

        if (interactKeyButton == null)
            interactKeyButton = FindButtonInPanel("InteractKeyButton", "InteractButton");
        if (interactKeyButton == null)
            interactKeyButton = FindButtonByChildText("F");

        if (closeButton == null)
            closeButton = FindButtonInPanel("CloseButton", "XButton", "ExitButton");

        if (lobbyButton == null)
            lobbyButton = FindButtonInPanel("LobbyButton", "MainMenuButton", "HomeButton");

        if (quitButton == null)
            quitButton = FindButtonInPanel("QuitButton", "GameQuitButton");

        if (soundValueText == null)
            soundValueText = FindTextInPanel("SoundValueText", "SoundText");

        if (brightnessValueText == null)
            brightnessValueText = FindTextInPanel("BrightnessValueText", "BrightnessText");

        if (upKeyText == null)
            upKeyText = FindTextInPanel("UpKeyText", "MoveUpText");
        if (upKeyText == null)
            upKeyText = FindTextByValue("W");

        if (downKeyText == null)
            downKeyText = FindTextInPanel("DownKeyText", "MoveDownText");
        if (downKeyText == null)
            downKeyText = FindTextByValue("S");

        if (leftKeyText == null)
            leftKeyText = FindTextInPanel("LeftKeyText", "MoveLeftText");
        if (leftKeyText == null)
            leftKeyText = FindTextByValue("A");

        if (rightKeyText == null)
            rightKeyText = FindTextInPanel("RightKeyText", "MoveRightText");
        if (rightKeyText == null)
            rightKeyText = FindTextByValue("D");

        if (skillKeyText == null)
            skillKeyText = FindTextInPanel("SkillKeyText");
        if (skillKeyText == null)
            skillKeyText = FindTextByValue("Q");

        if (dodgeKeyText == null)
            dodgeKeyText = FindTextInPanel("DodgeKeyText", "DashKeyText");
        if (dodgeKeyText == null)
            dodgeKeyText = FindTextByValue("Space");

        if (interactKeyText == null)
            interactKeyText = FindTextInPanel("InteractKeyText");
        if (interactKeyText == null)
            interactKeyText = FindTextByValue("F");
    }

    private Button FindButtonInPanel(params string[] names)
    {
        Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);

        foreach (string targetName in names)
        {
            foreach (Button button in buttons)
            {
                if (button != null && button.name == targetName)
                    return button;
            }
        }

        return null;
    }

    private TMP_Text FindTextInPanel(params string[] names)
    {
        TMP_Text[] texts = settingsPanel.GetComponentsInChildren<TMP_Text>(true);

        foreach (string targetName in names)
        {
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.name == targetName)
                    return text;
            }
        }

        return null;
    }

    private Button FindButtonByChildText(string textValue)
    {
        Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

            if (text != null && text.text.Trim() == textValue)
                return button;
        }

        return null;
    }

    private TMP_Text FindTextByValue(string textValue)
    {
        TMP_Text[] texts = settingsPanel.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.text.Trim() == textValue)
                return text;
        }

        return null;
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void SetSettingsObjectsActive(bool active)
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(active);
            return;
        }

        SetObjectActive(settingsPanel, active);
    }

    private void SetObjectActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    private void SetObjectActive(GameObject targetObject, bool active)
    {
        if (targetObject != null)
            targetObject.SetActive(active);
    }

    private TMP_Text ResolveKeyText(TMP_Text assignedText, Button keyButton)
    {
        if (assignedText != null)
            return assignedText;

        if (keyButton == null)
            return null;

        return keyButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void RefreshKeyTexts()
    {
        if (keyBindingManager == null)
            keyBindingManager = KeyBindingManager.GetOrCreate();

        SetKeyText(upKeyText, GameKeyAction.MoveUp);
        SetKeyText(downKeyText, GameKeyAction.MoveDown);
        SetKeyText(leftKeyText, GameKeyAction.MoveLeft);
        SetKeyText(rightKeyText, GameKeyAction.MoveRight);
        SetKeyText(skillKeyText, GameKeyAction.Skill);
        SetKeyText(dodgeKeyText, GameKeyAction.Dodge);
        SetKeyText(interactKeyText, GameKeyAction.Interact);
    }

    private void SetKeyText(TMP_Text targetText, GameKeyAction action)
    {
        if (targetText != null)
            targetText.text = keyBindingManager.GetKeyDisplayName(action);
    }

    private void UpdatePercentText(TMP_Text targetText, float value)
    {
        if (targetText != null)
            targetText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private IEnumerator ListenForKeyRoutine()
    {
        yield return null;

        while (waitingForAction.HasValue)
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                Key pressedKey = GetPressedKey(keyboard);

                if (pressedKey != Key.None && pressedKey != Key.Escape)
                {
                    keyBindingManager.SetKey(waitingForAction.Value, pressedKey);
                    if (waitingText != null)
                        waitingText.text = keyBindingManager.GetKeyDisplayName(waitingForAction.Value);

                    waitingForAction = null;
                    waitingText = null;
                    RefreshKeyTexts();
                    listenKeyCoroutine = null;
                    yield break;
                }
            }

            yield return null;
        }
    }

    private Key GetPressedKey(Keyboard keyboard)
    {
        foreach (var keyControl in keyboard.allKeys)
        {
            if (keyControl != null && keyControl.wasPressedThisFrame)
                return keyControl.keyCode;
        }

        return Key.None;
    }

    private void CancelKeyListen(bool refresh = true)
    {
        if (listenKeyCoroutine != null)
        {
            StopCoroutine(listenKeyCoroutine);
            listenKeyCoroutine = null;
        }

        waitingForAction = null;
        waitingText = null;

        if (refresh)
            RefreshKeyTexts();
    }

    private bool ShouldUseCapturedBlur()
    {
        if (!useCapturedBlurInGameScene)
            return false;

        string activeSceneName = SceneManager.GetActiveScene().name;

        if (!string.IsNullOrEmpty(lobbySceneName) && activeSceneName == lobbySceneName)
            return false;

        return Camera.main != null && blurBackgroundImage != null;
    }

    private void CaptureBlurredScreen()
    {
        int sourceWidth = Mathf.Max(1, Screen.width);
        int sourceHeight = Mathf.Max(1, Screen.height);
        Texture2D screenTexture = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGB24, false);
        screenTexture.ReadPixels(new Rect(0, 0, sourceWidth, sourceHeight), 0, 0);
        screenTexture.Apply();

        int targetWidth = Mathf.Max(1, sourceWidth / Mathf.Max(2, blurDownsample));
        int targetHeight = Mathf.Max(1, sourceHeight / Mathf.Max(2, blurDownsample));
        Color[] blurredPixels = CreateBlurredPixels(screenTexture, targetWidth, targetHeight);

        if (blurTexture != null)
            Destroy(blurTexture);

        blurTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        blurTexture.SetPixels(blurredPixels);
        blurTexture.Apply();

        blurBackgroundImage.texture = blurTexture;
        blurBackgroundImage.gameObject.SetActive(true);
        Destroy(screenTexture);
    }

    private Color[] CreateBlurredPixels(Texture2D source, int width, int height)
    {
        Color[] pixels = new Color[width * height];
        int sourceWidth = source.width;
        int sourceHeight = source.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) * sourceWidth / width), 0, sourceWidth - 1);
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) * sourceHeight / height), 0, sourceHeight - 1);
                pixels[y * width + x] = source.GetPixel(sourceX, sourceY);
            }
        }

        for (int i = 0; i < blurIterations; i++)
            pixels = BoxBlur(pixels, width, height);

        return pixels;
    }

    private Color[] BoxBlur(Color[] pixels, int width, int height)
    {
        Color[] result = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.black;
                int count = 0;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int sampleY = Mathf.Clamp(y + offsetY, 0, height - 1);

                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int sampleX = Mathf.Clamp(x + offsetX, 0, width - 1);
                        sum += pixels[sampleY * width + sampleX];
                        count++;
                    }
                }

                result[y * width + x] = sum / count;
            }
        }

        return result;
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

    private void EnsureEventSystem()
    {
        EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();
        EventSystem eventSystem = eventSystems.Length > 0 ? eventSystems[0] : null;

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        for (int i = 1; i < eventSystems.Length; i++)
        {
            if (eventSystems[i] != null)
                Destroy(eventSystems[i].gameObject);
        }

        StandaloneInputModule oldInputModule = eventSystem.GetComponent<StandaloneInputModule>();

        if (oldInputModule != null)
            oldInputModule.enabled = false;

        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputSystemModule == null)
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        if (inputSystemModule.actionsAsset == null)
            inputSystemModule.AssignDefaultActions();
    }
}
