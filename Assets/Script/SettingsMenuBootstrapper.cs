using UnityEngine;

public class SettingsMenuBootstrapper : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBeforeSceneLoad()
    {
        CreateInputHandlerIfMissing();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAfterSceneLoad()
    {
        CreateInputHandlerIfMissing();
    }

    private static void CreateInputHandlerIfMissing()
    {
        if (SettingsMenuInputHandler.Instance != null)
            return;

        if (FindObjectOfType<SettingsMenuInputHandler>() != null)
            return;

        GameObject inputObject = new GameObject("SettingsMenuInputHandler");
        Object.DontDestroyOnLoad(inputObject);
        inputObject.AddComponent<SettingsMenuInputHandler>();

        Debug.Log("SettingsMenuInputHandler 생성됨: ESC 입력을 감지합니다.");
    }
}
