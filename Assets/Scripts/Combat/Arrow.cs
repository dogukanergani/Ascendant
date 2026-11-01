using System.Collections.Generic;
using UnityEngine;

// Arrow fired by BowSkill. Flying mode: every frame it sweeps a circle along its path (no tunnelling), damages the
// enemies it passes (up to maxHits, so tier 2 pierces) and stops on walls. Falling mode (tier 3 volley): appears
// after a delay, drops onto a point and damages everything in a small radius when it lands.
public sealed class Arrow : MonoBehaviour
{
    [SerializeField] float speed = 22f;
    [SerializeField] float range = 12f;
    [SerializeField] float sweepRadius = 0.2f;
    [SerializeField] float knockbackSpeed = 5f;
    [SerializeField] LayerMask hitMask = (1 << 9) | (1 << 11);
    [SerializeField] LayerMask areaMask = 1 << 9;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Color landingFrom = new(1f, 0.95f, 0.7f);
    [SerializeField] Color landingTo = new(0.9f, 0.6f, 0.2f);

    readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    readonly Collider2D[] areaHits = new Collider2D[24];
    readonly HashSet<IDamageable> hit = new();

    // Flying
    Vector2 direction;
    Vector2 startPosition;
    DamageInfo damage;
    int maxHits = 1;
    int hitCount;

    // Falling
    bool falling;
    float delayLeft;
    float fallTime;
    float fallElapsed;
    Vector2 top;
    Vector2 landing;
    Vector2Int areaRange;
    float areaMultiplier;
    float areaRadius;

    // sweepFrom is where the shooter released it: the stretch from there to the spawn point is swept too.
    public void Launch(Vector2 launchDirection, DamageInfo info, Vector2 sweepFrom, int pierce)
    {
        direction = launchDirection.normalized;
        damage = info.WithKnockback(direction * knockbackSpeed);
        maxHits = Mathf.Max(1, pierce);
        startPosition = transform.position;
        transform.rotation = Quaternion.FromToRotation(Vector2.right, direction);
        Vector2 gap = startPosition - sweepFrom;
        if (gap.sqrMagnitude > 0.0001f)
        {
            Sweep(sweepFrom, gap.normalized, gap.magnitude);
        }
    }

    // Volley arrow: hidden for delay seconds, then falls from fallHeight above the target over fallDuration.
    public void Drop(Vector2 target, float delay, float fallHeight, float fallDuration, Vector2Int range, float multiplier, float radius)
    {
        falling = true;
        landing = target;
        top = target + Vector2.up * fallHeight;
        delayLeft = delay;
        fallTime = Mathf.Max(0.05f, fallDuration);
        areaRange = range;
        areaMultiplier = multiplier;
        areaRadius = radius;
        transform.position = top;
        transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    void Update()
    {
        if (falling)
        {
            TickFalling();
            return;
        }

        float step = speed * Time.deltaTime;
        if (Sweep(transform.position, direction, step))
        {
            return;
        }

        transform.position += (Vector3)(direction * step);
        if (((Vector2)transform.position - startPosition).sqrMagnitude >= range * range)
        {
            Destroy(gameObject);
        }
    }

    void TickFalling()
    {
        if (delayLeft > 0f)
        {
            delayLeft -= Time.deltaTime;
            if (delayLeft > 0f)
            {
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
        }

        fallElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(fallElapsed / fallTime);
        transform.position = Vector2.Lerp(top, landing, t * t);
        if (t < 1f)
        {
            return;
        }

        SpellExplosion.Spawn(landing, areaRadius, 0.2f, landingFrom, landingTo);
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = areaMask, useTriggers = true };
        int count = Physics2D.OverlapCircle(landing, areaRadius, filter, areaHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = areaHits[i];
            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || collider.GetComponentInParent<PlayerController2D>() != null || !hit.Add(target))
            {
                continue;
            }

            Vector2 away = (Vector2)collider.bounds.center - landing;
            Vector2 push = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up) * knockbackSpeed;
            target.Damage(DamageRoll.Roll(areaRange, areaMultiplier, push, DamageSource.Other));
        }

        Destroy(gameObject);
    }

    // Casts the arrow's circle along a segment, nearest first. Returns true if the arrow ended.
    bool Sweep(Vector2 from, Vector2 dir, float distance)
    {
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
        int count = Physics2D.CircleCast(from, sweepRadius, dir, filter, castHits, distance);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = castHits[i].collider;
            if (other.GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (hit.Contains(target) || !target.Damage(damage))
                {
                    continue;
                }

                hit.Add(target);
                hitCount++;
                if (hitCount >= maxHits)
                {
                    transform.position = castHits[i].centroid;
                    Destroy(gameObject);
                    return true;
                }

                continue;
            }

            if (!other.isTrigger)
            {
                transform.position = castHits[i].centroid;
                Destroy(gameObject);
                return true;
            }
        }

        return false;
    }
}
