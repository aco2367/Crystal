using UnityEngine;
using UnityEngine.InputSystem;

public enum GameKeyAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Skill,
    Dodge,
    Interact
}

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    [Header("Default Keys")]
    public Key moveUp = Key.W;
    public Key moveDown = Key.S;
    public Key moveLeft = Key.A;
    public Key moveRight = Key.D;
    public Key skill = Key.Q;
    public Key dodge = Key.Space;
    public Key interact = Key.F;

    private const string SavePrefix = "KeyBinding_";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadBindings();
    }

    public static KeyBindingManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        KeyBindingManager existing = FindObjectOfType<KeyBindingManager>();

        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerObject = new GameObject("KeyBindingManager");
        return managerObject.AddComponent<KeyBindingManager>();
    }

    public static bool IsPressed(GameKeyAction action)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        Key key = GetOrCreate().GetKey(action);
        return keyboard[key].isPressed;
    }

    public static bool WasPressedThisFrame(GameKeyAction action)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        Key key = GetOrCreate().GetKey(action);
        return keyboard[key].wasPressedThisFrame;
    }

    public Key GetKey(GameKeyAction action)
    {
        switch (action)
        {
            case GameKeyAction.MoveUp:
                return moveUp;
            case GameKeyAction.MoveDown:
                return moveDown;
            case GameKeyAction.MoveLeft:
                return moveLeft;
            case GameKeyAction.MoveRight:
                return moveRight;
            case GameKeyAction.Skill:
                return skill;
            case GameKeyAction.Dodge:
                return dodge;
            case GameKeyAction.Interact:
                return interact;
            default:
                return Key.None;
        }
    }

    public void SetKey(GameKeyAction action, Key key)
    {
        if (key == Key.None)
            return;

        switch (action)
        {
            case GameKeyAction.MoveUp:
                moveUp = key;
                break;
            case GameKeyAction.MoveDown:
                moveDown = key;
                break;
            case GameKeyAction.MoveLeft:
                moveLeft = key;
                break;
            case GameKeyAction.MoveRight:
                moveRight = key;
                break;
            case GameKeyAction.Skill:
                skill = key;
                break;
            case GameKeyAction.Dodge:
                dodge = key;
                break;
            case GameKeyAction.Interact:
                interact = key;
                break;
        }

        SaveBinding(action, key);
    }

    public string GetKeyDisplayName(GameKeyAction action)
    {
        return GetKey(action).ToString();
    }

    public void ResetToDefaults()
    {
        moveUp = Key.W;
        moveDown = Key.S;
        moveLeft = Key.A;
        moveRight = Key.D;
        skill = Key.Q;
        dodge = Key.Space;
        interact = Key.F;

        foreach (GameKeyAction action in System.Enum.GetValues(typeof(GameKeyAction)))
        {
            SaveBinding(action, GetKey(action));
        }
    }

    private void LoadBindings()
    {
        moveUp = LoadBinding(GameKeyAction.MoveUp, moveUp);
        moveDown = LoadBinding(GameKeyAction.MoveDown, moveDown);
        moveLeft = LoadBinding(GameKeyAction.MoveLeft, moveLeft);
        moveRight = LoadBinding(GameKeyAction.MoveRight, moveRight);
        skill = LoadBinding(GameKeyAction.Skill, skill);
        dodge = LoadBinding(GameKeyAction.Dodge, dodge);
        interact = LoadBinding(GameKeyAction.Interact, interact);
    }

    private Key LoadBinding(GameKeyAction action, Key defaultKey)
    {
        string saved = PlayerPrefs.GetString(SavePrefix + action, defaultKey.ToString());

        if (System.Enum.TryParse(saved, out Key loadedKey))
        {
            return loadedKey;
        }

        return defaultKey;
    }

    private void SaveBinding(GameKeyAction action, Key key)
    {
        PlayerPrefs.SetString(SavePrefix + action, key.ToString());
        PlayerPrefs.Save();
    }
}
