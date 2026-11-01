using UnityEngine;

// Health or stamina pickup. Drifts toward the player when close and applies its effect on arrival.
public sealed class Pickup : MonoBehaviour
{
    public enum Kind
    {
        HeartPart,
        StaminaOrb,
    }

    [SerializeField] Kind kind = Kind.HeartPart;
    [SerializeField] float amount = 1f;
    [SerializeField] float magnetRadius = 3f;
    [SerializeField] float collectRadius = 0.4f;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float acceleration = 25f;
    [SerializeField] float lifetime = 20f;

    static PlayerController2D player;

    Health health;
    Stamina stamina;
    float speed;
    float expiresAt;

    void OnEnable()
    {
        expiresAt = Time.time + lifetime;
        speed = 0f;
    }

    void Update()
    {
        if (lifetime > 0f && Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        if (!FindTarget() || !CanUse())
        {
            speed = 0f;
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        float distance = toPlayer.magnitude;
        if (distance <= collectRadius)
        {
            Collect();
            return;
        }

        if (distance > magnetRadius)
        {
            speed = 0f;
            return;
        }

        speed = Mathf.Min(maxSpeed, speed + acceleration * Time.deltaTime);
        transform.position += (Vector3)(toPlayer / distance * Mathf.Min(speed * Time.deltaTime, distance));
    }

    bool FindTarget()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController2D>();
            health = null;
        }

        if (player == null)
        {
            return false;
        }

        if (health == null)
        {
            health = player.GetComponent<Health>();
            stamina = player.GetComponent<Stamina>();
        }

        return true;
    }

    // A pickup the player cannot benefit from stays on the ground until they can.
    bool CanUse()
    {
        return kind == Kind.HeartPart
            ? health != null && !health.IsDead && !health.IsFull
            : stamina != null && stamina.Current < stamina.Max;
    }

    void Collect()
    {
        if (kind == Kind.HeartPart)
        {
            health.Heal(Mathf.RoundToInt(amount));
        }
        else
        {
            stamina.Restore(amount);
        }

        Destroy(gameObject);
    }
}
