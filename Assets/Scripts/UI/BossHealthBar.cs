using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss health bar at the top of the screen: the boss's name above a framed bar with a lagging "damage" strip.
// It appears when a room spawns an enemy whose EnemyStats are marked isBoss (RoomController.EnemySpawned) and
// fades away after the boss dies. Show() can also be called directly.
public sealed class BossHealthBar : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform slideRoot;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image fill;
    [SerializeField] Image ghost;
    [SerializeField] float fadeSpeed = 4f;
    [SerializeField] float slideDistance = 40f;
    // The damage strip waits this long after a hit, then drains at ghostSpeed (fraction of the bar per second).
    [SerializeField] float ghostDelay = 0.45f;
    [SerializeField] float ghostSpeed = 0.7f;
    [SerializeField] float hideDelay = 1.5f;

    readonly System.Collections.Generic.List<RoomController> rooms = new();
    Health boss;
    bool shown;
    float shownFraction = 1f;
    float ghostFraction = 1f;
    float lastHitAt = float.NegativeInfinity;
    float hideAt = float.PositiveInfinity;
    float visibility;
    Vector2 home;

    void Awake()
    {
        home = slideRoot.anchoredPosition;
        ApplyVisibility();
    }

    void Start()
    {
        foreach (RoomController room in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
        {
            room.EnemySpawned += OnEnemySpawned;
            rooms.Add(room);
        }
    }

    void OnDestroy()
    {
        foreach (RoomController room in rooms)
        {
            if (room != null)
            {
                room.EnemySpawned -= OnEnemySpawned;
            }
        }

        Unbind();
    }

    void OnEnemySpawned(GameObject enemy)
    {
        if (enemy.TryGetComponent(out EnemyAI ai) && ai.Stats != null && ai.Stats.isBoss && enemy.TryGetComponent(out Health health))
        {
            Show(health, string.IsNullOrEmpty(ai.Stats.displayName) ? enemy.name : ai.Stats.displayName);
        }
    }

    public void Show(Health target, string displayName)
    {
        Unbind();
        boss = target;
        boss.Died += OnBossDied;
        nameText.text = displayName;
        shown = true;
        hideAt = float.PositiveInfinity;
        shownFraction = ghostFraction = 1f;
        ApplyFill();
    }

    void Unbind()
    {
        if (boss != null)
        {
            boss.Died -= OnBossDied;
            boss = null;
        }
    }

    void OnBossDied()
    {
        // Keep the empty bar on screen for a moment, then fade it.
        shownFraction = 0f;
        lastHitAt = Time.unscaledTime;
        hideAt = Time.unscaledTime + hideDelay;
        Unbind();
    }

    void Update()
    {
        if (boss != null)
        {
            float fraction = boss.Max > 0 ? Mathf.Clamp01((float)boss.Current / boss.Max) : 0f;
            if (fraction < shownFraction)
            {
                lastHitAt = Time.unscaledTime;
            }

            shownFraction = fraction;
        }

        if (Time.unscaledTime > lastHitAt + ghostDelay)
        {
            ghostFraction = Mathf.MoveTowards(ghostFraction, shownFraction, ghostSpeed * Time.unscaledDeltaTime);
        }

        ghostFraction = Mathf.Max(ghostFraction, shownFraction);
        ApplyFill();

        if (Time.unscaledTime >= hideAt)
        {
            shown = false;
        }

        visibility = Mathf.MoveTowards(visibility, shown ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        ApplyVisibility();
    }

    void ApplyFill()
    {
        fill.fillAmount = shownFraction;
        ghost.fillAmount = ghostFraction;
    }

    void ApplyVisibility()
    {
        group.alpha = visibility;
        slideRoot.anchoredPosition = home + new Vector2(0f, (1f - visibility) * slideDistance);
    }
}
