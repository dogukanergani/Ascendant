using UnityEngine;

// Level state for a room-based level: counts kills, reveals the exit once the boss room is cleared and pays the
// level skill points when the exit is reached (via LevelCompleteTrigger.Completed).
public sealed class LevelManager : MonoBehaviour
{
    [SerializeField] RoomController[] rooms = System.Array.Empty<RoomController>();
    [SerializeField] RoomController bossRoom;
    [SerializeField] LevelCompleteTrigger exit;
    [SerializeField] int baseLevelPoints = 10;
    [SerializeField] int twoStarBonus = 5;
    [SerializeField] int threeStarBonus = 10;

    public int Kills { get; private set; }
    public bool BossDefeated { get; private set; }

    void Awake()
    {
        foreach (RoomController room in rooms)
        {
            room.EnemyKilled += OnEnemyKilled;
        }

        if (bossRoom != null)
        {
            bossRoom.Cleared += OnBossRoomCleared;
        }

        if (exit != null)
        {
            exit.Completed += OnLevelCompleted;
            exit.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        foreach (RoomController room in rooms)
        {
            if (room != null)
            {
                room.EnemyKilled -= OnEnemyKilled;
            }
        }

        if (bossRoom != null)
        {
            bossRoom.Cleared -= OnBossRoomCleared;
        }

        if (exit != null)
        {
            exit.Completed -= OnLevelCompleted;
        }
    }

    void OnEnemyKilled()
    {
        Kills++;
        // Kills are the level's collectible count on the score screen.
        if (exit != null)
        {
            exit.Collect();
        }
    }

    void OnBossRoomCleared(RoomController room)
    {
        BossDefeated = true;
        if (exit != null)
        {
            exit.gameObject.SetActive(true);
        }
    }

    void OnLevelCompleted(int stars)
    {
        int points = baseLevelPoints + (stars >= 3 ? threeStarBonus : stars == 2 ? twoStarBonus : 0);
        SkillPointService.AddPoints(points);
    }
}
