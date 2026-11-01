using System;
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

    readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    readonly Collider2D[] overlaps = new Collider2D[8];

    Rigidbody2D body;
    Vector2 direction;
    Vector2 startPosition;
    DamageInfo damage;
    bool finished;

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
    public void Launch(Vector2 launchDirection, DamageInfo info, Vector2 sweepFrom)
    {
        direction = launchDirection.normalized;
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

            Finish(true);
            return true;
        }

        if (!other.isTrigger)
        {
            Finish(false);
            return true;
        }

        return false;
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
