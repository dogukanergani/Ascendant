using UnityEngine;
using UnityEngine.SceneManagement;

// Scene names used by the menus. Level scenes are expected to be named Level_01, Level_02, ...
public static class SceneRouter
{
    public const string MainMenu = "MainMenu";
    public const string LevelSelect = "LevelSelect";

    public static string LevelSceneName(int level) => $"Level_{level + 1:00}";

    public static void LoadMenu() => SceneManager.LoadScene(MainMenu);

    public static void LoadLevelSelect() => SceneManager.LoadScene(LevelSelect);

    public static bool LoadLevel(int level)
    {
        string scene = LevelSceneName(level);
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"SceneRouter: scene '{scene}' is not in Build Settings yet.");
            return false;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
        return true;
    }
}
