using UnityEngine;

// Level goal. When the player touches it, the timer stops and the score screen is shown (which records the result
// and offers the skill tree and the next level). Collectibles call Collect() to count towards the score.
[RequireComponent(typeof(Collider2D))]
public sealed class LevelCompleteTrigger : MonoBehaviour
{
    [SerializeField] int levelIndex;
    [SerializeField] int totalCollectibles;
    [SerializeField] float parTime;
    [SerializeField] ScoreScreen scoreScreen;
    [SerializeField] LevelTimer timer;

    int collected;
    bool done;

    public void Collect() => collected++;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (done || other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        if (scoreScreen == null)
        {
            scoreScreen = FindFirstObjectByType<ScoreScreen>(FindObjectsInactive.Include);
        }

        if (timer == null)
        {
            timer = FindFirstObjectByType<LevelTimer>();
        }

        if (scoreScreen == null)
        {
            Debug.LogWarning("LevelCompleteTrigger: no ScoreScreen in the scene.", this);
            return;
        }

        done = true;
        timer?.Stop();
        scoreScreen.Show(levelIndex, timer != null ? timer.Elapsed : 0f, collected, totalCollectibles, parTime);
    }
}
