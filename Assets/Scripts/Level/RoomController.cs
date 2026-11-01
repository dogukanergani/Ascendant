using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One dungeon room. When the player steps inside, its doors close and the waves spawn inside the room one after
// another; once every enemy is dead the doors open and the room reports Cleared. Rooms without waves (the rest
// room) only drive the camera bounds.
// The trigger collider on this object is the activation zone (set by the level builder, normally the floor rect
// inset by one tile); floorSize is the whole floor and defines where enemies may appear.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoomController : MonoBehaviour
{
    [Serializable]
    public struct SpawnEntry
    {
        public GameObject prefab;
        public int count;
    }

    [Serializable]
    public sealed class Wave
    {
        public SpawnEntry[] entries = Array.Empty<SpawnEntry>();
        // Seconds between single spawns inside the wave.
        public float spawnInterval = 0.2f;
    }

    public enum RoomState { Idle, Active, Cleared }

    [SerializeField] Wave[] waves = Array.Empty<Wave>();
    [SerializeField] Door[] doors = Array.Empty<Door>();
    [SerializeField] Vector2 floorSize = new(12f, 8f);
    // Enemies never appear closer to the player than this (relaxed if the room is too small to find a spot).
    [SerializeField] float minPlayerDistance = 5f;
    // Distance kept from the walls when picking a spawn spot.
    [SerializeField] float wallMargin = 1.5f;
    [SerializeField] LayerMask blockMask = (1 << 11) | 1;
    [SerializeField] float spawnClearRadius = 0.5f;
    [SerializeField] float waveDelay = 1f;
    [SerializeField] GameObject clearReward;
    [SerializeField] bool clampCamera = true;

    readonly Collider2D[] overlapBuffer = new Collider2D[4];
    int alive;
    Transform playerTransform;

    public RoomState State { get; private set; } = RoomState.Idle;

    public int TotalEnemies
    {
        get
        {
            int total = 0;
            foreach (Wave wave in waves)
            {
                foreach (SpawnEntry entry in wave.entries)
                {
                    total += entry.count;
                }
            }

            return total;
        }
    }

    public Rect FloorRect => new(transform.position.x - floorSize.x * 0.5f, transform.position.y - floorSize.y * 0.5f, floorSize.x, floorSize.y);

    public event Action<RoomController> Cleared;
    public event Action EnemyKilled;
    // Raised for every enemy the room spawns; the HUD's boss bar listens for the boss.
    public event Action<GameObject> EnemySpawned;

    void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        playerTransform = player.transform;
        if (clampCamera && RoomCameraBounds.Instance != null)
        {
            // Walls are two tiles thick around the floor; the camera may show them.
            RoomCameraBounds.Instance.SetBounds(this, new Rect(FloorRect.xMin - 2f, FloorRect.yMin - 2f, floorSize.x + 4f, floorSize.y + 4f));
        }

        if (State == RoomState.Idle && waves.Length > 0)
        {
            StartCoroutine(Run());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (clampCamera && RoomCameraBounds.Instance != null && other.GetComponentInParent<PlayerController2D>() != null)
        {
            RoomCameraBounds.Instance.ClearBounds(this);
        }
    }

    IEnumerator Run()
    {
        State = RoomState.Active;
        foreach (Door door in doors)
        {
            door.Close();
        }

        yield return new WaitForSeconds(0.5f);
        for (int w = 0; w < waves.Length; w++)
        {
            yield return SpawnWave(waves[w]);
            while (alive > 0)
            {
                yield return null;
            }

            if (w < waves.Length - 1)
            {
                yield return new WaitForSeconds(waveDelay);
            }
        }

        State = RoomState.Cleared;
        foreach (Door door in doors)
        {
            door.Open();
        }

        if (clearReward != null)
        {
            Instantiate(clearReward, transform.position, Quaternion.identity);
        }

        Cleared?.Invoke(this);
    }

    IEnumerator SpawnWave(Wave wave)
    {
        var queue = new List<GameObject>();
        foreach (SpawnEntry entry in wave.entries)
        {
            for (int i = 0; i < entry.count; i++)
            {
                queue.Add(entry.prefab);
            }
        }

        // Shuffle so the kinds arrive mixed.
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }

        foreach (GameObject prefab in queue)
        {
            Spawn(prefab);
            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    void Spawn(GameObject prefab)
    {
        GameObject enemy = Instantiate(prefab, FindSpot(), Quaternion.identity);
        if (enemy.TryGetComponent(out EnemyAI ai))
        {
            ai.StartHunting();
        }

        EnemySpawned?.Invoke(enemy);

        if (!enemy.TryGetComponent(out Health health))
        {
            return;
        }

        alive++;
        void OnDied()
        {
            health.Died -= OnDied;
            alive--;
            EnemyKilled?.Invoke();
        }

        health.Died += OnDied;
    }

    Vector2 FindSpot()
    {
        Rect floor = FloorRect;
        float margin = Mathf.Min(wallMargin, Mathf.Min(floor.width, floor.height) * 0.3f);
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = blockMask };
        Vector2 best = floor.center;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var candidate = new Vector2(
                UnityEngine.Random.Range(floor.xMin + margin, floor.xMax - margin),
                UnityEngine.Random.Range(floor.yMin + margin, floor.yMax - margin));
            best = candidate;
            // The first attempts want the full distance; later ones relax it so small rooms still work.
            float minDist = attempt < 25 ? minPlayerDistance : minPlayerDistance * 0.5f;
            if (playerTransform != null && Vector2.Distance(candidate, playerTransform.position) < minDist)
            {
                continue;
            }

            if (Physics2D.OverlapCircle(candidate, spawnClearRadius, filter, overlapBuffer) == 0)
            {
                return candidate;
            }
        }

        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, floorSize);
    }
}
