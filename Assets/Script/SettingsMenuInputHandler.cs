using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsMenuInputHandler : MonoBehaviour
{
    public static SettingsMenuInputHandler Instance { get; private set; }

    private SettingsMenuController cachedSettingsMenu;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void RegisterSettingsMenu(SettingsMenuController settingsMenu)
    {
        if (settingsMenu == null)
            return;

        SettingsMenuInputHandler handler = Instance;

        if (handler == null)
        {
            GameObject inputObject = new GameObject("SettingsMenuInputHandler");
            DontDestroyOnLoad(inputObject);
            handler = inputObject.AddComponent<SettingsMenuInputHandler>();
        }

        handler.cachedSettingsMenu = settingsMenu;
        Debug.Log("SettingsMenuInputHandler에 설정창 등록 완료");
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("ESC 입력 감지");
            ToggleSettingsMenu();
        }
    }

    private void ToggleSettingsMenu()
    {
        if (TrainingPanelController.AnyOpen || TrainingPanelController.ConsumedEscapeThisFrame)
            return;

        Debug.Log("SettingsMenuInputHandler.ToggleSettingsMenu 호출");

        if (cachedSettingsMenu == null)
        {
            Debug.Log("등록된 설정창이 없어서 씬에서 다시 찾습니다.");
            cachedSettingsMenu = SettingsMenuController.GetAvailableInstance();
        }

        if (cachedSettingsMenu == null)
        {
            Debug.LogWarning("ESC는 감지됐지만 SettingsMenuController를 찾지 못했습니다. 메인 메뉴에서 게임을 시작했는지, SettingsMenuController가 붙어있는 오브젝트가 삭제되지 않았는지 확인하세요.");
            return;
        }

        Debug.Log("ESC로 설정창 토글");
        cachedSettingsMenu.Toggle();
    }
}
