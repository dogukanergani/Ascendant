using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Play-mode self-test: fires a number of spell bolts at moving enemies with line of sight and reports how many hit.
// Add to any object in a scene that has the player and enemies (for example next to an EnemySpawner) and enter Play.
public sealed class SpellBoltAccuracyTest : MonoBehaviour
{
    [SerializeField] SpellBolt boltPrefab;
    [SerializeField] int shots = 50;
    [SerializeField] float interval = 0.2f;
    [SerializeField] float maxTargetDistance = 6.5f;
    [SerializeField] float boltSpeed = 14f;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);
    [SerializeField] float spawnDistance = 0.6f;
    [SerializeField] LayerMask wallMask = 1 << 11;
    [SerializeField] bool runOnStart = true;

    readonly Dictionary<SpellBolt, Health> targets = new();
    int fired;
    int hits;
    int misses;
    int voided;

    void Start()
    {
        if (runOnStart)
        {
            StartCoroutine(Run());
        }
    }

    void OnEnable()
    {
        SpellBolt.Resolved += OnResolved;
    }

    void OnDisable()
    {
        SpellBolt.Resolved -= OnResolved;
    }

    void OnResolved(SpellBolt bolt, bool hit)
    {
        if (!targets.TryGetValue(bolt, out Health target))
        {
            return;
        }

        targets.Remove(bolt);
        if (hit)
        {
            hits++;
        }
        else if (target == null || target.IsDead)
        {
            // The intended target died to another bolt or the staff first: not a collision failure.
            voided++;
        }
        else
        {
            misses++;
        }
    }

    IEnumerator Run()
    {
        PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
        if (player == null || boltPrefab == null)
        {
            Debug.LogError("SpellBoltAccuracyTest: needs a player in the scene and a bolt prefab.");
            yield break;
        }

        // Give a spawner time to fill the room.
        yield return new WaitForSeconds(3f);
        float giveUp = Time.time + 60f;
        while (fired < shots && Time.time < giveUp)
        {
            if (TryFire(player))
            {
                fired++;
            }

            yield return new WaitForSeconds(interval);
        }

        yield return new WaitForSeconds(1.5f);
        int valid = hits + misses;
        float rate = valid > 0 ? 100f * hits / valid : 0f;
        Debug.Log($"[SpellBoltAccuracyTest] fired={fired} hits={hits} misses={misses} voided(target died first)={voided} unresolved={targets.Count} hitRate={rate:F1}%");
    }

    bool TryFire(PlayerController2D player)
    {
        Vector2 release = (Vector2)player.transform.position + spawnOffset;
        Health best = null;
        float bestDistance = maxTargetDistance;
        foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            Health health = enemy.GetComponent<Health>();
            if (health.IsDead)
            {
                continue;
            }

            Vector2 to = (Vector2)enemy.transform.position + spawnOffset - release;
            float distance = to.magnitude;
            if (distance < bestDistance && !Physics2D.Linecast(release, release + to, wallMask))
            {
                bestDistance = distance;
                best = health;
            }
        }

        if (best == null)
        {
            return false;
        }

        // Aim where the enemy will be when the bolt arrives.
        Vector2 aimPoint = (Vector2)best.transform.position + spawnOffset;
        Rigidbody2D body = best.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            aimPoint += body.linearVelocity * (bestDistance / boltSpeed);
        }

        Vector2 direction = (aimPoint - release).normalized;
        Vector2 origin = release + direction * spawnDistance;
        if (Physics2D.Linecast(release, origin, wallMask))
        {
            origin = release;
        }

        SpellBolt bolt = Instantiate(boltPrefab, origin, Quaternion.identity);
        targets[bolt] = best;
        DamageInfo damage = DamageRoll.Roll(new Vector2Int(8, 12), 1f, Vector2.zero, DamageSource.Spell);
        bolt.Launch(direction, damage, release);
        return true;
    }
}
