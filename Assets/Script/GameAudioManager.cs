using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameSfx
{
    SwordAttack,
    SwordSkill,
    ArcherAttack,
    ArcherSkill,
    TankAttackLift,
    TankAttackImpact,
    TankSkillStart,
    TankSkillEnd,
    PlayerHit,
    MonsterDeath,
    GoldPickup,
    ExperiencePickup,
    LevelUp,
    ButtonClick,
    CharacterSelect,
    AugmentSelect,
    SettingsOpen,
    Purchase,
    TimeOver,
    ForgeEnter,
    ForgeExit
}

public sealed class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioClip basicBgm1;
    [SerializeField] private AudioClip basicBgm2;
    [SerializeField] private AudioClip basicBgm3;
    [SerializeField] private AudioClip bossBgm;

    [Header("Player SFX")]
    [SerializeField] private AudioClip swordAttack;
    [SerializeField] private AudioClip swordSkill;
    [SerializeField] private AudioClip archerAttack;
    [SerializeField] private AudioClip archerSkill;
    [SerializeField] private AudioClip tankAttackLift;
    [SerializeField] private AudioClip tankAttackImpact;
    [SerializeField] private AudioClip tankSkillStart;
    [SerializeField] private AudioClip tankSkillEnd;
    [SerializeField] private AudioClip playerHit;

    [Header("Reward / UI SFX")]
    [SerializeField] private AudioClip monsterDeath;
    [SerializeField] private AudioClip goldPickup;
    [SerializeField] private AudioClip experiencePickup;
    [SerializeField] private AudioClip levelUp;
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip characterSelect;
    [SerializeField] private AudioClip augmentSelect;
    [SerializeField] private AudioClip settingsOpen;
    [SerializeField] private AudioClip purchase;
    [SerializeField] private AudioClip timeOver;
    [SerializeField] private AudioClip forgeEnter;
    [SerializeField] private AudioClip forgeExit;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private readonly HashSet<Button> boundButtons = new HashSet<Button>();
    private Coroutine buttonBindingCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAutomatically()
    {
        if (Instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>("GameAudioManager");
        if (prefab != null)
            Instantiate(prefab);
        else
            Debug.LogWarning("Resources/GameAudioManager 프리팹을 찾지 못했습니다.");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = bgmVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = sfxVolume;

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlaySceneBgm(SceneManager.GetActiveScene());
        BindButtonsInScene();
        buttonBindingCoroutine = StartCoroutine(BindNewButtonsRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlaySceneBgm(scene);
        BindButtonsInScene();
    }

    private IEnumerator BindNewButtonsRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.5f);

        while (true)
        {
            yield return wait;
            BindButtonsInScene();
        }
    }

    private void BindButtonsInScene()
    {
        boundButtons.RemoveWhere(button => button == null);
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null || !boundButtons.Add(button))
                continue;

            button.onClick.AddListener(PlayButtonClick);
        }
    }

    private static void PlayButtonClick()
    {
        Play(GameSfx.ButtonClick);
    }

    private void PlaySceneBgm(Scene scene)
    {
        string sceneName = scene.name.ToLowerInvariant();
        AudioClip nextClip;

        if (sceneName.Contains("boss"))
            nextClip = bossBgm;
        else
        {
            AudioClip[] basics = { basicBgm1, basicBgm2, basicBgm3 };
            nextClip = basics[Mathf.Abs(scene.buildIndex) % basics.Length];
        }

        if (nextClip == null || bgmSource.clip == nextClip)
            return;

        bgmSource.clip = nextClip;
        bgmSource.Play();
    }

    public static void Play(GameSfx sound)
    {
        if (Instance == null)
            return;

        AudioClip clip = Instance.GetClip(sound);
        if (clip != null)
            Instance.sfxSource.PlayOneShot(clip);
    }

    public static void PlayDelayed(GameSfx sound, float delay)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.PlayDelayedRoutine(sound, delay));
    }

    public static void SetMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
    }

    private IEnumerator PlayDelayedRoutine(GameSfx sound, float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        Play(sound);
    }

    private AudioClip GetClip(GameSfx sound)
    {
        switch (sound)
        {
            case GameSfx.SwordAttack: return swordAttack;
            case GameSfx.SwordSkill: return swordSkill;
            case GameSfx.ArcherAttack: return archerAttack;
            case GameSfx.ArcherSkill: return archerSkill;
            case GameSfx.TankAttackLift: return tankAttackLift;
            case GameSfx.TankAttackImpact: return tankAttackImpact;
            case GameSfx.TankSkillStart: return tankSkillStart;
            case GameSfx.TankSkillEnd: return tankSkillEnd;
            case GameSfx.PlayerHit: return playerHit;
            case GameSfx.MonsterDeath: return monsterDeath;
            case GameSfx.GoldPickup: return goldPickup;
            case GameSfx.ExperiencePickup: return experiencePickup;
            case GameSfx.LevelUp: return levelUp;
            case GameSfx.ButtonClick: return buttonClick;
            case GameSfx.CharacterSelect: return characterSelect;
            case GameSfx.AugmentSelect: return augmentSelect;
            case GameSfx.SettingsOpen: return settingsOpen;
            case GameSfx.Purchase: return purchase;
            case GameSfx.TimeOver: return timeOver;
            case GameSfx.ForgeEnter: return forgeEnter;
            case GameSfx.ForgeExit: return forgeExit;
            default: return null;
        }
    }
}
