using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Esc (or the gamepad Start button) opens a small pause panel in a level: Devam, Level Seç, Ana Menü. Esc again resumes.
// The skill tree is deliberately not reachable from here; it only opens from the level select and score screens.
// Pausing sets Time.timeScale to 0, stops the skill hotkeys and locks the player's input so nothing leaks through.
public sealed class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject content;
    [SerializeField] Button resumeButton;
    [SerializeField] Button levelSelectButton;
    [SerializeField] Button mainMenuButton;

    SkillManager skillManager;
    PlayerController2D player;
    Health playerHealth;
    bool skillManagerWasEnabled;
    bool blockedLastFrame;

    public static bool IsPaused { get; private set; }

    void Awake()
    {
        IsPaused = false;
        content.SetActive(false);
        resumeButton.onClick.AddListener(Resume);
        levelSelectButton.onClick.AddListener(SceneRouter.LoadLevelSelect);
        mainMenuButton.onClick.AddListener(SceneRouter.LoadMenu);
    }

    void Start()
    {
        skillManager = FindFirstObjectByType<SkillManager>();
        player = FindFirstObjectByType<PlayerController2D>();
        playerHealth = player != null ? player.GetComponent<Health>() : null;
    }

    void OnDestroy()
    {
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        // Esc is also the skill tree's close key, the score screen is a result and a dead player is reloading:
        // none of those may open the pause menu (the previous frame counts so the closing Esc is not reused).
        bool blocked = SkillTreePanel.IsOpen || ScoreScreen.IsShown || (playerHealth != null && playerHealth.IsDead);
        bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (IsPaused)
        {
            // A finishing hit stop must not undo the pause.
            Time.timeScale = 0f;
            if (pressed || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                Resume();
            }
        }
        else if (pressed && !blocked && !blockedLastFrame)
        {
            Pause();
        }

        blockedLastFrame = blocked;
    }

    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;
        content.SetActive(true);
        Time.timeScale = 0f;
        if (skillManager != null)
        {
            skillManagerWasEnabled = skillManager.enabled;
            skillManager.enabled = false;
        }

        if (player != null)
        {
            // Time.time is frozen while paused, so this lock lasts until the game resumes.
            player.LockInput(1000000f);
        }

        EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        content.SetActive(false);
        Time.timeScale = 1f;
        if (skillManager != null)
        {
            skillManager.enabled = skillManagerWasEnabled;
        }

        if (player != null)
        {
            // Short grace so the click that pressed Devam does not swing the staff.
            player.LockInput(0.15f);
        }
    }
}
