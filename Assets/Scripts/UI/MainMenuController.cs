using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] Button startButton;
    [SerializeField] Button levelSelectButton;
    [SerializeField] Button quitButton;
    [SerializeField] TMP_Text messageText;

    void Awake()
    {
        startButton.onClick.AddListener(OnStart);
        levelSelectButton.onClick.AddListener(SceneRouter.LoadLevelSelect);
        quitButton.onClick.AddListener(OnQuit);
        if (messageText != null)
        {
            messageText.text = string.Empty;
        }
    }

    void Start()
    {
        // Keyboard / gamepad need a selected button to navigate from.
        EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
    }

    void OnStart()
    {
        if (!SceneRouter.LoadLevel(ProgressService.GetNextLevel()) && messageText != null)
        {
            messageText.text = "Level henüz hazır değil";
        }
    }

    static void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
