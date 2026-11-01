using System;
using System.Collections.Generic;
using UnityEngine;

// Fast projectile fired by SpellBoltSkill. Every physics step it sweeps a circle along its path, so it cannot skip
// over a thin or moving collider, and it also hits whatever already overlaps it when it spawns. It damages the first
// IDamageable it reaches and vanishes on solid walls.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class SpellBolt : MonoBehaviour
{
    [SerializeField] float speed = 14f;
    [SerializeField] float range = 8f;
    [SerializeField] float sweepRadius = 0.35f;
    [SerializeField] float knockbackSpeed = 6f;
    [SerializeField] LayerMask hitMask = (1 << 9) | (1 << 11);

    [Header("Splash")]
    // Default blast radius; SpellBoltSkill overrides it per tier.
    [SerializeField] float splashRadius = 1.5f;
    // Enemies caught in the blast (not the one struck directly) take this share of the rolled damage.
    [SerializeField, Range(0f, 1f)] float splashFalloff = 0.7f;
    [SerializeField] LayerMask splashMask = 1 << 9;
    [SerializeField] float explosionDuration = 0.25f;
    [SerializeField] bool drawGizmos = true;

    readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    readonly Collider2D[] overlaps = new Collider2D[8];
    readonly Collider2D[] blastHits = new Collider2D[32];
    readonly List<IDamageable> blasted = new();

    Rigidbody2D body;
    Vector2 direction;
    Vector2 startPosition;
    Vector2 impactPoint;
    DamageInfo damage;
    bool finished;

    public float SplashRadius => splashRadius;

    // Raised when a bolt ends: true if it damaged something. Used by the accuracy self-test.
    public static event Action<SpellBolt, bool> Resolved;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    // sweepFrom is where the caster released it: the stretch from there to the spawn point is swept as well.
    // splashRadiusOverride < 0 keeps the prefab's radius.
    public void Launch(Vector2 launchDirection, DamageInfo info, Vector2 sweepFrom, float splashRadiusOverride = -1f)
    {
        if (splashRadiusOverride >= 0f)
        {
            splashRadius = splashRadiusOverride;
        }

        direction = launchDirection.normalized;
        impactPoint = transform.position;
        damage = info.WithKnockback(direction * knockbackSpeed);
        startPosition = transform.position;
        transform.rotation = Quaternion.FromToRotation(Vector2.right, direction);

        Vector2 gap = startPosition - sweepFrom;
        if (gap.sqrMagnitude > 0.0001f && Sweep(sweepFrom, gap.normalized, gap.magnitude))
        {
            return;
        }

        // Anything already overlapping the spawn point is hit at once.
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
        int count = Physics2D.OverlapCircle(startPosition, sweepRadius, filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            if (TryHit(overlaps[i]))
            {
                return;
            }
        }
    }

    void FixedUpdate()
    {
        if (finished)
        {
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        if (Sweep(body.position, direction, step))
        {
            return;
        }

        body.MovePosition(body.position + direction * step);
        if ((body.position - startPosition).sqrMagnitude >= range * range)
        {
            Finish(false);
        }
    }

    // Casts the bolt's circle along a segment and resolves the nearest relevant collider. Returns true if it ended.
    bool Sweep(Vector2 from, Vector2 dir, float distance)
    {
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
        int count = Physics2D.CircleCast(from, sweepRadius, dir, filter, castHits, distance);
        for (int i = 0; i < count; i++)
        {
            impactPoint = castHits[i].centroid;
            if (TryHit(castHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    bool TryHit(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() != null)
        {
            return false;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (!target.Damage(damage))
            {
                return false;
            }

            Explode(target);
            Finish(true);
            return true;
        }

        if (!other.isTrigger)
        {
            Explode(null);
            Finish(false);
            return true;
        }

        return false;
    }

    // Area damage around the impact: each enemy at most once, the directly struck one (already hit) excluded.
    void Explode(IDamageable struck)
    {
        if (finished)
        {
            return;
        }

        SpellExplosion.Spawn(impactPoint, splashRadius, explosionDuration);
        if (splashRadius <= 0f)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = splashMask, useTriggers = true };
        int count = Physics2D.OverlapCircle(impactPoint, splashRadius, filter, blastHits);
        blasted.Clear();
        if (struck != null)
        {
            blasted.Add(struck);
        }

        int splashAmount = Mathf.Max(1, Mathf.RoundToInt(damage.Amount * splashFalloff));
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = blastHits[i];
            if (collider.GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || blasted.Contains(target))
            {
                continue;
            }

            blasted.Add(target);
            Vector2 away = (Vector2)collider.bounds.center - impactPoint;
            Vector2 push = (away.sqrMagnitude > 0.0001f ? away.normalized : direction) * knockbackSpeed;
            target.Damage(new DamageInfo(splashAmount, push, damage.IsCritical, damage.Source));
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.5f, 0.55f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }

    void Finish(bool hit)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Resolved?.Invoke(this, hit);
        Destroy(gameObject);
    }
}
