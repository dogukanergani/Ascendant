using System.Collections.Generic;
using UnityEngine;

// Thrown by BombSkill: slides a short way, then explodes when the fuse runs out. The blast damages every enemy in
// the radius once, breaks BreakableWalls and (when chain is on, tier 3) sets off other bombs in the radius at once.
[RequireComponent(typeof(Collider2D))]
public sealed class Bomb : MonoBehaviour
{
    [SerializeField] float fuse = 1f;
    [SerializeField] float throwSpeed = 9f;
    // The throw decelerates to a stop over this time.
    [SerializeField] float throwTime = 0.3f;
    [SerializeField] float sweepRadius = 0.25f;
    [SerializeField] LayerMask wallMask = 1 << 11;
    [SerializeField] float knockbackSpeed = 8f;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Color explosionFrom = new(1f, 0.95f, 0.6f);
    [SerializeField] Color explosionTo = new(1f, 0.4f, 0.05f);

    readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
    readonly Collider2D[] blastHits = new Collider2D[48];
    readonly HashSet<IDamageable> damaged = new();

    Vector2 direction;
    Vector2Int damageRange;
    float radius = 2f;
    bool chain;
    float age;
    bool exploded;

    public void Launch(Vector2 throwDirection, Vector2Int range, float blastRadius, bool chainReaction)
    {
        direction = throwDirection.sqrMagnitude > 0.0001f ? throwDirection.normalized : Vector2.down;
        damageRange = range;
        radius = blastRadius;
        chain = chainReaction;
    }

    void Update()
    {
        if (exploded)
        {
            return;
        }

        age += Time.deltaTime;
        if (age < throwTime)
        {
            float speed = throwSpeed * (1f - age / throwTime);
            Move(speed * Time.deltaTime);
        }

        // Pulse faster as the fuse burns down.
        if (spriteRenderer != null)
        {
            float t = Mathf.Clamp01(age / fuse);
            float pulse = Mathf.Sin(age * Mathf.Lerp(10f, 40f, t)) * 0.5f + 0.5f;
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), pulse * t);
            transform.localScale = Vector3.one * (1f + 0.12f * pulse * t);
        }

        if (age >= fuse)
        {
            Detonate();
        }
    }

    void Move(float distance)
    {
        if (distance <= 0f)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = wallMask, useTriggers = false };
        int count = Physics2D.CircleCast(transform.position, sweepRadius, direction, filter, castHits, distance);
        if (count > 0)
        {
            // Stop in front of the wall and stay there.
            transform.position += (Vector3)(direction * Mathf.Max(0f, castHits[0].distance - 0.02f));
            age = Mathf.Max(age, throwTime);
            return;
        }

        transform.position += (Vector3)(direction * distance);
    }

    public void Detonate()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        Vector2 center = transform.position;
        SpellExplosion.Spawn(center, radius, 0.3f, explosionFrom, explosionTo);

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        int count = Physics2D.OverlapCircle(center, radius, filter, blastHits);
        damaged.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = blastHits[i];
            if (collider == null || collider.GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            BreakableWall wall = collider.GetComponentInParent<BreakableWall>();
            if (wall != null)
            {
                wall.Break();
                continue;
            }

            Bomb other = collider.GetComponentInParent<Bomb>();
            if (other != null)
            {
                if (chain && other != this)
                {
                    other.Detonate();
                }

                continue;
            }

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || !damaged.Add(target))
            {
                continue;
            }

            Vector2 away = (Vector2)collider.bounds.center - center;
            Vector2 push = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up) * knockbackSpeed;
            target.Damage(DamageRoll.Roll(damageRange, 1f, push, DamageSource.Spell));
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
