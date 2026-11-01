using UnityEngine;

// Test hazard: hurts the player on contact (ignored while invulnerable, e.g. during a dash).
[RequireComponent(typeof(Collider2D))]
public sealed class DamageTestTarget : MonoBehaviour
{
    void OnCollisionStay2D(Collision2D collision)
    {
        TryHurt(collision.collider);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryHurt(other);
    }

    void TryHurt(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out PlayerController2D player))
        {
            player.TakeHit(transform.position);
        }
    }
}
