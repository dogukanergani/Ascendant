using UnityEngine;

// Fast projectile fired by SpellBoltSkill. Damages the first IDamageable it touches and vanishes on solid walls.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class SpellBolt : MonoBehaviour
{
    [SerializeField] float speed = 14f;
    [SerializeField] float range = 8f;
    [SerializeField] int damage = 1;
    [SerializeField] float knockbackSpeed = 6f;

    Rigidbody2D body;
    Vector2 direction;
    Vector2 startPosition;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Launch(Vector2 launchDirection)
    {
        direction = launchDirection.normalized;
        startPosition = transform.position;
        transform.rotation = Quaternion.FromToRotation(Vector2.right, direction);
        body.linearVelocity = direction * speed;
    }

    void Update()
    {
        if (((Vector2)transform.position - startPosition).sqrMagnitude >= range * range)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() != null)
        {
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            target.Damage(damage, direction * knockbackSpeed);
            Destroy(gameObject);
        }
        else if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
