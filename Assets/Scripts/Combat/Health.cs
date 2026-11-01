using System;
using UnityEngine;

// Shared health for the player and enemies, counted in parts: a heart is partsPerHeart parts.
// Enemies use one part per hit point (SetMaxParts); the HUD draws the player's parts as hearts.
public sealed class Health : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHearts = 5;
    [SerializeField] int partsPerHeart = 2;
    [SerializeField] float invulnerabilityTime = 0.6f;

    int current;
    float invulnerableUntil = float.NegativeInfinity;

    public event Action<int, int> Changed;
    // Raised after a hit lands: damage in parts and the knockback vector.
    public event Action<int, Vector2> Damaged;
    public event Action Died;

    public int Current => current;
    public int Max => maxHearts * partsPerHeart;
    public int PartsPerHeart => partsPerHeart;
    public int MaxHearts => maxHearts;
    public bool IsDead => current <= 0;
    public bool IsFull => current >= Max;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        current = Max;
    }

    public bool Damage(int amount, Vector2 knockback)
    {
        if (IsDead || amount <= 0 || IsInvulnerable)
        {
            return false;
        }

        current = Mathf.Max(0, current - amount);
        invulnerableUntil = Time.time + invulnerabilityTime;
        Changed?.Invoke(current, Max);
        Damaged?.Invoke(amount, knockback);
        if (current == 0)
        {
            Died?.Invoke();
        }

        return true;
    }

    public void Heal(int parts)
    {
        if (IsDead || parts <= 0 || IsFull)
        {
            return;
        }

        current = Mathf.Min(Max, current + parts);
        Changed?.Invoke(current, Max);
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    public void Revive()
    {
        current = Max;
        invulnerableUntil = float.NegativeInfinity;
        Changed?.Invoke(current, Max);
    }
}
