using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scene names used by the menus. Level scenes are expected to be named Level_01, Level_02, ...
// Every load goes through a short fade to black (see ScreenFade).
public static class SceneRouter
{
    public const string MainMenu = "MainMenu";
    public const string LevelSelect = "LevelSelect";

    public static string LevelSceneName(int level) => $"Level_{level + 1:00}";

    public static bool IsLevelAvailable(int level) => Application.CanStreamedLevelBeLoaded(LevelSceneName(level));

    public static void LoadMenu() => Load(MainMenu);

    public static void LoadLevelSelect() => Load(LevelSelect);

    public static void ReloadCurrent() => Load(SceneManager.GetActiveScene().name);

    public static bool LoadLevel(int level)
    {
        string scene = LevelSceneName(level);
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"SceneRouter: scene '{scene}' is not in Build Settings yet.");
            return false;
        }

        Load(scene);
        return true;
    }

    static void Load(string scene)
    {
        Time.timeScale = 1f;
        ScreenFade.Instance.Load(scene);
    }
}

// Full-screen black overlay that survives scene loads. Fades out before a scene change and back in once the new
// scene is loaded; it also fades in at startup and after loads that did not come through SceneRouter.
public sealed class ScreenFade : MonoBehaviour
{
    public const float Duration = 0.25f;

    static ScreenFade instance;

    CanvasGroup group;
    Coroutine running;
    bool loading;

    public static ScreenFade Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new("ScreenFade");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ScreenFade>();
                instance.Build();
            }

            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        // Starts opaque, then fades in with the first scene.
        ScreenFade fade = Instance;
        fade.group.alpha = 1f;
        fade.running = fade.StartCoroutine(fade.FadeInRoutine());
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        GameObject imageObject = new("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)imageObject.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Load(string scene)
    {
        if (loading)
        {
            return;
        }

        loading = true;
        if (running != null)
        {
            StopCoroutine(running);
        }

        running = StartCoroutine(LoadRoutine(scene));
    }

    IEnumerator LoadRoutine(string scene)
    {
        group.blocksRaycasts = true;
        yield return Fade(1f);
        loading = false;
        SceneManager.LoadScene(scene);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loading = false;
        if (running != null)
        {
            StopCoroutine(running);
        }

        running = StartCoroutine(FadeInRoutine());
    }

    IEnumerator FadeInRoutine()
    {
        // One frame of black so the new scene's first frame is never shown half-built.
        yield return null;
        yield return Fade(0f);
        group.blocksRaycasts = false;
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < Duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / Duration));
            yield return null;
        }

        group.alpha = target;
    }
}
