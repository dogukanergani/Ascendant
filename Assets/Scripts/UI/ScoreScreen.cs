using TMPro;
using UnityEngine;
using UnityEngine.UI;

// End-of-level panel. Call Show() when the player reaches the goal; it also records the result in ProgressService.
public sealed class ScoreScreen : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text collectedText;
    [SerializeField] StarRow starRow;
    [SerializeField] Button nextButton;
    [SerializeField] Button retryButton;
    [SerializeField] Button menuButton;
    [SerializeField] Button skillTreeButton;
    [SerializeField] SkillTreePanel skillTreePanel;

    int level;

    void Awake()
    {
        panel.SetActive(false);
        skillTreeButton.onClick.AddListener(skillTreePanel.Open);
        nextButton.onClick.AddListener(OnNext);
        retryButton.onClick.AddListener(() => SceneRouter.LoadLevel(level));
        menuButton.onClick.AddListener(SceneRouter.LoadLevelSelect);
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

        timeText.text = $"Süre  {LevelTimer.Format(time)}";
        collectedText.text = $"Toplanan  {collected}/{totalCollectibles}";
        starRow.Set(stars);
        nextButton.gameObject.SetActive(levelIndex + 1 < ProgressService.LevelCount);
        panel.SetActive(true);
    }

    void OnNext()
    {
        SceneRouter.LoadLevel(level + 1);
    }
}
