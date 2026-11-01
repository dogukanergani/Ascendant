using System.Collections.Generic;
using UnityEngine;

// Test spawner: keeps up to maxAlive enemies around the player by spawning them just outside the screen at a fixed
// interval. For performance and fun testing of the fast, many-weak-enemies pacing.
public sealed class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public GameObject prefab;
        public float weight;
    }

    [SerializeField] Transform player;
    [SerializeField] Entry[] enemies = System.Array.Empty<Entry>();
    [SerializeField] int maxAlive = 15;
    [SerializeField] float spawnInterval = 0.8f;
    // Enemies appear just outside the camera view, and never closer to the player than this.
    [SerializeField] float minDistance = 6f;
    // Spawned enemies head for the player at once instead of patrolling where they appeared.
    [SerializeField] bool huntPlayer = true;
    [SerializeField] LayerMask wallMask = 1 << 11;
    [SerializeField] float clearRadius = 0.4f;

    readonly List<GameObject> alive = new();
    float nextSpawn;

    public int AliveCount => alive.Count;
    public int TotalSpawned { get; private set; }

    void Update()
    {
        alive.RemoveAll(e => e == null);
        if (player == null || enemies.Length == 0 || alive.Count >= maxAlive || Time.time < nextSpawn)
        {
            return;
        }

        nextSpawn = Time.time + spawnInterval;
        if (TryFindSpot(out Vector2 spot))
        {
            GameObject enemy = Instantiate(PickPrefab(), spot, Quaternion.identity);
            if (huntPlayer && enemy.TryGetComponent(out EnemyAI ai))
            {
                ai.StartHunting();
            }

            alive.Add(enemy);
            TotalSpawned++;
        }
    }

    bool TryFindSpot(out Vector2 spot)
    {
        // Half extents of the camera view: the spawn point is placed just beyond its edge in a random direction.
        Camera cam = Camera.main;
        float halfHeight = cam != null && cam.orthographic ? cam.orthographicSize : 0f;
        float halfWidth = halfHeight * (cam != null ? cam.aspect : 1f);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float edge = Mathf.Min(
                Mathf.Abs(dir.x) > 0.001f ? halfWidth / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.001f ? halfHeight / Mathf.Abs(dir.y) : float.MaxValue);
            // The first attempts want a spot outside the view; in a room too small for that, closer spots are used.
            float distance = attempt < 15 ? edge + Random.Range(0.5f, 2.5f) : Random.Range(minDistance, Mathf.Max(minDistance, edge));
            distance = Mathf.Max(minDistance, distance);
            Vector2 origin = player.position;
            Vector2 candidate = origin + dir * distance;
            // The spot must be open floor with a clear line to the player, not behind or inside a wall.
            if (!Physics2D.OverlapCircle(candidate, clearRadius, wallMask) && !Physics2D.Linecast(origin, candidate, wallMask))
            {
                spot = candidate;
                return true;
            }
        }

        spot = default;
        return false;
    }

    GameObject PickPrefab()
    {
        float total = 0f;
        foreach (Entry entry in enemies)
        {
            total += entry.weight;
        }

        float roll = Random.value * total;
        foreach (Entry entry in enemies)
        {
            roll -= entry.weight;
            if (roll <= 0f)
            {
                return entry.prefab;
            }
        }

        return enemies[0].prefab;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(player.position, minDistance);
    }
}
