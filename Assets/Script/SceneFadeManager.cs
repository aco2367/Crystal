using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneFadeManager : MonoBehaviour
{
    public static SceneFadeManager Instance { get; private set; }

    [Header("Fade")]
    [Min(0f)] public float fadeOutDuration = 0.35f;
    [Min(0f)] public float fadeInDuration = 0.35f;
    public Color fadeColor = Color.black;
    public int sortingOrder = 32760;

    private Canvas fadeCanvas;
    private Image fadeImage;
    private Coroutine transitionRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAutomatically()
    {
        GetOrCreate();
    }

    public static SceneFadeManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        SceneFadeManager existing = FindFirstObjectByType<SceneFadeManager>();

        if (existing != null)
            return existing;

        GameObject fadeObject = new GameObject("SceneFadeManager");
        return fadeObject.AddComponent<SceneFadeManager>();
    }

    public static void LoadScene(string sceneName)
    {
        GetOrCreate().BeginLoadScene(sceneName);
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
        BuildFadeCanvas();
    }

    private IEnumerator Start()
    {
        SetAlpha(1f);
        yield return FadeTo(0f, fadeInDuration);
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
    }

    public void BeginLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("페이드 전환할 씬 이름이 비어 있습니다.", this);
            return;
        }

        if (transitionRoutine != null)
            return;

        transitionRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        yield return FadeTo(1f, fadeOutDuration);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

        if (loadOperation == null)
        {
            Debug.LogWarning($"'{sceneName}' 씬 로드를 시작하지 못했습니다.", this);
            yield return FadeTo(0f, fadeInDuration);
            fadeImage.raycastTarget = false;
            transitionRoutine = null;
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        yield return null;
        yield return FadeTo(0f, fadeInDuration);

        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
        transitionRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = fadeImage.color.a;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void BuildFadeCanvas()
    {
        fadeCanvas = gameObject.GetComponent<Canvas>();

        if (fadeCanvas == null)
            fadeCanvas = gameObject.AddComponent<Canvas>();

        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = sortingOrder;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject(
            "FadeImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        imageObject.transform.SetParent(transform, false);
        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.raycastTarget = true;

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }
}
