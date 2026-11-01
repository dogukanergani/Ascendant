using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// End-of-level panel. Call Show() when the player reaches the goal; it also records the result in ProgressService.
// The result plays as a short sequence: the numbers count up one row after another, then the stars light up in turn.
public sealed class ScoreScreen : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text collectedText;
    [SerializeField] TMP_Text pointsText;
    [SerializeField] StarRow starRow;
    [SerializeField] Button nextButton;
    [SerializeField] Button retryButton;
    [SerializeField] Button menuButton;
    [SerializeField] Button skillTreeButton;
    [SerializeField] SkillTreePanel skillTreePanel;
    [SerializeField] float countDuration = 0.8f;

    int level;
    int pointsGained;

    public static bool IsShown { get; private set; }

    void Awake()
    {
        IsShown = false;
        panel.SetActive(false);
        skillTreeButton.onClick.AddListener(skillTreePanel.Open);
        nextButton.onClick.AddListener(OnNext);
        retryButton.onClick.AddListener(() => SceneRouter.LoadLevel(level));
        menuButton.onClick.AddListener(SceneRouter.LoadLevelSelect);
    }

    void OnEnable()
    {
        SkillPointService.Gained += OnPointsGained;
    }

    void OnDisable()
    {
        SkillPointService.Gained -= OnPointsGained;
    }

    void OnDestroy()
    {
        IsShown = false;
    }

    void OnPointsGained(int amount)
    {
        pointsGained += amount;
    }

    // Stars: 1 for finishing, +1 for collecting at least 60%, +1 for finishing within parTime (or all collected when parTime <= 0).
    public static int CalculateStars(float time, float parTime, int collected, int totalCollectibles)
    {
        int stars = 1;
        if (totalCollectibles <= 0 || collected >= Mathf.CeilToInt(totalCollectibles * 0.6f))
        {
            stars++;
        }

        bool fast = parTime > 0f ? time <= parTime : collected >= totalCollectibles;
        return fast ? stars + 1 : stars;
    }

    public void Show(int levelIndex, float time, int collected, int totalCollectibles, float parTime)
    {
        level = levelIndex;
        int stars = CalculateStars(time, parTime, collected, totalCollectibles);
        ProgressService.CompleteLevel(levelIndex, stars, time, collected);

        nextButton.gameObject.SetActive(levelIndex + 1 < ProgressService.LevelCount && SceneRouter.IsLevelAvailable(levelIndex + 1));
        timeText.text = LevelTimer.Format(0f);
        collectedText.text = $"0/{totalCollectibles}";
        pointsText.text = "+0";
        starRow.Set(0);
        IsShown = true;
        panel.SetActive(true);
        StartCoroutine(Sequence(time, collected, totalCollectibles, stars));
    }

    IEnumerator Sequence(float time, int collected, int total, int stars)
    {
        // The level may still be paying its points out in the call that showed this screen.
        yield return null;
        yield return Count(timeText, time, v => LevelTimer.Format(v));
        yield return Count(collectedText, collected, v => $"{Mathf.RoundToInt(v)}/{total}");
        yield return Count(pointsText, pointsGained, v => $"+{Mathf.RoundToInt(v)}");
        bool revealed = false;
        starRow.Reveal(stars, () => revealed = true);
        while (!revealed)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.15f);
        GameObject first = nextButton.gameObject.activeSelf ? nextButton.gameObject : menuButton.gameObject;
        EventSystem.current?.SetSelectedGameObject(first);
    }

    IEnumerator Count(TMP_Text text, float target, System.Func<float, string> format)
    {
        if (target <= 0f)
        {
            text.text = format(0f);
            yield break;
        }

        for (float t = 0f; t < countDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / countDuration;
            // Ease out so the number settles instead of stopping dead.
            text.text = format(target * (1f - (1f - k) * (1f - k)));
            yield return null;
        }

        text.text = format(target);
        yield return new WaitForSecondsRealtime(0.12f);
    }

    void OnNext()
    {
        SceneRouter.LoadLevel(level + 1);
    }
}
